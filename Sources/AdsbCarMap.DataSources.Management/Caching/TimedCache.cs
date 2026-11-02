using System.Text.Json;

namespace AdsbCarMap.DataSources.Management.Caching
{
    /// <summary>
    /// Memory of the information about single flights and aircraft, so that the same flight is not asked about twice. An item expires after a time chosen by the user counted from the moment it was last seen, not from the moment it was stored, so a flight that is still visible stays in the memory and a flight that disappeared (landed or left the area) is forgotten. Landed flights can be closed earlier. The memory can be kept in a file. A stored null means that the source did not know the item, and such an answer expires sooner than a found one.
    /// </summary>
    /// <typeparam name="T">Kind of the stored information, serializable to JSON.</typeparam>
    public sealed class TimedCache<T> : IExpiringCache where T : class
    {
        /// <summary>
        /// One stored item.
        /// </summary>
        /// <param name="Key">Key of the item.</param>
        /// <param name="StoredUtc">Moment at which the item was stored or last seen, in UTC.</param>
        /// <param name="Value">The information or null when the source did not know the item.</param>
        /// <param name="ClosedUntilUtc">Moment at which a closed item expires whatever the time chosen by the user, in UTC, or null when the item is not closed.</param>
        public sealed record Entry(string Key, DateTime StoredUtc, T? Value, DateTime? ClosedUntilUtc = null);

        /// <summary>
        /// Lock protecting the items.
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// Stored items by key.
        /// </summary>
        private readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns how long a found item stays valid, read at every use so that a changed setting takes effect immediately.
        /// </summary>
        private readonly Func<TimeSpan> maxAge;

        /// <summary>
        /// How long a stored null stays valid, at most.
        /// </summary>
        private readonly TimeSpan negativeMaxAge;

        /// <summary>
        /// Source of the current time in UTC, replaceable in tests.
        /// </summary>
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Number of items above which the oldest ones are removed.
        /// </summary>
        private readonly int maxEntries;

        /// <summary>
        /// Path of the file in which the items are kept, or null when they are kept only in memory.
        /// </summary>
        private string? filePath;

        /// <summary>
        /// Time to wait after a change before the file is written, so that a series of changes causes one write.
        /// </summary>
        private TimeSpan saveDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Set to 1 while a delayed write is waiting.
        /// </summary>
        private int savePending;

        /// <summary>
        /// Creates the cache with the system clock.
        /// </summary>
        /// <param name="maxAge">Returns how long a found item stays valid.</param>
        /// <param name="negativeMaxAge">How long a stored null stays valid, at most.</param>
        public TimedCache(Func<TimeSpan> maxAge, TimeSpan negativeMaxAge)
            : this(maxAge, negativeMaxAge, () => DateTime.UtcNow, 5000)
        {
        }

        /// <summary>
        /// Creates the cache.
        /// </summary>
        /// <param name="maxAge">Returns how long a found item stays valid.</param>
        /// <param name="negativeMaxAge">How long a stored null stays valid, at most.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        /// <param name="maxEntries">Number of items above which the oldest ones are removed.</param>
        public TimedCache(Func<TimeSpan> maxAge, TimeSpan negativeMaxAge, Func<DateTime> utcNow, int maxEntries)
        {
            this.maxAge = maxAge;
            this.negativeMaxAge = negativeMaxAge;
            this.utcNow = utcNow;
            this.maxEntries = maxEntries;
        }

        /// <summary>
        /// Number of stored items, including the expired ones that were not removed yet.
        /// </summary>
        public int Count
        {
            get
            {
                lock (gate)
                {
                    return items.Count;
                }
            }
        }

        /// <summary>
        /// Reads an item that is still valid.
        /// </summary>
        /// <param name="key">Key of the item, e.g. a callsign or an ICAO address; the letter case does not matter.</param>
        /// <param name="value">The information, or null when the source did not know the item.</param>
        /// <returns>True when a valid item is stored (its value can still be null), false when nothing is stored or it expired.</returns>
        public bool TryGet(string key, out T? value)
        {
            lock (gate)
            {
                if (items.TryGetValue(key, out Entry? entry) && IsValid(entry))
                {
                    value = entry.Value;

                    return true;
                }
            }

            value = null;

            return false;
        }

        /// <summary>
        /// Stores an item, replacing the previous one.
        /// </summary>
        /// <param name="key">Key of the item.</param>
        /// <param name="value">The information, or null when the source did not know the item.</param>
        public void Set(string key, T? value)
        {
            lock (gate)
            {
                items[key] = new Entry(key, utcNow(), value);

                if (items.Count > maxEntries)
                {
                    RemoveOldest(items.Count - maxEntries + maxEntries / 10);
                }
            }

            ScheduleSave();
        }

        /// <inheritdoc />
        public bool Touch(string key)
        {
            lock (gate)
            {
                if (!items.TryGetValue(key, out Entry? entry) || !IsValid(entry))
                {
                    return false;
                }

                items[key] = entry with { StoredUtc = utcNow(), ClosedUntilUtc = null };

                return true;
            }
        }

        /// <inheritdoc />
        public bool Close(string key, TimeSpan grace)
        {
            lock (gate)
            {
                if (!items.TryGetValue(key, out Entry? entry) || !IsValid(entry))
                {
                    return false;
                }

                if (entry.ClosedUntilUtc == null)
                {
                    items[key] = entry with { ClosedUntilUtc = utcNow() + grace };
                }

                return true;
            }
        }

        /// <summary>
        /// Removes an item, e.g. when the user asks for fresh data.
        /// </summary>
        /// <param name="key">Key of the item.</param>
        /// <returns>True when the item was stored.</returns>
        public bool Remove(string key)
        {
            bool removed;

            lock (gate)
            {
                removed = items.Remove(key);
            }

            if (removed)
            {
                ScheduleSave();
            }

            return removed;
        }

        /// <summary>
        /// Removes all items.
        /// </summary>
        public void Clear()
        {
            lock (gate)
            {
                items.Clear();
            }

            ScheduleSave();
        }

        /// <summary>
        /// Removes the items that expired.
        /// </summary>
        /// <returns>Number of removed items.</returns>
        public int Prune()
        {
            int removed;

            lock (gate)
            {
                List<string> expired = items.Values.Where(entry => !IsValid(entry)).Select(entry => entry.Key).ToList();

                foreach (string key in expired)
                {
                    items.Remove(key);
                }

                removed = expired.Count;
            }

            if (removed > 0)
            {
                ScheduleSave();
            }

            return removed;
        }

        /// <summary>
        /// Connects the cache to a file: loads the items from it, removes the expired ones and from now on writes the changes to it after a short delay. A missing or damaged file is ignored.
        /// </summary>
        /// <param name="path">Path of the file.</param>
        /// <param name="delay">Time to wait after a change before the file is written.</param>
        public void AttachFile(string path, TimeSpan delay)
        {
            filePath = path;
            saveDelay = delay;

            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                List<Entry>? loaded = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path));

                lock (gate)
                {
                    foreach (Entry entry in loaded ?? new List<Entry>())
                    {
                        if (!string.IsNullOrEmpty(entry.Key) && IsValid(entry))
                        {
                            items[entry.Key] = entry;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
            }
        }

        /// <summary>
        /// Writes the items to the file at once, without waiting for the delay. Does nothing when no file is attached.
        /// </summary>
        public void Flush()
        {
            string? path = filePath;

            if (path == null)
            {
                return;
            }

            List<Entry> snapshot;

            lock (gate)
            {
                snapshot = items.Values.Where(IsValid).ToList();
            }

            try
            {
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
                File.Move(temporary, path, true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Checks whether an item is still valid: a found item for the time chosen by the user counted from the last sighting, a stored null for the shorter of that time and the time for unknown items, and a closed item only until its end.
        /// </summary>
        /// <param name="entry">The item.</param>
        /// <returns>True when the item has not expired.</returns>
        private bool IsValid(Entry entry)
        {
            TimeSpan limit = maxAge();

            if (entry.Value == null && negativeMaxAge < limit)
            {
                limit = negativeMaxAge;
            }

            DateTime now = utcNow();

            return now - entry.StoredUtc < limit && (entry.ClosedUntilUtc == null || now < entry.ClosedUntilUtc);
        }

        /// <summary>
        /// Removes the oldest items. Must be called under the lock.
        /// </summary>
        /// <param name="count">Number of items to remove.</param>
        private void RemoveOldest(int count)
        {
            foreach (string key in items.Values.OrderBy(entry => entry.StoredUtc).Take(count).Select(entry => entry.Key).ToList())
            {
                items.Remove(key);
            }
        }

        /// <summary>
        /// Starts a delayed write of the file, unless one is already waiting.
        /// </summary>
        private void ScheduleSave()
        {
            if (filePath == null || Interlocked.Exchange(ref savePending, 1) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(saveDelay);
                Interlocked.Exchange(ref savePending, 0);
                Flush();
            });
        }
    }
}

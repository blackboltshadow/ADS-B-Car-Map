using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// One aircraft that the user selected on the map or that a watch alert reported, with the times it was noticed.
    /// </summary>
    public sealed class AircraftHistoryEntry
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft, in lower case.</param>
        /// <param name="callsign">Last known callsign or null.</param>
        /// <param name="registration">Last known registration or null.</param>
        /// <param name="typeCode">Last known ICAO type code or null.</param>
        /// <param name="firstSeenUtc">Time of the first notice in UTC.</param>
        /// <param name="lastSeenUtc">Time of the last notice in UTC.</param>
        /// <param name="count">Number of separate occasions on which the aircraft was noticed.</param>
        /// <param name="pinned">Whether the entry is pinned and never dropped by the limit.</param>
        /// <param name="watched">Whether the aircraft was ever watched with an alert.</param>
        public AircraftHistoryEntry(string hex, string? callsign, string? registration, string? typeCode, DateTime firstSeenUtc, DateTime lastSeenUtc, int count, bool pinned, bool watched)
        {
            Hex = hex;
            Callsign = callsign;
            Registration = registration;
            TypeCode = typeCode;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            Count = Math.Max(1, count);
            Pinned = pinned;
            Watched = watched;
        }

        /// <summary>
        /// ICAO address of the aircraft, in lower case.
        /// </summary>
        public string Hex { get; }

        /// <summary>
        /// Last known callsign or null.
        /// </summary>
        public string? Callsign { get; set; }

        /// <summary>
        /// Last known registration or null.
        /// </summary>
        public string? Registration { get; set; }

        /// <summary>
        /// Last known ICAO type code or null.
        /// </summary>
        public string? TypeCode { get; set; }

        /// <summary>
        /// Time of the first notice in UTC.
        /// </summary>
        public DateTime FirstSeenUtc { get; }

        /// <summary>
        /// Time of the last notice in UTC.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        /// <summary>
        /// Number of separate occasions on which the aircraft was noticed.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Whether the entry is pinned and never dropped by the limit.
        /// </summary>
        public bool Pinned { get; set; }

        /// <summary>
        /// Whether the aircraft was ever watched with an alert.
        /// </summary>
        public bool Watched { get; set; }
    }

    /// <summary>
    /// Bounded list of the aircraft that the user selected or watched, one entry per aircraft, with a text format for saving it.
    /// </summary>
    public sealed class AircraftHistory
    {
        /// <summary>
        /// Default number of unpinned entries that are kept.
        /// </summary>
        public const int DefaultCapacity = 200;

        /// <summary>
        /// Time after which a new notice of the same aircraft counts as another occasion.
        /// </summary>
        public static readonly TimeSpan RecountGap = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Entries by ICAO address.
        /// </summary>
        private readonly Dictionary<string, AircraftHistoryEntry> entries = new Dictionary<string, AircraftHistoryEntry>();

        /// <summary>
        /// Number of unpinned entries that are kept.
        /// </summary>
        private readonly int capacity;

        /// <summary>
        /// Creates an empty history.
        /// </summary>
        /// <param name="capacity">Number of unpinned entries that are kept, at least 1; the least recently seen ones are dropped first.</param>
        public AircraftHistory(int capacity = DefaultCapacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        /// <summary>
        /// Number of entries, pinned ones included.
        /// </summary>
        public int Count => entries.Count;

        /// <summary>
        /// Notes an aircraft: adds an entry or updates the existing one.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="callsign">Callsign or null; an empty value does not erase the known one.</param>
        /// <param name="registration">Registration or null; an empty value does not erase the known one.</param>
        /// <param name="typeCode">ICAO type code or null; an empty value does not erase the known one.</param>
        /// <param name="nowUtc">Time of the notice in UTC.</param>
        /// <param name="watched">Whether the notice comes from a watch alert.</param>
        public void Record(string hex, string? callsign, string? registration, string? typeCode, DateTime nowUtc, bool watched)
        {
            string key = hex.Trim().ToLowerInvariant();

            if (key.Length == 0)
            {
                return;
            }

            if (entries.TryGetValue(key, out AircraftHistoryEntry? entry))
            {
                if (nowUtc - entry.LastSeenUtc > RecountGap)
                {
                    entry.Count++;
                }

                if (nowUtc > entry.LastSeenUtc)
                {
                    entry.LastSeenUtc = nowUtc;
                }

                entry.Callsign = Prefer(callsign, entry.Callsign);
                entry.Registration = Prefer(registration, entry.Registration);
                entry.TypeCode = Prefer(typeCode, entry.TypeCode);
                entry.Watched = entry.Watched || watched;

                return;
            }

            entries[key] = new AircraftHistoryEntry(key, Prefer(callsign, null), Prefer(registration, null), Prefer(typeCode, null), nowUtc, nowUtc, 1, false, watched);
            Trim();
        }

        /// <summary>
        /// Pins or unpins an entry.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="pinned">New state of the pin.</param>
        public void SetPinned(string hex, bool pinned)
        {
            if (entries.TryGetValue(hex.Trim().ToLowerInvariant(), out AircraftHistoryEntry? entry))
            {
                entry.Pinned = pinned;
                Trim();
            }
        }

        /// <summary>
        /// Marks an aircraft as watched, when it is in the history.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public void MarkWatched(string hex)
        {
            if (entries.TryGetValue(hex.Trim().ToLowerInvariant(), out AircraftHistoryEntry? entry))
            {
                entry.Watched = true;
            }
        }

        /// <summary>
        /// Removes one entry.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public void Remove(string hex)
        {
            entries.Remove(hex.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Removes all entries that are not pinned.
        /// </summary>
        public void ClearUnpinned()
        {
            foreach (string key in entries.Where((KeyValuePair<string, AircraftHistoryEntry> pair) => !pair.Value.Pinned).Select((KeyValuePair<string, AircraftHistoryEntry> pair) => pair.Key).ToList())
            {
                entries.Remove(key);
            }
        }

        /// <summary>
        /// Gives the entries for display.
        /// </summary>
        /// <returns>A list with the pinned entries first, then the rest, each group with the most recently seen first.</returns>
        public List<AircraftHistoryEntry> GetForDisplay()
        {
            return entries.Values
                .OrderByDescending((AircraftHistoryEntry entry) => entry.Pinned)
                .ThenByDescending((AircraftHistoryEntry entry) => entry.LastSeenUtc)
                .ToList();
        }

        /// <summary>
        /// Writes the history as JSON.
        /// </summary>
        /// <returns>JSON array of the entries.</returns>
        public string Serialize()
        {
            using MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();

                foreach (AircraftHistoryEntry entry in entries.Values.OrderBy((AircraftHistoryEntry item) => item.LastSeenUtc))
                {
                    writer.WriteStartObject();
                    writer.WriteString("hex", entry.Hex);

                    if (entry.Callsign != null)
                    {
                        writer.WriteString("callsign", entry.Callsign);
                    }

                    if (entry.Registration != null)
                    {
                        writer.WriteString("registration", entry.Registration);
                    }

                    if (entry.TypeCode != null)
                    {
                        writer.WriteString("type", entry.TypeCode);
                    }

                    writer.WriteNumber("first", entry.FirstSeenUtc.Ticks);
                    writer.WriteNumber("last", entry.LastSeenUtc.Ticks);
                    writer.WriteNumber("count", entry.Count);
                    writer.WriteBoolean("pinned", entry.Pinned);
                    writer.WriteBoolean("watched", entry.Watched);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads a history written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="json">Saved text or null.</param>
        /// <param name="capacity">Number of unpinned entries that are kept.</param>
        /// <returns>The history; damaged text gives an empty one and damaged entries are skipped.</returns>
        public static AircraftHistory Parse(string? json, int capacity = DefaultCapacity)
        {
            AircraftHistory history = new AircraftHistory(capacity);

            if (string.IsNullOrWhiteSpace(json))
            {
                return history;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return history;
                }

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    AircraftHistoryEntry? entry = ParseEntry(element);

                    if (entry != null)
                    {
                        history.entries[entry.Hex] = entry;
                    }
                }

                history.Trim();
            }
            catch (JsonException)
            {
                return new AircraftHistory(capacity);
            }

            return history;
        }

        /// <summary>
        /// Picks the new text when it is not empty, otherwise the known one.
        /// </summary>
        /// <param name="value">New text or null.</param>
        /// <param name="known">Known text or null.</param>
        /// <returns>The chosen text or null.</returns>
        private static string? Prefer(string? value, string? known)
        {
            return string.IsNullOrWhiteSpace(value) ? known : value.Trim();
        }

        /// <summary>
        /// Drops the least recently seen unpinned entries above the capacity.
        /// </summary>
        private void Trim()
        {
            List<AircraftHistoryEntry> unpinned = entries.Values
                .Where((AircraftHistoryEntry entry) => !entry.Pinned)
                .OrderByDescending((AircraftHistoryEntry entry) => entry.LastSeenUtc)
                .ToList();

            for (int index = capacity; index < unpinned.Count; index++)
            {
                entries.Remove(unpinned[index].Hex);
            }
        }

        /// <summary>
        /// Reads one entry.
        /// </summary>
        /// <param name="element">JSON element of the entry.</param>
        /// <returns>The entry or null when it is damaged.</returns>
        private static AircraftHistoryEntry? ParseEntry(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? hex = ReadString(element, "hex")?.Trim().ToLowerInvariant();
            long? first = ReadLong(element, "first");
            long? last = ReadLong(element, "last");

            if (string.IsNullOrEmpty(hex) || first == null || last == null
                || first <= 0 || first > DateTime.MaxValue.Ticks || last <= 0 || last > DateTime.MaxValue.Ticks)
            {
                return null;
            }

            int count = (int)Math.Clamp(ReadLong(element, "count") ?? 1, 1, int.MaxValue);

            return new AircraftHistoryEntry(
                hex,
                ReadString(element, "callsign"),
                ReadString(element, "registration"),
                ReadString(element, "type"),
                new DateTime(first.Value, DateTimeKind.Utc),
                new DateTime(last.Value, DateTimeKind.Utc),
                count,
                ReadBool(element, "pinned"),
                ReadBool(element, "watched"));
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The text or null when the property is missing or is not a string.</returns>
        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>
        /// Reads an integer property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The number or null when the property is missing or is not an integer.</returns>
        private static long? ReadLong(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : null;
        }

        /// <summary>
        /// Reads a boolean property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The value, false when the property is missing or is not a boolean.</returns>
        private static bool ReadBool(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
        }
    }
}

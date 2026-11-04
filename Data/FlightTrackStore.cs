using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Keeps the full recorded paths of the flights downloaded from a source, also in a file, and combines them with the trail collected on the phone, so that the trail of a selected aircraft reaches back to the takeoff. A path is kept as long as its flight lasts; after the landing it is final and is refreshed only to notice the next flight, which replaces it.
    /// </summary>
    public sealed class FlightTrackStore
    {
        /// <summary>
        /// Largest number of points of one path; longer paths are thinned out to keep the drawing fast.
        /// </summary>
        public const int MaxPoints = 500;

        /// <summary>
        /// Largest number of remembered paths; the least recently downloaded one is dropped first.
        /// </summary>
        public const int MaxEntries = 20;

        /// <summary>
        /// How long a path of a landed flight is kept after the last download.
        /// </summary>
        public static readonly TimeSpan LandedMaxAge = TimeSpan.FromHours(24);

        /// <summary>
        /// How long a path of a flight whose landing was not seen is kept after the last download.
        /// </summary>
        public static readonly TimeSpan InFlightMaxAge = TimeSpan.FromHours(12);

        /// <summary>
        /// Minimum time between two downloads for an aircraft that has landed and is not flying; a download then only looks for the next flight.
        /// </summary>
        public static readonly TimeSpan LandedRefreshInterval = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Path of one aircraft as kept in the file.
        /// </summary>
        /// <param name="Hex">ICAO address of the aircraft.</param>
        /// <param name="UpdatedUtc">Moment of the last successful download.</param>
        /// <param name="Points">Points of the path.</param>
        public sealed record StoredTrack(string Hex, DateTime UpdatedUtc, TrailPoint[] Points);

        /// <summary>
        /// Path of one aircraft with the state of its requests.
        /// </summary>
        private sealed class Entry
        {
            /// <summary>
            /// Points of the path, empty when the source has no recording.
            /// </summary>
            public TrailPoint[] Points = Array.Empty<TrailPoint>();

            /// <summary>
            /// Moment of the last successful download, or default when nothing was downloaded.
            /// </summary>
            public DateTime UpdatedUtc;

            /// <summary>
            /// Moment of the last request in this run, successful or not.
            /// </summary>
            public DateTime RequestedUtc;

            /// <summary>
            /// Whether a request is in progress.
            /// </summary>
            public bool Loading;
        }

        /// <summary>
        /// Source of the paths.
        /// </summary>
        private readonly IFlightTrackSource source;

        /// <summary>
        /// Minimum time between two requests for the same flying aircraft.
        /// </summary>
        private readonly TimeSpan refreshInterval;

        /// <summary>
        /// Returns the current time in UTC.
        /// </summary>
        private readonly Func<DateTime> clock;

        /// <summary>
        /// File in which the paths are kept, or null when they are kept only in the RAM.
        /// </summary>
        private readonly string? filePath;

        /// <summary>
        /// Receives the failures of reading and writing the file, or null when nobody listens.
        /// </summary>
        private readonly Action<string, Exception>? reportError;

        /// <summary>
        /// Paths by ICAO address.
        /// </summary>
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        /// <summary>
        /// Protects <see cref="entries"/> and the file.
        /// </summary>
        private readonly object entriesLock = new object();

        /// <summary>
        /// Creates the store and reads the paths from the file.
        /// </summary>
        /// <param name="source">Source of the paths.</param>
        /// <param name="refreshInterval">Minimum time between two requests for the same flying aircraft.</param>
        /// <param name="clock">Returns the current time in UTC, or null for the system clock.</param>
        /// <param name="filePath">File in which the paths are kept, or null to keep them only in the RAM.</param>
        /// <param name="reportError">Receives the failures of reading and writing the file, or null when nobody listens.</param>
        public FlightTrackStore(IFlightTrackSource source, TimeSpan refreshInterval, Func<DateTime>? clock = null, string? filePath = null, Action<string, Exception>? reportError = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
            this.refreshInterval = refreshInterval;
            this.clock = clock ?? (() => DateTime.UtcNow);
            this.filePath = filePath;
            this.reportError = reportError;
            Load();
        }

        /// <summary>
        /// Tells whether a path describes a flight that has ended: the aircraft was in the air and the path ends on the ground.
        /// </summary>
        /// <param name="points">Points of the path.</param>
        /// <returns>True when the flight has landed.</returns>
        public static bool IsLanded(IReadOnlyList<TrailPoint> points)
        {
            return points.Count > 1 && points[points.Count - 1].OnGround && points.Any(point => !point.OnGround);
        }

        /// <summary>
        /// Downloads the path of an aircraft unless it was requested recently or a request is in progress. A landed flight is refreshed rarely and only while the aircraft is not flying; a new flight replaces the old path. A failed or limited request is not repeated before the interval passes.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="airborneNow">Whether the aircraft is flying now, according to the live data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that ends when the request is finished.</returns>
        public async Task RefreshAsync(string hex, bool airborneNow, CancellationToken cancellationToken)
        {
            Entry entry;

            lock (entriesLock)
            {
                DateTime now = clock();

                if (!entries.TryGetValue(hex, out Entry? existing))
                {
                    existing = new Entry();
                    entries[hex] = existing;
                    TrimEntries(hex, now);
                }

                TimeSpan interval = !airborneNow && IsLanded(existing.Points) ? LandedRefreshInterval : refreshInterval;

                if (existing.Loading || (existing.RequestedUtc != default && now - existing.RequestedUtc < interval))
                {
                    return;
                }

                existing.Loading = true;
                existing.RequestedUtc = now;
                entry = existing;
            }

            try
            {
                FlightTrack? track = await source.GetTrackAsync(hex, cancellationToken);

                if (track != null)
                {
                    TrailPoint[] points = Thin(track.Points);

                    lock (entriesLock)
                    {
                        entry.Points = points;
                        entry.UpdatedUtc = clock();
                        Save();
                    }
                }
            }
            finally
            {
                lock (entriesLock)
                {
                    entry.Loading = false;
                }
            }
        }

        /// <summary>
        /// Builds the trail of an aircraft: the downloaded path followed by the points collected on the phone after the end of the path.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="local">Trail collected on the phone.</param>
        /// <returns>The combined trail, or the local one when no path was downloaded.</returns>
        public TrailPoint[] Compose(string hex, TrailPoint[] local)
        {
            TrailPoint[] downloaded;

            lock (entriesLock)
            {
                downloaded = entries.TryGetValue(hex, out Entry? entry) ? entry.Points : Array.Empty<TrailPoint>();
            }

            if (downloaded.Length == 0)
            {
                return local;
            }

            DateTime lastUtc = downloaded[downloaded.Length - 1].TimestampUtc;
            List<TrailPoint> result = new List<TrailPoint>(downloaded);
            result.AddRange(local.Where(point => point.TimestampUtc > lastUtc));

            return result.ToArray();
        }

        /// <summary>
        /// Forgets all downloaded paths, also in the file.
        /// </summary>
        public void Clear()
        {
            lock (entriesLock)
            {
                entries.Clear();
                Save();
            }
        }

        /// <summary>
        /// Converts the points of a path and thins them out to <see cref="MaxPoints"/>, always keeping the first and the last point.
        /// </summary>
        /// <param name="points">Points of the path.</param>
        /// <returns>Points of the trail.</returns>
        public static TrailPoint[] Thin(IReadOnlyList<TrackPoint> points)
        {
            int count = Math.Min(points.Count, MaxPoints);
            TrailPoint[] result = new TrailPoint[count];

            for (int index = 0; index < count; index++)
            {
                int sourceIndex = count == points.Count ? index : (int)((long)index * (points.Count - 1) / (count - 1));
                TrackPoint point = points[sourceIndex];
                result[index] = new TrailPoint(point.Latitude, point.Longitude, point.AltitudeFeet, point.OnGround, point.TimestampUtc);
            }

            return result;
        }

        /// <summary>
        /// Tells whether a stored path is too old to be kept.
        /// </summary>
        /// <param name="points">Points of the path.</param>
        /// <param name="updatedUtc">Moment of the last download.</param>
        /// <param name="nowUtc">Current time.</param>
        /// <returns>True when the path should be dropped.</returns>
        private static bool IsExpired(TrailPoint[] points, DateTime updatedUtc, DateTime nowUtc)
        {
            return nowUtc - updatedUtc > (IsLanded(points) ? LandedMaxAge : InFlightMaxAge);
        }

        /// <summary>
        /// Drops the expired paths and then the least recently downloaded ones when there are too many.
        /// </summary>
        /// <param name="keepHex">ICAO address that must stay.</param>
        /// <param name="nowUtc">Current time.</param>
        private void TrimEntries(string keepHex, DateTime nowUtc)
        {
            foreach (string expired in entries.Where(pair => pair.Key != keepHex && !pair.Value.Loading && pair.Value.Points.Length > 0 && IsExpired(pair.Value.Points, pair.Value.UpdatedUtc, nowUtc)).Select(pair => pair.Key).ToList())
            {
                entries.Remove(expired);
            }

            while (entries.Count > MaxEntries)
            {
                string oldest = entries.Where(pair => pair.Key != keepHex && !pair.Value.Loading).OrderBy(pair => pair.Value.UpdatedUtc).Select(pair => pair.Key).FirstOrDefault() ?? string.Empty;

                if (oldest.Length == 0)
                {
                    return;
                }

                entries.Remove(oldest);
            }
        }

        /// <summary>
        /// Reads the paths from the file and drops the expired ones. A damaged file is ignored.
        /// </summary>
        private void Load()
        {
            if (filePath == null || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                StoredTrack[]? stored = JsonSerializer.Deserialize<StoredTrack[]>(File.ReadAllText(filePath));
                DateTime now = clock();

                foreach (StoredTrack item in stored ?? Array.Empty<StoredTrack>())
                {
                    if (item.Points.Length > 0 && !IsExpired(item.Points, item.UpdatedUtc, now))
                    {
                        entries[item.Hex] = new Entry { Points = item.Points, UpdatedUtc = item.UpdatedUtc };
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                reportError?.Invoke("Odczyt zapisanych śladów lotów nie powiódł się", exception);
            }
        }

        /// <summary>
        /// Writes the paths to the file. The caller holds <see cref="entriesLock"/>.
        /// </summary>
        private void Save()
        {
            if (filePath == null)
            {
                return;
            }

            try
            {
                StoredTrack[] stored = entries.Where(pair => pair.Value.Points.Length > 0).Select(pair => new StoredTrack(pair.Key, pair.Value.UpdatedUtc, pair.Value.Points)).ToArray();
                string? directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, JsonSerializer.Serialize(stored));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                reportError?.Invoke("Zapis śladów lotów nie powiódł się", exception);
            }
        }
    }
}

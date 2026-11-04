using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Keeps the full recorded paths of the flights downloaded from a source and combines them with the trail collected on the phone, so that the trail of a selected aircraft reaches back to the takeoff.
    /// </summary>
    public sealed class FlightTrackStore
    {
        /// <summary>
        /// Largest number of points of one path; longer paths are thinned out to keep the drawing fast.
        /// </summary>
        public const int MaxPoints = 500;

        /// <summary>
        /// Largest number of remembered paths; the least recently requested one is dropped first.
        /// </summary>
        public const int MaxEntries = 8;

        /// <summary>
        /// Path of one aircraft with the moment of the last request.
        /// </summary>
        private sealed class Entry
        {
            /// <summary>
            /// Points of the path, empty when the source has no recording.
            /// </summary>
            public TrailPoint[] Points = Array.Empty<TrailPoint>();

            /// <summary>
            /// Moment of the last request, successful or not.
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
        /// Minimum time between two requests for the same aircraft.
        /// </summary>
        private readonly TimeSpan refreshInterval;

        /// <summary>
        /// Returns the current time in UTC.
        /// </summary>
        private readonly Func<DateTime> clock;

        /// <summary>
        /// Paths by ICAO address.
        /// </summary>
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        /// <summary>
        /// Protects <see cref="entries"/>.
        /// </summary>
        private readonly object entriesLock = new object();

        /// <summary>
        /// Creates the store.
        /// </summary>
        /// <param name="source">Source of the paths.</param>
        /// <param name="refreshInterval">Minimum time between two requests for the same aircraft.</param>
        /// <param name="clock">Returns the current time in UTC, or null for the system clock.</param>
        public FlightTrackStore(IFlightTrackSource source, TimeSpan refreshInterval, Func<DateTime>? clock = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
            this.refreshInterval = refreshInterval;
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Downloads the path of an aircraft unless it was requested recently or a request is in progress. A failed or limited request is not repeated before the refresh interval passes.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that ends when the request is finished.</returns>
        public async Task RefreshAsync(string hex, CancellationToken cancellationToken)
        {
            Entry entry;

            lock (entriesLock)
            {
                DateTime now = clock();

                if (!entries.TryGetValue(hex, out Entry? existing))
                {
                    existing = new Entry();
                    entries[hex] = existing;
                    TrimEntries(hex);
                }

                if (existing.Loading || (existing.RequestedUtc != default && now - existing.RequestedUtc < refreshInterval))
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
        /// Forgets all downloaded paths.
        /// </summary>
        public void Clear()
        {
            lock (entriesLock)
            {
                entries.Clear();
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
        /// Drops the least recently requested paths when there are too many.
        /// </summary>
        /// <param name="keepHex">ICAO address that must stay.</param>
        private void TrimEntries(string keepHex)
        {
            while (entries.Count > MaxEntries)
            {
                string oldest = entries.Where(pair => pair.Key != keepHex && !pair.Value.Loading).OrderBy(pair => pair.Value.RequestedUtc).Select(pair => pair.Key).FirstOrDefault() ?? string.Empty;

                if (oldest.Length == 0)
                {
                    return;
                }

                entries.Remove(oldest);
            }
        }
    }
}

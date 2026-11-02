using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Maintains the position history (trails) of aircraft between consecutive downloads.
    /// </summary>
    public static class TrailTracker
    {
        /// <summary>
        /// Builds the trails after a download: appends the new positions, drops points older than the trail duration and trims each trail to the point limit.
        /// </summary>
        /// <param name="previous">Trails from before the download, keyed by hex code.</param>
        /// <param name="fetched">Aircraft returned by the download.</param>
        /// <param name="nowUtc">Time of the download.</param>
        /// <param name="trailDuration">How long trail points are kept.</param>
        /// <param name="maxTrailPoints">Maximum number of points in a single trail.</param>
        /// <returns>New trails keyed by hex code; aircraft missing from the download keep their remaining recent points.</returns>
        public static IReadOnlyDictionary<string, TrailPoint[]> Update(IReadOnlyDictionary<string, TrailPoint[]> previous, IReadOnlyList<Aircraft> fetched, DateTime nowUtc, TimeSpan trailDuration, int maxTrailPoints)
        {
            DateTime cutoffUtc = nowUtc - trailDuration;
            Dictionary<string, TrailPoint[]> result = new Dictionary<string, TrailPoint[]>();

            foreach (Aircraft item in fetched)
            {
                List<TrailPoint> points = new List<TrailPoint>();

                if (previous.TryGetValue(item.Hex, out TrailPoint[]? existing))
                {
                    points.AddRange(existing.Where(point => point.TimestampUtc >= cutoffUtc));
                }

                bool moved = points.Count == 0
                    || points[points.Count - 1].Latitude != item.Latitude
                    || points[points.Count - 1].Longitude != item.Longitude;

                if (moved)
                {
                    DateTime measuredAtUtc = nowUtc - TimeSpan.FromSeconds(item.SecondsSincePosition);
                    points.Add(new TrailPoint(item.Latitude, item.Longitude, item.AltitudeFeet, item.OnGround, measuredAtUtc));
                }

                if (points.Count > maxTrailPoints)
                {
                    points.RemoveRange(0, points.Count - maxTrailPoints);
                }

                result[item.Hex] = points.ToArray();
            }

            foreach (KeyValuePair<string, TrailPoint[]> entry in previous)
            {
                if (result.ContainsKey(entry.Key))
                {
                    continue;
                }

                TrailPoint[] remaining = entry.Value.Where(point => point.TimestampUtc >= cutoffUtc).ToArray();

                if (remaining.Length > 0)
                {
                    result[entry.Key] = remaining;
                }
            }

            return result;
        }
    }
}

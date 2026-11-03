using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Geometry of the airspace areas: which area lies under a point of the map.
    /// </summary>
    public static class AirspaceGeometry
    {
        /// <summary>
        /// Length of one degree of latitude, in kilometers.
        /// </summary>
        private const double KilometersPerDegree = 111.195;

        /// <summary>
        /// Tells whether a point lies inside the outline of an area.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>True when the point is inside one of the outlines. The outlines are treated as flat in degrees, which is exact enough for areas that are a few tens of kilometers across.</returns>
        public static bool Contains(AirspaceZone zone, double latitude, double longitude)
        {
            AirspaceBounds bounds = zone.Bounds;

            if (latitude < bounds.MinLatitude || latitude > bounds.MaxLatitude || longitude < bounds.MinLongitude || longitude > bounds.MaxLongitude)
            {
                return false;
            }

            foreach (IReadOnlyList<AirspaceVertex> outline in zone.Outlines)
            {
                if (ContainsInOutline(outline, latitude, longitude))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the area under a point. When areas overlap, the smallest one is returned, because it is the one hardest to hit and the most specific.
        /// </summary>
        /// <param name="views">The areas that are drawn.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>The area, or null when no drawn area contains the point.</returns>
        public static AirspaceZoneView? FindAt(IEnumerable<AirspaceZoneView> views, double latitude, double longitude)
        {
            AirspaceZoneView? best = null;
            double bestSize = double.MaxValue;

            foreach (AirspaceZoneView view in views)
            {
                if (!Contains(view.Zone, latitude, longitude))
                {
                    continue;
                }

                double size = (view.Zone.Bounds.MaxLatitude - view.Zone.Bounds.MinLatitude) * (view.Zone.Bounds.MaxLongitude - view.Zone.Bounds.MinLongitude);

                if (size < bestSize)
                {
                    bestSize = size;
                    best = view;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the distance from a point to an area.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>Zero when the point is inside the area, otherwise the distance in kilometers to the nearest edge. The edges are straight lines on a plane that touches the Earth at the point, which is exact enough for choosing which areas are near.</returns>
        public static double DistanceKilometers(AirspaceZone zone, double latitude, double longitude)
        {
            if (Contains(zone, latitude, longitude))
            {
                return 0.0;
            }

            double kilometersPerDegreeLongitude = KilometersPerDegree * Math.Cos(latitude * Math.PI / 180.0);
            double best = double.MaxValue;

            foreach (IReadOnlyList<AirspaceVertex> outline in zone.Outlines)
            {
                for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
                {
                    double ax = (outline[j].Longitude - longitude) * kilometersPerDegreeLongitude;
                    double ay = (outline[j].Latitude - latitude) * KilometersPerDegree;
                    double bx = (outline[i].Longitude - longitude) * kilometersPerDegreeLongitude;
                    double by = (outline[i].Latitude - latitude) * KilometersPerDegree;

                    best = Math.Min(best, DistanceToSegment(ax, ay, bx, by));
                }
            }

            return best;
        }

        /// <summary>
        /// Finds the middle of the rectangle that contains an area, where its label is placed.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <returns>Latitude and longitude of the middle.</returns>
        public static (double Latitude, double Longitude) GetLabelPoint(AirspaceZone zone)
        {
            return ((zone.Bounds.MinLatitude + zone.Bounds.MaxLatitude) / 2.0, (zone.Bounds.MinLongitude + zone.Bounds.MaxLongitude) / 2.0);
        }

        /// <summary>
        /// Calculates the distance from the origin to a segment.
        /// </summary>
        /// <param name="ax">Horizontal coordinate of the first end.</param>
        /// <param name="ay">Vertical coordinate of the first end.</param>
        /// <param name="bx">Horizontal coordinate of the second end.</param>
        /// <param name="by">Vertical coordinate of the second end.</param>
        /// <returns>The distance, in the unit of the coordinates.</returns>
        private static double DistanceToSegment(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared <= 0.0 ? 0.0 : Math.Clamp(-(ax * dx + ay * dy) / lengthSquared, 0.0, 1.0);
            double px = ax + t * dx;
            double py = ay + t * dy;

            return Math.Sqrt(px * px + py * py);
        }

        /// <summary>
        /// Tells whether a point is inside one outline, by counting the crossings of a ray going east from the point.
        /// </summary>
        /// <param name="outline">Corners of the outline.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>True when the point is inside.</returns>
        private static bool ContainsInOutline(IReadOnlyList<AirspaceVertex> outline, double latitude, double longitude)
        {
            bool inside = false;

            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                AirspaceVertex a = outline[i];
                AirspaceVertex b = outline[j];

                if ((a.Latitude > latitude) != (b.Latitude > latitude)
                    && longitude < (b.Longitude - a.Longitude) * (latitude - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}

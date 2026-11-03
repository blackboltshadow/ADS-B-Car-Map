namespace AdsbCarMap.Data
{
    /// <summary>
    /// Area watched by an alert: a circle or a polygon. A polygon also has a bounding circle (center and radius), which is what the aircraft requests are planned with.
    /// </summary>
    public sealed class AlertArea
    {
        /// <summary>
        /// Minimum number of vertices of a polygon.
        /// </summary>
        public const int MinimumVertices = 3;

        /// <summary>
        /// Maximum number of vertices of a polygon.
        /// </summary>
        public const int MaximumVertices = 32;

        /// <summary>
        /// Creates an area.
        /// </summary>
        /// <param name="kind">Way in which the center of the area is defined.</param>
        /// <param name="radiusKilometers">Radius of the area in kilometers; for a polygon the radius of the bounding circle.</param>
        /// <param name="center">Fixed center for <see cref="AlertAreaKind.Circle"/> (for <see cref="AlertAreaKind.Polygon"/> the center of the bounding circle), otherwise null.</param>
        /// <param name="airportIcao">ICAO code of the airport for <see cref="AlertAreaKind.AroundAirport"/>, otherwise null.</param>
        /// <param name="vertices">Vertices of the polygon for <see cref="AlertAreaKind.Polygon"/>, otherwise null.</param>
        public AlertArea(AlertAreaKind kind, double radiusKilometers, GeoPoint? center = null, string? airportIcao = null, IReadOnlyList<GeoPoint>? vertices = null)
        {
            Kind = kind;
            RadiusKilometers = radiusKilometers;
            Center = center;
            AirportIcao = airportIcao;
            Vertices = vertices;
        }

        /// <summary>
        /// Way in which the center of the area is defined.
        /// </summary>
        public AlertAreaKind Kind { get; }

        /// <summary>
        /// Radius of the area in kilometers; for a polygon the radius of the bounding circle.
        /// </summary>
        public double RadiusKilometers { get; }

        /// <summary>
        /// Fixed center for <see cref="AlertAreaKind.Circle"/> (for <see cref="AlertAreaKind.Polygon"/> the center of the bounding circle), otherwise null.
        /// </summary>
        public GeoPoint? Center { get; }

        /// <summary>
        /// ICAO code of the airport for <see cref="AlertAreaKind.AroundAirport"/>, otherwise null.
        /// </summary>
        public string? AirportIcao { get; }

        /// <summary>
        /// Vertices of the polygon for <see cref="AlertAreaKind.Polygon"/>, otherwise null.
        /// </summary>
        public IReadOnlyList<GeoPoint>? Vertices { get; }

        /// <summary>
        /// Creates a polygon area. The bounding circle is centered in the middle of the bounding box of the vertices and reaches the farthest vertex.
        /// </summary>
        /// <param name="vertices">Vertices in the drawing order, at least <see cref="MinimumVertices"/>; the polygon must not cross the 180th meridian.</param>
        /// <returns>The area.</returns>
        public static AlertArea CreatePolygon(IReadOnlyList<GeoPoint> vertices)
        {
            double minLatitude = double.MaxValue;
            double maxLatitude = double.MinValue;
            double minLongitude = double.MaxValue;
            double maxLongitude = double.MinValue;

            foreach (GeoPoint vertex in vertices)
            {
                minLatitude = Math.Min(minLatitude, vertex.Latitude);
                maxLatitude = Math.Max(maxLatitude, vertex.Latitude);
                minLongitude = Math.Min(minLongitude, vertex.Longitude);
                maxLongitude = Math.Max(maxLongitude, vertex.Longitude);
            }

            GeoPoint center = new GeoPoint((minLatitude + maxLatitude) / 2.0, (minLongitude + maxLongitude) / 2.0);
            double radius = 0.0;

            foreach (GeoPoint vertex in vertices)
            {
                radius = Math.Max(radius, GeoMath.DistanceKilometers(center.Latitude, center.Longitude, vertex.Latitude, vertex.Longitude));
            }

            return new AlertArea(AlertAreaKind.Polygon, Math.Max(radius, 0.1), center, null, new List<GeoPoint>(vertices));
        }

        /// <summary>
        /// Checks whether the vertices describe a valid polygon: closed by an edge from the last vertex to the first, with no two edges crossing or touching, no repeated vertex, no edge folded back onto the previous one and a non-zero area.
        /// </summary>
        /// <param name="vertices">Vertices in the drawing order.</param>
        /// <returns>True when the polygon has at least <see cref="MinimumVertices"/> vertices and does not intersect itself.</returns>
        public static bool IsSimple(IReadOnlyList<GeoPoint> vertices)
        {
            int count = vertices.Count;

            if (count < MinimumVertices)
            {
                return false;
            }

            double twiceArea = 0.0;

            for (int i = 0; i < count; i++)
            {
                GeoPoint a = vertices[i];
                GeoPoint b = vertices[(i + 1) % count];
                GeoPoint c = vertices[(i + 2) % count];

                if (a.Latitude == b.Latitude && a.Longitude == b.Longitude)
                {
                    return false;
                }

                twiceArea += a.Longitude * b.Latitude - b.Longitude * a.Latitude;

                double cross = Cross(a, b, c);

                if (cross == 0.0 && Dot(a, b, c) < 0.0)
                {
                    return false;
                }
            }

            if (twiceArea == 0.0)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    bool adjacent = j == i + 1 || (i == 0 && j == count - 1);

                    if (adjacent)
                    {
                        continue;
                    }

                    if (SegmentsIntersect(vertices[i], vertices[(i + 1) % count], vertices[j], vertices[(j + 1) % count]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Cross product of the vectors a to b and b to c; positive for a left turn.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        /// <param name="c">Third point.</param>
        /// <returns>The cross product in degrees squared.</returns>
        private static double Cross(GeoPoint a, GeoPoint b, GeoPoint c)
        {
            return (b.Longitude - a.Longitude) * (c.Latitude - b.Latitude) - (b.Latitude - a.Latitude) * (c.Longitude - b.Longitude);
        }

        /// <summary>
        /// Dot product of the vectors a to b and b to c; negative when the path folds back.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        /// <param name="c">Third point.</param>
        /// <returns>The dot product in degrees squared.</returns>
        private static double Dot(GeoPoint a, GeoPoint b, GeoPoint c)
        {
            return (b.Longitude - a.Longitude) * (c.Longitude - b.Longitude) + (b.Latitude - a.Latitude) * (c.Latitude - b.Latitude);
        }

        /// <summary>
        /// Checks whether two segments share a point, including touching ends and overlapping collinear parts.
        /// </summary>
        /// <param name="p1">Start of the first segment.</param>
        /// <param name="p2">End of the first segment.</param>
        /// <param name="q1">Start of the second segment.</param>
        /// <param name="q2">End of the second segment.</param>
        /// <returns>True when the segments intersect.</returns>
        private static bool SegmentsIntersect(GeoPoint p1, GeoPoint p2, GeoPoint q1, GeoPoint q2)
        {
            double d1 = Orientation(q1, q2, p1);
            double d2 = Orientation(q1, q2, p2);
            double d3 = Orientation(p1, p2, q1);
            double d4 = Orientation(p1, p2, q2);

            if (((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0)) && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0)))
            {
                return true;
            }

            return (d1 == 0.0 && OnSegment(q1, q2, p1))
                || (d2 == 0.0 && OnSegment(q1, q2, p2))
                || (d3 == 0.0 && OnSegment(p1, p2, q1))
                || (d4 == 0.0 && OnSegment(p1, p2, q2));
        }

        /// <summary>
        /// Orientation of a point relative to a directed line.
        /// </summary>
        /// <param name="a">Start of the line.</param>
        /// <param name="b">End of the line.</param>
        /// <param name="p">Point to test.</param>
        /// <returns>Positive on the left, negative on the right, zero on the line.</returns>
        private static double Orientation(GeoPoint a, GeoPoint b, GeoPoint p)
        {
            return (b.Longitude - a.Longitude) * (p.Latitude - a.Latitude) - (b.Latitude - a.Latitude) * (p.Longitude - a.Longitude);
        }

        /// <summary>
        /// Checks whether a point that lies on the line through a segment is within the segment.
        /// </summary>
        /// <param name="a">Start of the segment.</param>
        /// <param name="b">End of the segment.</param>
        /// <param name="p">Point on the line.</param>
        /// <returns>True when the point is between the ends.</returns>
        private static bool OnSegment(GeoPoint a, GeoPoint b, GeoPoint p)
        {
            return p.Longitude >= Math.Min(a.Longitude, b.Longitude) && p.Longitude <= Math.Max(a.Longitude, b.Longitude)
                && p.Latitude >= Math.Min(a.Latitude, b.Latitude) && p.Latitude <= Math.Max(a.Latitude, b.Latitude);
        }

        /// <summary>
        /// Creates a rectangle area (a polygon with four vertices) from two opposite corners.
        /// </summary>
        /// <param name="first">First corner.</param>
        /// <param name="second">Opposite corner.</param>
        /// <returns>The area.</returns>
        public static AlertArea CreateRectangle(GeoPoint first, GeoPoint second)
        {
            return CreatePolygon(new List<GeoPoint>
            {
                new GeoPoint(first.Latitude, first.Longitude),
                new GeoPoint(first.Latitude, second.Longitude),
                new GeoPoint(second.Latitude, second.Longitude),
                new GeoPoint(second.Latitude, first.Longitude),
            });
        }

        /// <summary>
        /// Finds the current center of the area.
        /// </summary>
        /// <param name="myPosition">Current position of the user or null when it is not known.</param>
        /// <param name="airportLookup">Returns the position of an airport by its ICAO code or null when it is not known.</param>
        /// <returns>The center or null when it cannot be determined now.</returns>
        public GeoPoint? ResolveCenter(GeoPoint? myPosition, Func<string, GeoPoint?> airportLookup)
        {
            switch (Kind)
            {
                case AlertAreaKind.AroundMe:
                    return myPosition;
                case AlertAreaKind.AroundAirport:
                    return AirportIcao == null ? null : airportLookup(AirportIcao);
                default:
                    return Center;
            }
        }

        /// <summary>
        /// Checks whether a point lies inside the area.
        /// </summary>
        /// <param name="center">Current center of the area, as returned by <see cref="ResolveCenter"/>.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>True when the point is within the radius of a circle or inside the polygon.</returns>
        public bool Contains(GeoPoint center, double latitude, double longitude)
        {
            if (Kind != AlertAreaKind.Polygon || Vertices == null)
            {
                return GeoMath.DistanceKilometers(center.Latitude, center.Longitude, latitude, longitude) <= RadiusKilometers;
            }

            bool inside = false;

            for (int current = 0, previous = Vertices.Count - 1; current < Vertices.Count; previous = current++)
            {
                GeoPoint a = Vertices[current];
                GeoPoint b = Vertices[previous];

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

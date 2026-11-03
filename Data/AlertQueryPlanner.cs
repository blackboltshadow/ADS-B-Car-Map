namespace AdsbCarMap.Data
{
    /// <summary>
    /// Plans the aircraft requests needed to check the alerts without the map: the areas of the rules that lie close to each other share one request.
    /// </summary>
    public static class AlertQueryPlanner
    {
        /// <summary>
        /// Kilometers in one nautical mile.
        /// </summary>
        public const double KilometersPerNauticalMile = 1.852;

        /// <summary>
        /// Largest radius accepted by the aircraft servers in nautical miles.
        /// </summary>
        public const double MaximumRadiusNauticalMiles = 250.0;

        /// <summary>
        /// Two areas are merged only when the circle around both is at most this many times larger than the larger of them.
        /// </summary>
        private const double MergeFactor = 1.5;

        /// <summary>
        /// Mean radius of the Earth in kilometers.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0088;

        /// <summary>
        /// Collects the areas of the rules whose center can be determined now.
        /// </summary>
        /// <param name="rules">Rules to collect the areas of.</param>
        /// <param name="myPosition">Current position of the user or null when it is not known.</param>
        /// <param name="airportLookup">Returns the position of an airport by its ICAO code or null when it is not known.</param>
        /// <returns>One circle per rule that has a center, in the order of the rules.</returns>
        public static List<AlertCircle> CollectCircles(IReadOnlyList<AlertRule> rules, GeoPoint? myPosition, Func<string, GeoPoint?> airportLookup)
        {
            List<AlertCircle> circles = new List<AlertCircle>();

            foreach (AlertRule rule in rules)
            {
                GeoPoint? center = rule.Area.ResolveCenter(myPosition, airportLookup);

                if (center != null)
                {
                    circles.Add(new AlertCircle(center, rule.Area.RadiusKilometers));
                }
            }

            return circles;
        }

        /// <summary>
        /// Merges areas that are close to each other, so that fewer requests are needed.
        /// </summary>
        /// <param name="circles">Areas to cover.</param>
        /// <returns>Circles that together cover all the areas; each one fits in the largest radius of the servers.</returns>
        public static List<AlertCircle> Plan(IReadOnlyList<AlertCircle> circles)
        {
            double maximumKilometers = MaximumRadiusNauticalMiles * KilometersPerNauticalMile;
            List<AlertCircle> groups = new List<AlertCircle>();

            foreach (AlertCircle circle in circles)
            {
                groups.Add(new AlertCircle(circle.Center, Math.Min(circle.RadiusKilometers, maximumKilometers)));
            }

            bool merged = true;

            while (merged)
            {
                merged = false;

                for (int first = 0; first < groups.Count && !merged; first++)
                {
                    for (int second = first + 1; second < groups.Count && !merged; second++)
                    {
                        AlertCircle enclosing = Enclose(groups[first], groups[second]);

                        if (enclosing.RadiusKilometers <= maximumKilometers
                            && enclosing.RadiusKilometers <= MergeFactor * Math.Max(groups[first].RadiusKilometers, groups[second].RadiusKilometers))
                        {
                            groups[first] = enclosing;
                            groups.RemoveAt(second);
                            merged = true;
                        }
                    }
                }
            }

            return groups;
        }

        /// <summary>
        /// Converts a radius to nautical miles for a request, rounded up.
        /// </summary>
        /// <param name="kilometers">Radius in kilometers.</param>
        /// <returns>Radius in nautical miles, at least 1 and at most the largest radius of the servers.</returns>
        public static double ToNauticalMiles(double kilometers)
        {
            return Math.Clamp(Math.Ceiling(kilometers / KilometersPerNauticalMile), 1.0, MaximumRadiusNauticalMiles);
        }

        /// <summary>
        /// Finds the smallest circle that contains both circles.
        /// </summary>
        /// <param name="first">First circle.</param>
        /// <param name="second">Second circle.</param>
        /// <returns>The circle around both.</returns>
        private static AlertCircle Enclose(AlertCircle first, AlertCircle second)
        {
            double distance = GeoMath.DistanceKilometers(first.Center.Latitude, first.Center.Longitude, second.Center.Latitude, second.Center.Longitude);

            if (distance + second.RadiusKilometers <= first.RadiusKilometers)
            {
                return first;
            }

            if (distance + first.RadiusKilometers <= second.RadiusKilometers)
            {
                return second;
            }

            double radius = (distance + first.RadiusKilometers + second.RadiusKilometers) / 2.0;
            double bearing = GeoMath.BearingDegrees(first.Center.Latitude, first.Center.Longitude, second.Center.Latitude, second.Center.Longitude);

            return new AlertCircle(MoveAlong(first.Center, bearing, radius - first.RadiusKilometers), radius);
        }

        /// <summary>
        /// Moves a point along a great circle.
        /// </summary>
        /// <param name="start">Starting point.</param>
        /// <param name="bearingDegrees">Initial bearing in degrees.</param>
        /// <param name="kilometers">Distance to move.</param>
        /// <returns>The point reached.</returns>
        private static GeoPoint MoveAlong(GeoPoint start, double bearingDegrees, double kilometers)
        {
            double angular = kilometers / EarthRadiusKilometers;
            double bearing = bearingDegrees * Math.PI / 180.0;
            double phi1 = start.Latitude * Math.PI / 180.0;
            double lambda1 = start.Longitude * Math.PI / 180.0;
            double phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(angular) + Math.Cos(phi1) * Math.Sin(angular) * Math.Cos(bearing));
            double lambda2 = lambda1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(phi1), Math.Cos(angular) - Math.Sin(phi1) * Math.Sin(phi2));

            return new GeoPoint(phi2 * 180.0 / Math.PI, ((lambda2 * 180.0 / Math.PI) + 540.0) % 360.0 - 180.0);
        }
    }
}

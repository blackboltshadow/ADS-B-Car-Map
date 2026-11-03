namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// Small geographic calculations on a sphere, used to derive the heading of a threat from its movement. The source library has no dependency on the application, so it does not share the helpers of the application.
    /// </summary>
    public static class ThreatGeo
    {
        /// <summary>
        /// Mean radius of the Earth in kilometers.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0088;

        /// <summary>
        /// Calculates the great-circle distance between two points.
        /// </summary>
        /// <param name="fromLatitude">Latitude of the first point in degrees.</param>
        /// <param name="fromLongitude">Longitude of the first point in degrees.</param>
        /// <param name="toLatitude">Latitude of the second point in degrees.</param>
        /// <param name="toLongitude">Longitude of the second point in degrees.</param>
        /// <returns>Distance in kilometers.</returns>
        public static double DistanceKilometers(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
        {
            double phi1 = ToRadians(fromLatitude);
            double phi2 = ToRadians(toLatitude);
            double deltaPhi = phi2 - phi1;
            double deltaLambda = ToRadians(toLongitude - fromLongitude);
            double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);

            return 2.0 * EarthRadiusKilometers * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        /// <summary>
        /// Calculates the initial bearing from the first point to the second.
        /// </summary>
        /// <param name="fromLatitude">Latitude of the first point in degrees.</param>
        /// <param name="fromLongitude">Longitude of the first point in degrees.</param>
        /// <param name="toLatitude">Latitude of the second point in degrees.</param>
        /// <param name="toLongitude">Longitude of the second point in degrees.</param>
        /// <returns>Bearing in degrees clockwise from north, from 0 up to but not including 360.</returns>
        public static double BearingDegrees(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
        {
            double phi1 = ToRadians(fromLatitude);
            double phi2 = ToRadians(toLatitude);
            double deltaLambda = ToRadians(toLongitude - fromLongitude);
            double y = Math.Sin(deltaLambda) * Math.Cos(phi2);
            double x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);

            return (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>Angle in radians.</returns>
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}

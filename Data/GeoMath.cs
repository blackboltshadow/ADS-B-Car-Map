namespace AdsbCarMap.Data
{
    /// <summary>
    /// Distance and bearing calculations between points on the globe.
    /// </summary>
    public static class GeoMath
    {
        /// <summary>
        /// Mean radius of the Earth in kilometers.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0088;

        /// <summary>
        /// Names of the 16 compass directions every 22.5 degrees, clockwise from north.
        /// </summary>
        private static readonly string[] CompassPoints = new string[]
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
        };

        /// <summary>
        /// Calculates the great-circle distance between two points with the haversine formula.
        /// </summary>
        /// <param name="latitude1">Latitude of the first point.</param>
        /// <param name="longitude1">Longitude of the first point.</param>
        /// <param name="latitude2">Latitude of the second point.</param>
        /// <param name="longitude2">Longitude of the second point.</param>
        /// <returns>Distance in kilometers.</returns>
        public static double DistanceKilometers(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double phi1 = ToRadians(latitude1);
            double phi2 = ToRadians(latitude2);
            double deltaPhi = ToRadians(latitude2 - latitude1);
            double deltaLambda = ToRadians(longitude2 - longitude1);

            double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0)
                + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);

            return 2.0 * EarthRadiusKilometers * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        /// <summary>
        /// Calculates the initial bearing from the first point to the second.
        /// </summary>
        /// <param name="latitude1">Latitude of the start point.</param>
        /// <param name="longitude1">Longitude of the start point.</param>
        /// <param name="latitude2">Latitude of the destination point.</param>
        /// <param name="longitude2">Longitude of the destination point.</param>
        /// <returns>Bearing in degrees from 0 to 360, measured clockwise from north.</returns>
        public static double BearingDegrees(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double phi1 = ToRadians(latitude1);
            double phi2 = ToRadians(latitude2);
            double deltaLambda = ToRadians(longitude2 - longitude1);

            double y = Math.Sin(deltaLambda) * Math.Cos(phi2);
            double x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);

            return (ToDegrees(Math.Atan2(y, x)) + 360.0) % 360.0;
        }

        /// <summary>
        /// Converts a bearing to a compass direction name, e.g. 47 degrees to "NE".
        /// </summary>
        /// <param name="degrees">Bearing in degrees.</param>
        /// <returns>Abbreviation of one of the 16 compass directions.</returns>
        public static string CompassPoint(double degrees)
        {
            int index = (int)Math.Round(((degrees % 360.0) + 360.0) % 360.0 / 22.5) % CompassPoints.Length;

            return CompassPoints[index];
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

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        /// <param name="radians">Angle in radians.</param>
        /// <returns>Angle in degrees.</returns>
        private static double ToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}

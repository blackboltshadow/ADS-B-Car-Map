namespace AdsbCarMap.Data
{
    /// <summary>
    /// Obliczenia odległości i kierunku między punktami na kuli ziemskiej.
    /// </summary>
    public static class GeoMath
    {
        /// <summary>
        /// Średni promień Ziemi w kilometrach.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0088;

        /// <summary>
        /// Nazwy 16 kierunków świata co 22,5 stopnia, od północy w prawo.
        /// </summary>
        private static readonly string[] CompassPoints = new string[]
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
        };

        /// <summary>
        /// Liczy odległość po kuli ziemskiej między dwoma punktami wzorem haversine.
        /// </summary>
        /// <param name="latitude1">Szerokość geograficzna pierwszego punktu.</param>
        /// <param name="longitude1">Długość geograficzna pierwszego punktu.</param>
        /// <param name="latitude2">Szerokość geograficzna drugiego punktu.</param>
        /// <param name="longitude2">Długość geograficzna drugiego punktu.</param>
        /// <returns>Odległość w kilometrach.</returns>
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
        /// Liczy początkowy kurs z pierwszego punktu na drugi.
        /// </summary>
        /// <param name="latitude1">Szerokość geograficzna punktu początkowego.</param>
        /// <param name="longitude1">Długość geograficzna punktu początkowego.</param>
        /// <param name="latitude2">Szerokość geograficzna punktu docelowego.</param>
        /// <param name="longitude2">Długość geograficzna punktu docelowego.</param>
        /// <returns>Kurs w stopniach od 0 do 360, liczony od północy w prawo.</returns>
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
        /// Zamienia kurs na nazwę kierunku świata, np. 47 stopni na "NE".
        /// </summary>
        /// <param name="degrees">Kurs w stopniach.</param>
        /// <returns>Skrót kierunku świata z 16 możliwych.</returns>
        public static string CompassPoint(double degrees)
        {
            int index = (int)Math.Round(((degrees % 360.0) + 360.0) % 360.0 / 22.5) % CompassPoints.Length;

            return CompassPoints[index];
        }

        /// <summary>
        /// Zamienia stopnie na radiany.
        /// </summary>
        /// <param name="degrees">Kąt w stopniach.</param>
        /// <returns>Kąt w radianach.</returns>
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Zamienia radiany na stopnie.
        /// </summary>
        /// <param name="radians">Kąt w radianach.</param>
        /// <returns>Kąt w stopniach.</returns>
        private static double ToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}

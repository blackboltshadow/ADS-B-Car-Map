namespace AdsbCarMap.DataSources.Management.Privacy
{
    /// <summary>
    /// Snaps the center of a query to a grid, so that the server learns the area but not the exact position. The result depends only on the input, so repeated queries cannot be averaged into the real position the way random noise could.
    /// </summary>
    public static class CoordinateRounder
    {
        /// <summary>
        /// Length of one degree of latitude in kilometers.
        /// </summary>
        private const double KilometersPerDegree = 111.195;

        /// <summary>
        /// Kilometers in a nautical mile.
        /// </summary>
        public const double KilometersPerNauticalMile = 1.852;

        /// <summary>
        /// Safety factor of the radius growth. The east-west width of a grid cell changes with the latitude, so the factor covers the cells far from the equator.
        /// </summary>
        private const double RadiusSafetyFactor = 1.1;

        /// <summary>
        /// Computes the circle that is sent instead of the real one.
        /// </summary>
        /// <param name="latitude">Real latitude of the center in degrees.</param>
        /// <param name="longitude">Real longitude of the center in degrees.</param>
        /// <param name="radiusNauticalMiles">Real radius in nautical miles.</param>
        /// <param name="options">How precisely the position is sent.</param>
        /// <returns>The circle to send; with the grid switched off the center is unchanged.</returns>
        public static QueryArea Round(double latitude, double longitude, double radiusNauticalMiles, PrivacyOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            double radius = radiusNauticalMiles;
            double roundedLatitude = latitude;
            double roundedLongitude = longitude;

            if (options.GridKilometers > 0.0)
            {
                double latitudeStep = options.GridKilometers / KilometersPerDegree;
                roundedLatitude = Math.Clamp(Math.Floor(latitude / latitudeStep + 0.5) * latitudeStep, -90.0, 90.0);

                double cosine = Math.Cos(roundedLatitude * Math.PI / 180.0);
                double longitudeStep = Math.Min(360.0, options.GridKilometers / (KilometersPerDegree * Math.Max(cosine, 1e-6)));
                roundedLongitude = NormalizeLongitude(Math.Floor(longitude / longitudeStep + 0.5) * longitudeStep);

                if (options.ExpandRadius)
                {
                    radius += GetMaxDisplacementNauticalMiles(options.GridKilometers);
                }
            }

            if (options.RadiusStepNauticalMiles > 0.0)
            {
                radius = Math.Ceiling(radius / options.RadiusStepNauticalMiles) * options.RadiusStepNauticalMiles;
            }

            return new QueryArea(roundedLatitude, roundedLongitude, radius);
        }

        /// <summary>
        /// Returns the largest distance between a real center and its snapped center. It depends only on the grid, never on the position, so the growth of the radius reveals nothing about where in the cell the real center is.
        /// </summary>
        /// <param name="gridKilometers">Size of the grid in kilometers.</param>
        /// <returns>Half of the diagonal of a grid cell with a safety margin, in nautical miles.</returns>
        public static double GetMaxDisplacementNauticalMiles(double gridKilometers)
        {
            return gridKilometers * Math.Sqrt(0.5) * RadiusSafetyFactor / KilometersPerNauticalMile;
        }

        /// <summary>
        /// Brings a longitude into the range from -180 to 180 degrees.
        /// </summary>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <returns>The same meridian within the range.</returns>
        private static double NormalizeLongitude(double longitude)
        {
            double result = longitude % 360.0;

            if (result > 180.0)
            {
                result -= 360.0;
            }
            else if (result < -180.0)
            {
                result += 360.0;
            }

            return result;
        }
    }
}

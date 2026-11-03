namespace AdsbCarMap.Data
{
    /// <summary>
    /// Point on the surface of the Earth.
    /// </summary>
    public sealed class GeoPoint
    {
        /// <summary>
        /// Creates a point.
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        public GeoPoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        /// <summary>
        /// Latitude in degrees.
        /// </summary>
        public double Latitude { get; }

        /// <summary>
        /// Longitude in degrees.
        /// </summary>
        public double Longitude { get; }
    }
}

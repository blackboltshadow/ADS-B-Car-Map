namespace AdsbCarMap.Data
{
    /// <summary>
    /// Circle on the surface of the Earth, used to plan the aircraft requests of the alerts.
    /// </summary>
    public sealed class AlertCircle
    {
        /// <summary>
        /// Creates a circle.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radiusKilometers">Radius in kilometers.</param>
        public AlertCircle(GeoPoint center, double radiusKilometers)
        {
            Center = center;
            RadiusKilometers = radiusKilometers;
        }

        /// <summary>
        /// Center of the circle.
        /// </summary>
        public GeoPoint Center { get; }

        /// <summary>
        /// Radius in kilometers.
        /// </summary>
        public double RadiusKilometers { get; }
    }
}

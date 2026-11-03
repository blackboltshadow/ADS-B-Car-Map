namespace AdsbCarMap.Data
{
    /// <summary>
    /// Circular area watched by an alert.
    /// </summary>
    public sealed class AlertArea
    {
        /// <summary>
        /// Creates an area.
        /// </summary>
        /// <param name="kind">Way in which the center of the area is defined.</param>
        /// <param name="radiusKilometers">Radius of the area in kilometers.</param>
        /// <param name="center">Fixed center for <see cref="AlertAreaKind.Circle"/>, otherwise null.</param>
        /// <param name="airportIcao">ICAO code of the airport for <see cref="AlertAreaKind.AroundAirport"/>, otherwise null.</param>
        public AlertArea(AlertAreaKind kind, double radiusKilometers, GeoPoint? center = null, string? airportIcao = null)
        {
            Kind = kind;
            RadiusKilometers = radiusKilometers;
            Center = center;
            AirportIcao = airportIcao;
        }

        /// <summary>
        /// Way in which the center of the area is defined.
        /// </summary>
        public AlertAreaKind Kind { get; }

        /// <summary>
        /// Radius of the area in kilometers.
        /// </summary>
        public double RadiusKilometers { get; }

        /// <summary>
        /// Fixed center for <see cref="AlertAreaKind.Circle"/>, otherwise null.
        /// </summary>
        public GeoPoint? Center { get; }

        /// <summary>
        /// ICAO code of the airport for <see cref="AlertAreaKind.AroundAirport"/>, otherwise null.
        /// </summary>
        public string? AirportIcao { get; }

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
    }
}

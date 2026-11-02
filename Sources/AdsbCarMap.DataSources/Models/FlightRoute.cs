namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Flight route with the departure and the arrival airport.
    /// </summary>
    /// <param name="OriginCode">Code of the departure airport, IATA or ICAO.</param>
    /// <param name="OriginCity">City of the departure airport or null when unknown.</param>
    /// <param name="DestinationCode">Code of the arrival airport, IATA or ICAO.</param>
    /// <param name="DestinationCity">City of the arrival airport or null when unknown.</param>
    /// <param name="AirlineName">Airline name or null when unknown.</param>
    /// <param name="OriginLatitude">Latitude of the departure airport or null when unknown.</param>
    /// <param name="OriginLongitude">Longitude of the departure airport or null when unknown.</param>
    /// <param name="DestinationLatitude">Latitude of the arrival airport or null when unknown.</param>
    /// <param name="DestinationLongitude">Longitude of the arrival airport or null when unknown.</param>
    public sealed record FlightRoute(
        string OriginCode,
        string? OriginCity,
        string DestinationCode,
        string? DestinationCity,
        string? AirlineName = null,
        double? OriginLatitude = null,
        double? OriginLongitude = null,
        double? DestinationLatitude = null,
        double? DestinationLongitude = null)
    {
        /// <summary>
        /// Whether the coordinates of both airports are known, which allows computing the flight progress.
        /// </summary>
        public bool HasCoordinates => OriginLatitude.HasValue && OriginLongitude.HasValue && DestinationLatitude.HasValue && DestinationLongitude.HasValue;

        /// <summary>
        /// Returns the route in a short form, e.g. "WAW→LHR".
        /// </summary>
        /// <returns>Airport codes joined with an arrow.</returns>
        public string ToShortString()
        {
            return OriginCode + "→" + DestinationCode;
        }
    }
}

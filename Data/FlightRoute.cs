namespace AdsbCarMap.Data
{
    /// <summary>
    /// Trasa lotu z lotniskiem wylotu i przylotu.
    /// </summary>
    /// <param name="OriginCode">Kod lotniska wylotu, IATA lub ICAO.</param>
    /// <param name="OriginCity">Miasto lotniska wylotu lub null, gdy nieznane.</param>
    /// <param name="DestinationCode">Kod lotniska przylotu, IATA lub ICAO.</param>
    /// <param name="DestinationCity">Miasto lotniska przylotu lub null, gdy nieznane.</param>
    /// <param name="AirlineName">Nazwa linii lotniczej lub null, gdy nieznana.</param>
    /// <param name="OriginLatitude">Szerokość geograficzna lotniska wylotu lub null, gdy nieznana.</param>
    /// <param name="OriginLongitude">Długość geograficzna lotniska wylotu lub null, gdy nieznana.</param>
    /// <param name="DestinationLatitude">Szerokość geograficzna lotniska przylotu lub null, gdy nieznana.</param>
    /// <param name="DestinationLongitude">Długość geograficzna lotniska przylotu lub null, gdy nieznana.</param>
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
        /// Informacja, czy znane są współrzędne obu lotnisk, co pozwala policzyć postęp lotu.
        /// </summary>
        public bool HasCoordinates => OriginLatitude.HasValue && OriginLongitude.HasValue && DestinationLatitude.HasValue && DestinationLongitude.HasValue;

        /// <summary>
        /// Zwraca trasę w krótkim zapisie, np. "WAW→LHR".
        /// </summary>
        /// <returns>Kody lotnisk połączone strzałką.</returns>
        public string ToShortString()
        {
            return OriginCode + "→" + DestinationCode;
        }
    }
}

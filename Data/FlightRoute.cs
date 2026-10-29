namespace AdsbCarMap.Data;

/// <summary>
/// Trasa lotu z lotniskiem wylotu i przylotu.
/// </summary>
/// <param name="OriginCode">Kod lotniska wylotu, IATA lub ICAO.</param>
/// <param name="OriginCity">Miasto lotniska wylotu lub null, gdy nieznane.</param>
/// <param name="DestinationCode">Kod lotniska przylotu, IATA lub ICAO.</param>
/// <param name="DestinationCity">Miasto lotniska przylotu lub null, gdy nieznane.</param>
/// <param name="AirlineName">Nazwa linii lotniczej lub null, gdy nieznana.</param>
public sealed record FlightRoute(string OriginCode, string? OriginCity, string DestinationCode, string? DestinationCity, string? AirlineName = null)
{
    /// <summary>
    /// Zwraca trasę w krótkim zapisie, np. "WAW→LHR".
    /// </summary>
    /// <returns>Kody lotnisk połączone strzałką.</returns>
    public string ToShortString()
    {
        return OriginCode + "→" + DestinationCode;
    }
}

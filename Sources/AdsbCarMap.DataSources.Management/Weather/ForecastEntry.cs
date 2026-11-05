using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Weather
{
    /// <summary>
    /// Forecast of an airport together with the moment it was downloaded.
    /// </summary>
    /// <param name="Forecast">The forecast, or null when the source has none for the airport, for example because no forecast is issued for it.</param>
    /// <param name="FetchedUtc">Moment at which the answer was downloaded, in UTC.</param>
    public sealed record ForecastEntry(Taf? Forecast, DateTime FetchedUtc);
}

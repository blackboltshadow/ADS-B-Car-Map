using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Weather
{
    /// <summary>
    /// Weather report of an airport together with the moment it was downloaded.
    /// </summary>
    /// <param name="Report">The report, or null when the source has no report for the airport, for example because it has no weather station.</param>
    /// <param name="FetchedUtc">Moment at which the answer was downloaded, in UTC.</param>
    /// <param name="DistanceKilometers">Distance in kilometers from the airport to the station when the report comes from the nearest station because the airport has none of its own; null for the report of the airport itself.</param>
    /// <param name="SearchedRadiusKilometers">Distance in kilometers within which the nearest station was looked for when the airport had no report of its own; null when it was not looked for. It tells whether the search has to be repeated after the user raises the distance.</param>
    public sealed record WeatherEntry(Metar? Report, DateTime FetchedUtc, double? DistanceKilometers = null, double? SearchedRadiusKilometers = null);
}

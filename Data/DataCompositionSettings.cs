using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.Selection;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Settings read by the sources of flight data at every request, so that a changed setting takes effect at once. The application takes them from its settings, the tests give fixed values.
    /// </summary>
    /// <param name="AircraftOrder">Names of the aircraft position sources in the order of preference.</param>
    /// <param name="RouteOrder">Names of the flight route sources in the order of preference.</param>
    /// <param name="FallbackEnabled">Whether the next source is asked when the previous one gives no data.</param>
    /// <param name="DetailsEnabled">Whether the data of the selected aircraft is completed from the other sources.</param>
    /// <param name="FlightInfoMaxAge">How long after the last sighting the information about a flight or aircraft is kept.</param>
    /// <param name="Report">Receives the failures of the sources and the switches between them, or null when nobody listens.</param>
    /// <param name="Privacy">Returns how precisely the position is sent to the aircraft sources, or null to send the exact position.</param>
    /// <param name="AreaSent">Receives the area sent to the aircraft sources at every query, or null when nobody listens.</param>
    /// <param name="WeatherMaxAge">Returns the time after which the weather report of an airport is downloaded again, or null for ten minutes.</param>
    /// <param name="ForecastMaxAge">Returns the time after which the forecast of an airport is downloaded again, or null for an hour.</param>
    public sealed record DataCompositionSettings(
        Func<IReadOnlyList<string>> AircraftOrder,
        Func<IReadOnlyList<string>> RouteOrder,
        Func<bool> FallbackEnabled,
        Func<bool> DetailsEnabled,
        Func<TimeSpan> FlightInfoMaxAge,
        Action<SourceEventKind, string, Exception?>? Report = null,
        Func<PrivacyOptions>? Privacy = null,
        Action<QueryArea>? AreaSent = null,
        Func<TimeSpan>? WeatherMaxAge = null,
        Func<TimeSpan>? ForecastMaxAge = null);
}

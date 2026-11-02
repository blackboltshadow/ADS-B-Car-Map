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
    public sealed record DataCompositionSettings(
        Func<IReadOnlyList<string>> AircraftOrder,
        Func<IReadOnlyList<string>> RouteOrder,
        Func<bool> FallbackEnabled,
        Func<bool> DetailsEnabled,
        Func<TimeSpan> FlightInfoMaxAge);
}

namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// One period of a terminal aerodrome forecast. The first period of a forecast describes the basic conditions; the following ones describe a change, and a group of a change gives only the elements that change.
    /// </summary>
    /// <param name="From">Beginning of the period.</param>
    /// <param name="To">End of the period.</param>
    /// <param name="Change">Kind of the change as in the forecast: FM (from), BECMG (becoming), TEMPO (temporarily), PROB (probability); null for the basic conditions.</param>
    /// <param name="Probability">Probability of the group in percent (30 or 40), or null when the group is not given with a probability.</param>
    /// <param name="WindDirectionDegrees">Direction the wind blows from in degrees, or null when it is variable, not given or does not change.</param>
    /// <param name="IsWindVariable">Whether the forecast gives a variable wind direction.</param>
    /// <param name="WindSpeedKnots">Mean wind speed in knots, or null when it is not given.</param>
    /// <param name="WindGustKnots">Gust speed in knots, or null when there are no gusts.</param>
    /// <param name="VisibilityMeters">Visibility in meters, or null when it is not given.</param>
    /// <param name="IsVisibilityLowerBound">Whether the visibility is only a lower bound, which means it is that value or more.</param>
    /// <param name="Clouds">Cloud layers from the lowest one; empty when the group gives none.</param>
    /// <param name="WeatherText">Weather codes, for example "BR" or "-RA", or null when there are none.</param>
    public sealed record TafPeriod(
        DateTimeOffset From,
        DateTimeOffset To,
        string? Change,
        int? Probability,
        int? WindDirectionDegrees,
        bool IsWindVariable,
        int? WindSpeedKnots,
        int? WindGustKnots,
        double? VisibilityMeters,
        bool IsVisibilityLowerBound,
        IReadOnlyList<CloudLayer> Clouds,
        string? WeatherText);

    /// <summary>
    /// Terminal aerodrome forecast (TAF) of an airport.
    /// </summary>
    /// <param name="IcaoCode">ICAO code of the airport.</param>
    /// <param name="IssuedAt">Time at which the forecast was issued.</param>
    /// <param name="ValidFrom">Beginning of the validity of the forecast.</param>
    /// <param name="ValidTo">End of the validity of the forecast.</param>
    /// <param name="RawText">Forecast in its original form.</param>
    /// <param name="Periods">Periods from the earliest one, the basic conditions first.</param>
    public sealed record Taf(
        string IcaoCode,
        DateTimeOffset IssuedAt,
        DateTimeOffset ValidFrom,
        DateTimeOffset ValidTo,
        string RawText,
        IReadOnlyList<TafPeriod> Periods);
}

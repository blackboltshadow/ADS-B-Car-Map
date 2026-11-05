namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Cloud layer of a weather report.
    /// </summary>
    /// <param name="Cover">Cover code as in the report: FEW, SCT, BKN, OVC, VV and so on.</param>
    /// <param name="BaseFeet">Height of the base above the airport in feet, or null when the report gives none.</param>
    public sealed record CloudLayer(string Cover, int? BaseFeet);

    /// <summary>
    /// Routine or special weather report of an airport (METAR or SPECI).
    /// </summary>
    /// <param name="IcaoCode">ICAO code of the airport.</param>
    /// <param name="ObservedAt">Time of the observation.</param>
    /// <param name="RawText">Report in its original form.</param>
    /// <param name="FlightCategory">Flight category given by the source: VFR, MVFR, IFR or LIFR; null when unknown.</param>
    /// <param name="WindDirectionDegrees">Direction the wind blows from in degrees, or null when it is variable or unknown.</param>
    /// <param name="IsWindVariable">Whether the report gives a variable wind direction.</param>
    /// <param name="WindSpeedKnots">Mean wind speed in knots, or null when unknown.</param>
    /// <param name="WindGustKnots">Gust speed in knots, or null when there are no gusts.</param>
    /// <param name="VisibilityMeters">Visibility in meters, or null when unknown.</param>
    /// <param name="IsVisibilityLowerBound">Whether the visibility is only a lower bound, which means it is that value or more.</param>
    /// <param name="Clouds">Cloud layers from the lowest one; empty when the sky is clear.</param>
    /// <param name="TemperatureCelsius">Air temperature in degrees Celsius, or null when unknown.</param>
    /// <param name="DewPointCelsius">Dew point in degrees Celsius, or null when unknown.</param>
    /// <param name="PressureHectopascals">Pressure reduced to sea level (QNH) in hectopascals, or null when unknown.</param>
    /// <param name="WeatherText">Present weather codes, for example "BR" or "-RA", or null when there are none.</param>
    /// <param name="Latitude">Latitude of the weather station in degrees, or null when the source does not give it.</param>
    /// <param name="Longitude">Longitude of the weather station in degrees, or null when the source does not give it.</param>
    public sealed record Metar(
        string IcaoCode,
        DateTimeOffset ObservedAt,
        string RawText,
        string? FlightCategory,
        int? WindDirectionDegrees,
        bool IsWindVariable,
        int? WindSpeedKnots,
        int? WindGustKnots,
        double? VisibilityMeters,
        bool IsVisibilityLowerBound,
        IReadOnlyList<CloudLayer> Clouds,
        double? TemperatureCelsius,
        double? DewPointCelsius,
        double? PressureHectopascals,
        string? WeatherText,
        double? Latitude = null,
        double? Longitude = null);
}

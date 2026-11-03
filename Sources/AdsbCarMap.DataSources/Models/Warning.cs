namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Voivodeship (or another region) a warning applies to.
    /// </summary>
    /// <param name="Slug">Identifier as the source writes it, e.g. "mazowieckie".</param>
    /// <param name="Name">Name to show, e.g. "Mazowieckie".</param>
    public sealed record WarningProvince(string Slug, string Name);

    /// <summary>
    /// Reading of a water gauge, given with the warning levels of the station.
    /// </summary>
    /// <param name="ValueCentimeters">Current water level in centimeters, or null when it is not given.</param>
    /// <param name="WarningCentimeters">Level at which a warning is issued, or null.</param>
    /// <param name="AlarmCentimeters">Level at which an alarm is issued, or null.</param>
    public sealed record WarningWaterLevel(double? ValueCentimeters, double? WarningCentimeters, double? AlarmCentimeters);

    /// <summary>
    /// Official notice of the regional warning system: a warning, a notice or a reading of a water gauge.
    /// </summary>
    /// <param name="Id">Identifier given by the source; the same notice keeps it when it is updated.</param>
    /// <param name="Category">Category as the source names it, e.g. "ogolne" or "hydrologiczne". It is text on purpose: a new category needs no change here.</param>
    /// <param name="Title">Title; empty for a reading of a gauge.</param>
    /// <param name="Summary">Short description; may be empty.</param>
    /// <param name="Content">Full text; may be empty.</param>
    /// <param name="IsAlarm">Whether the source marked the notice as an alarm.</param>
    /// <param name="ValidFromUtc">Start of the validity, in UTC.</param>
    /// <param name="ValidToUtc">End of the validity, in UTC, or null when the source gives none.</param>
    /// <param name="UpdatedUtc">Time of the last change at the source, in UTC.</param>
    /// <param name="Provinces">Regions the notice applies to; may be empty.</param>
    /// <param name="RiverName">Name of the river, or an empty text.</param>
    /// <param name="LocationName">Name of the place or the station, or an empty text.</param>
    /// <param name="Latitude">Latitude of the place, or null.</param>
    /// <param name="Longitude">Longitude of the place, or null.</param>
    /// <param name="WaterLevel">Reading of a water gauge, or null when the notice is not a reading.</param>
    public sealed record Warning(
        string Id,
        string Category,
        string Title,
        string Summary,
        string Content,
        bool IsAlarm,
        DateTime ValidFromUtc,
        DateTime? ValidToUtc,
        DateTime UpdatedUtc,
        IReadOnlyList<WarningProvince> Provinces,
        string RiverName,
        string LocationName,
        double? Latitude,
        double? Longitude,
        WarningWaterLevel? WaterLevel);
}

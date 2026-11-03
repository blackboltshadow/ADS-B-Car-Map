namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// One corner of the outline of an airspace area.
    /// </summary>
    /// <param name="Latitude">Latitude in degrees.</param>
    /// <param name="Longitude">Longitude in degrees.</param>
    public readonly record struct AirspaceVertex(double Latitude, double Longitude);

    /// <summary>
    /// Smallest rectangle in degrees that contains an area; it lets a map skip the areas outside the view without reading the outline.
    /// </summary>
    /// <param name="MinLatitude">Southern edge.</param>
    /// <param name="MaxLatitude">Northern edge.</param>
    /// <param name="MinLongitude">Western edge.</param>
    /// <param name="MaxLongitude">Eastern edge.</param>
    public readonly record struct AirspaceBounds(double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude);

    /// <summary>
    /// Restricted or reserved area of the airspace, e.g. a danger area or a temporary reserved area.
    /// </summary>
    /// <param name="Designator">Designation of the area, e.g. "EPD62".</param>
    /// <param name="Type">Kind of the area as the source names it, e.g. "D", "R", "TSA", "TRA". It is text on purpose: a new kind published by the source needs no change here.</param>
    /// <param name="Outlines">Outer outlines of the area; more than one for an area made of separate parts. Holes are not kept.</param>
    /// <param name="Bounds">Rectangle that contains all the outlines.</param>
    /// <param name="Reservations">Periods in which the area is planned or active; empty when the source gives none.</param>
    public sealed record AirspaceZone(
        string Designator,
        string Type,
        IReadOnlyList<IReadOnlyList<AirspaceVertex>> Outlines,
        AirspaceBounds Bounds,
        IReadOnlyList<AirspaceReservation> Reservations);
}

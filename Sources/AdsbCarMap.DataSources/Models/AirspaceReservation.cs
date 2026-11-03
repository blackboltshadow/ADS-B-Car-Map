namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// State of a reservation of an airspace area.
    /// </summary>
    public enum AirspaceReservationStatus
    {
        /// <summary>
        /// The source gave a state that is not known here.
        /// </summary>
        Other,

        /// <summary>
        /// The reservation is announced but not confirmed as active.
        /// </summary>
        Planned,

        /// <summary>
        /// The reservation is confirmed as active in its period.
        /// </summary>
        Activated,
    }

    /// <summary>
    /// What the height of an airspace limit is measured from.
    /// </summary>
    public enum AirspaceLimitReference
    {
        /// <summary>
        /// The ground; the height is zero.
        /// </summary>
        Ground,

        /// <summary>
        /// Altitude above mean sea level.
        /// </summary>
        Altitude,

        /// <summary>
        /// Flight level, i.e. pressure altitude.
        /// </summary>
        FlightLevel,
    }

    /// <summary>
    /// Lower or upper limit of an airspace area.
    /// </summary>
    /// <param name="Feet">Height in feet; zero for the ground.</param>
    /// <param name="Reference">What the height is measured from.</param>
    public readonly record struct AirspaceLimit(int Feet, AirspaceLimitReference Reference);

    /// <summary>
    /// One period in which an airspace area is reserved.
    /// </summary>
    /// <param name="StartUtc">Start of the period, in UTC.</param>
    /// <param name="EndUtc">End of the period, in UTC.</param>
    /// <param name="Lower">Lower limit of the reserved space.</param>
    /// <param name="Upper">Upper limit of the reserved space.</param>
    /// <param name="Status">State of the reservation.</param>
    /// <param name="Unit">Unit that reserved the area, as the source gives it; may be empty.</param>
    /// <param name="Remarks">Remarks of the source, e.g. a notice number; null when there are none.</param>
    public sealed record AirspaceReservation(
        DateTime StartUtc,
        DateTime EndUtc,
        AirspaceLimit Lower,
        AirspaceLimit Upper,
        AirspaceReservationStatus Status,
        string Unit,
        string? Remarks);
}

namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Kind of an airborne threat reported by a threat source.
    /// </summary>
    public enum ThreatKind
    {
        /// <summary>
        /// Kind that the application does not know.
        /// </summary>
        Unknown,

        /// <summary>
        /// Unmanned aerial vehicle (a drone of the Shahed class).
        /// </summary>
        Uav,

        /// <summary>
        /// Small first-person-view drone.
        /// </summary>
        Fpv,

        /// <summary>
        /// Reconnaissance drone.
        /// </summary>
        Recon,

        /// <summary>
        /// Cruise or other missile.
        /// </summary>
        Missile,

        /// <summary>
        /// Ballistic missile.
        /// </summary>
        Ballistic,

        /// <summary>
        /// Guided aerial bomb.
        /// </summary>
        Kab,

        /// <summary>
        /// MiG-31K carrier aircraft.
        /// </summary>
        Mig31k,
    }

    /// <summary>
    /// How sure the source is that a threat is real.
    /// </summary>
    public enum ThreatConfidence
    {
        /// <summary>
        /// Single or weak report.
        /// </summary>
        Low,

        /// <summary>
        /// Several reports or one trusted report.
        /// </summary>
        Medium,

        /// <summary>
        /// Confirmed by several reports.
        /// </summary>
        High,
    }

    /// <summary>
    /// State of a threat track.
    /// </summary>
    public enum ThreatStatus
    {
        /// <summary>
        /// The track is current.
        /// </summary>
        Active,

        /// <summary>
        /// The track has not been confirmed for a while.
        /// </summary>
        Stale,

        /// <summary>
        /// The threat is over and the track is about to disappear.
        /// </summary>
        Resolved,
    }

    /// <summary>
    /// One point of the recent path of a threat.
    /// </summary>
    /// <param name="Latitude">Latitude in degrees.</param>
    /// <param name="Longitude">Longitude in degrees.</param>
    /// <param name="TimeUtc">Time of the report, or null when the source did not give it.</param>
    public sealed record ThreatTrailPoint(double Latitude, double Longitude, DateTime? TimeUtc);

    /// <summary>
    /// A threat track reported by a threat source.
    /// </summary>
    /// <param name="Id">Identifier of the track, stable while the track lives.</param>
    /// <param name="Kind">Kind of the threat.</param>
    /// <param name="Title">Name of the threat as given by the source, in the language of the source.</param>
    /// <param name="Region">Region (oblast) the threat is in, or an empty text.</param>
    /// <param name="District">District the threat is in, or an empty text.</param>
    /// <param name="Locality">Nearest locality, or an empty text.</param>
    /// <param name="Latitude">Latitude of the reported position in degrees; for a threat that is <paramref name="AreaOnly"/> the center of the region.</param>
    /// <param name="Longitude">Longitude of the reported position in degrees; for a threat that is <paramref name="AreaOnly"/> the center of the region.</param>
    /// <param name="HeadingDegrees">Direction of flight in degrees clockwise from north, or null when unknown.</param>
    /// <param name="SpeedKmh">Speed in kilometers per hour, or null when unknown.</param>
    /// <param name="Confidence">How sure the source is.</param>
    /// <param name="SourceCount">Number of reports the track is based on.</param>
    /// <param name="Count">Number of objects in the group, or 0 when the source does not say.</param>
    /// <param name="UpdatedUtc">Time of the last change of the track, or null when unknown.</param>
    /// <param name="ConfirmedUtc">Time of the last confirmed position, or null when unknown.</param>
    /// <param name="Status">State of the track.</param>
    /// <param name="UncertaintyKm">Radius in kilometers within which the real position probably is, or 0 when unknown.</param>
    /// <param name="IsApproximate">Whether the position is only approximate.</param>
    /// <param name="IsPresumptiveCourse">Whether the heading is only an assumed course and not an observed one.</param>
    /// <param name="IsSea">Whether the threat is over the sea.</param>
    /// <param name="Advisory">Whether this is an observation and not a signal to take shelter, for example a takeoff of a carrier aircraft; it must not be shown as an alarm.</param>
    /// <param name="AreaOnly">Whether only a region is known: the position is not a place, and no movement or distance may be derived from it.</param>
    /// <param name="Explanation">Short description given by the source, or an empty text.</param>
    /// <param name="Trail">Recent path of the threat, oldest first; empty when unknown.</param>
    /// <param name="IsHeadingDerived">Whether the heading was not given by the source but worked out from the movement of the threat.</param>
    public sealed record Threat(
        string Id,
        ThreatKind Kind,
        string Title,
        string Region,
        string District,
        string Locality,
        double Latitude,
        double Longitude,
        double? HeadingDegrees,
        double? SpeedKmh,
        ThreatConfidence Confidence,
        int SourceCount,
        int Count,
        DateTime? UpdatedUtc,
        DateTime? ConfirmedUtc,
        ThreatStatus Status,
        double UncertaintyKm,
        bool IsApproximate,
        bool IsPresumptiveCourse,
        bool IsSea,
        bool Advisory,
        bool AreaOnly,
        string Explanation,
        IReadOnlyList<ThreatTrailPoint> Trail,
        bool IsHeadingDerived = false);
}

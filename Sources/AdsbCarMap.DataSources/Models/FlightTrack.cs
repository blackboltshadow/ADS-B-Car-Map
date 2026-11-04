namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Position of an aircraft recorded by a source in the past.
    /// </summary>
    /// <param name="Latitude">Latitude.</param>
    /// <param name="Longitude">Longitude.</param>
    /// <param name="AltitudeFeet">Barometric altitude in feet, or null when unknown.</param>
    /// <param name="OnGround">Whether the aircraft was on the ground.</param>
    /// <param name="TimestampUtc">Moment of the position measurement, in UTC.</param>
    public readonly record struct TrackPoint(double Latitude, double Longitude, int? AltitudeFeet, bool OnGround, DateTime TimestampUtc);

    /// <summary>
    /// Recorded path of a single flight of an aircraft, from the oldest point.
    /// </summary>
    /// <param name="Hex">ICAO address of the aircraft.</param>
    /// <param name="Points">Points of the flight in chronological order.</param>
    public sealed record FlightTrack(string Hex, IReadOnlyList<TrackPoint> Points);
}

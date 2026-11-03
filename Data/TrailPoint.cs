namespace AdsbCarMap.Data
{
    /// <summary>
    /// Aircraft trail point remembered from one of the earlier API responses.
    /// </summary>
    /// <param name="Latitude">Latitude.</param>
    /// <param name="Longitude">Longitude.</param>
    /// <param name="AltitudeFeet">Barometric altitude in feet, or null when unknown.</param>
    /// <param name="OnGround">Whether the aircraft was on the ground.</param>
    /// <param name="TimestampUtc">Moment of the position measurement, in UTC.</param>
    public readonly record struct TrailPoint(double Latitude, double Longitude, int? AltitudeFeet, bool OnGround, DateTime TimestampUtc);
}

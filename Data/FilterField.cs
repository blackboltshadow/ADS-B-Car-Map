namespace AdsbCarMap.Data
{
    /// <summary>
    /// Aircraft property tested by a filter condition.
    /// </summary>
    public enum FilterField
    {
        /// <summary>
        /// The aircraft is on the ground.
        /// </summary>
        OnGround,

        /// <summary>
        /// The aircraft is marked as military.
        /// </summary>
        Military,

        /// <summary>
        /// Kind of the aircraft ascertained by the classifier.
        /// </summary>
        Kind,

        /// <summary>
        /// Manufacturer of the aircraft type.
        /// </summary>
        Manufacturer,

        /// <summary>
        /// ICAO type code.
        /// </summary>
        TypeCode,

        /// <summary>
        /// Beginning of the callsign, which identifies the airline.
        /// </summary>
        CallsignPrefix,

        /// <summary>
        /// Barometric altitude in meters.
        /// </summary>
        Altitude,

        /// <summary>
        /// Airport the flight departed from, by code or city.
        /// </summary>
        Origin,

        /// <summary>
        /// Airport the flight is heading to, by code or city.
        /// </summary>
        Destination,
    }
}

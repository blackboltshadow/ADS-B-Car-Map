namespace AdsbCarMap.Data
{
    /// <summary>
    /// An aircraft type seen in the traffic that the type table does not describe, with an example of an aircraft of that type.
    /// </summary>
    public sealed class MissingTypeEntry
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="icao">ICAO type code.</param>
        /// <param name="count">Number of aircraft seen.</param>
        /// <param name="firstSeenUtc">Moment the type was first seen, in UTC.</param>
        /// <param name="lastSeenUtc">Moment the type was last seen, in UTC.</param>
        /// <param name="callsign">Callsign of an example aircraft, or an empty string.</param>
        /// <param name="registration">Registration of an example aircraft, or an empty string.</param>
        /// <param name="category">ADS-B category of an example aircraft, or an empty string.</param>
        /// <param name="isMilitary">Whether the example aircraft is marked as military.</param>
        public MissingTypeEntry(string icao, int count, DateTime firstSeenUtc, DateTime lastSeenUtc, string callsign, string registration, string category, bool isMilitary)
        {
            Icao = icao;
            Count = count;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            Callsign = callsign;
            Registration = registration;
            Category = category;
            IsMilitary = isMilitary;
        }

        /// <summary>
        /// ICAO type code.
        /// </summary>
        public string Icao { get; }

        /// <summary>
        /// Number of aircraft seen. An aircraft is counted once per run of the application, so one that is seen again after a restart is counted again.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Moment the type was first seen, in UTC.
        /// </summary>
        public DateTime FirstSeenUtc { get; }

        /// <summary>
        /// Moment the type was last seen, in UTC.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        /// <summary>
        /// Callsign of an example aircraft, or an empty string.
        /// </summary>
        public string Callsign { get; set; }

        /// <summary>
        /// Registration of an example aircraft, or an empty string.
        /// </summary>
        public string Registration { get; set; }

        /// <summary>
        /// ADS-B category of an example aircraft, or an empty string.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Whether the example aircraft is marked as military.
        /// </summary>
        public bool IsMilitary { get; set; }
    }
}

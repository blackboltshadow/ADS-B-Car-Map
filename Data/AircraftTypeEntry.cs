namespace AdsbCarMap.Data
{
    /// <summary>
    /// One row of the aircraft type table: an ICAO type code with its IATA code, family and name.
    /// </summary>
    public sealed class AircraftTypeEntry
    {
        /// <summary>
        /// Creates a row of the table.
        /// </summary>
        /// <param name="iata">IATA type code, or an empty string when the type has none.</param>
        /// <param name="icao">ICAO type code, for example "B738".</param>
        /// <param name="family">Name of the family the type belongs to.</param>
        /// <param name="name">Full name of the type.</param>
        public AircraftTypeEntry(string iata, string icao, string family, string name)
        {
            Iata = iata;
            Icao = icao;
            Family = family;
            Name = name;
        }

        /// <summary>
        /// IATA type code, or an empty string when the type has none.
        /// </summary>
        public string Iata { get; }

        /// <summary>
        /// ICAO type code, for example "B738".
        /// </summary>
        public string Icao { get; }

        /// <summary>
        /// Name of the family the type belongs to, for example "B737" for every 737 including the MAX.
        /// </summary>
        public string Family { get; }

        /// <summary>
        /// Full name of the type, for example "Boeing 737-800".
        /// </summary>
        public string Name { get; }
    }
}

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Size class of an airport, from the biggest. The order is used to rank airports.
    /// </summary>
    public enum AirportType
    {
        /// <summary>
        /// Large airport with regular scheduled flights.
        /// </summary>
        Large = 0,

        /// <summary>
        /// Medium airport.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Small airport or airfield.
        /// </summary>
        Small = 2,

        /// <summary>
        /// Heliport or helipad.
        /// </summary>
        Heliport = 3,
    }

    /// <summary>
    /// One airport from the OurAirports data bundled with the application.
    /// </summary>
    public sealed class Airport
    {
        /// <summary>
        /// Creates an airport.
        /// </summary>
        /// <param name="icao">ICAO or local code.</param>
        /// <param name="iata">IATA code, or an empty string when the airport has none.</param>
        /// <param name="type">Size class.</param>
        /// <param name="name">Name.</param>
        /// <param name="municipality">City or town, or an empty string.</param>
        /// <param name="country">ISO code of the country.</param>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <param name="elevationFeet">Elevation above sea level in feet, or null when unknown.</param>
        /// <param name="hasScheduledService">Whether the airport has scheduled passenger service.</param>
        /// <param name="isMilitary">Whether the airport is military, guessed from its name.</param>
        public Airport(string icao, string iata, AirportType type, string name, string municipality, string country, double latitude, double longitude, int? elevationFeet, bool hasScheduledService, bool isMilitary)
        {
            Icao = icao;
            Iata = iata;
            Type = type;
            Name = name;
            Municipality = municipality;
            Country = country;
            Latitude = latitude;
            Longitude = longitude;
            ElevationFeet = elevationFeet;
            HasScheduledService = hasScheduledService;
            IsMilitary = isMilitary;
            SearchText = TextFolding.Fold(icao + " " + iata + " " + name + " " + municipality);
        }

        /// <summary>
        /// ICAO or local code.
        /// </summary>
        public string Icao { get; }

        /// <summary>
        /// IATA code, or an empty string when the airport has none.
        /// </summary>
        public string Iata { get; }

        /// <summary>
        /// Size class.
        /// </summary>
        public AirportType Type { get; }

        /// <summary>
        /// Name of the airport.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// City or town, or an empty string.
        /// </summary>
        public string Municipality { get; }

        /// <summary>
        /// ISO code of the country.
        /// </summary>
        public string Country { get; }

        /// <summary>
        /// Latitude in degrees.
        /// </summary>
        public double Latitude { get; }

        /// <summary>
        /// Longitude in degrees.
        /// </summary>
        public double Longitude { get; }

        /// <summary>
        /// Elevation above sea level in feet, or null when unknown.
        /// </summary>
        public int? ElevationFeet { get; }

        /// <summary>
        /// Whether the airport has scheduled passenger service.
        /// </summary>
        public bool HasScheduledService { get; }

        /// <summary>
        /// Whether the airport is military, guessed from its name.
        /// </summary>
        public bool IsMilitary { get; }

        /// <summary>
        /// Whether the airport has a paved runway at least 800 m long. Set when the runways are loaded.
        /// </summary>
        public bool HasLongPavedRunway { get; private set; }

        /// <summary>
        /// Lower-case text without diacritics that is searched for the words of a query.
        /// </summary>
        public string SearchText { get; }

        /// <summary>
        /// Code shown on the map and in lists: the IATA code, or the ICAO code when there is no IATA code.
        /// </summary>
        public string Code => Iata.Length > 0 ? Iata : Icao;

        /// <summary>
        /// Records whether the airport has a paved runway at least 800 m long.
        /// </summary>
        /// <param name="value">True when it has one.</param>
        internal void SetLongPavedRunway(bool value)
        {
            HasLongPavedRunway = value;
        }
    }

    /// <summary>
    /// A runway of an airport, described by the positions of its two ends.
    /// </summary>
    /// <param name="LengthFeet">Length in feet, or 0 when unknown.</param>
    /// <param name="WidthFeet">Width in feet, or 0 when unknown.</param>
    /// <param name="Surface">Surface code, for example ASPH, or an empty string.</param>
    /// <param name="IsLighted">Whether the runway is lighted.</param>
    /// <param name="LowEndName">Designator of the low-numbered end, for example 15.</param>
    /// <param name="LowEndLatitude">Latitude of the low-numbered end.</param>
    /// <param name="LowEndLongitude">Longitude of the low-numbered end.</param>
    /// <param name="HighEndName">Designator of the high-numbered end, for example 33.</param>
    /// <param name="HighEndLatitude">Latitude of the high-numbered end.</param>
    /// <param name="HighEndLongitude">Longitude of the high-numbered end.</param>
    public sealed record Runway(int LengthFeet, int WidthFeet, string Surface, bool IsLighted, string LowEndName, double LowEndLatitude, double LowEndLongitude, string HighEndName, double HighEndLatitude, double HighEndLongitude);
}

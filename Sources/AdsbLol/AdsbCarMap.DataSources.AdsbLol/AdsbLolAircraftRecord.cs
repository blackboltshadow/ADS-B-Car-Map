using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// One element of the "ac" array of an adsb.lol response, with the fields named as in the API.
    /// </summary>
    internal sealed class AdsbLolAircraftRecord
    {
        /// <summary>
        /// ICAO address ("hex").
        /// </summary>
        public string? Hex { get; private set; }

        /// <summary>
        /// Callsign ("flight").
        /// </summary>
        public string? Flight { get; private set; }

        /// <summary>
        /// Latitude ("lat").
        /// </summary>
        public double? Lat { get; private set; }

        /// <summary>
        /// Longitude ("lon").
        /// </summary>
        public double? Lon { get; private set; }

        /// <summary>
        /// Barometric altitude in feet ("alt_baro") when it is a number.
        /// </summary>
        public double? AltBaro { get; private set; }

        /// <summary>
        /// Whether "alt_baro" is the text "ground".
        /// </summary>
        public bool IsGround { get; private set; }

        /// <summary>
        /// Ground speed in knots ("gs").
        /// </summary>
        public double? GroundSpeed { get; private set; }

        /// <summary>
        /// Ground track in degrees ("track").
        /// </summary>
        public double? Track { get; private set; }

        /// <summary>
        /// Heading in degrees ("true_heading"), used when there is no track.
        /// </summary>
        public double? TrueHeading { get; private set; }

        /// <summary>
        /// Age of the position in seconds ("seen_pos").
        /// </summary>
        public double? SeenPos { get; private set; }

        /// <summary>
        /// Aircraft type code ("t").
        /// </summary>
        public string? Type { get; private set; }

        /// <summary>
        /// Registration ("r").
        /// </summary>
        public string? Registration { get; private set; }

        /// <summary>
        /// Emitter category ("category").
        /// </summary>
        public string? Category { get; private set; }

        /// <summary>
        /// Database flags ("dbFlags"), the lowest bit marks a military aircraft.
        /// </summary>
        public double? DbFlags { get; private set; }

        /// <summary>
        /// Barometric vertical rate in feet per minute ("baro_rate").
        /// </summary>
        public double? BaroRate { get; private set; }

        /// <summary>
        /// Geometric vertical rate in feet per minute ("geom_rate").
        /// </summary>
        public double? GeomRate { get; private set; }

        /// <summary>
        /// Transponder code ("squawk").
        /// </summary>
        public string? Squawk { get; private set; }

        /// <summary>
        /// Reads a record from a JSON element.
        /// </summary>
        /// <param name="element">Element of the "ac" array.</param>
        /// <returns>The record.</returns>
        public static AdsbLolAircraftRecord FromJson(JsonElement element)
        {
            AdsbLolAircraftRecord record = new AdsbLolAircraftRecord();
            record.Hex = GetString(element, "hex");
            record.Flight = GetString(element, "flight");
            record.Lat = GetDouble(element, "lat");
            record.Lon = GetDouble(element, "lon");
            record.GroundSpeed = GetDouble(element, "gs");
            record.Track = GetDouble(element, "track");
            record.TrueHeading = GetDouble(element, "true_heading");
            record.SeenPos = GetDouble(element, "seen_pos");
            record.Type = GetString(element, "t");
            record.Registration = GetString(element, "r");
            record.Category = GetString(element, "category");
            record.DbFlags = GetDouble(element, "dbFlags");
            record.BaroRate = GetDouble(element, "baro_rate");
            record.GeomRate = GetDouble(element, "geom_rate");
            record.Squawk = GetString(element, "squawk");

            if (element.TryGetProperty("alt_baro", out JsonElement altitude))
            {
                if (altitude.ValueKind == JsonValueKind.String && altitude.GetString() == "ground")
                {
                    record.IsGround = true;
                }
                else if (altitude.ValueKind == JsonValueKind.Number)
                {
                    record.AltBaro = altitude.GetDouble();
                }
            }

            return record;
        }

        /// <summary>
        /// Converts the record to the aircraft model of the data source contract.
        /// </summary>
        /// <returns>The aircraft or null when the address or the position is missing.</returns>
        public Aircraft? ToAircraft()
        {
            if (string.IsNullOrEmpty(Hex) || !Lat.HasValue || !Lon.HasValue)
            {
                return null;
            }

            string? callsign = Flight?.Trim();
            string? typeCode = Type?.Trim();
            string? registration = Registration?.Trim();
            string? category = Category?.Trim();
            string? squawk = Squawk?.Trim();
            double? verticalRate = BaroRate ?? GeomRate;

            return new Aircraft(
                Hex,
                string.IsNullOrEmpty(callsign) ? null : callsign,
                Lat.Value,
                Lon.Value,
                AltBaro.HasValue ? (int)Math.Round(AltBaro.Value) : null,
                IsGround,
                GroundSpeed,
                Track ?? TrueHeading,
                SeenPos ?? 0.0,
                string.IsNullOrEmpty(typeCode) ? null : typeCode,
                string.IsNullOrEmpty(registration) ? null : registration,
                string.IsNullOrEmpty(category) ? null : category,
                DbFlags is double flags && ((int)flags & 1) != 0,
                verticalRate.HasValue ? (int)Math.Round(verticalRate.Value) : null,
                string.IsNullOrEmpty(squawk) ? null : squawk);
        }

        /// <summary>
        /// Converts the record to the descriptive data of the data source contract.
        /// </summary>
        /// <returns>The data or null when the address is missing.</returns>
        public AircraftDetails? ToDetails()
        {
            if (string.IsNullOrEmpty(Hex))
            {
                return null;
            }

            string? typeCode = Type?.Trim();
            string? registration = Registration?.Trim();
            string? category = Category?.Trim();

            return new AircraftDetails(
                Hex,
                string.IsNullOrEmpty(typeCode) ? null : typeCode,
                string.IsNullOrEmpty(registration) ? null : registration,
                string.IsNullOrEmpty(category) ? null : category);
        }
        /// <summary>
        /// Reads a numeric property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The value or null when the property does not exist or is not a number.</returns>
        private static double? GetDouble(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble();
            }

            return null;
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The value or null when the property does not exist or is not a string.</returns>
        private static string? GetString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }
    }
}

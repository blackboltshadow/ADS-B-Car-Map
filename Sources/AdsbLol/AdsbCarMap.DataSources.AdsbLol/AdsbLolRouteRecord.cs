using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// Answer of the adsb.lol route API: the airports of one flight, with the fields named as in the API.
    /// </summary>
    internal sealed class AdsbLolRouteRecord
    {
        /// <summary>
        /// One airport of the route.
        /// </summary>
        /// <param name="Icao">ICAO code ("icao").</param>
        /// <param name="Iata">IATA code ("iata").</param>
        /// <param name="Location">City ("location").</param>
        /// <param name="Latitude">Latitude ("lat").</param>
        /// <param name="Longitude">Longitude ("lon").</param>
        public sealed record Airport(string? Icao, string? Iata, string? Location, double? Latitude, double? Longitude);

        /// <summary>
        /// Airports of the route in the order of the flight ("_airports"); a route with stops has more than two.
        /// </summary>
        public List<Airport> Airports { get; } = new List<Airport>();

        /// <summary>
        /// Reads the answer from a JSON element.
        /// </summary>
        /// <param name="element">Root element of the answer.</param>
        /// <returns>The record.</returns>
        public static AdsbLolRouteRecord FromJson(JsonElement element)
        {
            AdsbLolRouteRecord record = new AdsbLolRouteRecord();

            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("_airports", out JsonElement airports) || airports.ValueKind != JsonValueKind.Array)
            {
                return record;
            }

            foreach (JsonElement airport in airports.EnumerateArray())
            {
                if (airport.ValueKind == JsonValueKind.Object)
                {
                    record.Airports.Add(new Airport(GetString(airport, "icao"), GetString(airport, "iata"), GetString(airport, "location"), GetDouble(airport, "lat"), GetDouble(airport, "lon")));
                }
            }

            return record;
        }

        /// <summary>
        /// Converts the record to the route model of the data source contract: the first airport is the origin and the last one the destination.
        /// </summary>
        /// <returns>The route or null when the answer has fewer than two airports with a code.</returns>
        public FlightRoute? ToRoute()
        {
            if (Airports.Count < 2)
            {
                return null;
            }

            Airport origin = Airports[0];
            Airport destination = Airports[^1];
            string? originCode = GetCode(origin);
            string? destinationCode = GetCode(destination);

            if (originCode == null || destinationCode == null)
            {
                return null;
            }

            return new FlightRoute(
                originCode,
                NullIfEmpty(origin.Location),
                destinationCode,
                NullIfEmpty(destination.Location),
                null,
                origin.Latitude,
                origin.Longitude,
                destination.Latitude,
                destination.Longitude);
        }

        /// <summary>
        /// Returns the IATA code of an airport, or the ICAO code when there is no IATA code.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <returns>The code or null when the airport has none.</returns>
        private static string? GetCode(Airport airport)
        {
            return !string.IsNullOrEmpty(airport.Iata) ? airport.Iata : NullIfEmpty(airport.Icao);
        }

        /// <summary>
        /// Converts an empty text to null.
        /// </summary>
        /// <param name="text">The text or null.</param>
        /// <returns>The text or null when it is empty.</returns>
        private static string? NullIfEmpty(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
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

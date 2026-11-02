using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using System.Net;
using System.Text.Json;

namespace AdsbCarMap.DataSources.Adsbdb
{
    /// <summary>
    /// Client of the adsbdb.com API returning the flight route for a callsign.
    /// </summary>
    public sealed class AdsbdbRouteClient : IRouteSource
    {
        /// <summary>
        /// Default base address of the adsbdb.com API.
        /// </summary>
        public const string DefaultBaseAddress = "https://api.adsbdb.com/";

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public const string SourceName = "adsbdb.com";

        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API.</param>
        public AdsbdbRouteClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => SourceName;

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync("v0/callsign/" + Uri.EscapeDataString(callsign), cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            if (!document.RootElement.TryGetProperty("response", out JsonElement body)
                || body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("flightroute", out JsonElement route)
                || route.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            (string? Code, string? City, double? Latitude, double? Longitude) origin = ParseAirport(route, "origin");
            (string? Code, string? City, double? Latitude, double? Longitude) destination = ParseAirport(route, "destination");

            if (origin.Code == null || destination.Code == null)
            {
                return null;
            }

            string? airlineName = null;

            if (route.TryGetProperty("airline", out JsonElement airline) && airline.ValueKind == JsonValueKind.Object)
            {
                airlineName = GetString(airline, "name");
            }

            return new FlightRoute(
                origin.Code,
                origin.City,
                destination.Code,
                destination.City,
                string.IsNullOrWhiteSpace(airlineName) ? null : airlineName,
                origin.Latitude,
                origin.Longitude,
                destination.Latitude,
                destination.Longitude);
        }

        /// <summary>
        /// Reads the code and the city of an airport from the route object.
        /// </summary>
        /// <param name="route">The "flightroute" object.</param>
        /// <param name="name">Name of the airport property, "origin" or "destination".</param>
        /// <returns>The IATA code, or the ICAO code when there is no IATA code, the city and the coordinates. Null when the data is missing.</returns>
        private static (string? Code, string? City, double? Latitude, double? Longitude) ParseAirport(JsonElement route, string name)
        {
            if (!route.TryGetProperty(name, out JsonElement airport) || airport.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null, null);
            }

            string? iata = GetString(airport, "iata_code");
            string? icao = GetString(airport, "icao_code");
            string? city = GetString(airport, "municipality");

            return (string.IsNullOrEmpty(iata) ? icao : iata, city, GetDouble(airport, "latitude"), GetDouble(airport, "longitude"));
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

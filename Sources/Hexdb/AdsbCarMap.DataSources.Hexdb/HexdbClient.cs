using System.Net;
using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Hexdb
{
    /// <summary>
    /// Client of the hexdb.io API returning the flight route for a callsign and the data of an aircraft by its ICAO address. The route is only a pair of ICAO airport codes, without names or coordinates. It only fetches and converts the data; pausing the requests after limit responses is up to the manager.
    /// </summary>
    public sealed class HexdbClient : IRouteSource, IAircraftLookupSource
    {
        /// <summary>
        /// Default base address of the hexdb.io API.
        /// </summary>
        public const string DefaultBaseAddress = "https://hexdb.io/";

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public const string SourceName = "hexdb.io";

        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API.</param>
        public HexdbClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => SourceName;

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            JsonElement? body = await GetBodyAsync("api/v1/route/icao/" + Uri.EscapeDataString(callsign), cancellationToken);

            if (body == null)
            {
                return null;
            }

            string? route = GetString(body.Value, "route");
            string[] codes = string.IsNullOrWhiteSpace(route) ? Array.Empty<string>() : route.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (codes.Length < 2)
            {
                return null;
            }

            return new FlightRoute(codes[0], null, codes[^1], null);
        }

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
        {
            JsonElement? body = await GetBodyAsync("api/v1/aircraft/" + Uri.EscapeDataString(hex), cancellationToken);

            if (body == null)
            {
                return null;
            }

            return new AircraftDetails(
                hex,
                NullIfEmpty(GetString(body.Value, "ICAOTypeCode")),
                NullIfEmpty(GetString(body.Value, "Registration")),
                null,
                NullIfEmpty(GetString(body.Value, "Type")),
                NullIfEmpty(GetString(body.Value, "Manufacturer")),
                NullIfEmpty(GetString(body.Value, "RegisteredOwners")));
        }

        /// <summary>
        /// Downloads an answer and returns its JSON object.
        /// </summary>
        /// <param name="path">Path of the request relative to the base address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The root element, or null when the server does not know the item (404) or the answer is not an object.</returns>
        private async Task<JsonElement?> GetBodyAsync(string path, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }

        /// <summary>
        /// Reads the pause requested by the server with the Retry-After header.
        /// </summary>
        /// <param name="response">The limit response.</param>
        /// <returns>The requested pause or zero when the header is missing.</returns>
        private static TimeSpan GetRetryAfter(HttpResponseMessage response)
        {
            TimeSpan? requested = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);

            return requested.HasValue && requested.Value > TimeSpan.Zero ? requested.Value : TimeSpan.Zero;
        }

        /// <summary>
        /// Converts an empty text to null.
        /// </summary>
        /// <param name="text">The text or null.</param>
        /// <returns>The trimmed text or null when it is empty.</returns>
        private static string? NullIfEmpty(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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

using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.RateLimiting;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// Client of the adsb.lol API returning the aircraft within a radius of a point.
    /// </summary>
    public sealed class AdsbLolClient : IAircraftSource
    {
        /// <summary>
        /// Default base address of the adsb.lol API.
        /// </summary>
        public const string DefaultBaseAddress = "https://api.adsb.lol/";

        /// <summary>
        /// Maximum query radius accepted by the API, in nautical miles.
        /// </summary>
        private const double MaxRadiusNauticalMiles = 250.0;

        /// <summary>
        /// Pauses the requests after 429 responses.
        /// </summary>
        private readonly BackoffRateLimiter rateLimiter = new BackoffRateLimiter();

        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API.</param>
        public AdsbLolClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => "adsb.lol";

        /// <inheritdoc />
        public RateLimitStatus GetRateLimitStatus()
        {
            return rateLimiter.GetStatus();
        }

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
        {
            double radius = Math.Clamp(radiusNauticalMiles, 1.0, MaxRadiusNauticalMiles);

            string path = string.Format(
                CultureInfo.InvariantCulture,
                "v2/point/{0:0.#####}/{1:0.#####}/{2:0}",
                latitude,
                longitude,
                Math.Ceiling(radius));

            TimeSpan remainingBlock = rateLimiter.GetRemainingBlock();

            if (remainingBlock > TimeSpan.Zero)
            {
                throw new RateLimitedException(remainingBlock);
            }

            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(rateLimiter.RegisterRateLimit(response.Headers.RetryAfter));
            }

            response.EnsureSuccessStatusCode();
            rateLimiter.Reset();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            List<Aircraft> result = new List<Aircraft>();

            if (!document.RootElement.TryGetProperty("ac", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (JsonElement element in list.EnumerateArray())
            {
                Aircraft? aircraft = ParseAircraft(element);

                if (aircraft != null)
                {
                    result.Add(aircraft);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a JSON element to an aircraft.
        /// </summary>
        /// <param name="element">Element of the "ac" array of the API response.</param>
        /// <returns>The aircraft or null when the address or the position is missing.</returns>
        private static Aircraft? ParseAircraft(JsonElement element)
        {
            string? hex = GetString(element, "hex");
            double? latitude = GetDouble(element, "lat");
            double? longitude = GetDouble(element, "lon");

            if (string.IsNullOrEmpty(hex) || !latitude.HasValue || !longitude.HasValue)
            {
                return null;
            }

            string? callsign = GetString(element, "flight")?.Trim();

            bool onGround = false;
            int? altitudeFeet = null;

            if (element.TryGetProperty("alt_baro", out JsonElement altitude))
            {
                if (altitude.ValueKind == JsonValueKind.String && altitude.GetString() == "ground")
                {
                    onGround = true;
                }
                else if (altitude.ValueKind == JsonValueKind.Number)
                {
                    altitudeFeet = (int)Math.Round(altitude.GetDouble());
                }
            }

            double? track = GetDouble(element, "track") ?? GetDouble(element, "true_heading");
            string? typeCode = GetString(element, "t")?.Trim();
            string? registration = GetString(element, "r")?.Trim();
            string? category = GetString(element, "category")?.Trim();
            bool isMilitary = GetDouble(element, "dbFlags") is double flags && ((int)flags & 1) != 0;
            double? verticalRate = GetDouble(element, "baro_rate") ?? GetDouble(element, "geom_rate");
            string? squawk = GetString(element, "squawk")?.Trim();

            return new Aircraft(
                hex,
                string.IsNullOrEmpty(callsign) ? null : callsign,
                latitude.Value,
                longitude.Value,
                altitudeFeet,
                onGround,
                GetDouble(element, "gs"),
                track,
                GetDouble(element, "seen_pos") ?? 0.0,
                string.IsNullOrEmpty(typeCode) ? null : typeCode,
                string.IsNullOrEmpty(registration) ? null : registration,
                string.IsNullOrEmpty(category) ? null : category,
                isMilitary,
                verticalRate.HasValue ? (int)Math.Round(verticalRate.Value) : null,
                string.IsNullOrEmpty(squawk) ? null : squawk);
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

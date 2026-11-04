using System.Globalization;
using System.Net;
using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// Client of the adsb.lol API returning the aircraft within a radius of a point and the data of a single aircraft. It only fetches and converts the data; pausing the requests after limit responses is up to the manager.
    /// </summary>
    public sealed class AdsbLolClient : IAircraftSource, IAircraftLookupSource, IMilitaryAircraftSource
    {
        /// <summary>
        /// Default base address of the adsb.lol API.
        /// </summary>
        public const string DefaultBaseAddress = "https://api.adsb.lol/";

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public const string SourceName = "adsb.lol";

        /// <summary>
        /// Maximum query radius accepted by the API, in nautical miles.
        /// </summary>
        private const double MaxRadiusNauticalMiles = 250.0;

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
        public string Name => SourceName;

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

            List<Aircraft> result = new List<Aircraft>();

            foreach (AdsbLolAircraftRecord record in await GetRecordsAsync(path, cancellationToken))
            {
                Aircraft? aircraft = record.ToAircraft();

                if (aircraft != null)
                {
                    result.Add(aircraft);
                }
            }

            return result;
        }

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken)
        {
            List<Aircraft> result = new List<Aircraft>();

            foreach (AdsbLolAircraftRecord record in await GetRecordsAsync("v2/mil", cancellationToken))
            {
                Aircraft? aircraft = record.ToAircraft();

                if (aircraft != null)
                {
                    result.Add(aircraft);
                }
            }

            return result;
        }

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
        {
            foreach (AdsbLolAircraftRecord record in await GetRecordsAsync("v2/hex/" + Uri.EscapeDataString(hex), cancellationToken))
            {
                if (string.Equals(record.Hex, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return record.ToDetails();
                }
            }

            return null;
        }

        /// <summary>
        /// Downloads the "ac" array of a response.
        /// </summary>
        /// <param name="path">Path of the request relative to the base address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The records of the response, empty when the response has no "ac" array.</returns>
        private async Task<List<AdsbLolAircraftRecord>> GetRecordsAsync(string path, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            List<AdsbLolAircraftRecord> records = new List<AdsbLolAircraftRecord>();

            if (!document.RootElement.TryGetProperty("ac", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return records;
            }

            foreach (JsonElement element in list.EnumerateArray())
            {
                records.Add(AdsbLolAircraftRecord.FromJson(element));
            }

            return records;
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
    }
}

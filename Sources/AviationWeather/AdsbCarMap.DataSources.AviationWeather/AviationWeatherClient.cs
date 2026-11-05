using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AviationWeather
{
    /// <summary>
    /// Client of the aviationweather.gov API (NOAA Aviation Weather Center) returning the current METAR reports. It only fetches and converts the data; pausing the requests after limit responses is up to the caller.
    /// </summary>
    public sealed partial class AviationWeatherClient : IWeatherSource
    {
        /// <summary>
        /// Default base address of the API.
        /// </summary>
        public const string DefaultBaseAddress = "https://aviationweather.gov/";

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public const string SourceName = "aviationweather.gov";

        /// <summary>
        /// Largest number of airports asked for in one request, which keeps the address short.
        /// </summary>
        public const int MaxCodesPerRequest = 100;

        /// <summary>
        /// Largest radius of a search around a point in kilometers; a larger one is cut to it.
        /// </summary>
        public const double MaxRadiusKilometers = 300.0;

        /// <summary>
        /// Length of one degree of latitude in kilometers.
        /// </summary>
        private const double KilometersPerDegree = 111.195;

        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API.</param>
        public AviationWeatherClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => SourceName;

        /// <inheritdoc />
        /// <exception cref="System.Text.Json.JsonException">Invalid JSON response.</exception>
        public async Task<IReadOnlyList<Metar>> GetMetarsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
        {
            List<string> codes = icaoCodes
                .Select((string code) => code.Trim().ToUpperInvariant())
                .Where((string code) => IcaoPattern().IsMatch(code))
                .Distinct()
                .ToList();
            List<Metar> result = new List<Metar>();

            for (int start = 0; start < codes.Count; start += MaxCodesPerRequest)
            {
                string ids = string.Join(",", codes.Skip(start).Take(MaxCodesPerRequest));
                result.AddRange(await GetAsync("api/data/metar?format=json&ids=" + ids, cancellationToken));
            }

            return result;
        }

        /// <inheritdoc />
        /// <exception cref="System.Text.Json.JsonException">Invalid JSON response.</exception>
        public Task<IReadOnlyList<Metar>> GetMetarsNearAsync(double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken)
        {
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || !double.IsFinite(radiusKilometers) || radiusKilometers <= 0.0 || Math.Abs(latitude) > 90.0 || Math.Abs(longitude) > 180.0)
            {
                return Task.FromResult<IReadOnlyList<Metar>>(new List<Metar>());
            }

            double radius = Math.Min(radiusKilometers, MaxRadiusKilometers);
            double latitudeSpan = radius / KilometersPerDegree;
            double longitudeSpan = Math.Min(180.0, radius / (KilometersPerDegree * Math.Max(0.05, Math.Cos(latitude * Math.PI / 180.0))));
            double south = Math.Max(-90.0, latitude - latitudeSpan);
            double north = Math.Min(90.0, latitude + latitudeSpan);
            double west = Math.Max(-180.0, longitude - longitudeSpan);
            double east = Math.Min(180.0, longitude + longitudeSpan);
            string box = string.Join(",", new double[] { south, west, north, east }.Select((double value) => value.ToString("0.###", CultureInfo.InvariantCulture)));

            return GetAsync("api/data/metar?format=json&bbox=" + box, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="System.Text.Json.JsonException">Invalid JSON response.</exception>
        public async Task<IReadOnlyList<Taf>> GetTafsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
        {
            List<string> codes = icaoCodes
                .Select((string code) => code.Trim().ToUpperInvariant())
                .Where((string code) => IcaoPattern().IsMatch(code))
                .Distinct()
                .ToList();
            List<Taf> result = new List<Taf>();

            for (int start = 0; start < codes.Count; start += MaxCodesPerRequest)
            {
                string ids = string.Join(",", codes.Skip(start).Take(MaxCodesPerRequest));
                string? body = await GetBodyAsync("api/data/taf?format=json&ids=" + ids, cancellationToken);

                if (body != null)
                {
                    result.AddRange(AviationWeatherParser.ParseTafs(body));
                }
            }

            return result;
        }

        /// <summary>
        /// Downloads and converts one answer with reports.
        /// </summary>
        /// <param name="query">Path and query of the request relative to the base address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reports; empty when the server has none (204).</returns>
        private async Task<IReadOnlyList<Metar>> GetAsync(string query, CancellationToken cancellationToken)
        {
            string? body = await GetBodyAsync(query, cancellationToken);

            return body == null ? new List<Metar>() : AviationWeatherParser.Parse(body);
        }

        /// <summary>
        /// Downloads one answer.
        /// </summary>
        /// <param name="query">Path and query of the request relative to the base address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The body of the answer, or null when the server has none (204, 404 or an empty body).</returns>
        private async Task<string?> GetBodyAsync(string query, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(query, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(body) ? null : body;
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
        /// Pattern of an ICAO airport code: four letters or digits.
        /// </summary>
        /// <returns>The pattern.</returns>
        [GeneratedRegex("^[A-Z0-9]{4}$")]
        private static partial Regex IcaoPattern();
    }
}

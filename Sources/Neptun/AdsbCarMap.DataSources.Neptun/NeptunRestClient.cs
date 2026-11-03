using System.Net;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// Client of the Neptun endpoint that returns the current list of the threats. It only fetches and converts the data; the pace of the requests is up to the feed.
    /// </summary>
    public sealed class NeptunRestClient
    {
        /// <summary>
        /// Default base address of the Neptun API.
        /// </summary>
        public const string DefaultBaseAddress = "https://neptun.in.ua/";

        /// <summary>
        /// Path of the endpoint with the threats relative to the base address.
        /// </summary>
        public const string ThreatsPath = "api/v1/threats";

        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API.</param>
        public NeptunRestClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <summary>
        /// Downloads the current threats.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The threats that could be read.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        /// <exception cref="System.Text.Json.JsonException">Invalid JSON response.</exception>
        public async Task<IReadOnlyList<Threat>> GetThreatsAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(ThreatsPath, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            return NeptunParser.ParseThreats(await response.Content.ReadAsStringAsync(cancellationToken));
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

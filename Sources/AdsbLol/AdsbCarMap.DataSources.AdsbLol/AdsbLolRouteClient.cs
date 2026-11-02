using System.Net;
using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// Client of the adsb.lol route API returning the flight route for a callsign. The server redirects the request to the standing data of the routes (one small file per callsign). It only fetches and converts the data; pausing the requests after limit responses is up to the manager.
    /// </summary>
    public sealed class AdsbLolRouteClient : IRouteSource
    {
        /// <summary>
        /// HTTP client with the base address of the API.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the API; it must follow redirects.</param>
        public AdsbLolRouteClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => AdsbLolClient.SourceName;

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync("api/0/route/" + Uri.EscapeDataString(callsign), cancellationToken);

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

            return AdsbLolRouteRecord.FromJson(document.RootElement).ToRoute();
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

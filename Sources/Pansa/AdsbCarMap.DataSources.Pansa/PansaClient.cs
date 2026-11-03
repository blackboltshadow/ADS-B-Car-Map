using System.Net;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Pansa
{
    /// <summary>
    /// Client of the airspace map of PAŻP (the Polish air navigation services). The service has no documented API: the client reads the map data that the web map of the service loads. It only fetches and converts the data; the pace of the requests is up to the caller.
    /// </summary>
    public sealed class PansaClient : IAirspaceSource
    {
        /// <summary>
        /// Default base address of the airspace service.
        /// </summary>
        public const string DefaultBaseAddress = "https://airspace.pansa.pl/";

        /// <summary>
        /// Path of the map with the Updated Use Plan, which is asked first.
        /// </summary>
        public const string UupPath = "map-configuration/uup";

        /// <summary>
        /// Path of the map with the Airspace Use Plan, which is asked when the first one fails or is empty.
        /// </summary>
        public const string AupPath = "map-configuration/aup";

        /// <summary>
        /// HTTP client with the base address of the service.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the service.</param>
        public PansaClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => "PAŻP";

        /// <inheritdoc />
        public async Task<IReadOnlyList<AirspaceZone>> GetZonesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<AirspaceZone> planned = await GetZonesAsync(UupPath, cancellationToken);

                if (planned.Count > 0)
                {
                    return planned;
                }
            }
            catch (Exception exception) when (exception is not RateLimitedException && !cancellationToken.IsCancellationRequested)
            {
            }

            return await GetZonesAsync(AupPath, cancellationToken);
        }

        /// <summary>
        /// Downloads one map.
        /// </summary>
        /// <param name="path">Path of the map relative to the base address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The areas that could be read.</returns>
        private async Task<IReadOnlyList<AirspaceZone>> GetZonesAsync(string path, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            return PansaParser.ParseZones(await response.Content.ReadAsStringAsync(cancellationToken));
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

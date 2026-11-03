using System.Net;
using System.Text.RegularExpressions;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Rso
{
    /// <summary>
    /// Client of the public feed of the notices of the regional warning system (RSO), which TVP publishes. The feed is not documented as an interface and its terms are not known; the client only fetches and converts the data, and the pace of the requests is up to the caller. It asks for the first page of each category, which holds the 20 newest notices. When the caller is interested in a few regions, it asks for each of them separately, so that a busy region does not push the notices of a quiet one off the page.
    /// </summary>
    public sealed class RsoClient : IWarningSource
    {
        /// <summary>
        /// Default base address of the feed.
        /// </summary>
        public const string DefaultBaseAddress = "https://komunikaty.tvp.pl/";

        /// <summary>
        /// The most regions asked for one by one; for more regions, or none, one request per category asks for all of them.
        /// </summary>
        public const int MaximumSeparateProvinces = 4;

        /// <summary>
        /// Name used in the path of the feed for all regions.
        /// </summary>
        public const string AllProvinces = "wszystkie";

        /// <summary>
        /// Pattern of a name that may be put into the path: lower case letters and hyphens. It keeps a damaged setting from changing the address.
        /// </summary>
        private static readonly Regex SafeName = new Regex("^[a-z]+(-[a-z]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// HTTP client with the base address of the feed.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the feed.</param>
        public RsoClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => "RSO (TVP)";

        /// <summary>
        /// Builds the path of the request for a category and a region.
        /// </summary>
        /// <param name="province">Name of the region in the path, or <see cref="AllProvinces"/>.</param>
        /// <param name="category">Category in the path.</param>
        /// <returns>The path with the query, relative to the base address.</returns>
        public static string BuildPath(string province, string category)
        {
            return "komunikatyxml/" + province + "/" + category + "/1?_format=json";
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Warning>> GetWarningsAsync(IReadOnlyCollection<string> categories, IReadOnlyCollection<string> provinces, CancellationToken cancellationToken)
        {
            List<string> safeCategories = categories.Where((string name) => SafeName.IsMatch(name)).Distinct().ToList();
            List<string> safeProvinces = provinces.Where((string name) => SafeName.IsMatch(name)).Distinct().ToList();
            List<string> regions = safeProvinces.Count >= 1 && safeProvinces.Count <= MaximumSeparateProvinces ? safeProvinces : new List<string> { AllProvinces };
            Dictionary<string, Warning> byId = new Dictionary<string, Warning>();

            foreach (string category in safeCategories)
            {
                foreach (string region in regions)
                {
                    foreach (Warning warning in await GetPageAsync(region, category, cancellationToken))
                    {
                        byId[category + "|" + warning.Id] = warning;
                    }
                }
            }

            return new List<Warning>(byId.Values);
        }

        /// <summary>
        /// Downloads one page.
        /// </summary>
        /// <param name="province">Region in the path.</param>
        /// <param name="category">Category in the path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The notices that could be read.</returns>
        private async Task<IReadOnlyList<Warning>> GetPageAsync(string province, string category, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(BuildPath(province, category), cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            return RsoParser.ParseWarnings(await response.Content.ReadAsStringAsync(cancellationToken), category);
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

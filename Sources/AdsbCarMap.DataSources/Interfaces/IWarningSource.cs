using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the official warnings and notices for the regions of the country.
    /// </summary>
    public interface IWarningSource : IDataSource
    {
        /// <summary>
        /// Downloads the newest notices of the given categories.
        /// </summary>
        /// <param name="categories">Categories as the source names them. A category the source does not know gives no notices.</param>
        /// <param name="provinces">Regions the caller is interested in, as the source names them; empty means all of them. The source may use it to ask for less data, but may also return notices of other regions, so the caller filters again.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The notices, each only once per category, in no particular order.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Warning>> GetWarningsAsync(IReadOnlyCollection<string> categories, IReadOnlyCollection<string> provinces, CancellationToken cancellationToken);
    }
}

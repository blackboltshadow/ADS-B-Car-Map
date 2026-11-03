using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the restricted and reserved areas of the airspace.
    /// </summary>
    public interface IAirspaceSource : IDataSource
    {
        /// <summary>
        /// Downloads the current list of the airspace areas with their reservations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>All areas the source publishes, whether they are active now or not.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<AirspaceZone>> GetZonesAsync(CancellationToken cancellationToken);
    }
}

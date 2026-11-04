using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Optional ability of an aircraft source: returning all the military aircraft it knows about with a single request, wherever they are. The request does not depend on the position of the user.
    /// </summary>
    public interface IMilitaryAircraftSource
    {
        /// <summary>
        /// Downloads all the military aircraft that have a position. Aircraft without a position are skipped.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The military aircraft, or null when the source has no such request, so that the caller asks for an area instead.</returns>
        /// <exception cref="RateLimitedException">The source limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken);
    }
}

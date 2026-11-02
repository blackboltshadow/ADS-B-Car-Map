using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the aircraft around a point.
    /// </summary>
    public interface IAircraftSource : IDataSource
    {
        /// <summary>
        /// Downloads the aircraft within a radius of the given point. Aircraft without a position are skipped.
        /// </summary>
        /// <param name="latitude">Latitude of the center.</param>
        /// <param name="longitude">Longitude of the center.</param>
        /// <param name="radiusNauticalMiles">Radius in nautical miles, limited by the source to its maximum.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aircraft list.</returns>
        /// <exception cref="RateLimitedException">The source limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken);
    }
}

using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the flight route for a callsign.
    /// </summary>
    public interface IRouteSource : IDataSource
    {
        /// <summary>
        /// Downloads the flight route for a callsign.
        /// </summary>
        /// <param name="callsign">Callsign, e.g. "LOT281".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The route or null when the source does not know the callsign.</returns>
        /// <exception cref="HttpRequestException">Network error or a failure status code other than not found.</exception>
        Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken);
    }
}

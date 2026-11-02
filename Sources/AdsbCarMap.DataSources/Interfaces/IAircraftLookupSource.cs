using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Data source that can return the descriptive data of one aircraft by its ICAO address.
    /// </summary>
    public interface IAircraftLookupSource : IDataSource
    {
        /// <summary>
        /// Downloads the descriptive data of one aircraft.
        /// </summary>
        /// <param name="hex">ICAO address in hexadecimal notation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The data or null when the source does not know the aircraft.</returns>
        /// <exception cref="RateLimitedException">The source limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken);
    }
}

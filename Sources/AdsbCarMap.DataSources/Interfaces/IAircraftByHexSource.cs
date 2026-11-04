using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Optional request of an aircraft source: the current state of chosen aircraft by their ICAO addresses, wherever they are. It does not depend on the area of the map, so a selected aircraft can be followed after the map is moved away from it.
    /// </summary>
    public interface IAircraftByHexSource
    {
        /// <summary>
        /// Downloads the current state of the given aircraft.
        /// </summary>
        /// <param name="hexes">ICAO addresses of the aircraft.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aircraft that the source currently tracks, or null when the source has no such request.</returns>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Aircraft>?> GetAircraftByHexAsync(IReadOnlyList<string> hexes, CancellationToken cancellationToken);
    }
}

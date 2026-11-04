using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the recorded path of the current flight of an aircraft.
    /// </summary>
    public interface IFlightTrackSource : IDataSource
    {
        /// <summary>
        /// Downloads the path of the current flight of an aircraft.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The path of the last flight or null when the source has no recording of the aircraft.</returns>
        /// <exception cref="HttpRequestException">Network error or a failure status code other than not found.</exception>
        Task<FlightTrack?> GetTrackAsync(string hex, CancellationToken cancellationToken);
    }
}

using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Client of a data source returning the current weather reports and the forecasts of airports.
    /// </summary>
    public interface IWeatherSource : IDataSource
    {
        /// <summary>
        /// Downloads the newest report of each of the given airports.
        /// </summary>
        /// <param name="icaoCodes">ICAO codes of the airports. A code the source does not know, or an airport without a station, gives no report.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reports, at most one per airport, in no particular order; empty when there are none.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Metar>> GetMetarsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads the newest reports of the stations around a point. It is used for an airport that has no station of its own.
        /// </summary>
        /// <param name="latitude">Latitude of the point in degrees.</param>
        /// <param name="longitude">Longitude of the point in degrees.</param>
        /// <param name="radiusKilometers">Distance from the point in kilometers. The source may answer with the stations of a square around the point, so the caller measures the distance again.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reports, at most one per station, in no particular order; empty when there are none.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Metar>> GetMetarsNearAsync(double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads the newest forecast of each of the given airports.
        /// </summary>
        /// <param name="icaoCodes">ICAO codes of the airports. A code the source does not know, or an airport for which no forecast is issued, gives no forecast.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The forecasts, at most one per airport, in no particular order; empty when there are none.</returns>
        /// <exception cref="RateLimitedException">The server limits the requests and asks the client to wait.</exception>
        /// <exception cref="HttpRequestException">Network error or a failure status code.</exception>
        Task<IReadOnlyList<Taf>> GetTafsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken);
    }
}

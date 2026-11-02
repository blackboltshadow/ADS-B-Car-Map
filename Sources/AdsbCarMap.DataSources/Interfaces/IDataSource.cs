using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Common part of every data source client: its name and the current state of its request limits.
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Display name of the source, e.g. "adsb.lol".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Reads the current state of the request limits of this source without sending any request.
        /// </summary>
        /// <returns>The state of the limits at the moment of the call.</returns>
        RateLimitStatus GetRateLimitStatus();
    }
}

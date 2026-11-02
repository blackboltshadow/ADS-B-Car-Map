using AdsbCarMap.DataSources.Interfaces;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Data source whose request limits are tracked by the manager.
    /// </summary>
    public interface IRateLimitedSource : IDataSource
    {
        /// <summary>
        /// Reads the current state of the request limits without sending any request.
        /// </summary>
        /// <returns>The state of the limits at the moment of the call.</returns>
        RateLimitStatus GetRateLimitStatus();
    }
}

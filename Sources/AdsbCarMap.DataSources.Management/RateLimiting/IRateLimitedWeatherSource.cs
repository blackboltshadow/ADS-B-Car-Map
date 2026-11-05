using AdsbCarMap.DataSources.Interfaces;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Weather source whose request limits are tracked by the manager.
    /// </summary>
    public interface IRateLimitedWeatherSource : IWeatherSource, IRateLimitedSource
    {
    }
}

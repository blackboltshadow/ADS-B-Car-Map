using AdsbCarMap.DataSources.Interfaces;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Route source whose request limits are tracked by the manager.
    /// </summary>
    public interface IRateLimitedRouteSource : IRouteSource, IRateLimitedSource
    {
    }
}

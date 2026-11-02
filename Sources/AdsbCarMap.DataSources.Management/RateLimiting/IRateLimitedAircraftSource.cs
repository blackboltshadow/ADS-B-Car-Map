using AdsbCarMap.DataSources.Interfaces;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Aircraft source whose request limits are tracked by the manager.
    /// </summary>
    public interface IRateLimitedAircraftSource : IAircraftSource, IRateLimitedSource
    {
    }
}

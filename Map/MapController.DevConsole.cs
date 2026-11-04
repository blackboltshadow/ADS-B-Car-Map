using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.Services;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Data for the developer console.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Copies the state shown in the developer console.
        /// </summary>
        /// <returns>The state of the map, the position of the device, the last query and the aircraft.</returns>
        public DevConsoleSnapshot GetDevSnapshot()
        {
            (QueryArea? sent, DateTime? sentAtUtc) = DevConsoleState.GetLastSent();
            DateTime loadEndedUtc = dataLoop.LoadEndedUtc;
            bool following = IsFollowing;

            lock (stateLock)
            {
                double wantedRadius = MapMath.CalculateRadiusNauticalMiles(centerLatitude, zoom, surfaceWidth, surfaceHeight);

                return new DevConsoleSnapshot(
                    zoom,
                    zoomTarget,
                    centerLatitude,
                    centerLongitude,
                    following,
                    surfaceWidth,
                    surfaceHeight,
                    hasUserLocation,
                    userLatitude,
                    userLongitude,
                    hasUserLocation ? userLocationAtUtc : null,
                    wantedRadius,
                    sent,
                    sentAtUtc,
                    latestAircraft.Count,
                    Volatile.Read(ref visibleAircraftCount),
                    loadEndedUtc.Ticks == 0 ? null : loadEndedUtc,
                    dataLoop.SourceName,
                    dataLoop.RateLimitRemaining);
            }
        }
    }
}

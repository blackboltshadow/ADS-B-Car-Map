using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Gives the line about the runway that the traffic uses to the airport card and the popup, from the aircraft that the map shows now.
    /// </summary>
    public static class AirportRunwayTraffic
    {
        /// <summary>
        /// Returns the line about the runway ends that the traffic near the airport uses.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <returns>The line; empty when the setting is off or the traffic does not show which runway is used.</returns>
        public static string BuildLine(Airport airport, IReadOnlyList<Runway> runways)
        {
            if (!AppSettings.ShowRunwayFromTraffic || runways.Count == 0)
            {
                return string.Empty;
            }

            return RunwayTrafficText.BuildLine(RunwayInUseDetector.Detect(airport, runways, LiveTraffic.GetSnapshot().Aircraft));
        }
    }
}

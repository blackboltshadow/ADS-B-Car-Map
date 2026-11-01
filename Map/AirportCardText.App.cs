using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// The part of the airport card text that reads the altitude unit from the application settings.
    /// </summary>
    public static partial class AirportCardText
    {
        /// <summary>
        /// Returns the detail lines of the card in the altitude unit from the settings.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="hasUserLocation">Whether the user position is known.</param>
        /// <param name="userLatitude">Latitude of the user.</param>
        /// <param name="userLongitude">Longitude of the user.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLines(Airport airport, IReadOnlyList<Runway> runways, bool hasUserLocation, double userLatitude, double userLongitude)
        {
            return BuildLines(airport, runways, hasUserLocation, userLatitude, userLongitude, AppSettings.AltitudeUnit);
        }
    }
}

using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// The part of the aircraft card text that reads the units from the application settings.
    /// </summary>
    public static partial class AircraftCardText
    {
        /// <summary>
        /// Computes the flight progress along the route in the units from the settings.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <param name="route">Flight route or null.</param>
        /// <param name="fraction">Flown part of the route from 0 to 1.</param>
        /// <param name="text">Progress description: flown part, distance to the destination and estimated time.</param>
        /// <returns>False when the route is unknown or has no coordinates.</returns>
        public static bool TryBuildProgress(SelectedAircraftInfo info, Aircraft aircraft, FlightRoute? route, out float fraction, out string text)
        {
            return TryBuildProgress(info, aircraft, route, out fraction, out text, AppSettings.Units);
        }

        /// <summary>
        /// Builds the lines of the left column in the units from the settings: altitude, speed, track and data age.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLeftLines(SelectedAircraftInfo info, Aircraft aircraft)
        {
            return BuildLeftLines(info, aircraft, AppSettings.Units);
        }

        /// <summary>
        /// Builds the lines of the right column in the units from the settings: vertical rate, distance and direction from the user, and squawk.
        /// </summary>
        /// <param name="info">Data of the selected aircraft and the user position.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildRightLines(SelectedAircraftInfo info, Aircraft aircraft)
        {
            return BuildRightLines(info, aircraft, AppSettings.Units);
        }
    }
}

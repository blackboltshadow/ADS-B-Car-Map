using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// The part of the aircraft card text that reads the altitude unit from the application settings.
    /// </summary>
    public static partial class AircraftCardText
    {
        /// <summary>
        /// Builds the lines of the left column: altitude in the unit from the settings, speed, track and data age.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLeftLines(SelectedAircraftInfo info, Aircraft aircraft)
        {
            return BuildLeftLines(info, aircraft, AppSettings.AltitudeUnit);
        }
    }
}

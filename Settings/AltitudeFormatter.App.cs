namespace AdsbCarMap.Settings
{
    /// <summary>
    /// The part of the altitude formatting that reads the unit from the application settings.
    /// </summary>
    public static partial class AltitudeFormatter
    {
        /// <summary>
        /// Formats an altitude given in feet in the unit from the settings.
        /// </summary>
        /// <param name="feet">Altitude in feet.</param>
        /// <returns>Text such as "10 668 m", "35 000 ft" or "FL350".</returns>
        public static string Format(double feet)
        {
            return Format(feet, AppSettings.AltitudeUnit);
        }

        /// <summary>
        /// Formats an altitude for the aircraft card in the unit from the settings, with the equivalent in parentheses.
        /// </summary>
        /// <param name="feet">Altitude in feet.</param>
        /// <returns>Text such as "10 668 m (35 000 ft)" or "FL350 (10 668 m)".</returns>
        public static string FormatWithEquivalent(double feet)
        {
            return FormatWithEquivalent(feet, AppSettings.AltitudeUnit);
        }

        /// <summary>
        /// Formats an altitude threshold of the legend, given in meters, in the unit from the settings.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="isLast">Whether this is the highest threshold, meaning "and above".</param>
        /// <returns>Label text.</returns>
        public static string FormatLegend(double meters, bool isLast)
        {
            return FormatLegend(meters, isLast, AppSettings.AltitudeUnit);
        }
    }
}

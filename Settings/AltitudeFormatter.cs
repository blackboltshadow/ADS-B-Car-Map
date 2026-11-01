using System.Globalization;

namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Unit in which altitude is presented.
    /// </summary>
    public enum AltitudeUnit
    {
        /// <summary>
        /// Meters and kilometers.
        /// </summary>
        Metric = 0,

        /// <summary>
        /// Feet.
        /// </summary>
        Feet = 1,

        /// <summary>
        /// Flight level, hundreds of feet (for example FL350).
        /// </summary>
        FlightLevel = 2,
    }

    /// <summary>
    /// Formats altitudes according to the unit chosen in the settings. Shared by the map, the search results and the aircraft card, so the phone and Android Auto always agree.
    /// </summary>
    public static partial class AltitudeFormatter
    {
        /// <summary>
        /// Feet to meters conversion factor.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Culture used for the digit grouping of the displayed numbers.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Formats an altitude given in feet in the given unit.
        /// </summary>
        /// <param name="feet">Altitude in feet.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Text such as "10 668 m", "35 000 ft" or "FL350".</returns>
        public static string Format(double feet, AltitudeUnit unit)
        {
            switch (unit)
            {
                case AltitudeUnit.Feet:
                    return string.Format(DisplayCulture, "{0:N0} ft", feet);
                case AltitudeUnit.FlightLevel:
                    return string.Format(CultureInfo.InvariantCulture, "FL{0:000}", Math.Round(feet / 100.0));
                default:
                    return string.Format(DisplayCulture, "{0:N0} m", feet * MetersPerFoot);
            }
        }

        /// <summary>
        /// Formats an altitude for the aircraft card: the chosen unit first, then the metric (or feet, when the unit is metric) equivalent in parentheses.
        /// </summary>
        /// <param name="feet">Altitude in feet.</param>
        /// <param name="unit">Unit shown first.</param>
        /// <returns>Text such as "10 668 m (35 000 ft)" or "FL350 (10 668 m)".</returns>
        public static string FormatWithEquivalent(double feet, AltitudeUnit unit)
        {
            AltitudeUnit other = unit == AltitudeUnit.Metric ? AltitudeUnit.Feet : AltitudeUnit.Metric;

            return Format(feet, unit) + " (" + Format(feet, other) + ")";
        }

        /// <summary>
        /// Formats an altitude threshold of the legend, given in meters, in the unit from the settings. Metric values below one kilometer are in meters, higher ones in kilometers; feet are rounded to hundreds.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="isLast">Whether this is the highest threshold, meaning "and above".</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Label text.</returns>
        public static string FormatLegend(double meters, bool isLast, AltitudeUnit unit)
        {
            double feet = meters / MetersPerFoot;
            string text;

            switch (unit)
            {
                case AltitudeUnit.Feet:
                    text = string.Format(DisplayCulture, "{0:N0} ft", Math.Round(feet / 100.0) * 100.0);
                    break;
                case AltitudeUnit.FlightLevel:
                    text = string.Format(CultureInfo.InvariantCulture, "FL{0:000}", Math.Round(feet / 100.0));
                    break;
                default:
                    text = meters < 1000.0
                        ? Math.Round(meters).ToString("0", DisplayCulture) + " m"
                        : (meters / 1000.0).ToString("0.#", DisplayCulture) + " km";
                    break;
            }

            return isLast ? text + "+" : text;
        }
    }
}

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
        /// Formats a length given in feet, such as a runway length. Flight levels are a way of writing altitude, so a length is shown in feet for them.
        /// </summary>
        /// <param name="feet">Length in feet.</param>
        /// <param name="unit">Altitude unit chosen by the user; metric gives meters, the other units give feet.</param>
        /// <returns>Text such as "3 505 m" or "11 500 ft".</returns>
        public static string FormatLength(double feet, AltitudeUnit unit)
        {
            return unit == AltitudeUnit.Metric
                ? string.Format(DisplayCulture, "{0:N0} m", feet * MetersPerFoot)
                : string.Format(DisplayCulture, "{0:N0} ft", feet);
        }

        /// <summary>
        /// Converts an altitude in meters, as stored in the filters, to the number shown in the unit.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="unit">Unit of the result; flight levels are hundreds of feet.</param>
        /// <returns>Altitude in meters, feet or hundreds of feet.</returns>
        public static double MetersToUnit(double meters, AltitudeUnit unit)
        {
            switch (unit)
            {
                case AltitudeUnit.Feet:
                    return meters / MetersPerFoot;
                case AltitudeUnit.FlightLevel:
                    return meters / MetersPerFoot / 100.0;
                default:
                    return meters;
            }
        }

        /// <summary>
        /// Converts a number typed in the unit to an altitude in meters, as stored in the filters.
        /// </summary>
        /// <param name="value">Altitude in meters, feet or hundreds of feet.</param>
        /// <param name="unit">Unit of the value.</param>
        /// <returns>Altitude in meters.</returns>
        public static double UnitToMeters(double value, AltitudeUnit unit)
        {
            switch (unit)
            {
                case AltitudeUnit.Feet:
                    return value * MetersPerFoot;
                case AltitudeUnit.FlightLevel:
                    return value * 100.0 * MetersPerFoot;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Returns the symbol of an altitude unit.
        /// </summary>
        /// <param name="unit">Unit.</param>
        /// <returns>"m", "ft" or "FL".</returns>
        public static string Symbol(AltitudeUnit unit)
        {
            switch (unit)
            {
                case AltitudeUnit.Feet:
                    return "ft";
                case AltitudeUnit.FlightLevel:
                    return "FL";
                default:
                    return "m";
            }
        }

        /// <summary>
        /// Formats an altitude given in meters, as stored in the filters, in the unit.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Text such as "1000 m", "3281 ft" or "FL033".</returns>
        public static string FormatMeters(double meters, AltitudeUnit unit)
        {
            return Format(meters / MetersPerFoot, unit);
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

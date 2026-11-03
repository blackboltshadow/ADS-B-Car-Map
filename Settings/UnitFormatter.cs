using System.Globalization;

namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Unit in which distances are presented.
    /// </summary>
    public enum DistanceUnit
    {
        /// <summary>
        /// Kilometers.
        /// </summary>
        Kilometers = 0,

        /// <summary>
        /// Nautical miles.
        /// </summary>
        NauticalMiles = 1,

        /// <summary>
        /// Statute miles.
        /// </summary>
        Miles = 2,
    }

    /// <summary>
    /// Unit in which ground speed is presented.
    /// </summary>
    public enum SpeedUnit
    {
        /// <summary>
        /// Kilometers per hour.
        /// </summary>
        KilometersPerHour = 0,

        /// <summary>
        /// Knots, nautical miles per hour.
        /// </summary>
        Knots = 1,

        /// <summary>
        /// Statute miles per hour.
        /// </summary>
        MilesPerHour = 2,
    }

    /// <summary>
    /// Unit in which the vertical rate is presented.
    /// </summary>
    public enum VerticalSpeedUnit
    {
        /// <summary>
        /// Feet per minute.
        /// </summary>
        FeetPerMinute = 0,

        /// <summary>
        /// Meters per second.
        /// </summary>
        MetersPerSecond = 1,
    }

    /// <summary>
    /// Formats distances, speeds and vertical rates according to the units chosen in the settings. Together with <see cref="AltitudeFormatter"/> it is the only place that converts units, so the phone and Android Auto always agree. Data is stored in kilometers, knots and feet per minute.
    /// </summary>
    public static partial class UnitFormatter
    {
        /// <summary>
        /// Kilometers in one nautical mile.
        /// </summary>
        public const double KilometersPerNauticalMile = 1.852;

        /// <summary>
        /// Kilometers in one statute mile.
        /// </summary>
        public const double KilometersPerMile = 1.609344;

        /// <summary>
        /// Statute miles per hour in one knot.
        /// </summary>
        private const double MilesPerHourPerKnot = KilometersPerNauticalMile / KilometersPerMile;

        /// <summary>
        /// Meters per second in one foot per minute.
        /// </summary>
        private const double MetersPerSecondPerFootPerMinute = 0.3048 / 60.0;

        /// <summary>
        /// Distance in the displayed unit below which one decimal place is shown by <see cref="FormatDistanceCompact"/>.
        /// </summary>
        private const double CompactDecimalLimit = 10.0;

        /// <summary>
        /// Culture used for the digit grouping of the displayed numbers.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Converts a distance in kilometers to the unit.
        /// </summary>
        /// <param name="kilometers">Distance in kilometers.</param>
        /// <param name="unit">Target unit.</param>
        /// <returns>Distance in the unit.</returns>
        public static double KilometersToUnit(double kilometers, DistanceUnit unit)
        {
            switch (unit)
            {
                case DistanceUnit.NauticalMiles:
                    return kilometers / KilometersPerNauticalMile;
                case DistanceUnit.Miles:
                    return kilometers / KilometersPerMile;
                default:
                    return kilometers;
            }
        }

        /// <summary>
        /// Converts a distance in the unit to kilometers.
        /// </summary>
        /// <param name="value">Distance in the unit.</param>
        /// <param name="unit">Unit of the value.</param>
        /// <returns>Distance in kilometers.</returns>
        public static double UnitToKilometers(double value, DistanceUnit unit)
        {
            switch (unit)
            {
                case DistanceUnit.NauticalMiles:
                    return value * KilometersPerNauticalMile;
                case DistanceUnit.Miles:
                    return value * KilometersPerMile;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Returns the symbol of a distance unit.
        /// </summary>
        /// <param name="unit">Unit.</param>
        /// <returns>"km", "NM" or "mi".</returns>
        public static string DistanceSymbol(DistanceUnit unit)
        {
            switch (unit)
            {
                case DistanceUnit.NauticalMiles:
                    return "NM";
                case DistanceUnit.Miles:
                    return "mi";
                default:
                    return "km";
            }
        }

        /// <summary>
        /// Formats a distance in the given unit with a fixed number of decimal places.
        /// </summary>
        /// <param name="kilometers">Distance in kilometers.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <param name="decimals">Number of decimal places.</param>
        /// <returns>Text such as "111 km", "60,0 NM" or "69 mi".</returns>
        public static string FormatDistance(double kilometers, DistanceUnit unit, int decimals)
        {
            return KilometersToUnit(kilometers, unit).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), DisplayCulture) + " " + DistanceSymbol(unit);
        }

        /// <summary>
        /// Formats a distance with one decimal place below ten units and none above. Used where the distance may be short, such as in alerts.
        /// </summary>
        /// <param name="kilometers">Distance in kilometers.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Text such as "3,5 km" or "50 km".</returns>
        public static string FormatDistanceCompact(double kilometers, DistanceUnit unit)
        {
            return FormatDistance(kilometers, unit, KilometersToUnit(kilometers, unit) < CompactDecimalLimit ? 1 : 0);
        }

        /// <summary>
        /// Returns the symbol of a speed unit.
        /// </summary>
        /// <param name="unit">Unit.</param>
        /// <returns>"km/h", "kt" or "mph".</returns>
        public static string SpeedSymbol(SpeedUnit unit)
        {
            switch (unit)
            {
                case SpeedUnit.Knots:
                    return "kt";
                case SpeedUnit.MilesPerHour:
                    return "mph";
                default:
                    return "km/h";
            }
        }

        /// <summary>
        /// Formats a ground speed.
        /// </summary>
        /// <param name="knots">Speed in knots.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Text such as "741 km/h", "400 kt" or "460 mph".</returns>
        public static string FormatSpeed(double knots, SpeedUnit unit)
        {
            double value;

            switch (unit)
            {
                case SpeedUnit.KilometersPerHour:
                    value = knots * KilometersPerNauticalMile;
                    break;
                case SpeedUnit.MilesPerHour:
                    value = knots * MilesPerHourPerKnot;
                    break;
                default:
                    value = knots;
                    break;
            }

            return value.ToString("N0", DisplayCulture) + " " + SpeedSymbol(unit);
        }

        /// <summary>
        /// Returns the symbol of a vertical rate unit.
        /// </summary>
        /// <param name="unit">Unit.</param>
        /// <returns>"ft/min" or "m/s".</returns>
        public static string VerticalSpeedSymbol(VerticalSpeedUnit unit)
        {
            return unit == VerticalSpeedUnit.MetersPerSecond ? "m/s" : "ft/min";
        }

        /// <summary>
        /// Formats the magnitude of a vertical rate.
        /// </summary>
        /// <param name="feetPerMinute">Vertical rate in feet per minute; the sign is ignored.</param>
        /// <param name="unit">Unit of the result.</param>
        /// <returns>Text such as "1 500 ft/min" or "7,6 m/s".</returns>
        public static string FormatVerticalSpeed(double feetPerMinute, VerticalSpeedUnit unit)
        {
            double magnitude = Math.Abs(feetPerMinute);

            if (unit == VerticalSpeedUnit.MetersPerSecond)
            {
                return (magnitude * MetersPerSecondPerFootPerMinute).ToString("N1", DisplayCulture) + " m/s";
            }

            return magnitude.ToString("N0", DisplayCulture) + " ft/min";
        }
    }
}

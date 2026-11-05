using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the distance, speed and vertical rate formatting.
    /// </summary>
    public class UnitFormatterTests
    {
        /// <summary>
        /// Replaces the non-breaking spaces used as digit group separators with plain spaces.
        /// </summary>
        /// <param name="text">Formatted text.</param>
        /// <returns>Text with plain spaces.</returns>
        private static string Plain(string text)
        {
            return text.Replace(' ', ' ').Replace(' ', ' ');
        }

        /// <summary>
        /// Distances are converted from kilometers to the unit and back.
        /// </summary>
        [Theory]
        [InlineData(DistanceUnit.Kilometers, 100.0, 100.0, "km")]
        [InlineData(DistanceUnit.NauticalMiles, 185.2, 100.0, "NM")]
        [InlineData(DistanceUnit.Miles, 160.9344, 100.0, "mi")]
        public void Distance_ConvertsBothWays(DistanceUnit unit, double kilometers, double inUnit, string symbol)
        {
            Assert.Equal(inUnit, UnitFormatter.KilometersToUnit(kilometers, unit), 6);
            Assert.Equal(kilometers, UnitFormatter.UnitToKilometers(inUnit, unit), 6);
            Assert.Equal(symbol, UnitFormatter.DistanceSymbol(unit));
        }

        /// <summary>
        /// A distance is written with the requested number of decimal places and the symbol of the unit.
        /// </summary>
        [Theory]
        [InlineData(DistanceUnit.Kilometers, 0, "1 111 km")]
        [InlineData(DistanceUnit.Kilometers, 1, "1 111,2 km")]
        [InlineData(DistanceUnit.NauticalMiles, 0, "600 NM")]
        [InlineData(DistanceUnit.Miles, 0, "690 mi")]
        public void FormatDistance_UsesDecimalsAndSymbol(DistanceUnit unit, int decimals, string expected)
        {
            Assert.Equal(expected, Plain(UnitFormatter.FormatDistance(1111.2, unit, decimals)));
        }

        /// <summary>
        /// The compact format has one decimal place below ten units of the chosen unit, judged after the conversion.
        /// </summary>
        [Theory]
        [InlineData(DistanceUnit.Kilometers, 3.5, "3,5 km")]
        [InlineData(DistanceUnit.Kilometers, 12.4, "12 km")]
        [InlineData(DistanceUnit.Kilometers, 9.96, "10,0 km")]
        [InlineData(DistanceUnit.Miles, 12.0, "7,5 mi")]
        [InlineData(DistanceUnit.NauticalMiles, 12.0, "6,5 NM")]
        [InlineData(DistanceUnit.NauticalMiles, 30.0, "16 NM")]
        [InlineData(DistanceUnit.NauticalMiles, 20.0, "11 NM")]
        public void FormatDistanceCompact_DecimalsBelowTen(DistanceUnit unit, double kilometers, string expected)
        {
            Assert.Equal(expected, Plain(UnitFormatter.FormatDistanceCompact(kilometers, unit)));
        }

        /// <summary>
        /// A ground speed in knots is converted to the chosen unit.
        /// </summary>
        [Theory]
        [InlineData(SpeedUnit.KilometersPerHour, "741 km/h", "km/h")]
        [InlineData(SpeedUnit.Knots, "400 kt", "kt")]
        [InlineData(SpeedUnit.MilesPerHour, "460 mph", "mph")]
        public void FormatSpeed_ConvertsFromKnots(SpeedUnit unit, string expected, string symbol)
        {
            Assert.Equal(expected, Plain(UnitFormatter.FormatSpeed(400.0, unit)));
            Assert.Equal(symbol, UnitFormatter.SpeedSymbol(unit));
        }

        /// <summary>
        /// The magnitude of a vertical rate is written in the chosen unit, whatever its sign.
        /// </summary>
        [Theory]
        [InlineData(VerticalSpeedUnit.FeetPerMinute, 1500.0, "1 500 ft/min")]
        [InlineData(VerticalSpeedUnit.FeetPerMinute, -800.0, "800 ft/min")]
        [InlineData(VerticalSpeedUnit.MetersPerSecond, 1500.0, "7,6 m/s")]
        [InlineData(VerticalSpeedUnit.MetersPerSecond, -800.0, "4,1 m/s")]
        public void FormatVerticalSpeed_ConvertsAndIgnoresSign(VerticalSpeedUnit unit, double feetPerMinute, string expected)
        {
            Assert.Equal(expected, Plain(UnitFormatter.FormatVerticalSpeed(feetPerMinute, unit)));
        }

        /// <summary>
        /// The symbol of the vertical rate unit matches the unit.
        /// </summary>
        [Fact]
        public void VerticalSpeedSymbol_MatchesUnit()
        {
            Assert.Equal("ft/min", UnitFormatter.VerticalSpeedSymbol(VerticalSpeedUnit.FeetPerMinute));
            Assert.Equal("m/s", UnitFormatter.VerticalSpeedSymbol(VerticalSpeedUnit.MetersPerSecond));
        }

        /// <summary>
        /// The default set of units is metric with feet per minute for the vertical rate.
        /// </summary>
        [Fact]
        public void DisplayUnits_DefaultIsMetric()
        {
            Assert.Equal(AltitudeUnit.Metric, DisplayUnits.Default.Altitude);
            Assert.Equal(DistanceUnit.Kilometers, DisplayUnits.Default.Distance);
            Assert.Equal(SpeedUnit.KilometersPerHour, DisplayUnits.Default.Speed);
            Assert.Equal(VerticalSpeedUnit.FeetPerMinute, DisplayUnits.Default.VerticalSpeed);
        }

        /// <summary>
        /// Checks that pressure is written as whole hectopascals or as inches of mercury with two decimals.
        /// </summary>
        [Theory]
        [InlineData(PressureUnit.Hectopascals, 1013.4, "1013 hPa")]
        [InlineData(PressureUnit.InchesOfMercury, 1013.25, "29,92 inHg")]
        [InlineData(PressureUnit.InchesOfMercury, 1031.0, "30,45 inHg")]
        public void FormatPressure_ConvertsToUnit(PressureUnit unit, double hectopascals, string expected)
        {
            Assert.Equal(expected, Plain(UnitFormatter.FormatPressure(hectopascals, unit)));
        }

        /// <summary>
        /// Checks the symbols of the pressure units.
        /// </summary>
        [Fact]
        public void PressureSymbol_ReturnsSymbol()
        {
            Assert.Equal("hPa", UnitFormatter.PressureSymbol(PressureUnit.Hectopascals));
            Assert.Equal("inHg", UnitFormatter.PressureSymbol(PressureUnit.InchesOfMercury));
        }
    }
}

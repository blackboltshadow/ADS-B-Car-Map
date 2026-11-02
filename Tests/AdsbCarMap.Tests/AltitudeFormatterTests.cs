using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the altitude formatting in the three units.
    /// </summary>
    public class AltitudeFormatterTests
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
        /// Altitudes are converted to the chosen unit.
        /// </summary>
        [Theory]
        [InlineData(35000.0, AltitudeUnit.Metric, "10 668 m")]
        [InlineData(35000.0, AltitudeUnit.Feet, "35 000 ft")]
        [InlineData(35000.0, AltitudeUnit.FlightLevel, "FL350")]
        [InlineData(5000.0, AltitudeUnit.FlightLevel, "FL050")]
        [InlineData(0.0, AltitudeUnit.Metric, "0 m")]
        [InlineData(0.0, AltitudeUnit.FlightLevel, "FL000")]
        [InlineData(-50.0, AltitudeUnit.Feet, "-50 ft")]
        public void Format_ConvertsToUnit(double feet, AltitudeUnit unit, string expected)
        {
            Assert.Equal(expected, Plain(AltitudeFormatter.Format(feet, unit)));
        }

        /// <summary>
        /// The flight level is rounded to hundreds of feet.
        /// </summary>
        [Fact]
        public void Format_FlightLevel_RoundsToHundreds()
        {
            Assert.Equal("FL350", AltitudeFormatter.Format(34960.0, AltitudeUnit.FlightLevel));
            Assert.Equal("FL349", AltitudeFormatter.Format(34940.0, AltitudeUnit.FlightLevel));
        }

        /// <summary>
        /// The card shows the chosen unit first and the metric or feet equivalent in parentheses.
        /// </summary>
        [Theory]
        [InlineData(AltitudeUnit.Metric, "10 668 m (35 000 ft)")]
        [InlineData(AltitudeUnit.Feet, "35 000 ft (10 668 m)")]
        [InlineData(AltitudeUnit.FlightLevel, "FL350 (10 668 m)")]
        public void FormatWithEquivalent_ShowsUnitAndEquivalent(AltitudeUnit unit, string expected)
        {
            Assert.Equal(expected, Plain(AltitudeFormatter.FormatWithEquivalent(35000.0, unit)));
        }

        /// <summary>
        /// Metric legend labels use meters below one kilometer and kilometers above.
        /// </summary>
        [Theory]
        [InlineData(500.0, false, "500 m")]
        [InlineData(999.4, false, "999 m")]
        [InlineData(1000.0, false, "1 km")]
        [InlineData(1500.0, false, "1,5 km")]
        [InlineData(12000.0, true, "12 km+")]
        public void FormatLegend_Metric(double meters, bool isLast, string expected)
        {
            Assert.Equal(expected, Plain(AltitudeFormatter.FormatLegend(meters, isLast, AltitudeUnit.Metric)));
        }

        /// <summary>
        /// Feet legend labels are rounded to hundreds and flight level labels to flight levels.
        /// </summary>
        [Theory]
        [InlineData(3048.0, AltitudeUnit.Feet, "10 000 ft")]
        [InlineData(3050.0, AltitudeUnit.Feet, "10 000 ft")]
        [InlineData(12000.0, AltitudeUnit.Feet, "39 400 ft")]
        [InlineData(3048.0, AltitudeUnit.FlightLevel, "FL100")]
        [InlineData(12000.0, AltitudeUnit.FlightLevel, "FL394")]
        public void FormatLegend_FeetAndFlightLevel(double meters, AltitudeUnit unit, string expected)
        {
            Assert.Equal(expected, Plain(AltitudeFormatter.FormatLegend(meters, false, unit)));
        }

        /// <summary>
        /// The highest threshold gets a plus sign in every unit.
        /// </summary>
        [Theory]
        [InlineData(AltitudeUnit.Metric)]
        [InlineData(AltitudeUnit.Feet)]
        [InlineData(AltitudeUnit.FlightLevel)]
        public void FormatLegend_LastThreshold_EndsWithPlus(AltitudeUnit unit)
        {
            Assert.EndsWith("+", AltitudeFormatter.FormatLegend(9000.0, true, unit));
            Assert.DoesNotContain("+", AltitudeFormatter.FormatLegend(9000.0, false, unit));
        }
    }
}

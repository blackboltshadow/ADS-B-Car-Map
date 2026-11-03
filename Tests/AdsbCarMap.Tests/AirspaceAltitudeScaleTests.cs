using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the scale of the slider that chooses the band of heights of the airspace filter.
    /// </summary>
    public class AirspaceAltitudeScaleTests
    {
        /// <summary>
        /// The scale starts at the ground, ends at flight level 660 and grows with every step.
        /// </summary>
        [Fact]
        public void Steps_StartAtGroundEndAtTopAndGrow()
        {
            Assert.Equal(0, AirspaceAltitudeScale.FeetAt(0));
            Assert.Equal(66000, AirspaceAltitudeScale.FeetAt(AirspaceAltitudeScale.LastIndex));
            Assert.Equal(AirspaceAltitudeScale.Count - 1, AirspaceAltitudeScale.LastIndex);

            for (int i = 1; i < AirspaceAltitudeScale.Count; i++)
            {
                Assert.True(AirspaceAltitudeScale.FeetAt(i) > AirspaceAltitudeScale.FeetAt(i - 1));
            }
        }

        /// <summary>
        /// The steps are finer at low heights: 500 ft up to 10 000 ft, 1000 ft up to 30 000 ft and 2000 ft above.
        /// </summary>
        [Fact]
        public void Steps_AreFinerAtLowHeights()
        {
            Assert.Equal(500, AirspaceAltitudeScale.FeetAt(1));
            Assert.Equal(10000, AirspaceAltitudeScale.FeetAt(20));
            Assert.Equal(11000, AirspaceAltitudeScale.FeetAt(21));
            Assert.Equal(30000, AirspaceAltitudeScale.FeetAt(40));
            Assert.Equal(32000, AirspaceAltitudeScale.FeetAt(41));
            Assert.Equal(59, AirspaceAltitudeScale.Count);
        }

        /// <summary>
        /// An index out of range is moved to the nearest end.
        /// </summary>
        [Fact]
        public void FeetAt_ClampsTheIndex()
        {
            Assert.Equal(0, AirspaceAltitudeScale.FeetAt(-5));
            Assert.Equal(66000, AirspaceAltitudeScale.FeetAt(1000));
        }

        /// <summary>
        /// A height is moved to the nearest step, the lower one when it is exactly between two steps, and a height beyond the scale to its end.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(10000, 20)]
        [InlineData(1200, 2)]
        [InlineData(1250, 2)]
        [InlineData(1300, 3)]
        [InlineData(10500, 20)]
        [InlineData(10501, 21)]
        [InlineData(500000, 58)]
        [InlineData(-100, 0)]
        public void IndexOf_FindsTheNearestStep(int feet, int expected)
        {
            Assert.Equal(expected, AirspaceAltitudeScale.IndexOf(feet));
        }

        /// <summary>
        /// Every height of the scale finds its own step.
        /// </summary>
        [Fact]
        public void IndexOf_RoundTripsEveryStep()
        {
            for (int i = 0; i < AirspaceAltitudeScale.Count; i++)
            {
                Assert.Equal(i, AirspaceAltitudeScale.IndexOf(AirspaceAltitudeScale.FeetAt(i)));
            }
        }

        /// <summary>
        /// The band is written in the unit chosen by the user, and the top of the scale gets a plus.
        /// </summary>
        [Fact]
        public void FormatRange_WritesTheBandInTheUnit()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Od " + AltitudeFormatter.Format(0, AltitudeUnit.Metric) + " do " + AltitudeFormatter.Format(10000, AltitudeUnit.Metric), AirspaceText.FormatRange(0, 10000, AltitudeUnit.Metric));
            Assert.Equal("Od FL050 do FL245", AirspaceText.FormatRange(5000, 24500, AltitudeUnit.FlightLevel));
            Assert.Equal("Od FL000 do FL660+", AirspaceText.FormatRange(0, 66000, AltitudeUnit.FlightLevel));
            Assert.EndsWith(" ft", AirspaceText.FormatRange(0, 5000, AltitudeUnit.Feet));
            Assert.Equal("From {0} to {1}", Loc.Translate("Od {0} do {1}", AppLanguage.English));
        }
    }
}

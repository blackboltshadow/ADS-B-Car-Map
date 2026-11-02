using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the placement of an altitude on the color scale.
    /// </summary>
    public class AltitudeBlendTests
    {
        /// <summary>
        /// Thresholds of the scale used in the tests, in meters.
        /// </summary>
        private static readonly double[] Stops = new double[] { 300.0, 1500.0, 3000.0, 6000.0, 12000.0 };

        /// <summary>
        /// Altitudes up to the first threshold use the first color.
        /// </summary>
        [Theory]
        [InlineData(-100.0)]
        [InlineData(0.0)]
        [InlineData(300.0)]
        public void Locate_UpToFirstThreshold_IsFirstColor(double meters)
        {
            Assert.Equal(new AltitudeBlend(0, 0, 0.0), AltitudeBlend.Locate(meters, Stops, 5));
        }

        /// <summary>
        /// Altitudes between thresholds blend the neighboring colors linearly.
        /// </summary>
        [Theory]
        [InlineData(900.0, 0, 1, 0.5)]
        [InlineData(1500.0, 0, 1, 1.0)]
        [InlineData(2250.0, 1, 2, 0.5)]
        [InlineData(9000.0, 3, 4, 0.5)]
        public void Locate_BetweenThresholds_BlendsNeighbors(double meters, int from, int to, double fraction)
        {
            AltitudeBlend blend = AltitudeBlend.Locate(meters, Stops, 5);

            Assert.Equal(from, blend.FromIndex);
            Assert.Equal(to, blend.ToIndex);
            Assert.Equal(fraction, blend.Fraction, 9);
        }

        /// <summary>
        /// Altitudes above the last threshold use the last color.
        /// </summary>
        [Fact]
        public void Locate_AboveLastThreshold_IsLastColor()
        {
            Assert.Equal(new AltitudeBlend(4, 4, 0.0), AltitudeBlend.Locate(20000.0, Stops, 5));
        }

        /// <summary>
        /// Equal neighboring thresholds do not divide by zero: the altitude reaches the first of them with the full share of its color.
        /// </summary>
        [Fact]
        public void Locate_EqualNeighboringThresholds_DoNotDivideByZero()
        {
            double[] flat = new double[] { 300.0, 1000.0, 1000.0, 6000.0, 12000.0 };

            Assert.Equal(new AltitudeBlend(0, 1, 1.0), AltitudeBlend.Locate(1000.0, flat, 5));
            Assert.Equal(new AltitudeBlend(2, 3, 0.1), AltitudeBlend.Locate(1500.0, flat, 5));
        }

        /// <summary>
        /// The channel blend interpolates linearly and rounds.
        /// </summary>
        [Theory]
        [InlineData(255, 60, 0.0, 255)]
        [InlineData(255, 60, 1.0, 60)]
        [InlineData(255, 60, 0.5, 158)]
        [InlineData(0, 255, 0.5, 128)]
        [InlineData(100, 100, 0.7, 100)]
        public void BlendChannel_InterpolatesAndRounds(int from, int to, double fraction, int expected)
        {
            Assert.Equal(expected, AltitudeBlend.BlendChannel(from, to, fraction));
        }
    }
}

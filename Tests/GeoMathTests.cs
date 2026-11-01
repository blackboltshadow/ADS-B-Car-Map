using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of distance, bearing and compass point calculations.
    /// </summary>
    public class GeoMathTests
    {
        /// <summary>
        /// Distance between identical points is zero.
        /// </summary>
        [Fact]
        public void DistanceKilometers_SamePoint_IsZero()
        {
            Assert.Equal(0.0, GeoMath.DistanceKilometers(52.0, 21.0, 52.0, 21.0), 6);
        }

        /// <summary>
        /// One degree of latitude is about 111.2 km.
        /// </summary>
        [Fact]
        public void DistanceKilometers_OneDegreeOfLatitude_IsAbout111Km()
        {
            Assert.Equal(111.19, GeoMath.DistanceKilometers(0.0, 0.0, 1.0, 0.0), 1);
        }

        /// <summary>
        /// Warsaw to Berlin is about 516 km.
        /// </summary>
        [Fact]
        public void DistanceKilometers_WarsawToBerlin_IsAbout516Km()
        {
            Assert.InRange(GeoMath.DistanceKilometers(52.2297, 21.0122, 52.5200, 13.4050), 510.0, 522.0);
        }

        /// <summary>
        /// Distance is symmetric.
        /// </summary>
        [Fact]
        public void DistanceKilometers_IsSymmetric()
        {
            double forward = GeoMath.DistanceKilometers(10.0, 20.0, -30.0, 40.0);
            double backward = GeoMath.DistanceKilometers(-30.0, 40.0, 10.0, 20.0);

            Assert.Equal(forward, backward, 9);
        }

        /// <summary>
        /// Cardinal directions give bearings of 0, 90, 180 and 270 degrees.
        /// </summary>
        [Theory]
        [InlineData(1.0, 0.0, 0.0)]
        [InlineData(0.0, 1.0, 90.0)]
        [InlineData(-1.0, 0.0, 180.0)]
        [InlineData(0.0, -1.0, 270.0)]
        public void BearingDegrees_CardinalDirections(double latitude, double longitude, double expected)
        {
            Assert.Equal(expected, GeoMath.BearingDegrees(0.0, 0.0, latitude, longitude), 6);
        }

        /// <summary>
        /// Compass points are chosen from 16 sectors, with wrap-around at north.
        /// </summary>
        [Theory]
        [InlineData(0.0, "N")]
        [InlineData(11.0, "N")]
        [InlineData(12.0, "NNE")]
        [InlineData(47.0, "NE")]
        [InlineData(90.0, "E")]
        [InlineData(180.0, "S")]
        [InlineData(270.0, "W")]
        [InlineData(350.0, "N")]
        [InlineData(360.0, "N")]
        [InlineData(-90.0, "W")]
        [InlineData(450.0, "E")]
        public void CompassPoint_MapsDegreesToName(double degrees, string expected)
        {
            Assert.Equal(expected, GeoMath.CompassPoint(degrees));
        }
    }
}

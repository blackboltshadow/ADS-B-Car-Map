using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of dead reckoning and download radius calculations.
    /// </summary>
    public class MapMathTests
    {
        /// <summary>
        /// Creates an aircraft with the given motion data.
        /// </summary>
        private static Aircraft Create(bool onGround, double? speedKnots, double? trackDegrees, double secondsSincePosition = 0.0)
        {
            return new Aircraft("abc123", "TST1", 50.0, 20.0, 30000, onGround, speedKnots, trackDegrees, secondsSincePosition, "B738", "SP-TST");
        }

        /// <summary>
        /// An aircraft on the ground keeps its reported position.
        /// </summary>
        [Fact]
        public void Extrapolate_OnGround_ReturnsReportedPosition()
        {
            (double Latitude, double Longitude) result = MapMath.Extrapolate(Create(true, 400.0, 90.0), 60.0);

            Assert.Equal(50.0, result.Latitude);
            Assert.Equal(20.0, result.Longitude);
        }

        /// <summary>
        /// Missing speed or track keeps the reported position.
        /// </summary>
        [Theory]
        [InlineData(null, 90.0)]
        [InlineData(400.0, null)]
        public void Extrapolate_MissingSpeedOrTrack_ReturnsReportedPosition(double? speed, double? track)
        {
            (double Latitude, double Longitude) result = MapMath.Extrapolate(Create(false, speed, track), 60.0);

            Assert.Equal(50.0, result.Latitude);
            Assert.Equal(20.0, result.Longitude);
        }

        /// <summary>
        /// Flying north at 60 knots for one hour moves the aircraft one degree of latitude.
        /// </summary>
        [Fact]
        public void Extrapolate_North_MovesLatitude()
        {
            (double Latitude, double Longitude) result = MapMath.Extrapolate(Create(false, 60.0, 0.0), 3600.0);

            Assert.Equal(51.0, result.Latitude, 6);
            Assert.Equal(20.0, result.Longitude, 6);
        }

        /// <summary>
        /// Flying east moves only the longitude, scaled by the cosine of latitude.
        /// </summary>
        [Fact]
        public void Extrapolate_East_MovesLongitudeScaledByLatitude()
        {
            (double Latitude, double Longitude) result = MapMath.Extrapolate(Create(false, 60.0, 90.0), 3600.0);

            Assert.Equal(50.0, result.Latitude, 6);
            Assert.Equal(20.0 + 1.0 / Math.Cos(50.0 * Math.PI / 180.0), result.Longitude, 6);
        }

        /// <summary>
        /// The age of the report is added to the elapsed time.
        /// </summary>
        [Fact]
        public void Extrapolate_AddsSecondsSincePosition()
        {
            (double Latitude, double Longitude) result = MapMath.Extrapolate(Create(false, 60.0, 0.0, 1800.0), 1800.0);

            Assert.Equal(51.0, result.Latitude, 6);
        }

        /// <summary>
        /// Close to the pole the longitude scale is limited to 0.1, so the result stays finite.
        /// </summary>
        [Fact]
        public void Extrapolate_NearPole_LimitsLongitudeScale()
        {
            Aircraft polar = new Aircraft("abc123", null, 89.9999, 0.0, null, false, 60.0, 90.0, 0.0, null, null);

            (double Latitude, double Longitude) result = MapMath.Extrapolate(polar, 3600.0);

            Assert.Equal(10.0, result.Longitude, 6);
        }

        /// <summary>
        /// A small area gives the minimum radius.
        /// </summary>
        [Fact]
        public void CalculateRadiusNauticalMiles_SmallArea_ReturnsMinimum()
        {
            Assert.Equal(10.0, MapMath.CalculateRadiusNauticalMiles(50.0, 16.0, 800, 480));
        }

        /// <summary>
        /// Zooming out by one level doubles the radius when it is above the minimum.
        /// </summary>
        [Fact]
        public void CalculateRadiusNauticalMiles_ZoomOutOneLevel_DoublesRadius()
        {
            double near = MapMath.CalculateRadiusNauticalMiles(0.0, 8.0, 1000, 1000);
            double far = MapMath.CalculateRadiusNauticalMiles(0.0, 7.0, 1000, 1000);

            Assert.Equal(near * 2.0, far, 6);
        }

        /// <summary>
        /// At the equator the radius equals half the diagonal times the meters per pixel, in nautical miles.
        /// </summary>
        [Fact]
        public void CalculateRadiusNauticalMiles_Equator_MatchesFormula()
        {
            double expected = Math.Sqrt(2.0) * 500.0 * 156543.03392 / Math.Pow(2.0, 8.0) / 1852.0;

            Assert.Equal(expected, MapMath.CalculateRadiusNauticalMiles(0.0, 8.0, 1000, 1000), 6);
        }
    }
}

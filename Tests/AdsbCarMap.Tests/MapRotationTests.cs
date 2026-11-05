using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the rotation of the map: angles, vectors and the projection of points on a turned screen.
    /// </summary>
    public class MapRotationTests
    {
        /// <summary>
        /// Angles are brought to the range from 0 to 360.
        /// </summary>
        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(360.0, 0.0)]
        [InlineData(-90.0, 270.0)]
        [InlineData(725.0, 5.0)]
        public void Normalize_BringsAngleToRange(double degrees, double expected)
        {
            Assert.Equal(expected, MapRotation.Normalize(degrees), 9);
        }

        /// <summary>
        /// The turn between two angles goes the short way round, also across north.
        /// </summary>
        [Theory]
        [InlineData(350.0, 10.0, 20.0)]
        [InlineData(10.0, 350.0, -20.0)]
        [InlineData(0.0, 180.0, 180.0)]
        [InlineData(90.0, 90.0, 0.0)]
        public void ShortestDelta_GoesTheShortWay(double from, double to, double expected)
        {
            Assert.Equal(expected, MapRotation.ShortestDelta(from, to), 9);
        }

        /// <summary>
        /// The ease turns across north instead of the long way round, and reaches the target.
        /// </summary>
        [Fact]
        public void Ease_CrossesNorthTheShortWay()
        {
            double next = MapRotation.Ease(350.0, 10.0, 0.05);

            Assert.True(next > 350.0 || next < 10.0);
            Assert.InRange(MapRotation.ShortestDelta(350.0, next), 0.0, 20.0);
            Assert.Equal(10.0, MapRotation.Ease(9.99, 10.0, 0.05), 9);
        }

        /// <summary>
        /// A zero time step does not move the angle.
        /// </summary>
        [Fact]
        public void Ease_WithoutTime_StaysPut()
        {
            Assert.Equal(40.0, MapRotation.Ease(40.0, 100.0, 0.0), 9);
        }

        /// <summary>
        /// A vector turns clockwise on the screen, where the vertical axis grows downwards.
        /// </summary>
        [Fact]
        public void RotateVector_TurnsClockwiseOnScreen()
        {
            (double X, double Y) up = MapRotation.RotateVector(0.0, -1.0, 90.0);

            Assert.Equal(1.0, up.X, 9);
            Assert.Equal(0.0, up.Y, 9);
        }

        /// <summary>
        /// Turning a vector forth and back gives the original vector.
        /// </summary>
        [Fact]
        public void RotateVector_RoundTrip()
        {
            (double X, double Y) turned = MapRotation.RotateVector(3.0, -4.0, 37.0);
            (double X, double Y) back = MapRotation.RotateVector(turned.X, turned.Y, -37.0);

            Assert.Equal(3.0, back.X, 9);
            Assert.Equal(-4.0, back.Y, 9);
        }

        /// <summary>
        /// The bearing is trusted only for a device that moves fast enough and reports one.
        /// </summary>
        [Theory]
        [InlineData(true, 10.0, true)]
        [InlineData(true, 1.5, true)]
        [InlineData(true, 1.0, false)]
        [InlineData(false, 20.0, false)]
        public void IsBearingReliable_NeedsBearingAndSpeed(bool hasBearing, double speed, bool expected)
        {
            Assert.Equal(expected, MapRotation.IsBearingReliable(hasBearing, speed));
        }

        /// <summary>
        /// The covering square is as long as the screen diagonal.
        /// </summary>
        [Fact]
        public void GetCoveringSize_IsDiagonal()
        {
            Assert.Equal(500, MapRotation.GetCoveringSize(300, 400));
        }

        /// <summary>
        /// With the map heading east, a point to the north of the center is drawn to the left of it, and a point to the east is drawn above it.
        /// </summary>
        [Fact]
        public void ProjectToScreen_HeadingEast_PutsEastAtTop()
        {
            (float X, float Y) north = WebMercator.ProjectToScreen(52.1, 21.0, 52.0, 21.0, 10.0, 400, 800, -90.0);
            (float X, float Y) east = WebMercator.ProjectToScreen(52.0, 21.1, 52.0, 21.0, 10.0, 400, 800, -90.0);

            Assert.True(north.X < 200f);
            Assert.Equal(400f, north.Y, 0);
            Assert.True(east.Y < 400f);
            Assert.Equal(200f, east.X, 0);
        }

        /// <summary>
        /// A point on the screen converts back to the coordinates that were projected there, at any rotation.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-90.0)]
        [InlineData(33.0)]
        [InlineData(-170.0)]
        public void ScreenToLatLon_UndoesProjection(double rotation)
        {
            (float X, float Y) screen = WebMercator.ProjectToScreen(52.05, 21.07, 52.0, 21.0, 11.0, 400, 800, rotation);
            (double Latitude, double Longitude) back = WebMercator.ScreenToLatLon(screen.X, screen.Y, 52.0, 21.0, 11.0, 400, 800, rotation);

            Assert.Equal(52.05, back.Latitude, 4);
            Assert.Equal(21.07, back.Longitude, 4);
        }
    }
}

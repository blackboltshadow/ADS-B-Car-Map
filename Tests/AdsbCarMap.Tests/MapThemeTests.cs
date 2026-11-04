using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the map theme logic.
    /// </summary>
    public class MapThemeTests
    {
        /// <summary>
        /// The automatic theme follows the system night mode.
        /// </summary>
        [Theory]
        [InlineData(MapTheme.Automatic, true, true)]
        [InlineData(MapTheme.Automatic, false, false)]
        [InlineData(MapTheme.Light, true, false)]
        [InlineData(MapTheme.Light, false, false)]
        [InlineData(MapTheme.Dark, true, true)]
        [InlineData(MapTheme.Dark, false, true)]
        public void IsDark_FollowsThemeAndSystem(int theme, bool systemDark, bool expected)
        {
            Assert.Equal(expected, MapTheme.IsDark(theme, systemDark));
        }

        /// <summary>
        /// The dark matrix turns white into a dark gray and black into a light gray, and keeps the hues of the rows consistent.
        /// </summary>
        [Fact]
        public void CreateDarkMatrix_InvertsLightness()
        {
            float[] matrix = MapTheme.CreateDarkMatrix();

            Assert.Equal(20, matrix.Length);

            for (int row = 0; row < 3; row++)
            {
                float white = (matrix[row * 5] + matrix[row * 5 + 1] + matrix[row * 5 + 2]) * 255.0f + matrix[row * 5 + 4];
                float black = matrix[row * 5 + 4];

                Assert.InRange(white, 0.0f, 60.0f);
                Assert.InRange(black, 180.0f, 255.0f);
            }
        }
    }
}

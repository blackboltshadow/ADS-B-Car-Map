using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the remembered map view and its stored text.
    /// </summary>
    public class MapViewStateTests
    {
        /// <summary>
        /// The formatted text is read back without loss.
        /// </summary>
        [Fact]
        public void FormatAndTryParse_RoundTrip()
        {
            MapViewState state = new MapViewState(52.2297, 21.0122, 9.25, false);

            MapViewState? parsed = MapViewState.TryParse(state.Format(), 4.0, 13.0);

            Assert.NotNull(parsed);
            Assert.Equal(52.2297, parsed.Latitude);
            Assert.Equal(21.0122, parsed.Longitude);
            Assert.Equal(9.25, parsed.Zoom);
            Assert.False(parsed.FollowLocation);
        }

        /// <summary>
        /// The orientation of the map is stored and restored.
        /// </summary>
        [Fact]
        public void FormatAndTryParse_KeepsHeadingOrientation()
        {
            MapViewState state = new MapViewState(52.0, 21.0, 9.0, true, MapOrientation.Heading);

            MapViewState? parsed = MapViewState.TryParse(state.Format(), 4.0, 13.0);

            Assert.NotNull(parsed);
            Assert.Equal(MapOrientation.Heading, parsed.Orientation);
        }

        /// <summary>
        /// Text written by an older version, without the orientation, means that north was at the top.
        /// </summary>
        [Fact]
        public void TryParse_OldTextWithoutOrientation_IsNorth()
        {
            MapViewState? parsed = MapViewState.TryParse("50;20;8;1", 4.0, 13.0);

            Assert.NotNull(parsed);
            Assert.Equal(MapOrientation.North, parsed.Orientation);
        }

        /// <summary>
        /// An orientation value other than 0 or 1 makes the text invalid.
        /// </summary>
        [Fact]
        public void TryParse_InvalidOrientation_ReturnsNull()
        {
            Assert.Null(MapViewState.TryParse("50;20;8;1;2", 4.0, 13.0));
        }

        /// <summary>
        /// The follow flag is stored and restored.
        /// </summary>
        [Fact]
        public void TryParse_FollowFlag()
        {
            MapViewState? parsed = MapViewState.TryParse("50;20;8;1", 4.0, 13.0);

            Assert.NotNull(parsed);
            Assert.True(parsed.FollowLocation);
        }

        /// <summary>
        /// Zoom outside the allowed range is moved into it, also when the limits are given in reverse.
        /// </summary>
        [Theory]
        [InlineData("50;20;2;1", 4.0, 13.0, 4.0)]
        [InlineData("50;20;17;1", 4.0, 13.0, 13.0)]
        [InlineData("50;20;17;1", 13.0, 4.0, 13.0)]
        public void TryParse_ClampsZoom(string text, double minimum, double maximum, double expected)
        {
            MapViewState? parsed = MapViewState.TryParse(text, minimum, maximum);

            Assert.NotNull(parsed);
            Assert.Equal(expected, parsed.Zoom);
        }

        /// <summary>
        /// Missing, malformed or out-of-range text gives no state.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("50;20;8")]
        [InlineData("50;20;8;1;5")]
        [InlineData("abc;20;8;1")]
        [InlineData("50;20;8;2")]
        [InlineData("91;20;8;1")]
        [InlineData("50;181;8;1")]
        [InlineData("NaN;20;8;1")]
        [InlineData("50;20;Infinity;1")]
        public void TryParse_InvalidText_ReturnsNull(string? text)
        {
            Assert.Null(MapViewState.TryParse(text, 4.0, 13.0));
        }
    }
}

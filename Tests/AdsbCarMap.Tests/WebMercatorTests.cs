using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of Web Mercator conversions and tile selection.
    /// </summary>
    public class WebMercatorTests
    {
        /// <summary>
        /// World size doubles with every zoom level.
        /// </summary>
        [Fact]
        public void WorldSize_DoublesPerZoomLevel()
        {
            Assert.Equal(256.0, WebMercator.WorldSize(0.0));
            Assert.Equal(1024.0, WebMercator.WorldSize(2.0));
        }

        /// <summary>
        /// The point (0, 0) lies in the middle of the world map.
        /// </summary>
        [Fact]
        public void LatLonToWorldPixel_Origin_IsWorldCenter()
        {
            (double X, double Y) pixel = WebMercator.LatLonToWorldPixel(0.0, 0.0, 3.0);

            Assert.Equal(1024.0, pixel.X, 6);
            Assert.Equal(1024.0, pixel.Y, 6);
        }

        /// <summary>
        /// Converting to pixels and back returns the original coordinates.
        /// </summary>
        [Theory]
        [InlineData(52.2297, 21.0122, 10.0)]
        [InlineData(-33.8688, 151.2093, 7.5)]
        [InlineData(64.1466, -21.9426, 14.0)]
        public void LatLonToWorldPixel_RoundTrips(double latitude, double longitude, double zoom)
        {
            (double X, double Y) pixel = WebMercator.LatLonToWorldPixel(latitude, longitude, zoom);
            (double Latitude, double Longitude) back = WebMercator.WorldPixelToLatLon(pixel.X, pixel.Y, zoom);

            Assert.Equal(latitude, back.Latitude, 6);
            Assert.Equal(longitude, back.Longitude, 6);
        }

        /// <summary>
        /// Latitudes beyond the Mercator limit are clamped instead of producing infinity.
        /// </summary>
        [Fact]
        public void LatLonToWorldPixel_PoleLatitude_IsClamped()
        {
            (double X, double Y) pixel = WebMercator.LatLonToWorldPixel(90.0, 0.0, 2.0);

            Assert.True(double.IsFinite(pixel.Y));
            Assert.InRange(pixel.Y, -0.01, 0.01);
        }

        /// <summary>
        /// The screen center projects onto the middle of the screen.
        /// </summary>
        [Fact]
        public void ProjectToScreen_CenterPoint_IsScreenMiddle()
        {
            (float X, float Y) point = WebMercator.ProjectToScreen(52.0, 21.0, 52.0, 21.0, 8.0, 800, 600);

            Assert.Equal(400.0f, point.X, 3);
            Assert.Equal(300.0f, point.Y, 3);
        }

        /// <summary>
        /// A point just across the antimeridian is projected next to the center, not a world away.
        /// </summary>
        [Fact]
        public void ProjectToScreen_AcrossAntimeridian_UsesNearestWorldCopy()
        {
            (float X, float Y) point = WebMercator.ProjectToScreen(0.0, -179.5, 0.0, 179.5, 5.0, 800, 600);

            Assert.InRange(point.X, 400.0f, 600.0f);
        }

        /// <summary>
        /// Visible tiles cover the whole screen.
        /// </summary>
        [Fact]
        public void GetVisibleTiles_CoverTheScreen()
        {
            List<VisibleTile> tiles = WebMercator.GetVisibleTiles(52.0, 21.0, 10, 800, 600);

            Assert.NotEmpty(tiles);
            Assert.True(tiles.Min(tile => tile.ScreenX) <= 0.0f);
            Assert.True(tiles.Min(tile => tile.ScreenY) <= 0.0f);
            Assert.True(tiles.Max(tile => tile.ScreenX) + WebMercator.TileSize >= 800.0f);
            Assert.True(tiles.Max(tile => tile.ScreenY) + WebMercator.TileSize >= 600.0f);
        }

        /// <summary>
        /// Tile column numbers wrap around the date line and stay within the grid.
        /// </summary>
        [Fact]
        public void GetVisibleTiles_NearDateLine_WrapsColumns()
        {
            List<VisibleTile> tiles = WebMercator.GetVisibleTiles(0.0, 179.9, 4, 1000, 400);

            Assert.All(tiles, tile => Assert.InRange(tile.Key.X, 0, 15));
            Assert.Contains(tiles, tile => tile.Key.X == 15);
            Assert.Contains(tiles, tile => tile.Key.X == 0);
        }

        /// <summary>
        /// Rows outside the world are not returned near the poles.
        /// </summary>
        [Fact]
        public void GetVisibleTiles_NearPole_KeepsRowsInRange()
        {
            List<VisibleTile> tiles = WebMercator.GetVisibleTiles(85.0, 0.0, 3, 800, 800);

            Assert.All(tiles, tile => Assert.InRange(tile.Key.Y, 0, 7));
        }

        /// <summary>
        /// An empty screen yields no tiles.
        /// </summary>
        [Fact]
        public void GetVisibleTiles_ZeroSize_ReturnsEmpty()
        {
            Assert.Empty(WebMercator.GetVisibleTiles(0.0, 0.0, 5, 0, 600));
        }
    }
}

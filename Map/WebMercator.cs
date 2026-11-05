namespace AdsbCarMap.Map
{
    /// <summary>
    /// Coordinate conversions in the Web Mercator projection (EPSG:3857) for 256 px tiles.
    /// </summary>
    public static class WebMercator
    {
        /// <summary>
        /// Tile side length in pixels.
        /// </summary>
        public const int TileSize = 256;

        /// <summary>
        /// Largest latitude that can be represented in the projection.
        /// </summary>
        public const double MaxLatitude = 85.05112878;

        /// <summary>
        /// Returns the side length of the whole world map in pixels for a zoom level.
        /// </summary>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>Side length of the world map in pixels.</returns>
        public static double WorldSize(double zoom)
        {
            return TileSize * Math.Pow(2.0, zoom);
        }

        /// <summary>
        /// Converts geographic coordinates to world map pixels.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>Position in pixels measured from the top left corner of the world map.</returns>
        public static (double X, double Y) LatLonToWorldPixel(double latitude, double longitude, double zoom)
        {
            double size = WorldSize(zoom);
            double clampedLatitude = Math.Clamp(latitude, -MaxLatitude, MaxLatitude);
            double sinLatitude = Math.Sin(clampedLatitude * Math.PI / 180.0);

            double x = (longitude + 180.0) / 360.0 * size;
            double y = (0.5 - Math.Log((1.0 + sinLatitude) / (1.0 - sinLatitude)) / (4.0 * Math.PI)) * size;

            return (x, y);
        }

        /// <summary>
        /// Converts world map pixels to geographic coordinates. The longitude is not normalized.
        /// </summary>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <param name="y">Vertical position in pixels.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>Latitude and longitude.</returns>
        public static (double Latitude, double Longitude) WorldPixelToLatLon(double x, double y, double zoom)
        {
            double size = WorldSize(zoom);

            double longitude = x / size * 360.0 - 180.0;
            double n = Math.PI - 2.0 * Math.PI * y / size;
            double latitude = 180.0 / Math.PI * Math.Atan(Math.Sinh(n));

            return (latitude, longitude);
        }

        /// <summary>
        /// Projects a geographic point onto a screen with the given center. Picks the copy of the world closest to the center.
        /// </summary>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="rotationDegrees">Clockwise rotation of the map around the screen center in degrees; zero keeps north at the top.</param>
        /// <returns>Position of the point on the screen in pixels.</returns>
        public static (float X, float Y) ProjectToScreen(double latitude, double longitude, double centerLatitude, double centerLongitude, double zoom, int width, int height, double rotationDegrees = 0.0)
        {
            double size = WorldSize(zoom);
            (double X, double Y) point = LatLonToWorldPixel(latitude, longitude, zoom);
            (double X, double Y) center = LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);

            double dx = point.X - center.X;

            if (dx > size / 2.0)
            {
                dx -= size;
            }
            else if (dx < -size / 2.0)
            {
                dx += size;
            }

            double dy = point.Y - center.Y;

            (double X, double Y) turned = MapRotation.RotateVector(dx, dy, rotationDegrees);

            return ((float)(width / 2.0 + turned.X), (float)(height / 2.0 + turned.Y));
        }

        /// <summary>
        /// Converts a point of the screen to geographic coordinates; the opposite of <see cref="ProjectToScreen"/>. The longitude is not normalized.
        /// </summary>
        /// <param name="x">Horizontal position on the screen in pixels.</param>
        /// <param name="y">Vertical position on the screen in pixels.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="rotationDegrees">Clockwise rotation of the map around the screen center in degrees.</param>
        /// <returns>Latitude and longitude of the point.</returns>
        public static (double Latitude, double Longitude) ScreenToLatLon(double x, double y, double centerLatitude, double centerLongitude, double zoom, int width, int height, double rotationDegrees = 0.0)
        {
            (double X, double Y) center = LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
            (double X, double Y) offset = MapRotation.RotateVector(x - (width / 2.0), y - (height / 2.0), -rotationDegrees);

            return WorldPixelToLatLon(center.X + offset.X, center.Y + offset.Y, zoom);
        }

        /// <summary>
        /// Finds the tiles covering a screen with the given center.
        /// </summary>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <returns>List of tiles with their screen positions.</returns>
        public static List<VisibleTile> GetVisibleTiles(double centerLatitude, double centerLongitude, int zoom, int width, int height)
        {
            List<VisibleTile> result = new List<VisibleTile>();

            if (width <= 0 || height <= 0)
            {
                return result;
            }

            int tileCount = 1 << zoom;
            (double X, double Y) center = LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);

            double left = center.X - width / 2.0;
            double top = center.Y - height / 2.0;

            int firstColumn = (int)Math.Floor(left / TileSize);
            int lastColumn = (int)Math.Floor((left + width) / TileSize);
            int firstRow = Math.Max(0, (int)Math.Floor(top / TileSize));
            int lastRow = Math.Min(tileCount - 1, (int)Math.Floor((top + height) / TileSize));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    int wrappedColumn = ((column % tileCount) + tileCount) % tileCount;
                    float screenX = (float)(column * (double)TileSize - left);
                    float screenY = (float)(row * (double)TileSize - top);

                    result.Add(new VisibleTile(new TileKey(zoom, wrappedColumn, row), screenX, screenY));
                }
            }

            return result;
        }
    }
}

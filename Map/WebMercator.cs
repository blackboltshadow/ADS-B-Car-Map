namespace AdsbCarMap.Map;

/// <summary>
/// Przeliczenia współrzędnych w odwzorowaniu Web Mercator (EPSG:3857) dla kafelków 256 px.
/// </summary>
public static class WebMercator
{
    /// <summary>
    /// Bok kafelka w pikselach.
    /// </summary>
    public const int TileSize = 256;

    /// <summary>
    /// Największa szerokość geograficzna możliwa do przedstawienia w odwzorowaniu.
    /// </summary>
    public const double MaxLatitude = 85.05112878;

    /// <summary>
    /// Zwraca bok całej mapy świata w pikselach dla poziomu przybliżenia.
    /// </summary>
    /// <param name="zoom">Poziom przybliżenia.</param>
    /// <returns>Bok mapy świata w pikselach.</returns>
    public static double WorldSize(double zoom)
    {
        return TileSize * Math.Pow(2.0, zoom);
    }

    /// <summary>
    /// Zamienia współrzędne geograficzne na piksele mapy świata.
    /// </summary>
    /// <param name="latitude">Szerokość geograficzna.</param>
    /// <param name="longitude">Długość geograficzna.</param>
    /// <param name="zoom">Poziom przybliżenia.</param>
    /// <returns>Pozycja w pikselach liczona od lewego górnego rogu mapy świata.</returns>
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
    /// Zamienia piksele mapy świata na współrzędne geograficzne. Długość nie jest normalizowana.
    /// </summary>
    /// <param name="x">Pozycja w poziomie w pikselach.</param>
    /// <param name="y">Pozycja w pionie w pikselach.</param>
    /// <param name="zoom">Poziom przybliżenia.</param>
    /// <returns>Szerokość i długość geograficzna.</returns>
    public static (double Latitude, double Longitude) WorldPixelToLatLon(double x, double y, double zoom)
    {
        double size = WorldSize(zoom);

        double longitude = x / size * 360.0 - 180.0;
        double n = Math.PI - 2.0 * Math.PI * y / size;
        double latitude = 180.0 / Math.PI * Math.Atan(Math.Sinh(n));

        return (latitude, longitude);
    }

    /// <summary>
    /// Rzutuje punkt geograficzny na ekran o podanym środku. Wybiera kopię świata najbliższą środkowi.
    /// </summary>
    /// <param name="latitude">Szerokość geograficzna punktu.</param>
    /// <param name="longitude">Długość geograficzna punktu.</param>
    /// <param name="centerLatitude">Szerokość geograficzna środka ekranu.</param>
    /// <param name="centerLongitude">Długość geograficzna środka ekranu.</param>
    /// <param name="zoom">Poziom przybliżenia.</param>
    /// <param name="width">Szerokość ekranu w pikselach.</param>
    /// <param name="height">Wysokość ekranu w pikselach.</param>
    /// <returns>Pozycja punktu na ekranie w pikselach.</returns>
    public static (float X, float Y) ProjectToScreen(double latitude, double longitude, double centerLatitude, double centerLongitude, double zoom, int width, int height)
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

        return ((float)(width / 2.0 + dx), (float)(height / 2.0 + dy));
    }

    /// <summary>
    /// Wyznacza kafelki pokrywające ekran o podanym środku.
    /// </summary>
    /// <param name="centerLatitude">Szerokość geograficzna środka ekranu.</param>
    /// <param name="centerLongitude">Długość geograficzna środka ekranu.</param>
    /// <param name="zoom">Poziom przybliżenia.</param>
    /// <param name="width">Szerokość ekranu w pikselach.</param>
    /// <param name="height">Wysokość ekranu w pikselach.</param>
    /// <returns>Lista kafelków z pozycjami na ekranie.</returns>
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

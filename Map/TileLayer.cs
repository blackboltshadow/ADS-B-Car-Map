using System.Collections.Concurrent;
using System.Globalization;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.Util;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the map tiles: keeps the decoded tiles in memory, downloads the missing ones in the background and substitutes them with enlarged or reduced tiles of neighboring zoom levels until they arrive.
    /// </summary>
    internal sealed class TileLayer
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Highest zoom level of the OpenStreetMap tiles.
        /// </summary>
        public const int MaxTileZoom = 19;

        /// <summary>
        /// How many levels lower the base tile is always loaded; it serves as a background while the more detailed tile has not arrived yet.
        /// </summary>
        private const int BaseLayerLevels = 2;

        /// <summary>
        /// How many levels down a fallback tile is searched for.
        /// </summary>
        private const int MaxFallbackLevels = 3;

        /// <summary>
        /// Width of the band around the screen, in tile pixels, from which tiles are loaded ahead of time.
        /// </summary>
        private const int TilePrefetchPixels = 256;

        /// <summary>
        /// Maximum number of tiles downloaded at the same time.
        /// </summary>
        private const int MaxConcurrentTileDownloads = 6;

        /// <summary>
        /// Number of spare tile slots above the visible tile count, so that the settings limit does not cause constant downloading.
        /// </summary>
        private const int TileCacheMargin = 16;

        /// <summary>
        /// Interval between tile statistics log entries.
        /// </summary>
        private static readonly TimeSpan TileStatisticsInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Decoded tiles ready to be drawn.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, Bitmap> bitmaps = new ConcurrentDictionary<TileKey, Bitmap>();

        /// <summary>
        /// Tiles that are currently being downloaded or decoded.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, byte> loadingTiles = new ConcurrentDictionary<TileKey, byte>();

        /// <summary>
        /// Number of the frame in which a tile was last drawn or decoded. Used to remove the least recently used tiles.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, long> tileLastUsedFrame = new ConcurrentDictionary<TileKey, long>();

        /// <summary>
        /// Tiles needed in the last frame: visible ones, neighbors to load ahead of time, and the lower-level base layer. The set is replaced every frame and is not modified after publication.
        /// </summary>
        private HashSet<TileKey> wantedTiles = new HashSet<TileKey>();

        /// <summary>
        /// Limits the number of simultaneous tile downloads.
        /// </summary>
        private readonly SemaphoreSlim tileGate = new SemaphoreSlim(MaxConcurrentTileDownloads);

        /// <summary>
        /// Source region of the tile used as a fallback.
        /// </summary>
        private readonly Rect fallbackSource = new Rect();

        /// <summary>
        /// Area in which the fallback tile is drawn.
        /// </summary>
        private readonly RectF fallbackDestination = new RectF();

        /// <summary>
        /// Number of the most recently started drawing frame.
        /// </summary>
        private long frameCounter;

        /// <summary>
        /// Number of tiles covering the screen in the last frame.
        /// </summary>
        private int lastVisibleTileCount;

        /// <summary>
        /// Time after which a failed tile download can be retried.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, DateTime> tileRetryAfterUtc = new ConcurrentDictionary<TileKey, DateTime>();

        /// <summary>
        /// Number of tiles decoded since the controller was started.
        /// </summary>
        private long tilesDecodedCount;

        /// <summary>
        /// Number of failed attempts to download or decode a tile since the controller was started.
        /// </summary>
        private long tilesFailedCount;

        /// <summary>
        /// Size and format of the most recently decoded tile.
        /// </summary>
        private string lastDecodedTileInfo = "brak";

        /// <summary>
        /// Moment of the last tile statistics log entry, in UTC.
        /// </summary>
        private DateTime lastTileStatisticsUtc = DateTime.MinValue;

        /// <summary>
        /// Paint used to draw the tiles.
        /// </summary>
        private readonly Paint tilePaint = new Paint(PaintFlags.FilterBitmap);

        /// <summary>
        /// Map tile provider.
        /// </summary>
        private readonly TileProvider tileProvider;

        /// <summary>
        /// Creates the layer.
        /// </summary>
        /// <param name="tileProvider">Map tile provider.</param>
        public TileLayer(TileProvider tileProvider)
        {
            this.tileProvider = tileProvider;
        }

        /// <summary>
        /// Gets a value indicating whether any tile is currently being downloaded or decoded.
        /// </summary>
        public bool IsLoading
        {
            get
            {
                return !loadingTiles.IsEmpty;
            }
        }

        /// <summary>
        /// Forgets the tiles wanted by the last frame, so that queued downloads are skipped.
        /// </summary>
        public void ResetWanted()
        {
            Volatile.Write(ref wantedTiles, new HashSet<TileKey>());
        }

        /// <summary>
        /// Draws the tiles covering the view and requests the missing and neighboring ones.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="latitude">Latitude of the view center.</param>
        /// <param name="longitude">Longitude of the view center.</param>
        /// <param name="zoomLevel">Current zoom level.</param>
        /// <param name="width">Surface width in pixels.</param>
        /// <param name="height">Surface height in pixels.</param>
        public void Draw(Canvas canvas, double latitude, double longitude, double zoomLevel, int width, int height)
        {
            long frame = Interlocked.Increment(ref frameCounter);
            int tileZoom = Math.Clamp((int)Math.Round(zoomLevel), 0, MaxTileZoom);
            float tileScale = (float)Math.Pow(2.0, zoomLevel - tileZoom);
            int virtualWidth = (int)Math.Ceiling(width / tileScale);
            int virtualHeight = (int)Math.Ceiling(height / tileScale);

            List<VisibleTile> tiles = WebMercator.GetVisibleTiles(latitude, longitude, tileZoom, virtualWidth, virtualHeight);
            List<VisibleTile> prefetchTiles = WebMercator.GetVisibleTiles(latitude, longitude, tileZoom, virtualWidth + 2 * TilePrefetchPixels, virtualHeight + 2 * TilePrefetchPixels);
            List<TileKey> baseKeys = new List<TileKey>();
            HashSet<TileKey> wanted = new HashSet<TileKey>();

            foreach (VisibleTile tile in tiles)
            {
                if (tile.Key.Zoom >= BaseLayerLevels)
                {
                    TileKey baseKey = new TileKey(tile.Key.Zoom - BaseLayerLevels, tile.Key.X >> BaseLayerLevels, tile.Key.Y >> BaseLayerLevels);

                    if (wanted.Add(baseKey))
                    {
                        baseKeys.Add(baseKey);
                    }
                }
            }

            foreach (VisibleTile tile in tiles)
            {
                wanted.Add(tile.Key);
            }

            foreach (VisibleTile tile in prefetchTiles)
            {
                wanted.Add(tile.Key);
            }

            Volatile.Write(ref wantedTiles, wanted);
            Volatile.Write(ref lastVisibleTileCount, wanted.Count);

            foreach (TileKey key in wanted)
            {
                if (bitmaps.ContainsKey(key))
                {
                    tileLastUsedFrame[key] = frame;
                }
            }

            canvas.Save();
            canvas.Translate(width / 2f, height / 2f);
            canvas.Scale(tileScale, tileScale);
            canvas.Translate(-virtualWidth / 2f, -virtualHeight / 2f);

            int missingTiles = 0;

            foreach (VisibleTile tile in tiles)
            {
                if (bitmaps.TryGetValue(tile.Key, out Bitmap? bitmap))
                {
                    canvas.DrawBitmap(bitmap, tile.ScreenX, tile.ScreenY, tilePaint);
                }
                else
                {
                    missingTiles++;
                    DrawFallbackTile(canvas, tile, frame);
                }
            }

            canvas.Restore();

            foreach (TileKey key in baseKeys)
            {
                RequestTile(key);
            }

            foreach (VisibleTile tile in tiles)
            {
                RequestTile(tile.Key);
            }

            foreach (VisibleTile tile in prefetchTiles)
            {
                RequestTile(tile.Key);
            }

            LogTileStatistics(tiles.Count, missingTiles, zoomLevel);
        }

        /// <summary>
        /// Writes a tile status summary to the log every few seconds.
        /// </summary>
        /// <param name="visibleTiles">Number of tiles covering the screen.</param>
        /// <param name="missingTiles">Number of tiles not in memory yet.</param>
        /// <param name="zoomLevel">Current zoom level.</param>
        private void LogTileStatistics(int visibleTiles, int missingTiles, double zoomLevel)
        {
            DateTime now = DateTime.UtcNow;

            if (now - lastTileStatisticsUtc < TileStatisticsInterval)
            {
                return;
            }

            lastTileStatisticsUtc = now;

            Log.Info(LogTag, string.Format(
                CultureInfo.InvariantCulture,
                "Kafelki: zoom {0:0.00}, widoczne {1}, brakujące {2}, w pamięci {3}, w toku {4}, zdekodowane {5}, błędy {6}, ostatni {7}",
                zoomLevel,
                visibleTiles,
                missingTiles,
                bitmaps.Count,
                loadingTiles.Count,
                Interlocked.Read(ref tilesDecodedCount),
                Interlocked.Read(ref tilesFailedCount),
                lastDecodedTileInfo));
        }

        /// <summary>
        /// Draws in place of a missing tile its enlarged part from a tile of a lower zoom level or, when there is none, reduced tiles of a higher level. Leaves the background when nothing is available.
        /// </summary>
        /// <param name="canvas">Surface canvas with the tile scale set.</param>
        /// <param name="tile">Missing tile with its position on the screen.</param>
        /// <param name="frame">Number of the current frame.</param>
        private void DrawFallbackTile(Canvas canvas, VisibleTile tile, long frame)
        {
            TileKey key = tile.Key;
            int tileSize = WebMercator.TileSize;

            for (int levels = 1; levels <= MaxFallbackLevels && key.Zoom - levels >= 0; levels++)
            {
                TileKey parentKey = new TileKey(key.Zoom - levels, key.X >> levels, key.Y >> levels);

                if (!bitmaps.TryGetValue(parentKey, out Bitmap? parent))
                {
                    continue;
                }

                int sourceSize = Math.Max(1, parent.Width >> levels);
                int sourceLeft = (key.X & ((1 << levels) - 1)) * sourceSize;
                int sourceTop = (key.Y & ((1 << levels) - 1)) * sourceSize;

                fallbackSource.Set(sourceLeft, sourceTop, sourceLeft + sourceSize, sourceTop + sourceSize);
                fallbackDestination.Set(tile.ScreenX, tile.ScreenY, tile.ScreenX + tileSize, tile.ScreenY + tileSize);
                canvas.DrawBitmap(parent, fallbackSource, fallbackDestination, tilePaint);
                tileLastUsedFrame[parentKey] = frame;

                return;
            }

            if (key.Zoom >= MaxTileZoom)
            {
                return;
            }

            int half = tileSize / 2;

            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    TileKey childKey = new TileKey(key.Zoom + 1, key.X * 2 + column, key.Y * 2 + row);

                    if (!bitmaps.TryGetValue(childKey, out Bitmap? child))
                    {
                        continue;
                    }

                    float left = tile.ScreenX + column * half;
                    float top = tile.ScreenY + row * half;

                    fallbackDestination.Set(left, top, left + half, top + half);
                    canvas.DrawBitmap(child, null, fallbackDestination, tilePaint);
                    tileLastUsedFrame[childKey] = frame;
                }
            }
        }

        /// <summary>
        /// Requests the download and decoding of a tile if it is not already in memory, in progress, or waiting for a retry.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        private void RequestTile(TileKey key)
        {
            if (bitmaps.ContainsKey(key))
            {
                return;
            }

            DateTime retryAfter;

            if (tileRetryAfterUtc.TryGetValue(key, out retryAfter) && retryAfter > DateTime.UtcNow)
            {
                return;
            }

            if (!loadingTiles.TryAdd(key, 0))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await tileGate.WaitAsync();

                try
                {
                    if (!Volatile.Read(ref wantedTiles).Contains(key))
                    {
                        return;
                    }

                    byte[]? data = await tileProvider.GetTileAsync(key, CancellationToken.None);
                    Bitmap? bitmap = data == null ? null : BitmapFactory.DecodeByteArray(data, 0, data.Length);

                    if (bitmap != null)
                    {
                        tileLastUsedFrame[key] = Interlocked.Read(ref frameCounter);
                        bitmaps[key] = bitmap;
                        tileRetryAfterUtc.TryRemove(key, out DateTime removedRetry);
                        Interlocked.Increment(ref tilesDecodedCount);
                        lastDecodedTileInfo = string.Format(CultureInfo.InvariantCulture, "{0}x{1} {2}", bitmap.Width, bitmap.Height, bitmap.GetConfig());
                        TrimBitmapCache();
                    }
                    else
                    {
                        if (data != null)
                        {
                            DevLogger.Warn(LogTag, "Dekodowanie pobranego kafelka nie powiodło się");
                        }

                        Interlocked.Increment(ref tilesFailedCount);
                        tileRetryAfterUtc[key] = DateTime.UtcNow + AppSettings.TileRetry;
                    }
                }
                catch (Exception exception)
                {
                    DevLogger.Error(LogTag, "Nieoczekiwany wyjątek przy wczytywaniu kafelka", exception);
                    Interlocked.Increment(ref tilesFailedCount);
                    tileRetryAfterUtc[key] = DateTime.UtcNow + AppSettings.TileRetry;
                }
                finally
                {
                    tileGate.Release();
                    loadingTiles.TryRemove(key, out byte removedMarker);
                }
            });
        }

        /// <summary>
        /// Removes the excess decoded tiles when the cache exceeds the allowed size.
        /// </summary>
        private void TrimBitmapCache()
        {
            int limit = Math.Max(AppSettings.MaxCachedTiles, Volatile.Read(ref lastVisibleTileCount) + TileCacheMargin);
            int excess = bitmaps.Count - limit;

            if (excess <= 0)
            {
                return;
            }

            long currentFrame = Interlocked.Read(ref frameCounter);

            List<TileKey> keysToRemove = bitmaps.Keys
                .Select(key => (Key: key, LastUsed: tileLastUsedFrame.TryGetValue(key, out long usedFrame) ? usedFrame : 0L))
                .Where(entry => entry.LastUsed < currentFrame)
                .OrderBy(entry => entry.LastUsed)
                .Take(excess)
                .Select(entry => entry.Key)
                .ToList();

            foreach (TileKey key in keysToRemove)
            {
                bitmaps.TryRemove(key, out Bitmap? removedBitmap);
                tileLastUsedFrame.TryRemove(key, out long removedFrame);
            }
        }
    }
}

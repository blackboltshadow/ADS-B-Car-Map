using AdsbCarMap.Services;
using System.Globalization;
using Android.Util;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Fetches map tiles from an XYZ server and keeps their raw data in memory and, optionally, on the disk.
    /// </summary>
    public sealed class TileProvider
    {
        /// <summary>
        /// OpenStreetMap tile URL template.
        /// </summary>
        public const string OpenStreetMapTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

        /// <summary>
        /// Attribution text required by the OpenStreetMap license.
        /// </summary>
        public const string OpenStreetMapAttribution = "© OpenStreetMap contributors";

        /// <summary>
        /// Tag used in the Android log.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Maximum number of tiles kept in memory.
        /// </summary>
        private const int MaxCachedTiles = 512;

        /// <summary>
        /// HTTP client used to download the tiles.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// URL template with the {z}, {x} and {y} placeholders.
        /// </summary>
        private readonly string urlTemplate;

        /// <summary>
        /// Tile cache on the disk, or null when the tiles are kept only in memory.
        /// </summary>
        private readonly TileDiskCache? diskCache;

        /// <summary>
        /// Returns the time a tile on the disk is considered fresh.
        /// </summary>
        private readonly Func<TimeSpan> maxAgeProvider;

        /// <summary>
        /// Lock protecting the cache.
        /// </summary>
        private readonly object cacheLock = new object();

        /// <summary>
        /// Nodes of the LRU list indexed by the tile identifier.
        /// </summary>
        private readonly Dictionary<TileKey, LinkedListNode<KeyValuePair<TileKey, byte[]>>> cache = new Dictionary<TileKey, LinkedListNode<KeyValuePair<TileKey, byte[]>>>();

        /// <summary>
        /// Order in which the tiles were used, the most recently used first.
        /// </summary>
        private readonly LinkedList<KeyValuePair<TileKey, byte[]>> usageOrder = new LinkedList<KeyValuePair<TileKey, byte[]>>();

        /// <summary>
        /// Creates a tile provider.
        /// </summary>
        /// <param name="httpClient">HTTP client with the User-Agent header.</param>
        /// <param name="urlTemplate">URL template with the {z}, {x} and {y} placeholders.</param>
        /// <param name="tileSize">Side of a tile in pixels.</param>
        /// <param name="diskCache">Tile cache on the disk, or null for memory only.</param>
        /// <param name="maxAgeProvider">Returns the time a tile on the disk is considered fresh; required when the disk cache is used.</param>
        public TileProvider(HttpClient httpClient, string urlTemplate, int tileSize, TileDiskCache? diskCache = null, Func<TimeSpan>? maxAgeProvider = null)
        {
            this.httpClient = httpClient;
            this.urlTemplate = urlTemplate;
            this.diskCache = diskCache;
            this.maxAgeProvider = maxAgeProvider ?? (() => TimeSpan.Zero);
            TileSize = tileSize;
        }

        /// <summary>
        /// Side of a tile in pixels.
        /// </summary>
        public int TileSize { get; }

        /// <summary>
        /// Returns the tile data from memory, from a fresh file on the disk or from the server. When the download fails, an outdated file from the disk is returned instead. Does not throw network exceptions.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Encoded tile image, or null when the tile is available nowhere.</returns>
        public async Task<byte[]?> GetTileAsync(TileKey key, CancellationToken cancellationToken)
        {
            byte[]? cached = TryGetCached(key);

            if (cached != null)
            {
                return cached;
            }

            byte[]? stale = null;

            if (diskCache != null)
            {
                DateTime writtenUtc = DateTime.MinValue;
                byte[]? fromDisk = await Task.Run(() => diskCache.TryRead(key, out writtenUtc), cancellationToken);

                if (fromDisk != null)
                {
                    if (DateTime.UtcNow - writtenUtc < maxAgeProvider())
                    {
                        AddToCache(key, fromDisk);

                        return fromDisk;
                    }

                    stale = fromDisk;
                }
            }

            string url = urlTemplate
                .Replace("{z}", key.Zoom.ToString(CultureInfo.InvariantCulture))
                .Replace("{x}", key.X.ToString(CultureInfo.InvariantCulture))
                .Replace("{y}", key.Y.ToString(CultureInfo.InvariantCulture));

            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    DevLogger.Warn(LogTag, "Serwer kafelków zwrócił kod " + (int)response.StatusCode);
                    return stale;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                AddToCache(key, data);

                if (diskCache != null)
                {
                    _ = Task.Run(() => diskCache.Write(key, data));
                }

                return data;
            }
            catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                DevLogger.Warn(LogTag, "Pobieranie kafelka nie powiodło się (" + exception.GetType().Name + "): " + exception.Message);
                return stale;
            }
        }

        /// <summary>
        /// Returns a tile from the cache and marks it as most recently used.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <returns>Tile data, or null when it is not in the cache.</returns>
        private byte[]? TryGetCached(TileKey key)
        {
            lock (cacheLock)
            {
                if (!cache.TryGetValue(key, out LinkedListNode<KeyValuePair<TileKey, byte[]>>? node))
                {
                    return null;
                }

                usageOrder.Remove(node);
                usageOrder.AddFirst(node);

                return node.Value.Value;
            }
        }

        /// <summary>
        /// Adds a tile to the cache and removes the least recently used ones when the limit is exceeded.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <param name="data">Tile data.</param>
        private void AddToCache(TileKey key, byte[] data)
        {
            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out LinkedListNode<KeyValuePair<TileKey, byte[]>>? existing))
                {
                    usageOrder.Remove(existing);
                }

                LinkedListNode<KeyValuePair<TileKey, byte[]>> node = usageOrder.AddFirst(new KeyValuePair<TileKey, byte[]>(key, data));
                cache[key] = node;

                while (cache.Count > MaxCachedTiles)
                {
                    LinkedListNode<KeyValuePair<TileKey, byte[]>>? oldest = usageOrder.Last;

                    if (oldest == null)
                    {
                        break;
                    }

                    usageOrder.RemoveLast();
                    cache.Remove(oldest.Value.Key);
                }
            }
        }
    }
}

using System.Globalization;
using Android.Util;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Pobiera kafelki mapy z serwera XYZ i przechowuje ich surowe dane w pamięci.
    /// </summary>
    public sealed class TileProvider
    {
        /// <summary>
        /// Szablon adresu kafelków OpenStreetMap.
        /// </summary>
        public const string OpenStreetMapTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

        /// <summary>
        /// Tekst atrybucji wymagany przez licencję OpenStreetMap.
        /// </summary>
        public const string OpenStreetMapAttribution = "© OpenStreetMap contributors";

        /// <summary>
        /// Tag używany w logach Androida.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Maksymalna liczba kafelków przechowywanych w pamięci.
        /// </summary>
        private const int MaxCachedTiles = 512;

        /// <summary>
        /// Klient HTTP używany do pobierania kafelków.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Szablon adresu z miejscami {z}, {x} i {y}.
        /// </summary>
        private readonly string urlTemplate;

        /// <summary>
        /// Blokada chroniąca cache.
        /// </summary>
        private readonly object cacheLock = new object();

        /// <summary>
        /// Węzły listy LRU indeksowane identyfikatorem kafelka.
        /// </summary>
        private readonly Dictionary<TileKey, LinkedListNode<KeyValuePair<TileKey, byte[]>>> cache = new Dictionary<TileKey, LinkedListNode<KeyValuePair<TileKey, byte[]>>>();

        /// <summary>
        /// Kolejność użycia kafelków, od ostatnio użytego na początku.
        /// </summary>
        private readonly LinkedList<KeyValuePair<TileKey, byte[]>> usageOrder = new LinkedList<KeyValuePair<TileKey, byte[]>>();

        /// <summary>
        /// Tworzy dostawcę kafelków.
        /// </summary>
        /// <param name="httpClient">Klient HTTP z nagłówkiem User-Agent.</param>
        /// <param name="urlTemplate">Szablon adresu z miejscami {z}, {x} i {y}.</param>
        /// <param name="tileSize">Bok kafelka w pikselach.</param>
        public TileProvider(HttpClient httpClient, string urlTemplate, int tileSize)
        {
            this.httpClient = httpClient;
            this.urlTemplate = urlTemplate;
            TileSize = tileSize;
        }

        /// <summary>
        /// Bok kafelka w pikselach.
        /// </summary>
        public int TileSize { get; }

        /// <summary>
        /// Zwraca dane kafelka z cache lub z serwera. Nie zgłasza wyjątków sieciowych.
        /// </summary>
        /// <param name="key">Identyfikator kafelka.</param>
        /// <param name="cancellationToken">Token anulowania.</param>
        /// <returns>Zakodowany obraz kafelka lub null, gdy pobranie się nie powiodło.</returns>
        public async Task<byte[]?> GetTileAsync(TileKey key, CancellationToken cancellationToken)
        {
            byte[]? cached = TryGetCached(key);

            if (cached != null)
            {
                return cached;
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
                    Log.Warn(LogTag, "Kafelek " + url + " zwrócił kod " + (int)response.StatusCode);
                    return null;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                AddToCache(key, data);

                return data;
            }
            catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                Log.Warn(LogTag, "Pobieranie kafelka " + url + " nie powiodło się: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Zwraca kafelek z cache i oznacza go jako ostatnio użyty.
        /// </summary>
        /// <param name="key">Identyfikator kafelka.</param>
        /// <returns>Dane kafelka lub null, gdy nie ma go w cache.</returns>
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
        /// Dodaje kafelek do cache i usuwa najdawniej użyte, gdy przekroczono limit.
        /// </summary>
        /// <param name="key">Identyfikator kafelka.</param>
        /// <param name="data">Dane kafelka.</param>
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

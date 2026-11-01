using AdsbCarMap.Data;
using AdsbCarMap.Map;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Long-lived objects shared by the Android Auto screens and the phone screen.
    /// </summary>
    public static class AppServices
    {
        /// <summary>
        /// Value of the User-Agent header sent to the data and tile servers. Contains the contact from the user's adsbcarmap-contact.props file, if it exists. The field must be declared before the HTTP clients.
        /// </summary>
        public static readonly string UserAgent = string.IsNullOrEmpty(BuildInfo.Contact)
            ? "AdsbCarMap/" + BuildInfo.Version
            : "AdsbCarMap/" + BuildInfo.Version + " (kontakt: " + BuildInfo.Contact + ")";

        /// <summary>
        /// HTTP client used for the aircraft requests.
        /// </summary>
        private static readonly HttpClient AdsbHttpClient = CreateHttpClient(AdsbClient.DefaultBaseAddress);

        /// <summary>
        /// HTTP client used to download the map tiles.
        /// </summary>
        private static readonly HttpClient TileHttpClient = CreateHttpClient(null);

        /// <summary>
        /// HTTP client used for the flight route requests.
        /// </summary>
        private static readonly HttpClient RouteHttpClient = CreateHttpClient(RouteClient.DefaultBaseAddress);

        /// <summary>
        /// ADS-B API client.
        /// </summary>
        public static AdsbClient Adsb { get; } = new AdsbClient(AdsbHttpClient);

        /// <summary>
        /// Cache of the flight routes downloaded from adsbdb.com.
        /// </summary>
        public static RouteCache Routes { get; } = new RouteCache(new RouteClient(RouteHttpClient));

        /// <summary>
        /// OpenStreetMap tile provider with an in-memory cache.
        /// </summary>
        public static TileProvider Tiles { get; } = new TileProvider(TileHttpClient, TileProvider.OpenStreetMapTemplate, 256);

        /// <summary>
        /// Aircraft selection and filters shared by the phone map and the Android Auto map.
        /// </summary>
        public static SelectionState Selection { get; } = new SelectionState();

        /// <summary>
        /// Airports and runways bundled with the application, loaded in the background at the first use.
        /// </summary>
        public static AirportDatabase Airports { get; } = CreateAirports();

        /// <summary>
        /// Favorite airports, shared by the phone and Android Auto.
        /// </summary>
        public static FavoriteAirports Favorites { get; } = new FavoriteAirports();

        /// <summary>
        /// Creates the airport database and starts loading it in the background.
        /// </summary>
        /// <returns>The database, not yet loaded.</returns>
        private static AirportDatabase CreateAirports()
        {
            AirportDatabase database = new AirportDatabase();
            database.StartLoading();

            return database;
        }

        /// <summary>
        /// Creates an HTTP client with a timeout and the User-Agent header.
        /// </summary>
        /// <param name="baseAddress">Base address, or null when not needed.</param>
        /// <returns>Configured HTTP client.</returns>
        private static HttpClient CreateHttpClient(string? baseAddress)
        {
            HttpClient client = new HttpClient();

            if (baseAddress != null)
            {
                client.BaseAddress = new Uri(baseAddress);
            }

            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            return client;
        }
    }
}

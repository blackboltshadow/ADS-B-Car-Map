using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Long-lived objects shared by the Android Auto screens and the phone screen. The sources of the flight data are put together by <see cref="DataComposition"/>, which is created at the first use, so the order of the static members below does not matter for it. The remaining static members are initialized in the order of their declaration, so each must be declared after the members that it uses.
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
        /// HTTP client used to download the map tiles.
        /// </summary>
        private static readonly HttpClient TileHttpClient = CreateHttpClient(null);

        /// <summary>
        /// The sources of the flight data with their limits, fallbacks and memory, created at the first use.
        /// </summary>
        private static readonly Lazy<DataComposition> Composition = new Lazy<DataComposition>(CreateComposition);

        /// <summary>
        /// Cache of the flight routes downloaded from the route sources, created at the first use.
        /// </summary>
        private static readonly Lazy<RouteCache> RouteCacheInstance = new Lazy<RouteCache>(() => new RouteCache(Composition.Value.RouteSources, Composition.Value.RouteMemory));

        /// <summary>
        /// Aircraft source used by the maps: the sources in the order chosen in the settings, the next one asked when the previous one gives no data.
        /// </summary>
        public static IAircraftSource Adsb => Composition.Value.AircraftSources;

        /// <summary>
        /// Completes the type, the full type name and the registration of the selected aircraft from the other sources.
        /// </summary>
        public static AircraftEnricher Enricher => Composition.Value.Enricher;

        /// <summary>
        /// Keeps the memory of the flight information in line with the visible aircraft: marks the visible flights as seen and closes those that have landed.
        /// </summary>
        public static FlightCacheKeeper FlightKeeper => Composition.Value.Keeper;

        /// <summary>
        /// Cache of the flight routes downloaded from the route sources.
        /// </summary>
        public static RouteCache Routes => RouteCacheInstance.Value;

        /// <summary>
        /// Map tiles stored on the disk, in the cache directory of the application.
        /// </summary>
        public static TileDiskCache TileDisk { get; } = new TileDiskCache(System.IO.Path.Combine(Android.App.Application.Context.CacheDir!.AbsolutePath, "tiles"), () => AppSettings.TileCacheMaxBytes);

        /// <summary>
        /// OpenStreetMap tile provider with a cache in memory and on the disk.
        /// </summary>
        public static TileProvider Tiles { get; } = new TileProvider(TileHttpClient, TileProvider.OpenStreetMapTemplate, 256, TileDisk, () => AppSettings.TileCacheMaxAge);

        /// <summary>
        /// Aircraft selection and filters shared by the phone map and the Android Auto map.
        /// </summary>
        public static SelectionState Selection { get; } = new SelectionState();

        /// <summary>
        /// Airports and runways bundled with the application, loaded in the background at the first use.
        /// </summary>
        public static AirportDatabase Airports { get; } = CreateAirports();

        /// <summary>
        /// Table of the aircraft types bundled with the application, loaded when the services are created and used by the filters.
        /// </summary>
        public static AircraftTypeTable AircraftTypes { get; } = LoadAircraftTypes();

        /// <summary>
        /// Log of the aircraft types seen in the traffic that the type table does not describe.
        /// </summary>
        public static MissingTypeStore MissingTypes { get; } = new MissingTypeStore();

        /// <summary>
        /// Favorite airports, shared by the phone and Android Auto.
        /// </summary>
        public static FavoriteAirports Favorites { get; } = new FavoriteAirports();

        /// <summary>
        /// Predefined and user-made aircraft filters with the set of switched on ones, shared by the phone and Android Auto.
        /// </summary>
        public static AircraftFilterStore Filters { get; } = new AircraftFilterStore();

        /// <summary>
        /// Number of flights and aircraft whose information is kept in the memory, including the answers that nothing is known.
        /// </summary>
        public static int FlightInfoCount => Composition.Value.FlightInfoCount;

        /// <summary>
        /// Forgets all stored information about flights and aircraft, also in the files.
        /// </summary>
        public static void ClearFlightInfo()
        {
            Composition.Value.ClearFlightInfo();
        }

        /// <summary>
        /// Puts together the sources of the flight data from the settings, the airports and the cache directory of the application.
        /// </summary>
        /// <returns>The composition.</returns>
        private static DataComposition CreateComposition()
        {
            DataCompositionSettings settings = new DataCompositionSettings(
                () => AppSettings.DataSourceOrder,
                () => AppSettings.RouteSourceOrder,
                () => AppSettings.DataSourceFallback,
                () => AppSettings.DataSourceDetails,
                () => AppSettings.FlightInfoMaxAge);

            return new DataComposition(System.IO.Path.Combine(Android.App.Application.Context.CacheDir!.AbsolutePath, "flights"), address => CreateHttpClient(address), Airports, settings);
        }

        /// <summary>
        /// Reads the bundled aircraft type table and makes it the one used by the filters.
        /// </summary>
        /// <returns>The table in use; empty when the bundled file could not be read.</returns>
        private static AircraftTypeTable LoadAircraftTypes()
        {
            AircraftTypeTable.LoadBundled();

            return AircraftTypeTable.Current;
        }

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

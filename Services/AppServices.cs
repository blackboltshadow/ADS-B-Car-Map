using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Adsbdb;
using AdsbCarMap.DataSources.AdsbLol;
using AdsbCarMap.DataSources.AirplanesLive;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Long-lived objects shared by the Android Auto screens and the phone screen. Static members are initialized in the order of their declaration, so every member must be declared after the members that it uses; otherwise it silently receives null.
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
        /// HTTP client used for the adsb.lol aircraft requests.
        /// </summary>
        private static readonly HttpClient AdsbHttpClient = CreateHttpClient(AdsbLolClient.DefaultBaseAddress);

        /// <summary>
        /// HTTP client used for the airplanes.live aircraft requests.
        /// </summary>
        private static readonly HttpClient AirplanesLiveHttpClient = CreateHttpClient(AirplanesLiveClient.DefaultBaseAddress);

        /// <summary>
        /// HTTP client used to download the map tiles.
        /// </summary>
        private static readonly HttpClient TileHttpClient = CreateHttpClient(null);

        /// <summary>
        /// HTTP client used for the flight route requests.
        /// </summary>
        private static readonly HttpClient RouteHttpClient = CreateHttpClient(AdsbdbRouteClient.DefaultBaseAddress);

        /// <summary>
        /// Time after which the answer that a source does not know a flight is asked about again, at most.
        /// </summary>
        private static readonly TimeSpan RouteNotFoundMaxAge = TimeSpan.FromHours(6);

        /// <summary>
        /// Time after which the answer that no source knows more about an aircraft is asked about again, at most.
        /// </summary>
        private static readonly TimeSpan AircraftNotFoundMaxAge = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Delay between a change of the flight information and writing it to the file.
        /// </summary>
        private static readonly TimeSpan FlightInfoSaveDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Routes by callsign, kept for the time chosen in the settings counted from the last sighting, and in a file in the cache directory.
        /// </summary>
        private static readonly TimedCache<FlightRoute> RouteMemory = CreateFlightInfoCache<FlightRoute>("routes.json", RouteNotFoundMaxAge);

        /// <summary>
        /// Type, registration and category found by the ICAO address, kept for the time chosen in the settings and in a file in the cache directory.
        /// </summary>
        private static readonly TimedCache<AircraftDetails> AircraftMemory = CreateFlightInfoCache<AircraftDetails>("aircraft-details.json", AircraftNotFoundMaxAge);

        /// <summary>
        /// Aircraft sources queried in the order chosen in the settings; the next source is asked when the previous one gives no data (if enabled in the settings).
        /// </summary>
        private static readonly FallbackAircraftSource AircraftSources = new FallbackAircraftSource(
            new List<IRateLimitedAircraftSource> { new RateLimitedAircraftSource(new AdsbLolClient(AdsbHttpClient)), new RateLimitedAircraftSource(new AirplanesLiveClient(AirplanesLiveHttpClient)) },
            () => AppSettings.DataSourceOrder,
            () => AppSettings.DataSourceFallback);

        /// <summary>
        /// Aircraft source used by the maps.
        /// </summary>
        public static IAircraftSource Adsb => AircraftSources;

        /// <summary>
        /// Completes the type, the registration and the category of the selected aircraft from the other sources when the main source lacks them.
        /// </summary>
        public static AircraftEnricher Enricher { get; } = new AircraftEnricher(AircraftSources, () => AppSettings.DataSourceDetails, AircraftMemory);

        /// <summary>
        /// Cache of the flight routes downloaded from adsbdb.com.
        /// </summary>
        public static RouteCache Routes { get; } = new RouteCache(new AdsbdbRouteClient(RouteHttpClient), RouteMemory);

        /// <summary>
        /// Keeps the memory of the flight information in line with the visible aircraft: marks the visible flights as seen and closes those that have landed.
        /// </summary>
        public static FlightCacheKeeper FlightKeeper { get; } = new FlightCacheKeeper(RouteMemory, AircraftMemory);

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
        /// Number of flights and aircraft whose information is kept in the memory, including the answers that nothing is known.
        /// </summary>
        public static int FlightInfoCount => RouteMemory.Count + AircraftMemory.Count;

        /// <summary>
        /// Forgets all stored information about flights and aircraft, also in the files.
        /// </summary>
        public static void ClearFlightInfo()
        {
            RouteMemory.Clear();
            AircraftMemory.Clear();
            RouteMemory.Flush();
            AircraftMemory.Flush();
        }

        /// <summary>
        /// Creates a memory of flight information kept for the time chosen in the settings and in a file in the cache directory of the application.
        /// </summary>
        /// <typeparam name="T">Kind of the stored information.</typeparam>
        /// <param name="fileName">Name of the file in the directory "flights" of the cache.</param>
        /// <param name="notFoundMaxAge">Time after which the answer that nothing is known is asked about again, at most.</param>
        /// <returns>The memory with the stored items already loaded.</returns>
        private static TimedCache<T> CreateFlightInfoCache<T>(string fileName, TimeSpan notFoundMaxAge) where T : class
        {
            TimedCache<T> cache = new TimedCache<T>(() => AppSettings.FlightInfoMaxAge, notFoundMaxAge);
            string directory = System.IO.Path.Combine(Android.App.Application.Context.CacheDir!.AbsolutePath, "flights");

            try
            {
                System.IO.Directory.CreateDirectory(directory);
                cache.AttachFile(System.IO.Path.Combine(directory, fileName), FlightInfoSaveDelay);
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
            }

            return cache;
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

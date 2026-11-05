using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Neptun;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Pansa;
using AdsbCarMap.DataSources.Rso;
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
        /// Requests sent to external servers by every HTTP client of the application, shown on the request log screen. Declared before the HTTP clients, which use it.
        /// </summary>
        public static RequestLog Requests { get; } = new RequestLog();

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
        private static readonly Lazy<RouteCache> RouteCacheInstance = new Lazy<RouteCache>(() => new RouteCache(Composition.Value.RouteSources, Composition.Value.RouteMemory, exception => DevLogger.Warn("AdsbCarMap", "Pobieranie trasy lotu nie powiodło się", exception)));

        /// <summary>
        /// Feed of the airborne threats from NEPTUN, created at the first use. It does nothing until it is started, which only the phone map does, and only while the threat layer is switched on.
        /// </summary>
        private static readonly Lazy<IThreatFeed> ThreatFeedInstance = new Lazy<IThreatFeed>(CreateThreatFeed);

        /// <summary>
        /// Feed of the airspace areas from PAŻP, created at the first use. It does nothing until it is started, which only the phone map does, and only while the airspace layer is switched on.
        /// </summary>
        private static readonly Lazy<PollingFeed<AirspaceZone>> AirspaceFeedInstance = new Lazy<PollingFeed<AirspaceZone>>(CreateAirspaceFeed);

        /// <summary>
        /// Choice of the kinds and states of the airspace areas that the layer shows, read from the device at the first use.
        /// </summary>
        private static readonly Lazy<AirspaceConfigStore> AirspaceConfigInstance = new Lazy<AirspaceConfigStore>(() => new AirspaceConfigStore());

        /// <summary>
        /// Feed of the official notices of the regional warning system, created at the first use. It does nothing until it is started, which only the screen of the notices does.
        /// </summary>
        private static readonly Lazy<PollingFeed<Warning>> WarningFeedInstance = new Lazy<PollingFeed<Warning>>(CreateWarningFeed);

        /// <summary>
        /// Choice of the categories and voivodeships of the notices, read from the device at the first use.
        /// </summary>
        private static readonly Lazy<WarningConfigStore> WarningConfigInstance = new Lazy<WarningConfigStore>(() => new WarningConfigStore());

        /// <summary>
        /// Aircraft source used by the maps: the sources in the order chosen in the settings, the next one asked when the previous one gives no data.
        /// </summary>
        public static IAircraftSource Adsb => Composition.Value.PositionSource;

        /// <summary>
        /// Feed of the airborne threats from NEPTUN, used by the layer on the phone map.
        /// </summary>
        public static IThreatFeed Threats => ThreatFeedInstance.Value;

        /// <summary>
        /// Feed of the airspace areas from PAŻP, used by the layer on the phone map.
        /// </summary>
        public static PollingFeed<AirspaceZone> Airspace => AirspaceFeedInstance.Value;

        /// <summary>
        /// Choice of the kinds and states of the airspace areas that the layer shows.
        /// </summary>
        public static AirspaceConfigStore AirspaceConfig => AirspaceConfigInstance.Value;

        /// <summary>
        /// Feed of the official notices of the regional warning system, used by the screen of the notices.
        /// </summary>
        public static PollingFeed<Warning> Warnings => WarningFeedInstance.Value;

        /// <summary>
        /// Choice of the categories and voivodeships of the notices.
        /// </summary>
        public static WarningConfigStore WarningConfig => WarningConfigInstance.Value;

        /// <summary>
        /// Completes the type, the full type name and the registration of the selected aircraft from the other sources.
        /// </summary>
        public static AircraftEnricher Enricher => Composition.Value.Enricher;

        /// <summary>
        /// Keeps the memory of the flight information in line with the visible aircraft: marks the visible flights as seen and closes those that have landed.
        /// </summary>
        public static FlightCacheKeeper FlightKeeper => Composition.Value.Keeper;

        /// <summary>
        /// Full recorded paths of the selected flights, which extend the trail collected on the phone back to the takeoff.
        /// </summary>
        public static FlightTrackStore FlightTracks => Composition.Value.Tracks;

        /// <summary>
        /// Weather reports of airports, downloaded again after the time chosen in the settings.
        /// </summary>
        public static WeatherStore Weather => Composition.Value.Weather;

        /// <summary>
        /// Forecasts (TAF) of airports, downloaded again after the time chosen in the settings.
        /// </summary>
        public static ForecastStore Forecast => Composition.Value.Forecast;

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
        /// Alert rules with the global switch of alerts.
        /// </summary>
        public static AlertRuleStore Alerts { get; } = new AlertRuleStore();

        /// <summary>
        /// Number of flights and aircraft whose information is kept in the memory, including the answers that nothing is known.
        /// </summary>
        public static int FlightInfoCount => Composition.Value.FlightInfoCount;

        /// <summary>
        /// Reads the state of the aircraft and route sources without sending any request.
        /// </summary>
        /// <returns>The states of the sources.</returns>
        public static IReadOnlyList<SourceState> GetSourceStates()
        {
            return Composition.Value.GetSourceStates();
        }

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
                () => AppSettings.FlightInfoMaxAge,
                ReportSourceEvent,
                () => AppSettings.PrivacyOptions,
                DevConsoleState.RecordSent,
                () => AppSettings.AirportWeatherMaxAge,
                () => AppSettings.AirportForecastMaxAge);

            return new DataComposition(System.IO.Path.Combine(Android.App.Application.Context.CacheDir!.AbsolutePath, "flights"), address => CreateHttpClient(address), Airports, settings);
        }

        /// <summary>
        /// Creates the feed of the threats: the download client and the connection to the stream use the same User-Agent as the other servers, and the transport is read from the settings at every step.
        /// </summary>
        /// <returns>The feed, stopped.</returns>
        private static IThreatFeed CreateThreatFeed()
        {
            return new NeptunThreatFeed(
                new NeptunRestClient(CreateHttpClient(NeptunRestClient.DefaultBaseAddress)),
                () => new WebSocketThreatConnection(new Uri(NeptunThreatFeed.DefaultStreamAddress), UserAgent),
                GetThreatTransport,
                (string message, Exception? exception) => DevLogger.Warn("Threats", message, exception));
        }

        /// <summary>
        /// Creates the feed of the airspace areas. The map is bigger than a typical answer of the other servers, so the client waits longer for it.
        /// </summary>
        /// <returns>The feed, stopped.</returns>
        private static PollingFeed<AirspaceZone> CreateAirspaceFeed()
        {
            HttpClient client = CreateHttpClient(PansaClient.DefaultBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);

            PansaClient source = new PansaClient(client);

            return new PollingFeed<AirspaceZone>(
                source.Name,
                source.GetZonesAsync,
                () => DateTime.UtcNow,
                (TimeSpan time, CancellationToken token) => Task.Delay(time, token),
                () => TimeSpan.FromMinutes(AppSettings.AirspaceRefreshMinutes),
                ReportSourceEvent);
        }

        /// <summary>
        /// Creates the feed of the official notices. What it downloads follows the choice of the categories and voivodeships at every download.
        /// </summary>
        /// <returns>The feed, stopped.</returns>
        private static PollingFeed<Warning> CreateWarningFeed()
        {
            HttpClient client = CreateHttpClient(RsoClient.DefaultBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(20);
            RsoClient source = new RsoClient(client);

            PollingFeed<Warning> feed = new PollingFeed<Warning>(
                source.Name,
                (CancellationToken token) =>
                {
                    WarningFilterConfig config = WarningConfig.Config;

                    return source.GetWarningsAsync(config.ShownCategories, config.Provinces, token);
                },
                () => DateTime.UtcNow,
                (TimeSpan time, CancellationToken token) => Task.Delay(time, token),
                () => TimeSpan.FromMinutes(AppSettings.WarningsRefreshMinutes),
                ReportSourceEvent);

            feed.Changed += () =>
            {
                if (feed.State == PollingFeedState.Ready)
                {
                    _ = Task.Run(WarningAlertMonitor.Check);
                }
            };

            return feed;
        }

        /// <summary>
        /// Converts the setting of the way of obtaining the threats to the transport of the feed.
        /// </summary>
        /// <returns>The transport.</returns>
        private static ThreatTransport GetThreatTransport()
        {
            switch (AppSettings.ThreatTransportIndex)
            {
                case 1:
                    return ThreatTransport.WebSocketOnly;
                case 2:
                    return ThreatTransport.RestOnly;
                default:
                    return ThreatTransport.WebSocketWithFallback;
            }
        }

        /// <summary>
        /// Passes an event of the data sources to the developer log.
        /// </summary>
        /// <param name="kind">Kind of the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        private static void ReportSourceEvent(SourceEventKind kind, string message, Exception? exception)
        {
            if (kind == SourceEventKind.Information)
            {
                DevLogger.Info("DataSources", message);
            }
            else
            {
                DevLogger.Warn("DataSources", message, exception);
            }
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
            HttpClient client = new HttpClient(new RequestLoggingHandler(Requests, new HttpClientHandler()));

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

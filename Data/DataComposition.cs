using AdsbCarMap.DataSources.Adsbdb;
using AdsbCarMap.DataSources.AdsbLol;
using AdsbCarMap.DataSources.AdsbFi;
using AdsbCarMap.DataSources.AirplanesLive;
using AdsbCarMap.DataSources.AviationWeather;
using AdsbCarMap.DataSources.Hexdb;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Puts together everything that fetches and remembers the data of flights: the aircraft and route sources with their limits and fallbacks, the memory of the flight information and the completing of the selected aircraft. Everything is created in one constructor in a fixed order, from parts that are given from outside, so a part cannot silently stay null and the whole can be created and tested without Android.
    /// </summary>
    public sealed class DataComposition
    {
        /// <summary>
        /// Time after which the answer that a source does not know a flight is asked about again, at most.
        /// </summary>
        public static readonly TimeSpan RouteNotFoundMaxAge = TimeSpan.FromHours(6);

        /// <summary>
        /// Time after which the answer that no source knows more about an aircraft is asked about again, at most.
        /// </summary>
        public static readonly TimeSpan AircraftNotFoundMaxAge = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Delay between a change of the flight information and writing it to the file.
        /// </summary>
        public static readonly TimeSpan FlightInfoSaveDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Minimum time between two downloads of the full path of the same flying aircraft.
        /// </summary>
        public static readonly TimeSpan FlightTrackRefreshInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Time after which the weather report of an airport is downloaded again when the settings do not say.
        /// </summary>
        public static readonly TimeSpan DefaultWeatherMaxAge = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Time after which the forecast of an airport is downloaded again when the settings do not say.
        /// </summary>
        public static readonly TimeSpan DefaultForecastMaxAge = TimeSpan.FromHours(1);

        /// <summary>
        /// Source of the weather reports and the forecasts, whose pause after limit responses is shown among the states of the sources.
        /// </summary>
        private readonly IRateLimitedWeatherSource weatherSource;

        /// <summary>
        /// Creates everything.
        /// </summary>
        /// <param name="flightCacheDirectory">Directory in which the memory of the flight information is kept in files.</param>
        /// <param name="createHttpClient">Creates an HTTP client for a base address.</param>
        /// <param name="airports">Airports used to complete the routes that are only airport codes.</param>
        /// <param name="settings">Settings read at every request.</param>
        public DataComposition(string flightCacheDirectory, Func<string, HttpClient> createHttpClient, AirportDatabase airports, DataCompositionSettings settings)
        {
            ArgumentException.ThrowIfNullOrEmpty(flightCacheDirectory);
            ArgumentNullException.ThrowIfNull(createHttpClient);
            ArgumentNullException.ThrowIfNull(airports);
            ArgumentNullException.ThrowIfNull(settings);

            HttpClient adsbLolHttp = createHttpClient(AdsbLolClient.DefaultBaseAddress);
            HttpClient airplanesLiveHttp = createHttpClient(AirplanesLiveClient.DefaultBaseAddress);
            HttpClient adsbFiHttp = createHttpClient(AdsbFiClient.DefaultBaseAddress);
            HttpClient adsbdbHttp = createHttpClient(AdsbdbClient.DefaultBaseAddress);
            HttpClient hexdbHttp = createHttpClient(HexdbClient.DefaultBaseAddress);

            RouteMemory = CreateMemory<FlightRoute>(flightCacheDirectory, "routes.json", RouteNotFoundMaxAge, settings.FlightInfoMaxAge);
            AircraftMemory = CreateMemory<AircraftDetails>(flightCacheDirectory, "aircraft-details.json", AircraftNotFoundMaxAge, settings.FlightInfoMaxAge);

            AircraftSources = new FallbackAircraftSource(
                new List<IRateLimitedAircraftSource> { new RateLimitedAircraftSource(new AdsbLolClient(adsbLolHttp)), new RateLimitedAircraftSource(new AirplanesLiveClient(airplanesLiveHttp)), new RateLimitedAircraftSource(new AdsbFiClient(adsbFiHttp)) },
                settings.AircraftOrder,
                settings.FallbackEnabled,
                settings.Report);

            PositionSource = new PrivacyAircraftSource(AircraftSources, settings.Privacy ?? (() => PrivacyOptions.Exact), settings.Report, settings.AreaSent);

            RateLimitedRouteSource adsbdb = new RateLimitedRouteSource(new AdsbdbClient(adsbdbHttp));
            RateLimitedRouteSource hexdb = new RateLimitedRouteSource(new AirportCompletingRouteSource(new HexdbClient(hexdbHttp), airports));

            RouteSources = new FallbackRouteSource(
                new List<IRateLimitedRouteSource> { adsbdb, new RateLimitedRouteSource(new AdsbLolRouteClient(adsbLolHttp)), hexdb },
                settings.RouteOrder,
                settings.FallbackEnabled,
                settings.Report);

            Enricher = new AircraftEnricher(AircraftSources, new List<IRateLimitedSource> { adsbdb, hexdb }, settings.DetailsEnabled, AircraftMemory);
            Keeper = new FlightCacheKeeper(RouteMemory, AircraftMemory);
            RateLimitedWeatherSource weatherSource = new RateLimitedWeatherSource(new AviationWeatherClient(createHttpClient(AviationWeatherClient.DefaultBaseAddress)));
            this.weatherSource = weatherSource;

            Weather = new WeatherStore(
                weatherSource,
                CreateMemory<WeatherEntry>(flightCacheDirectory, "weather.json", WeatherStore.RetentionAge, () => WeatherStore.RetentionAge),
                settings.WeatherMaxAge ?? (() => DefaultWeatherMaxAge),
                settings.Report);
            Forecast = new ForecastStore(
                weatherSource,
                CreateMemory<ForecastEntry>(flightCacheDirectory, "forecast.json", ForecastStore.RetentionAge, () => ForecastStore.RetentionAge),
                settings.ForecastMaxAge ?? (() => DefaultForecastMaxAge),
                settings.Report);
            Tracks = new FlightTrackStore(new RateLimitedTrackSource(new AdsbLolTrackClient(createHttpClient(AdsbLolTrackClient.DefaultBaseAddress))), FlightTrackRefreshInterval, null, Path.Combine(flightCacheDirectory, "tracks.json"), (message, exception) => settings.Report?.Invoke(SourceEventKind.Failure, message, exception));
        }

        /// <summary>
        /// Aircraft position sources queried in the order chosen in the settings; the next source is asked when the previous one gives no data (if enabled in the settings).
        /// </summary>
        public FallbackAircraftSource AircraftSources { get; }

        /// <summary>
        /// The aircraft sources as the maps query them: the position of every query is rounded as the privacy settings say. Queries by the ICAO address carry no position and go straight to <see cref="AircraftSources"/>.
        /// </summary>
        public IAircraftSource PositionSource { get; }

        /// <summary>
        /// Flight route sources queried in the order chosen in the settings; the next source is asked when the previous one does not know the flight or gives no answer (if enabled in the settings).
        /// </summary>
        public FallbackRouteSource RouteSources { get; }

        /// <summary>
        /// Routes by callsign, kept for the time chosen in the settings counted from the last sighting, and in a file.
        /// </summary>
        public TimedCache<FlightRoute> RouteMemory { get; }

        /// <summary>
        /// Data of the aircraft found by the ICAO address, kept for the time chosen in the settings counted from the last sighting, and in a file.
        /// </summary>
        public TimedCache<AircraftDetails> AircraftMemory { get; }

        /// <summary>
        /// Completes the type, the full type name and the registration of the selected aircraft from the sources that return the data of an aircraft by its ICAO address: first adsbdb.com and hexdb.io, then the position sources.
        /// </summary>
        public AircraftEnricher Enricher { get; }

        /// <summary>
        /// Keeps the memory of the flight information in line with the visible aircraft: marks the visible flights as seen and closes those that have landed.
        /// </summary>
        public FlightCacheKeeper Keeper { get; }

        /// <summary>
        /// Full recorded paths of the selected flights, which extend the trail collected on the phone back to the takeoff.
        /// </summary>
        public FlightTrackStore Tracks { get; }

        /// <summary>
        /// Weather reports of airports, downloaded again after the time chosen in the settings and kept in a file for longer, so that the last known report can be shown after a failed download.
        /// </summary>
        public WeatherStore Weather { get; }

        /// <summary>
        /// Forecasts (TAF) of airports, downloaded again after the time chosen in the settings and kept in a file for longer, so that the last known forecast can be shown after a failed download. It uses the same source, and so the same pause after limit responses, as <see cref="Weather"/>.
        /// </summary>
        public ForecastStore Forecast { get; }

        /// <summary>
        /// Number of flights and aircraft whose information is kept in the memory, including the answers that nothing is known.
        /// </summary>
        public int FlightInfoCount => RouteMemory.Count + AircraftMemory.Count;

        /// <summary>
        /// Reads the state of every aircraft and route source without sending any request.
        /// </summary>
        /// <returns>The aircraft sources, then the route sources, each in the order of preference, and the source of the weather and the forecasts.</returns>
        public IReadOnlyList<SourceState> GetSourceStates()
        {
            List<SourceState> states = new List<SourceState>();
            string? lastAircraft = AircraftSources.LastUsedSourceName;
            string? lastRoute = RouteSources.LastUsedSourceName;

            foreach (IRateLimitedAircraftSource source in AircraftSources.GetAllInOrder())
            {
                states.Add(new SourceState("Samoloty", source.Name, source.GetRateLimitStatus(), string.Equals(source.Name, lastAircraft, StringComparison.Ordinal)));
            }

            foreach (IRateLimitedRouteSource source in RouteSources.GetAllInOrder())
            {
                states.Add(new SourceState("Trasy", source.Name, source.GetRateLimitStatus(), string.Equals(source.Name, lastRoute, StringComparison.Ordinal)));
            }

            states.Add(new SourceState("Pogoda i prognozy", weatherSource.Name, weatherSource.GetRateLimitStatus(), false));

            return states;
        }

        /// <summary>
        /// Forgets all stored information about flights and aircraft, also in the files.
        /// </summary>
        public void ClearFlightInfo()
        {
            RouteMemory.Clear();
            AircraftMemory.Clear();
            RouteMemory.Flush();
            AircraftMemory.Flush();
            Tracks.Clear();
        }

        /// <summary>
        /// Creates a memory of flight information kept in a file of the given directory. A directory that cannot be used leaves the memory only in the RAM.
        /// </summary>
        /// <typeparam name="T">Kind of the stored information.</typeparam>
        /// <param name="directory">Directory of the file; it is created when missing.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="notFoundMaxAge">Time after which the answer that nothing is known is asked about again, at most.</param>
        /// <param name="maxAge">Returns how long after the last sighting the information is kept.</param>
        /// <returns>The memory with the stored items already loaded.</returns>
        private static TimedCache<T> CreateMemory<T>(string directory, string fileName, TimeSpan notFoundMaxAge, Func<TimeSpan> maxAge) where T : class
        {
            TimedCache<T> memory = new TimedCache<T>(maxAge, notFoundMaxAge);

            try
            {
                Directory.CreateDirectory(directory);
                memory.AttachFile(Path.Combine(directory, fileName), FlightInfoSaveDelay);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }

            return memory;
        }
    }
}

using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the memory of airport weather reports and of the pause of the weather source after limit responses.
    /// </summary>
    public class WeatherStoreTests : IDisposable
    {
        /// <summary>
        /// Weather source that returns a prepared report for each requested airport that it knows, counting the requests.
        /// </summary>
        private sealed class StubSource : IWeatherSource
        {
            /// <summary>
            /// Airports for which the source has a report.
            /// </summary>
            public HashSet<string> Known { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Exception to throw, or null to answer.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Task the answer waits for, or null to answer at once.
            /// </summary>
            public Task? Gate { get; set; }

            /// <summary>
            /// Codes of each request in the order of the requests.
            /// </summary>
            public List<List<string>> Requests { get; } = new List<List<string>>();

            /// <summary>
            /// Reports returned for a search around a point.
            /// </summary>
            public List<Metar> Nearby { get; } = new List<Metar>();

            /// <summary>
            /// Number of searches around a point.
            /// </summary>
            public int NearRequests { get; private set; }

            /// <inheritdoc />
            public string Name => "stub";

            /// <inheritdoc />
            public async Task<IReadOnlyList<Metar>> GetMetarsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
            {
                lock (Requests)
                {
                    Requests.Add(icaoCodes.ToList());
                }

                if (Gate != null)
                {
                    await Gate;
                }

                if (Failure != null)
                {
                    throw Failure;
                }

                return icaoCodes.Where((string code) => Known.Contains(code)).Select(CreateMetar).ToList();
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Taf>> GetTafsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
            {
                TafRequests++;

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult<IReadOnlyList<Taf>>(new List<Taf>());
            }

            /// <summary>
            /// Number of requests for forecasts.
            /// </summary>
            public int TafRequests { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Metar>> GetMetarsNearAsync(double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken)
            {
                NearRequests++;

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult<IReadOnlyList<Metar>>(Nearby.ToList());
            }
        }

        /// <summary>
        /// Latitude of the airport without a station used by the tests (Mielec).
        /// </summary>
        private const double AirportLatitude = 50.3224;

        /// <summary>
        /// Longitude of the airport without a station used by the tests (Mielec).
        /// </summary>
        private const double AirportLongitude = 21.4617;

        /// <summary>
        /// Path of the file used by the persistence test, removed after the test.
        /// </summary>
        private readonly string filePath = Path.Combine(Path.GetTempPath(), "weather-" + Guid.NewGuid().ToString("N") + ".json");

        /// <summary>
        /// Current time returned by the clock of the tests.
        /// </summary>
        private DateTime now = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        public void Dispose()
        {
            File.Delete(filePath);
            File.Delete(filePath + ".tmp");
        }

        /// <summary>
        /// Creates a report with a cloud layer.
        /// </summary>
        /// <param name="code">ICAO code.</param>
        /// <returns>The report.</returns>
        private static Metar CreateMetar(string code)
        {
            return new Metar(code.ToUpperInvariant(), new DateTimeOffset(2026, 11, 5, 11, 50, 0, TimeSpan.Zero), "METAR " + code, "VFR", 270, false, 12, 22, 9000.0, false, new List<CloudLayer> { new CloudLayer("BKN", 2500) }, 8.0, 5.0, 1018.0, null);
        }

        /// <summary>
        /// Creates the memory of the answers over the clock of the tests.
        /// </summary>
        /// <returns>The memory.</returns>
        private TimedCache<WeatherEntry> CreateMemory()
        {
            return new TimedCache<WeatherEntry>(() => WeatherStore.RetentionAge, WeatherStore.RetentionAge, () => now, 100);
        }

        /// <summary>
        /// Creates the store over the clock of the tests.
        /// </summary>
        /// <param name="source">Source of the reports.</param>
        /// <param name="minutes">Interval after which a report is downloaded again, in minutes.</param>
        /// <param name="report">Receives the failures, or null.</param>
        /// <returns>The store.</returns>
        private WeatherStore CreateStore(StubSource source, int minutes = 10, Action<SourceEventKind, string, Exception?>? report = null)
        {
            return new WeatherStore(source, CreateMemory(), () => TimeSpan.FromMinutes(minutes), () => now, report);
        }

        /// <summary>
        /// A downloaded report is stored under the upper-case code and can be read in any letter case.
        /// </summary>
        [Fact]
        public async Task Refresh_StoresReport()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            WeatherStore store = CreateStore(source);

            Assert.True(await store.RefreshAsync(new List<string> { " epwa " }, false, CancellationToken.None));

            WeatherEntry? entry = store.Get("EpWa");

            Assert.NotNull(entry);
            Assert.Equal("EPWA", entry.Report?.IcaoCode);
            Assert.Equal(now, entry.FetchedUtc);
            Assert.Equal(1, store.Count);
            Assert.Equal("stub", store.SourceName);
        }

        /// <summary>
        /// An airport that was not asked about has no stored answer.
        /// </summary>
        [Fact]
        public void Get_Unknown_ReturnsNull()
        {
            Assert.Null(CreateStore(new StubSource()).Get("EPWA"));
        }

        /// <summary>
        /// A fresh answer is not downloaded again, and only the airports that need it are requested.
        /// </summary>
        [Fact]
        public async Task Refresh_FreshEntry_IsNotRequestedAgain()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            source.Known.Add("EPKK");
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(5);
            await store.RefreshAsync(new List<string> { "EPWA", "EPKK" }, false, CancellationToken.None);
            await store.RefreshAsync(new List<string> { "EPWA", "EPKK" }, false, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
            Assert.Equal(new List<string> { "EPKK" }, source.Requests[1]);
        }

        /// <summary>
        /// An answer older than the interval is downloaded again, and the interval is read at every use.
        /// </summary>
        [Fact]
        public async Task Refresh_OldEntry_IsRequestedAgain()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            int minutes = 10;
            WeatherStore store = new WeatherStore(source, CreateMemory(), () => TimeSpan.FromMinutes(minutes), () => now);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(11);
            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(11);
            minutes = 30;
            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
        }

        /// <summary>
        /// The force flag downloads fresh answers too.
        /// </summary>
        [Fact]
        public async Task Refresh_Force_RequestsFreshEntry()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            await store.RefreshAsync(new List<string> { "EPWA" }, true, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
        }

        /// <summary>
        /// With nothing to ask about no request is made.
        /// </summary>
        [Fact]
        public async Task Refresh_NoCodes_MakesNoRequest()
        {
            StubSource source = new StubSource();

            Assert.True(await CreateStore(source).RefreshAsync(new List<string> { " ", string.Empty }, false, CancellationToken.None));
            Assert.Empty(source.Requests);
        }

        /// <summary>
        /// An airport without a report is stored as such and asked about again no sooner than after the longer interval.
        /// </summary>
        [Fact]
        public async Task Refresh_NoReport_IsAskedAboutLessOften()
        {
            StubSource source = new StubSource();
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPZZ" }, false, CancellationToken.None);

            WeatherEntry? entry = store.Get("EPZZ");

            Assert.NotNull(entry);
            Assert.Null(entry.Report);

            now += TimeSpan.FromMinutes(20);
            await store.RefreshAsync(new List<string> { "EPZZ" }, false, CancellationToken.None);

            Assert.Single(source.Requests);

            now += TimeSpan.FromMinutes(11);
            await store.RefreshAsync(new List<string> { "EPZZ" }, false, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
        }

        /// <summary>
        /// When the interval is longer than the pause for airports without a report, it applies to them as well.
        /// </summary>
        [Fact]
        public async Task IsFresh_NoReport_UsesLongerOfTheIntervals()
        {
            StubSource source = new StubSource();
            WeatherStore store = CreateStore(source, 60);

            await store.RefreshAsync(new List<string> { "EPZZ" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(45);

            Assert.True(store.IsFresh(store.Get("EPZZ")!));

            now += TimeSpan.FromMinutes(20);

            Assert.False(store.IsFresh(store.Get("EPZZ")!));
        }

        /// <summary>
        /// After a failed download the old answer stays, the failure is reported and false is returned.
        /// </summary>
        [Fact]
        public async Task Refresh_Failure_KeepsOldEntryAndReports()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            List<(SourceEventKind Kind, string Message)> events = new List<(SourceEventKind, string)>();
            WeatherStore store = CreateStore(source, 10, (SourceEventKind kind, string message, Exception? exception) => events.Add((kind, message)));

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(20);
            source.Failure = new HttpRequestException("no network");

            Assert.False(await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None));

            WeatherEntry? entry = store.Get("EPWA");

            Assert.NotNull(entry?.Report);
            Assert.False(store.IsFresh(entry));
            Assert.Equal(SourceEventKind.Failure, Assert.Single(events).Kind);
            Assert.Contains("HttpRequestException", events[0].Message);
        }

        /// <summary>
        /// A limit response is reported as a pause and also gives false.
        /// </summary>
        [Fact]
        public async Task Refresh_RateLimited_ReportsAndReturnsFalse()
        {
            StubSource source = new StubSource { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            List<string> messages = new List<string>();
            WeatherStore store = CreateStore(source, 10, (SourceEventKind kind, string message, Exception? exception) => messages.Add(message));

            Assert.False(await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None));
            Assert.Contains("429", Assert.Single(messages));
            Assert.Null(store.Get("EPWA"));
        }

        /// <summary>
        /// A failure without a listener still gives false.
        /// </summary>
        [Fact]
        public async Task Refresh_FailureWithoutListener_ReturnsFalse()
        {
            StubSource source = new StubSource { Failure = new InvalidOperationException("broken") };

            Assert.False(await CreateStore(source).RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None));
        }

        /// <summary>
        /// A cancellation asked for by the caller is not a failure of the source.
        /// </summary>
        [Fact]
        public async Task Refresh_Cancelled_Throws()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            StubSource source = new StubSource { Failure = new OperationCanceledException() };
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateStore(source).RefreshAsync(new List<string> { "EPWA" }, false, cancellation.Token));
        }

        /// <summary>
        /// Two callers asking about the same airport at the same time cause one request.
        /// </summary>
        [Fact]
        public async Task Refresh_Concurrent_MakesOneRequest()
        {
            TaskCompletionSource release = new TaskCompletionSource();
            StubSource source = new StubSource { Gate = release.Task };
            source.Known.Add("EPWA");
            WeatherStore store = CreateStore(source);

            Task<bool> first = store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            Task<bool> second = store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            release.SetResult();

            Assert.True(await first);
            Assert.True(await second);
            Assert.Single(source.Requests);
        }

        /// <summary>
        /// A stored answer is forgotten when it is older than the retention time.
        /// </summary>
        [Fact]
        public async Task Get_AfterRetention_ReturnsNull()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(89);

            Assert.NotNull(store.Get("EPWA"));

            now += TimeSpan.FromMinutes(2);

            Assert.Null(store.Get("EPWA"));
        }

        /// <summary>
        /// Clearing the store forgets the answers.
        /// </summary>
        [Fact]
        public async Task Clear_ForgetsEntries()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            store.Clear();

            Assert.Equal(0, store.Count);
            Assert.Null(store.Get("EPWA"));
        }

        /// <summary>
        /// The answers survive writing to the file and loading it again, with the cloud layers.
        /// </summary>
        [Fact]
        public async Task Memory_SurvivesFile()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            TimedCache<WeatherEntry> first = CreateMemory();
            first.AttachFile(filePath, TimeSpan.FromHours(1));
            WeatherStore store = new WeatherStore(source, first, () => TimeSpan.FromMinutes(10), () => now);

            await store.RefreshAsync(new List<string> { "EPWA", "EPZZ" }, false, CancellationToken.None);
            first.Flush();

            TimedCache<WeatherEntry> second = CreateMemory();
            second.AttachFile(filePath, TimeSpan.FromHours(1));
            WeatherStore loaded = new WeatherStore(source, second, () => TimeSpan.FromMinutes(10), () => now);

            Metar? metar = loaded.Get("EPWA")?.Report;

            Assert.NotNull(metar);
            Assert.Equal(new CloudLayer("BKN", 2500), Assert.Single(metar.Clouds));
            Assert.Equal(270, metar.WindDirectionDegrees);
            Assert.Equal(new DateTimeOffset(2026, 11, 5, 11, 50, 0, TimeSpan.Zero), metar.ObservedAt);
            Assert.NotNull(loaded.Get("EPZZ"));
            Assert.Null(loaded.Get("EPZZ")?.Report);
        }

        /// <summary>
        /// Creates a report of a station at a position.
        /// </summary>
        /// <param name="code">ICAO code of the station.</param>
        /// <param name="latitude">Latitude of the station, or null.</param>
        /// <param name="longitude">Longitude of the station, or null.</param>
        /// <returns>The report.</returns>
        private static Metar CreateStation(string code, double? latitude, double? longitude)
        {
            return CreateMetar(code) with { Latitude = latitude, Longitude = longitude };
        }

        /// <summary>
        /// An airport without a report of its own takes the report of the nearest station within the distance, with the distance.
        /// </summary>
        [Fact]
        public async Task Nearest_AirportWithoutReport_TakesNearestStation()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPKK", 50.078, 19.785));
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            Assert.True(await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None));

            WeatherEntry? entry = store.Get("EPML");

            Assert.Equal("EPRZ", entry?.Report?.IcaoCode);
            Assert.InRange(entry!.DistanceKilometers!.Value, 40.0, 52.0);
            Assert.Equal(60.0, entry.SearchedRadiusKilometers);
            Assert.Single(source.Requests);
            Assert.Equal(1, source.NearRequests);
        }

        /// <summary>
        /// An airport with a report of its own does not look for another station.
        /// </summary>
        [Fact]
        public async Task Nearest_AirportWithOwnReport_DoesNotSearch()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPRZ");
            source.Nearby.Add(CreateStation("EPKK", 50.078, 19.785));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPRZ", 50.112, 22.025, 60.0, false, CancellationToken.None);

            Assert.Null(store.Get("EPRZ")?.DistanceKilometers);
            Assert.Equal("EPRZ", store.Get("EPRZ")?.Report?.IcaoCode);
            Assert.Equal(0, source.NearRequests);
        }

        /// <summary>
        /// With the distance switched off no station is looked for.
        /// </summary>
        [Fact]
        public async Task Nearest_ZeroRadius_DoesNotSearch()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 0.0, false, CancellationToken.None);

            Assert.Null(store.Get("EPML")?.Report);
            Assert.Equal(0, source.NearRequests);
        }

        /// <summary>
        /// A station beyond the distance, a station of the airport itself and a station without a position are not taken.
        /// </summary>
        [Fact]
        public async Task Nearest_IgnoresFarOwnAndUnlocatedStations()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            source.Nearby.Add(CreateStation("EPML", AirportLatitude, AirportLongitude));
            source.Nearby.Add(CreateStation("EPXX", null, null));
            WeatherStore store = CreateStore(source);

            Assert.True(await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 30.0, false, CancellationToken.None));

            WeatherEntry? entry = store.Get("EPML");

            Assert.Null(entry?.Report);
            Assert.Null(entry?.DistanceKilometers);
            Assert.Equal(30.0, entry?.SearchedRadiusKilometers);
        }

        /// <summary>
        /// The nearest of several stations is taken.
        /// </summary>
        [Fact]
        public async Task Nearest_PicksTheClosest()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            source.Nearby.Add(CreateStation("EPKT", 50.474, 19.075));
            source.Nearby.Add(CreateStation("EPTA", 50.2, 21.3));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 100.0, false, CancellationToken.None);

            Assert.Equal("EPTA", store.Get("EPML")?.Report?.IcaoCode);
        }

        /// <summary>
        /// A fresh answer from the nearest station is not downloaded again, and a stale one is refreshed with the search only, without asking the airport again.
        /// </summary>
        [Fact]
        public async Task Nearest_FreshIsKept_StaleRepeatsOnlyTheSearch()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(5);
            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);

            Assert.Equal(1, source.NearRequests);
            Assert.Single(source.Requests);

            now += TimeSpan.FromMinutes(11);
            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);

            Assert.Equal(2, source.NearRequests);
            Assert.Single(source.Requests);
        }

        /// <summary>
        /// Raising the distance repeats a search that found nothing, and lowering it below the distance of the station drops that station.
        /// </summary>
        [Fact]
        public async Task Nearest_ChangedRadius_RepeatsTheSearch()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 20.0, false, CancellationToken.None);

            Assert.Null(store.Get("EPML")?.Report);
            Assert.False(store.NeedsRefresh("EPML", 20.0));
            Assert.True(store.NeedsRefresh("EPML", 60.0));

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);

            Assert.Equal("EPRZ", store.Get("EPML")?.Report?.IcaoCode);
            Assert.False(store.NeedsRefresh("EPML", 60.0));
            Assert.True(store.NeedsRefresh("EPML", 20.0));
            Assert.True(store.NeedsRefresh("EPML", 0.0));

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 0.0, false, CancellationToken.None);

            Assert.Null(store.Get("EPML")?.Report);
            Assert.False(store.NeedsRefresh("EPML", 0.0));
        }

        /// <summary>
        /// The force flag asks the airport and the nearest station again.
        /// </summary>
        [Fact]
        public async Task Nearest_Force_AsksAgain()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);
            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, true, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
            Assert.Equal(2, source.NearRequests);
        }

        /// <summary>
        /// A station that appears at the airport itself replaces the nearest one once the answer is old.
        /// </summary>
        [Fact]
        public async Task Nearest_OwnReportAppears_ReplacesNearestStation()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            WeatherStore store = CreateStore(source);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);
            source.Known.Add("EPML");

            Assert.True(await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, true, CancellationToken.None));
            Assert.Equal("EPML", store.Get("EPML")?.Report?.IcaoCode);
            Assert.Null(store.Get("EPML")?.DistanceKilometers);
        }

        /// <summary>
        /// A failed search returns false, is reported and keeps the old answer.
        /// </summary>
        [Fact]
        public async Task Nearest_SearchFailure_ReturnsFalseAndReports()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            List<string> messages = new List<string>();
            WeatherStore store = CreateStore(source, 10, (SourceEventKind kind, string message, Exception? exception) => messages.Add(message));

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(20);
            source.Failure = new HttpRequestException("no network");

            Assert.False(await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None));
            Assert.Equal("EPRZ", store.Get("EPML")?.Report?.IcaoCode);
            Assert.Single(messages);
        }

        /// <summary>
        /// Without a code nothing is downloaded.
        /// </summary>
        [Fact]
        public async Task Nearest_NoCode_DoesNothing()
        {
            StubSource source = new StubSource();

            Assert.True(await CreateStore(source).RefreshAsync(" ", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None));
            Assert.Empty(source.Requests);
            Assert.Equal(0, source.NearRequests);
        }

        /// <summary>
        /// The distance and the searched distance survive writing to the file and loading it again.
        /// </summary>
        [Fact]
        public async Task Nearest_SurvivesFile()
        {
            StubSource source = new StubSource();
            source.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            TimedCache<WeatherEntry> first = CreateMemory();
            first.AttachFile(filePath, TimeSpan.FromHours(1));
            WeatherStore store = new WeatherStore(source, first, () => TimeSpan.FromMinutes(10), () => now);

            await store.RefreshAsync("EPML", AirportLatitude, AirportLongitude, 60.0, false, CancellationToken.None);
            first.Flush();

            TimedCache<WeatherEntry> second = CreateMemory();
            second.AttachFile(filePath, TimeSpan.FromHours(1));
            WeatherEntry? loaded = new WeatherStore(source, second, () => TimeSpan.FromMinutes(10), () => now).Get("EPML");

            Assert.Equal("EPRZ", loaded?.Report?.IcaoCode);
            Assert.InRange(loaded!.DistanceKilometers!.Value, 40.0, 52.0);
            Assert.Equal(60.0, loaded.SearchedRadiusKilometers);
            Assert.Equal(22.025, loaded.Report!.Longitude);
        }

        /// <summary>
        /// The wrapper passes a request for forecasts through the same pause as the requests for reports, because it is the same service.
        /// </summary>
        [Fact]
        public async Task RateLimited_Forecasts_ShareThePauseWithReports()
        {
            StubSource inner = new StubSource();
            RateLimitedWeatherSource source = new RateLimitedWeatherSource(inner);

            Assert.Empty(await source.GetTafsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            inner.Failure = new RateLimitedException(TimeSpan.FromSeconds(30));

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetTafsAsync(new List<string> { "EPWA" }, CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.Equal(2, inner.TafRequests + inner.Requests.Count);
        }

        /// <summary>
        /// The wrapper passes a search around a point through the same pause as the other requests.
        /// </summary>
        [Fact]
        public async Task RateLimited_Near_PassesThroughAndPauses()
        {
            StubSource inner = new StubSource();
            inner.Nearby.Add(CreateStation("EPRZ", 50.112, 22.025));
            RateLimitedWeatherSource source = new RateLimitedWeatherSource(inner);

            Assert.Single(await source.GetMetarsNearAsync(AirportLatitude, AirportLongitude, 60.0, CancellationToken.None));

            inner.Failure = new RateLimitedException(TimeSpan.FromSeconds(30));

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMetarsNearAsync(AirportLatitude, AirportLongitude, 60.0, CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.Equal(2, inner.NearRequests + inner.Requests.Count);
        }

        /// <summary>
        /// The wrapper passes the answer through, has the name of the wrapped source and starts with no pause.
        /// </summary>
        [Fact]
        public async Task RateLimited_PassesAnswerThrough()
        {
            StubSource inner = new StubSource();
            inner.Known.Add("EPWA");
            RateLimitedWeatherSource source = new RateLimitedWeatherSource(inner);

            IReadOnlyList<Metar> result = await source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("stub", source.Name);
            Assert.True(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// After a limit response the source is paused and the next request does not reach the wrapped source.
        /// </summary>
        [Fact]
        public async Task RateLimited_AfterLimit_PausesRequests()
        {
            StubSource inner = new StubSource { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            RateLimitedWeatherSource source = new RateLimitedWeatherSource(inner);

            RateLimitedException first = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(30), first.RetryAfter);
            Assert.Single(inner.Requests);
            Assert.False(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// Another failure is rethrown and remembered in the state of the source.
        /// </summary>
        [Fact]
        public async Task RateLimited_Failure_IsRethrownAndRemembered()
        {
            StubSource inner = new StubSource { Failure = new HttpRequestException("no network") };
            RateLimitedWeatherSource source = new RateLimitedWeatherSource(inner);

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.NotNull(source.GetRateLimitStatus().LastFailure);
        }

        /// <summary>
        /// A null wrapped source or limiter is refused.
        /// </summary>
        [Fact]
        public void RateLimited_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new RateLimitedWeatherSource(null!));
            Assert.Throws<ArgumentNullException>(() => new RateLimitedWeatherSource(new StubSource(), null!));
        }

        /// <summary>
        /// A null source, memory, interval or clock is refused.
        /// </summary>
        [Fact]
        public void Store_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new WeatherStore(null!, CreateMemory(), () => TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => new WeatherStore(new StubSource(), null!, () => TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => new WeatherStore(new StubSource(), CreateMemory(), null!));
            Assert.Throws<ArgumentNullException>(() => new WeatherStore(new StubSource(), CreateMemory(), () => TimeSpan.Zero, null!));
        }
    }
}

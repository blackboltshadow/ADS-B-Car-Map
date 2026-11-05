using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the memory of airport forecasts.
    /// </summary>
    public class ForecastStoreTests : IDisposable
    {
        /// <summary>
        /// Source that returns a prepared forecast for each requested airport that it knows, counting the requests.
        /// </summary>
        private sealed class StubSource : IWeatherSource
        {
            /// <summary>
            /// Airports for which the source has a forecast.
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
            /// Moment from which a forecast given by the source is valid, in UTC.
            /// </summary>
            public DateTime ValidFromUtc { get; set; }

            /// <summary>
            /// Codes of each request in the order of the requests.
            /// </summary>
            public List<List<string>> Requests { get; } = new List<List<string>>();

            /// <inheritdoc />
            public string Name => "stub";

            /// <inheritdoc />
            public Task<IReadOnlyList<Metar>> GetMetarsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<Metar>>(new List<Metar>());
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Metar>> GetMetarsNearAsync(double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<Metar>>(new List<Metar>());
            }

            /// <inheritdoc />
            public async Task<IReadOnlyList<Taf>> GetTafsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
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

                return icaoCodes.Where((string code) => Known.Contains(code)).Select((string code) => CreateTaf(code, ValidFromUtc)).ToList();
            }
        }

        /// <summary>
        /// Path of the file used by the persistence test, removed after the test.
        /// </summary>
        private readonly string filePath = Path.Combine(Path.GetTempPath(), "forecast-" + Guid.NewGuid().ToString("N") + ".json");

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
        /// Creates a forecast valid for 24 hours with two periods.
        /// </summary>
        /// <param name="code">ICAO code.</param>
        /// <param name="validFromUtc">Beginning of the validity, in UTC.</param>
        /// <returns>The forecast.</returns>
        private static Taf CreateTaf(string code, DateTime validFromUtc)
        {
            DateTimeOffset from = new DateTimeOffset(validFromUtc, TimeSpan.Zero);
            List<TafPeriod> periods = new List<TafPeriod>
            {
                new TafPeriod(from, from.AddHours(24), null, null, 280, false, 10, null, 8000.0, false, new List<CloudLayer> { new CloudLayer("NSC", null) }, null),
                new TafPeriod(from.AddHours(6), from.AddHours(12), "TEMPO", 40, null, false, null, null, 4000.0, false, new List<CloudLayer> { new CloudLayer("BKN", 300) }, "BR"),
            };

            return new Taf(code.ToUpperInvariant(), from, from, from.AddHours(24), "TAF " + code, periods);
        }

        /// <summary>
        /// Creates the memory of the answers over the clock of the tests.
        /// </summary>
        /// <returns>The memory.</returns>
        private TimedCache<ForecastEntry> CreateMemory()
        {
            return new TimedCache<ForecastEntry>(() => ForecastStore.RetentionAge, ForecastStore.RetentionAge, () => now, 100);
        }

        /// <summary>
        /// Creates the store over the clock of the tests.
        /// </summary>
        /// <param name="source">Source of the forecasts.</param>
        /// <param name="minutes">Interval after which a forecast is downloaded again, in minutes.</param>
        /// <param name="report">Receives the failures, or null.</param>
        /// <returns>The store.</returns>
        private ForecastStore CreateStore(StubSource source, int minutes = 60, Action<SourceEventKind, string, Exception?>? report = null)
        {
            source.ValidFromUtc = now;

            return new ForecastStore(source, CreateMemory(), () => TimeSpan.FromMinutes(minutes), () => now, report);
        }

        /// <summary>
        /// A downloaded forecast is stored under the upper-case code and can be read in any letter case, with the moment of the download.
        /// </summary>
        [Fact]
        public async Task Refresh_StoresForecast()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            ForecastStore store = CreateStore(source);

            Assert.True(await store.RefreshAsync(new List<string> { " epwa " }, false, CancellationToken.None));

            ForecastEntry? entry = store.Get("EpWa");

            Assert.NotNull(entry);
            Assert.Equal("EPWA", entry.Forecast?.IcaoCode);
            Assert.Equal(now, entry.FetchedUtc);
            Assert.Equal(1, store.Count);
            Assert.Null(store.Get("EPKK"));
        }

        /// <summary>
        /// A fresh answer is not downloaded again, only the airports that need it are requested, and a stale answer is requested again.
        /// </summary>
        [Fact]
        public async Task Refresh_FreshIsKept_OldIsRequestedAgain()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            source.Known.Add("EPKK");
            ForecastStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(30);
            await store.RefreshAsync(new List<string> { "EPWA", "EPKK" }, false, CancellationToken.None);

            Assert.Equal(new List<string> { "EPKK" }, source.Requests[1]);
            Assert.False(store.NeedsRefresh("EPWA"));

            now += TimeSpan.FromMinutes(31);

            Assert.True(store.NeedsRefresh("EPWA"));
            Assert.False(store.NeedsRefresh("EPKK"));

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);

            Assert.Equal(3, source.Requests.Count);
        }

        /// <summary>
        /// The force flag downloads fresh answers too, and with nothing to ask about no request is made.
        /// </summary>
        [Fact]
        public async Task Refresh_ForceAndNoCodes()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            ForecastStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            await store.RefreshAsync(new List<string> { "EPWA" }, true, CancellationToken.None);

            Assert.Equal(2, source.Requests.Count);
            Assert.True(await store.RefreshAsync(new List<string> { " ", string.Empty }, false, CancellationToken.None));
            Assert.Equal(2, source.Requests.Count);
        }

        /// <summary>
        /// An airport without a forecast is stored as such and asked about again no sooner than after three hours.
        /// </summary>
        [Fact]
        public async Task Refresh_NoForecast_IsAskedAboutRarely()
        {
            StubSource source = new StubSource();
            ForecastStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPML" }, false, CancellationToken.None);

            Assert.Null(store.Get("EPML")?.Forecast);
            Assert.NotNull(store.Get("EPML"));

            now += TimeSpan.FromMinutes(100);

            Assert.False(store.NeedsRefresh("EPML"));

            now += TimeSpan.FromMinutes(81);

            Assert.True(store.NeedsRefresh("EPML"));
        }

        /// <summary>
        /// A forecast that is no longer valid is asked about again after at most fifteen minutes, even with a long interval.
        /// </summary>
        [Fact]
        public async Task IsFresh_ExpiredForecast_IsAskedAboutSooner()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            ForecastStore store = CreateStore(source, 180);
            source.ValidFromUtc = now - TimeSpan.FromHours(25);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);

            Assert.True(store.Get("EPWA")!.Forecast!.ValidTo.UtcDateTime < now);
            Assert.True(store.IsFresh(store.Get("EPWA")!));

            now += TimeSpan.FromMinutes(14);

            Assert.True(store.IsFresh(store.Get("EPWA")!));

            now += TimeSpan.FromMinutes(2);

            Assert.False(store.IsFresh(store.Get("EPWA")!));
        }

        /// <summary>
        /// After a failed download the old answer stays, the failure is reported and false is returned; a limit response and a failure without a listener are handled too.
        /// </summary>
        [Fact]
        public async Task Refresh_Failure_KeepsOldEntryAndReports()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            List<string> messages = new List<string>();
            ForecastStore store = CreateStore(source, 60, (SourceEventKind kind, string message, Exception? exception) => messages.Add(message));

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromMinutes(90);
            source.Failure = new HttpRequestException("no network");

            Assert.False(await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None));
            Assert.NotNull(store.Get("EPWA")?.Forecast);
            Assert.Contains("HttpRequestException", Assert.Single(messages));

            source.Failure = new RateLimitedException(TimeSpan.FromSeconds(30));

            Assert.False(await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None));
            Assert.Contains("429", messages[1]);

            source.Failure = new InvalidOperationException("broken");

            Assert.False(await CreateStore(source).RefreshAsync(new List<string> { "EPKK" }, false, CancellationToken.None));
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
            ForecastStore store = CreateStore(source);

            Task<bool> first = store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            Task<bool> second = store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            release.SetResult();

            Assert.True(await first);
            Assert.True(await second);
            Assert.Single(source.Requests);
        }

        /// <summary>
        /// A stored answer is forgotten after the retention time, and clearing forgets everything.
        /// </summary>
        [Fact]
        public async Task Get_AfterRetention_ReturnsNull_ClearForgets()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            ForecastStore store = CreateStore(source);

            await store.RefreshAsync(new List<string> { "EPWA" }, false, CancellationToken.None);
            now += TimeSpan.FromHours(5);

            Assert.NotNull(store.Get("EPWA"));

            now += TimeSpan.FromHours(2);

            Assert.Null(store.Get("EPWA"));

            now -= TimeSpan.FromHours(2);
            await store.RefreshAsync(new List<string> { "EPWA" }, true, CancellationToken.None);
            store.Clear();

            Assert.Equal(0, store.Count);
        }

        /// <summary>
        /// The forecast with its periods survives writing to the file and loading it again.
        /// </summary>
        [Fact]
        public async Task Memory_SurvivesFile()
        {
            StubSource source = new StubSource();
            source.Known.Add("EPWA");
            TimedCache<ForecastEntry> first = CreateMemory();
            first.AttachFile(filePath, TimeSpan.FromHours(1));
            source.ValidFromUtc = now;
            ForecastStore store = new ForecastStore(source, first, () => TimeSpan.FromMinutes(60), () => now);

            await store.RefreshAsync(new List<string> { "EPWA", "EPML" }, false, CancellationToken.None);
            first.Flush();

            TimedCache<ForecastEntry> second = CreateMemory();
            second.AttachFile(filePath, TimeSpan.FromHours(1));
            ForecastStore loaded = new ForecastStore(source, second, () => TimeSpan.FromMinutes(60), () => now);
            Taf? taf = loaded.Get("EPWA")?.Forecast;

            Assert.NotNull(taf);
            Assert.Equal(2, taf.Periods.Count);
            Assert.Equal("TEMPO", taf.Periods[1].Change);
            Assert.Equal(40, taf.Periods[1].Probability);
            Assert.Equal(new CloudLayer("BKN", 300), Assert.Single(taf.Periods[1].Clouds));
            Assert.Equal("BR", taf.Periods[1].WeatherText);
            Assert.Equal("TAF EPWA", taf.RawText);
            Assert.NotNull(loaded.Get("EPML"));
            Assert.Null(loaded.Get("EPML")?.Forecast);
        }

        /// <summary>
        /// A null source, memory, interval or clock is refused.
        /// </summary>
        [Fact]
        public void Store_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ForecastStore(null!, CreateMemory(), () => TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => new ForecastStore(new StubSource(), null!, () => TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => new ForecastStore(new StubSource(), CreateMemory(), null!));
            Assert.Throws<ArgumentNullException>(() => new ForecastStore(new StubSource(), CreateMemory(), () => TimeSpan.Zero, null!));
        }
    }
}

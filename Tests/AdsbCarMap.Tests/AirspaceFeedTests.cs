using System.Net;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the feed of the airspace areas, of the search for the area under a point and of the texts of the layer.
    /// </summary>
    public class AirspaceFeedTests
    {
        /// <summary>
        /// The moment returned by the clock of the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Source whose answer is chosen by the test.
        /// </summary>
        private sealed class FakeSource : IAirspaceSource
        {
            /// <summary>
            /// Gives the answer or throws.
            /// </summary>
            public Func<IReadOnlyList<AirspaceZone>> Answer { get; set; } = () => new List<AirspaceZone>();

            /// <summary>
            /// Number of the requests.
            /// </summary>
            public int Calls { get; private set; }

            /// <inheritdoc />
            public string Name => "fake";

            /// <inheritdoc />
            public Task<IReadOnlyList<AirspaceZone>> GetZonesAsync(CancellationToken cancellationToken)
            {
                Calls++;

                return Task.FromResult(Answer());
            }
        }

        /// <summary>
        /// Creates a square area around a point.
        /// </summary>
        /// <param name="designator">Designation of the area.</param>
        /// <param name="latitude">Latitude of the middle.</param>
        /// <param name="longitude">Longitude of the middle.</param>
        /// <param name="half">Half of the side in degrees.</param>
        /// <returns>The area.</returns>
        private static AirspaceZone Square(string designator, double latitude, double longitude, double half)
        {
            List<AirspaceVertex> outline = new List<AirspaceVertex>
            {
                new AirspaceVertex(latitude - half, longitude - half),
                new AirspaceVertex(latitude - half, longitude + half),
                new AirspaceVertex(latitude + half, longitude + half),
                new AirspaceVertex(latitude + half, longitude - half),
            };

            return new AirspaceZone(designator, "D", new List<IReadOnlyList<AirspaceVertex>> { outline }, new AirspaceBounds(latitude - half, latitude + half, longitude - half, longitude + half), new List<AirspaceReservation>());
        }

        /// <summary>
        /// Creates a feed over a source with the clock and a delay that does nothing.
        /// </summary>
        /// <param name="source">Source of the areas.</param>
        /// <param name="events">List that receives the reported events.</param>
        /// <returns>The feed.</returns>
        private static AirspaceFeed CreateFeed(FakeSource source, List<string> events)
        {
            return new AirspaceFeed(source, () => Now, (TimeSpan time, CancellationToken token) => Task.CompletedTask, () => TimeSpan.FromMinutes(5), (SourceEventKind kind, string message, Exception? exception) => events.Add(kind + ":" + message));
        }

        /// <summary>
        /// A successful download stores the areas and the time and moves the feed to the ready state.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_StoresZonesAndReturnsInterval()
        {
            FakeSource source = new FakeSource { Answer = () => new List<AirspaceZone> { Square("A", 50, 20, 1) } };
            AirspaceFeed feed = CreateFeed(source, new List<string>());
            int changes = 0;
            feed.Changed += () => changes++;

            TimeSpan wait = await feed.RefreshAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromMinutes(5), wait);
            Assert.Single(feed.Zones);
            Assert.Equal(AirspaceFeedState.Ready, feed.State);
            Assert.Equal(Now, feed.LastUpdateUtc);
            Assert.Null(feed.LastFailure);
            Assert.Equal("fake", feed.Name);
            Assert.True(changes > 0);
        }

        /// <summary>
        /// A failure keeps the older areas, reports the failure and asks for a quick retry that grows with every next failure but never exceeds the normal interval.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_BacksOffAfterFailures()
        {
            FakeSource source = new FakeSource { Answer = () => new List<AirspaceZone> { Square("A", 50, 20, 1) } };
            List<string> events = new List<string>();
            AirspaceFeed feed = CreateFeed(source, events);
            await feed.RefreshAsync(CancellationToken.None);
            source.Answer = () => throw new HttpRequestException("down", null, HttpStatusCode.BadGateway);

            TimeSpan first = await feed.RefreshAsync(CancellationToken.None);
            TimeSpan second = await feed.RefreshAsync(CancellationToken.None);
            TimeSpan third = await feed.RefreshAsync(CancellationToken.None);
            TimeSpan fourth = await feed.RefreshAsync(CancellationToken.None);
            TimeSpan fifth = await feed.RefreshAsync(CancellationToken.None);
            TimeSpan sixth = await feed.RefreshAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(30), first);
            Assert.Equal(TimeSpan.FromSeconds(60), second);
            Assert.Equal(TimeSpan.FromSeconds(120), third);
            Assert.Equal(TimeSpan.FromSeconds(240), fourth);
            Assert.Equal(TimeSpan.FromMinutes(5), fifth);
            Assert.Equal(TimeSpan.FromMinutes(5), sixth);
            Assert.Single(feed.Zones);
            Assert.Equal(AirspaceFeedState.Offline, feed.State);
            Assert.Equal("błąd serwera", feed.LastFailure?.Description);
            Assert.Equal(Now, feed.LastFailure?.OccurredUtc);
            Assert.Equal(6, events.Count);
            Assert.StartsWith("Failure:fake: błąd serwera", events[0]);
        }

        /// <summary>
        /// A success after failures starts the backoff again.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_SuccessResetsBackoff()
        {
            FakeSource source = new FakeSource { Answer = () => throw new HttpRequestException("down") };
            AirspaceFeed feed = CreateFeed(source, new List<string>());
            await feed.RefreshAsync(CancellationToken.None);
            await feed.RefreshAsync(CancellationToken.None);
            source.Answer = () => new List<AirspaceZone>();
            await feed.RefreshAsync(CancellationToken.None);
            source.Answer = () => throw new HttpRequestException("down");

            TimeSpan wait = await feed.RefreshAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(30), wait);
            Assert.Equal("brak połączenia", feed.LastFailure?.Description);
        }

        /// <summary>
        /// A rate limit makes the feed wait as long as the server asked, but at least the normal interval.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_RateLimitWaitsForRetryAfter()
        {
            FakeSource source = new FakeSource { Answer = () => throw new RateLimitedException(TimeSpan.FromMinutes(20)) };
            AirspaceFeed feed = CreateFeed(source, new List<string>());

            TimeSpan longWait = await feed.RefreshAsync(CancellationToken.None);
            source.Answer = () => throw new RateLimitedException(TimeSpan.FromSeconds(10));
            TimeSpan shortWait = await feed.RefreshAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromMinutes(20), longWait);
            Assert.Equal(TimeSpan.FromMinutes(5), shortWait);
            Assert.Equal("limit zapytań (429)", feed.LastFailure?.Description);
            Assert.Equal(AirspaceFeedState.Offline, feed.State);
        }

        /// <summary>
        /// An invalid answer and a timeout are failures of the feed and do not end it.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_TreatsInvalidAnswerAndTimeoutAsFailures()
        {
            FakeSource source = new FakeSource { Answer = () => throw new System.Text.Json.JsonException("bad") };
            AirspaceFeed feed = CreateFeed(source, new List<string>());

            await feed.RefreshAsync(CancellationToken.None);
            Assert.Equal("nieprawidłowa odpowiedź", feed.LastFailure?.Description);

            source.Answer = () => throw new TaskCanceledException("timeout");
            await feed.RefreshAsync(CancellationToken.None);
            Assert.Equal("przekroczono czas oczekiwania", feed.LastFailure?.Description);
        }

        /// <summary>
        /// A cancellation requested by the caller ends the download with the cancellation, not with a failure.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_PropagatesRequestedCancellation()
        {
            FakeSource source = new FakeSource { Answer = () => new List<AirspaceZone> { Square("A", 50, 20, 1) } };
            AirspaceFeed feed = CreateFeed(source, new List<string>());
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => feed.RefreshAsync(cancellation.Token));

            Assert.Empty(feed.Zones);
            Assert.Null(feed.LastFailure);
        }

        /// <summary>
        /// Starting downloads the areas again and again, stopping ends the loop and keeps the data, and a second start or stop does nothing.
        /// </summary>
        [Fact]
        public async Task StartAndStop_RunTheLoop()
        {
            FakeSource source = new FakeSource { Answer = () => new List<AirspaceZone> { Square("A", 50, 20, 1) } };
            TaskCompletionSource<bool> twoCalls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AirspaceFeed feed = new AirspaceFeed(
                source,
                () => Now,
                async (TimeSpan time, CancellationToken token) =>
                {
                    if (source.Calls >= 2)
                    {
                        twoCalls.TrySetResult(true);
                        await Task.Delay(Timeout.Infinite, token);
                    }
                },
                () => TimeSpan.FromMinutes(5),
                null);

            Assert.Equal(AirspaceFeedState.Stopped, feed.State);
            feed.Stop();
            feed.Start();
            feed.Start();
            await twoCalls.Task.WaitAsync(TimeSpan.FromSeconds(10));
            feed.Stop();
            feed.Stop();

            Assert.Equal(AirspaceFeedState.Stopped, feed.State);
            Assert.Single(feed.Zones);
            Assert.NotNull(feed.LastUpdateUtc);

            feed.Start();
            Assert.Equal(AirspaceFeedState.Ready, feed.State);
            feed.Stop();
        }

        /// <summary>
        /// A feed started without data is loading until the first answer.
        /// </summary>
        [Fact]
        public void Start_WithoutDataIsLoading()
        {
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
            FakeSource source = new FakeSource();
            AirspaceFeed feed = new AirspaceFeed(source, () => Now, (TimeSpan time, CancellationToken token) => Task.Delay(Timeout.Infinite, token), () => TimeSpan.FromMinutes(5), null);

            feed.Start();
            AirspaceFeedState state = feed.State;
            feed.Stop();
            release.TrySetResult(true);

            Assert.True(state == AirspaceFeedState.Loading || state == AirspaceFeedState.Ready);
        }

        /// <summary>
        /// The area under a point is found; a point outside every area, and a point inside the rectangle but outside the outline, give null.
        /// </summary>
        [Fact]
        public void Contains_UsesTheOutline()
        {
            List<AirspaceVertex> triangle = new List<AirspaceVertex> { new AirspaceVertex(50, 20), new AirspaceVertex(50, 22), new AirspaceVertex(52, 20) };
            AirspaceZone zone = new AirspaceZone("T", "R", new List<IReadOnlyList<AirspaceVertex>> { triangle }, new AirspaceBounds(50, 52, 20, 22), new List<AirspaceReservation>());

            Assert.True(AirspaceGeometry.Contains(zone, 50.5, 20.5));
            Assert.False(AirspaceGeometry.Contains(zone, 51.8, 21.8));
            Assert.False(AirspaceGeometry.Contains(zone, 49, 20.5));
            Assert.False(AirspaceGeometry.Contains(zone, 50.5, 23));
        }

        /// <summary>
        /// An area made of several parts contains the points of every part.
        /// </summary>
        [Fact]
        public void Contains_HandlesSeveralOutlines()
        {
            AirspaceZone first = Square("A", 50, 20, 0.5);
            AirspaceZone second = Square("B", 55, 30, 0.5);
            AirspaceZone both = new AirspaceZone("AB", "D", new List<IReadOnlyList<AirspaceVertex>> { first.Outlines[0], second.Outlines[0] }, new AirspaceBounds(49.5, 55.5, 19.5, 30.5), new List<AirspaceReservation>());

            Assert.True(AirspaceGeometry.Contains(both, 50, 20));
            Assert.True(AirspaceGeometry.Contains(both, 55, 30));
            Assert.False(AirspaceGeometry.Contains(both, 52, 25));
        }

        /// <summary>
        /// Where areas overlap, the smallest one is found.
        /// </summary>
        [Fact]
        public void FindAt_ReturnsSmallestAreaUnderPoint()
        {
            AirspaceZoneView big = new AirspaceZoneView(Square("BIG", 50, 20, 2), AirspaceZoneState.Active, null);
            AirspaceZoneView small = new AirspaceZoneView(Square("SMALL", 50, 20, 0.5), AirspaceZoneState.Planned, null);
            List<AirspaceZoneView> views = new List<AirspaceZoneView> { big, small };

            Assert.Same(small, AirspaceGeometry.FindAt(views, 50, 20));
            Assert.Same(big, AirspaceGeometry.FindAt(views, 51.5, 20));
            Assert.Null(AirspaceGeometry.FindAt(views, 60, 20));
            Assert.Null(AirspaceGeometry.FindAt(new List<AirspaceZoneView>(), 50, 20));
        }

        /// <summary>
        /// The label is placed in the middle of the rectangle of the area.
        /// </summary>
        [Fact]
        public void GetLabelPoint_IsMiddleOfBounds()
        {
            (double Latitude, double Longitude) point = AirspaceGeometry.GetLabelPoint(Square("A", 50, 20, 1));

            Assert.Equal(50.0, point.Latitude, 6);
            Assert.Equal(20.0, point.Longitude, 6);
        }

        /// <summary>
        /// The line next to the attribution tells what the feed does.
        /// </summary>
        [Fact]
        public void BuildStateText_DescribesEveryState()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("ładowanie…", AirspaceText.BuildStateText(AirspaceFeedState.Loading, 0));
            Assert.Equal("stref: 12", AirspaceText.BuildStateText(AirspaceFeedState.Ready, 12));
            Assert.Equal("brak połączenia", AirspaceText.BuildStateText(AirspaceFeedState.Offline, 0));
            Assert.Equal("brak połączenia, dane mogą być nieaktualne, stref: 3", AirspaceText.BuildStateText(AirspaceFeedState.Offline, 3));
            Assert.Equal(string.Empty, AirspaceText.BuildStateText(AirspaceFeedState.Stopped, 5));
        }

        /// <summary>
        /// Every kind gets a color, the restricting kinds are red and orange, and an unknown kind is gray.
        /// </summary>
        [Fact]
        public void GetColor_ColorsKinds()
        {
            Assert.Equal(AirspaceText.GetColor("R"), AirspaceText.GetColor("p"));
            Assert.NotEqual(AirspaceText.GetColor("R"), AirspaceText.GetColor("D"));
            HashSet<int> colors = new HashSet<int>();

            foreach (string type in new List<string> { "R", "D", "TSA", "TRA", "ADHOC", "MRT", "ATZ", "UNKNOWN" })
            {
                colors.Add(AirspaceText.GetColor(type));
            }

            Assert.Equal(8, colors.Count);
            Assert.Equal(AirspaceText.GetColor("UNKNOWN"), AirspaceText.GetColor("OTHER"));
        }

        /// <summary>
        /// The source status card lists the airspace feed: its state, the last update and the last failure.
        /// </summary>
        [Fact]
        public void FormatAirspaceFeed_DescribesTheSource()
        {
            Loc.Language = AppLanguage.Polish;
            DateTime update = Now.AddMinutes(-3);
            AirspaceFeedFailure failure = new AirspaceFeedFailure("limit zapytań (429)", Now.AddMinutes(-10));

            string on = SourceStateText.FormatAirspaceFeed(true, AirspaceFeedState.Ready, 188, update, failure, Now);
            string off = SourceStateText.FormatAirspaceFeed(false, AirspaceFeedState.Stopped, 0, null, null, Now);

            Assert.StartsWith("Strefy przestrzeni powietrznej:\n• PAŻP — działa, stref: 188\n", on);
            Assert.Contains("ostatnia aktualizacja: ", on);
            Assert.Contains("ostatni błąd: limit zapytań (429), ", on);
            Assert.Equal("Strefy przestrzeni powietrznej:\n• PAŻP — warstwa wyłączona, brak połączenia z serwisem", off);
        }

        /// <summary>
        /// Every state of the feed has a description.
        /// </summary>
        [Fact]
        public void DescribeAirspaceFeed_CoversEveryState()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("pobieranie…", SourceStateText.DescribeAirspaceFeed(true, AirspaceFeedState.Loading, 0));
            Assert.Equal("działa, stref: 5", SourceStateText.DescribeAirspaceFeed(true, AirspaceFeedState.Ready, 5));
            Assert.Equal("brak połączenia, aplikacja ponawia próby", SourceStateText.DescribeAirspaceFeed(true, AirspaceFeedState.Offline, 0));
            Assert.Equal("wstrzymane, mapa jest w tle", SourceStateText.DescribeAirspaceFeed(true, AirspaceFeedState.Stopped, 0));
        }
    }
}

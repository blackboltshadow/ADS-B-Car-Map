using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the store of the full flight paths.
    /// </summary>
    public class FlightTrackStoreTests
    {
        /// <summary>
        /// Moment from which the test paths start.
        /// </summary>
        private static readonly DateTime Start = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Source returning a prepared path or throwing a prepared exception, counting the requests.
        /// </summary>
        private sealed class StubSource : IFlightTrackSource
        {
            /// <summary>
            /// Path to return.
            /// </summary>
            public FlightTrack? Track { get; set; }

            /// <summary>
            /// Exception to throw instead of returning the path.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Task to wait for before answering, or null to answer at once.
            /// </summary>
            public Task? Gate { get; set; }

            /// <summary>
            /// Number of requests.
            /// </summary>
            public int Requests { get; private set; }

            /// <inheritdoc />
            public string Name => "stub";

            /// <inheritdoc />
            public async Task<FlightTrack?> GetTrackAsync(string hex, CancellationToken cancellationToken)
            {
                Requests++;

                if (Gate != null)
                {
                    await Gate;
                }

                if (Failure != null)
                {
                    throw Failure;
                }

                return Track;
            }
        }

        /// <summary>
        /// Creates a path of consecutive points one minute apart.
        /// </summary>
        /// <param name="count">Number of points.</param>
        /// <returns>The path.</returns>
        private static FlightTrack CreateTrack(int count)
        {
            List<TrackPoint> points = new List<TrackPoint>();

            for (int index = 0; index < count; index++)
            {
                points.Add(new TrackPoint(50.0 + index * 0.001, 20.0, 1000 + index, false, Start.AddMinutes(index)));
            }

            return new FlightTrack("abc123", points);
        }

        /// <summary>
        /// Creates a trail point at the given time.
        /// </summary>
        /// <param name="minutes">Minutes after <see cref="Start"/>.</param>
        /// <returns>The point.</returns>
        private static TrailPoint Local(int minutes)
        {
            return new TrailPoint(60.0, 30.0, 5000, false, Start.AddMinutes(minutes));
        }

        /// <summary>
        /// Without a downloaded path the local trail is returned unchanged.
        /// </summary>
        [Fact]
        public void Compose_NothingDownloaded_ReturnsLocal()
        {
            FlightTrackStore store = new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60));
            TrailPoint[] local = new TrailPoint[] { Local(1) };

            Assert.Same(local, store.Compose("abc123", local));
        }

        /// <summary>
        /// The downloaded path is followed only by the local points that are newer than its end.
        /// </summary>
        [Fact]
        public async Task Compose_AppendsOnlyNewerLocalPoints()
        {
            StubSource source = new StubSource { Track = CreateTrack(10) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60));

            await store.RefreshAsync("abc123", CancellationToken.None);
            TrailPoint[] result = store.Compose("abc123", new TrailPoint[] { Local(8), Local(9), Local(10), Local(11) });

            Assert.Equal(12, result.Length);
            Assert.Equal(50.0, result[0].Latitude);
            Assert.Equal(60.0, result[10].Latitude);
            Assert.Equal(Start.AddMinutes(11), result[11].TimestampUtc);
        }

        /// <summary>
        /// A path with more points than the limit is thinned out evenly, keeping both ends.
        /// </summary>
        [Fact]
        public async Task Refresh_LongPath_IsThinnedKeepingEnds()
        {
            StubSource source = new StubSource { Track = CreateTrack(2000) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60));

            await store.RefreshAsync("abc123", CancellationToken.None);
            TrailPoint[] result = store.Compose("abc123", Array.Empty<TrailPoint>());

            Assert.Equal(FlightTrackStore.MaxPoints, result.Length);
            Assert.Equal(Start, result[0].TimestampUtc);
            Assert.Equal(Start.AddMinutes(1999), result[result.Length - 1].TimestampUtc);
            Assert.True(result.Zip(result.Skip(1), (a, b) => a.TimestampUtc < b.TimestampUtc).All(ordered => ordered));
        }

        /// <summary>
        /// The same aircraft is not requested again before the refresh interval passes.
        /// </summary>
        [Fact]
        public async Task Refresh_WithinInterval_DoesNotRequestAgain()
        {
            DateTime now = Start;
            StubSource source = new StubSource { Track = CreateTrack(3) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60), () => now);

            await store.RefreshAsync("abc123", CancellationToken.None);
            now = now.AddSeconds(30);
            await store.RefreshAsync("abc123", CancellationToken.None);
            now = now.AddSeconds(31);
            await store.RefreshAsync("abc123", CancellationToken.None);

            Assert.Equal(2, source.Requests);
        }

        /// <summary>
        /// A failed request is not repeated before the interval passes and keeps the previously downloaded path.
        /// </summary>
        [Fact]
        public async Task Refresh_Failure_KeepsPreviousPathAndWaits()
        {
            DateTime now = Start;
            StubSource source = new StubSource { Track = CreateTrack(3) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60), () => now);

            await store.RefreshAsync("abc123", CancellationToken.None);
            now = now.AddSeconds(61);
            source.Failure = new HttpRequestException("boom");

            await Assert.ThrowsAsync<HttpRequestException>(() => store.RefreshAsync("abc123", CancellationToken.None));
            await store.RefreshAsync("abc123", CancellationToken.None);

            Assert.Equal(2, source.Requests);
            Assert.Equal(3, store.Compose("abc123", Array.Empty<TrailPoint>()).Length);
        }

        /// <summary>
        /// A request is not started while another one for the same aircraft is in progress.
        /// </summary>
        [Fact]
        public async Task Refresh_WhileLoading_DoesNotRequestAgain()
        {
            TaskCompletionSource gate = new TaskCompletionSource();
            StubSource source = new StubSource { Track = CreateTrack(3), Gate = gate.Task };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.Zero);

            Task first = store.RefreshAsync("abc123", CancellationToken.None);
            await store.RefreshAsync("abc123", CancellationToken.None);
            gate.SetResult();
            await first;

            Assert.Equal(1, source.Requests);
        }

        /// <summary>
        /// An aircraft without a recording gives the local trail and a request is made.
        /// </summary>
        [Fact]
        public async Task Refresh_NoRecording_KeepsLocalTrail()
        {
            StubSource source = new StubSource { Track = null };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60));
            TrailPoint[] local = new TrailPoint[] { Local(1) };

            await store.RefreshAsync("abc123", CancellationToken.None);

            Assert.Same(local, store.Compose("abc123", local));
            Assert.Equal(1, source.Requests);
        }

        /// <summary>
        /// Only the limited number of paths is remembered and the oldest request is dropped first.
        /// </summary>
        [Fact]
        public async Task Refresh_TooManyAircraft_DropsLeastRecentlyRequested()
        {
            DateTime now = Start;
            StubSource source = new StubSource { Track = CreateTrack(3) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60), () => now);

            for (int index = 0; index <= FlightTrackStore.MaxEntries; index++)
            {
                await store.RefreshAsync("hex" + index, CancellationToken.None);
                now = now.AddSeconds(1);
            }

            Assert.Equal(3, store.Compose("hex" + FlightTrackStore.MaxEntries, Array.Empty<TrailPoint>()).Length);
            Assert.Empty(store.Compose("hex0", Array.Empty<TrailPoint>()));
            Assert.Equal(3, store.Compose("hex1", Array.Empty<TrailPoint>()).Length);
        }

        /// <summary>
        /// Clearing forgets the downloaded paths.
        /// </summary>
        [Fact]
        public async Task Clear_ForgetsPaths()
        {
            StubSource source = new StubSource { Track = CreateTrack(3) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60));

            await store.RefreshAsync("abc123", CancellationToken.None);
            store.Clear();

            Assert.Empty(store.Compose("abc123", Array.Empty<TrailPoint>()));
        }
    }
}

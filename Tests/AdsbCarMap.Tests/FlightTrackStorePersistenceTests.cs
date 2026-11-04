using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of keeping the flight paths in a file and of the landing detection.
    /// </summary>
    public class FlightTrackStorePersistenceTests
    {
        /// <summary>
        /// Moment from which the test paths start.
        /// </summary>
        private static readonly DateTime Start = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Source returning a prepared path and counting the requests.
        /// </summary>
        private sealed class StubSource : IFlightTrackSource
        {
            /// <summary>
            /// Path to return.
            /// </summary>
            public FlightTrack? Track { get; set; }

            /// <summary>
            /// Number of requests.
            /// </summary>
            public int Requests { get; private set; }

            /// <inheritdoc />
            public string Name => "stub";

            /// <inheritdoc />
            public Task<FlightTrack?> GetTrackAsync(string hex, CancellationToken cancellationToken)
            {
                Requests++;

                return Task.FromResult(Track);
            }
        }

        /// <summary>
        /// Creates a path that takes off and flies, and optionally lands, one point per minute.
        /// </summary>
        /// <param name="landed">Whether the path ends on the ground.</param>
        /// <returns>The path.</returns>
        private static FlightTrack CreateFlight(bool landed)
        {
            List<TrackPoint> points = new List<TrackPoint>
            {
                new TrackPoint(50.0, 20.0, null, true, Start),
                new TrackPoint(50.1, 20.1, 3000, false, Start.AddMinutes(1)),
                new TrackPoint(50.2, 20.2, 30000, false, Start.AddMinutes(2)),
            };

            if (landed)
            {
                points.Add(new TrackPoint(50.3, 20.3, null, true, Start.AddMinutes(3)));
            }

            return new FlightTrack("abc123", points);
        }

        /// <summary>
        /// Creates the path of a flight that took off hours after <see cref="CreateFlight"/>.
        /// </summary>
        /// <returns>The path.</returns>
        private static FlightTrack CreateNextFlight()
        {
            return new FlightTrack("abc123", new List<TrackPoint>
            {
                new TrackPoint(51.0, 21.0, null, true, Start.AddHours(5)),
                new TrackPoint(51.1, 21.1, 2000, false, Start.AddHours(5).AddMinutes(1)),
            });
        }

        /// <summary>
        /// Creates a location of the path file in a new temporary directory.
        /// </summary>
        /// <returns>Path of the file, which does not exist yet.</returns>
        private static string CreateFilePath()
        {
            return Path.Combine(Path.GetTempPath(), "adsbcarmap-tracks-" + Guid.NewGuid().ToString("N"), "tracks.json");
        }

        /// <summary>
        /// Counts the points of the path kept for an aircraft.
        /// </summary>
        /// <param name="store">Store to read.</param>
        /// <returns>Number of points.</returns>
        private static int Count(FlightTrackStore store)
        {
            return store.Compose("abc123", Array.Empty<TrailPoint>()).Length;
        }

        /// <summary>
        /// A flight has landed only when it was in the air and the path ends on the ground.
        /// </summary>
        [Fact]
        public void IsLanded_RecognizesLandingOnly()
        {
            Assert.True(FlightTrackStore.IsLanded(FlightTrackStore.Thin(CreateFlight(true).Points)));
            Assert.False(FlightTrackStore.IsLanded(FlightTrackStore.Thin(CreateFlight(false).Points)));
            Assert.False(FlightTrackStore.IsLanded(new TrailPoint[] { new TrailPoint(1, 1, null, true, Start), new TrailPoint(1, 1, null, true, Start.AddMinutes(1)) }));
            Assert.False(FlightTrackStore.IsLanded(Array.Empty<TrailPoint>()));
        }

        /// <summary>
        /// A downloaded path is read back from the file by a new store.
        /// </summary>
        [Fact]
        public async Task Refresh_SavesPathToFile_AndNewStoreReadsIt()
        {
            string file = CreateFilePath();
            FlightTrackStore first = new FlightTrackStore(new StubSource { Track = CreateFlight(false) }, TimeSpan.FromSeconds(60), () => Start.AddMinutes(10), file);

            await first.RefreshAsync("abc123", true, CancellationToken.None);
            FlightTrackStore second = new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start.AddMinutes(11), file);

            Assert.Equal(3, Count(second));
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }

        /// <summary>
        /// A path of a flight in progress is dropped after twelve hours without a download, and a landed one after a day.
        /// </summary>
        [Fact]
        public async Task Load_DropsExpiredPaths()
        {
            string file = CreateFilePath();
            FlightTrackStore inFlight = new FlightTrackStore(new StubSource { Track = CreateFlight(false) }, TimeSpan.FromSeconds(60), () => Start, file);
            await inFlight.RefreshAsync("abc123", true, CancellationToken.None);

            Assert.Equal(3, Count(new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start.AddHours(11), file)));
            Assert.Equal(0, Count(new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start.AddHours(13), file)));

            FlightTrackStore landed = new FlightTrackStore(new StubSource { Track = CreateFlight(true) }, TimeSpan.FromSeconds(60), () => Start, file);
            await landed.RefreshAsync("abc123", true, CancellationToken.None);

            Assert.Equal(4, Count(new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start.AddHours(20), file)));
            Assert.Equal(0, Count(new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start.AddHours(25), file)));
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }

        /// <summary>
        /// A damaged file is ignored and reported.
        /// </summary>
        [Fact]
        public void Load_DamagedFile_IsReportedAndIgnored()
        {
            string file = CreateFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{not json");
            List<string> errors = new List<string>();

            FlightTrackStore store = new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), null, file, (message, exception) => errors.Add(message));

            Assert.Single(errors);
            Assert.Equal(0, Count(store));
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }

        /// <summary>
        /// A landed flight is not downloaded again while the aircraft stands, but it is when the aircraft flies, and the new flight replaces the old path.
        /// </summary>
        [Fact]
        public async Task Refresh_LandedFlight_WaitsUntilAircraftFliesAndThenReplacesPath()
        {
            DateTime now = Start;
            StubSource source = new StubSource { Track = CreateFlight(true) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60), () => now);

            await store.RefreshAsync("abc123", true, CancellationToken.None);
            now = now.AddMinutes(5);
            await store.RefreshAsync("abc123", false, CancellationToken.None);
            Assert.Equal(1, source.Requests);

            source.Track = CreateNextFlight();
            await store.RefreshAsync("abc123", true, CancellationToken.None);

            TrailPoint[] result = store.Compose("abc123", Array.Empty<TrailPoint>());
            Assert.Equal(2, source.Requests);
            Assert.Equal(2, result.Length);
            Assert.Equal(51.0, result[0].Latitude);
        }

        /// <summary>
        /// A landed flight is checked for the next one after the long interval even when the aircraft is not seen flying.
        /// </summary>
        [Fact]
        public async Task Refresh_LandedFlight_ChecksAgainAfterLongInterval()
        {
            DateTime now = Start;
            StubSource source = new StubSource { Track = CreateFlight(true) };
            FlightTrackStore store = new FlightTrackStore(source, TimeSpan.FromSeconds(60), () => now);

            await store.RefreshAsync("abc123", false, CancellationToken.None);
            now = now.Add(FlightTrackStore.LandedRefreshInterval).AddSeconds(1);
            await store.RefreshAsync("abc123", false, CancellationToken.None);

            Assert.Equal(2, source.Requests);
        }

        /// <summary>
        /// Clearing removes the paths from the file too.
        /// </summary>
        [Fact]
        public async Task Clear_RemovesPathsFromFile()
        {
            string file = CreateFilePath();
            FlightTrackStore store = new FlightTrackStore(new StubSource { Track = CreateFlight(false) }, TimeSpan.FromSeconds(60), () => Start, file);

            await store.RefreshAsync("abc123", true, CancellationToken.None);
            store.Clear();

            Assert.Equal(0, Count(new FlightTrackStore(new StubSource(), TimeSpan.FromSeconds(60), () => Start, file)));
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }
    }
}

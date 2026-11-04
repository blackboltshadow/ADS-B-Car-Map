using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the pause of the flight path source after limit responses.
    /// </summary>
    public class RateLimitedTrackSourceTests
    {
        /// <summary>
        /// Source throwing a prepared exception or returning a path, counting the requests.
        /// </summary>
        private sealed class StubSource : IFlightTrackSource
        {
            /// <summary>
            /// Exception to throw, or null to return a path.
            /// </summary>
            public Exception? Failure { get; set; }

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

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult<FlightTrack?>(new FlightTrack(hex, new List<TrackPoint>()));
            }
        }

        /// <summary>
        /// A successful request returns the path of the wrapped source.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_Success_ReturnsTrack()
        {
            RateLimitedTrackSource source = new RateLimitedTrackSource(new StubSource());

            FlightTrack? track = await source.GetTrackAsync("abc123", CancellationToken.None);

            Assert.Equal("abc123", track!.Hex);
            Assert.Equal("stub", source.Name);
        }

        /// <summary>
        /// After a limit response the next requests are refused without asking the wrapped source.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_LimitResponse_PausesSource()
        {
            StubSource inner = new StubSource { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            RateLimitedTrackSource source = new RateLimitedTrackSource(inner);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetTrackAsync("abc123", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetTrackAsync("abc123", CancellationToken.None));

            Assert.Equal(1, inner.Requests);
        }

        /// <summary>
        /// A network failure is passed on to the caller.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_NetworkFailure_IsRethrown()
        {
            RateLimitedTrackSource source = new RateLimitedTrackSource(new StubSource { Failure = new HttpRequestException("boom") });

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetTrackAsync("abc123", CancellationToken.None));
        }
    }
}

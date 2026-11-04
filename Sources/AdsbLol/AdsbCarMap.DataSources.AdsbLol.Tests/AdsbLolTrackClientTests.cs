using System.Net;
using AdsbCarMap.DataSources.AdsbLol;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AdsbLol.Tests
{
    /// <summary>
    /// Tests of the adsb.lol flight path client.
    /// </summary>
    public class AdsbLolTrackClientTests
    {
        /// <summary>
        /// Trace file with two flights separated by the new flight flag; the second one starts on the ground.
        /// </summary>
        private const string TwoFlights = "{\"icao\":\"ae145e\",\"timestamp\":1000000000.5,\"trace\":["
            + "[0.0,50.0,20.0,35000,450.0,90.0,0,0,null],"
            + "[60.0,50.1,20.1,34000,450.0,90.0,0,0,null],"
            + "[600.0,52.0,21.0,\"ground\",5.0,10.0,2,0,null],"
            + "[660.0,52.0,21.1,2500,150.0,10.0,0,1500,null]]}";

        /// <summary>
        /// Handler returning a fixed status and body and remembering the requested path.
        /// </summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            /// <summary>
            /// Status code to return.
            /// </summary>
            private readonly HttpStatusCode status;

            /// <summary>
            /// Response body.
            /// </summary>
            private readonly string body;

            /// <summary>
            /// Creates the handler.
            /// </summary>
            /// <param name="status">Status code to return.</param>
            /// <param name="body">Response body.</param>
            public StubHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            /// <summary>
            /// Path of the last request.
            /// </summary>
            public string? LastPath { get; private set; }

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastPath = request.RequestUri?.AbsolutePath;

                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
            }
        }

        /// <summary>
        /// Requests a path through a stub handler.
        /// </summary>
        /// <param name="status">Status code to return.</param>
        /// <param name="body">Response body.</param>
        /// <param name="handler">Handler used, to inspect the request.</param>
        /// <returns>The path or null.</returns>
        private static async Task<FlightTrack?> GetAsync(HttpStatusCode status, string body, StubHandler? handler = null)
        {
            using HttpClient http = new HttpClient(handler ?? new StubHandler(status, body)) { BaseAddress = new Uri(AdsbLolTrackClient.DefaultBaseAddress) };
            AdsbLolTrackClient client = new AdsbLolTrackClient(http);

            return await client.GetTrackAsync("AE145E", CancellationToken.None);
        }

        /// <summary>
        /// Only the last flight is returned, with the ground flag, altitude and time converted.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_CutsOutLastFlight()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, TwoFlights);

            FlightTrack? track = await GetAsync(HttpStatusCode.OK, TwoFlights, handler);

            Assert.NotNull(track);
            Assert.Equal("ae145e", track!.Hex);
            Assert.Equal(2, track.Points.Count);
            Assert.True(track.Points[0].OnGround);
            Assert.Null(track.Points[0].AltitudeFeet);
            Assert.False(track.Points[1].OnGround);
            Assert.Equal(2500, track.Points[1].AltitudeFeet);
            Assert.Equal(52.0, track.Points[1].Latitude);
            Assert.Equal(21.1, track.Points[1].Longitude);
            Assert.Equal(DateTime.UnixEpoch.AddSeconds(1000000660.5), track.Points[1].TimestampUtc);
        }

        /// <summary>
        /// The request goes to the file named with the last two characters of the address.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_RequestsTraceFile()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, TwoFlights);

            await GetAsync(HttpStatusCode.OK, TwoFlights, handler);

            Assert.Equal("/data/traces/5e/trace_full_ae145e.json", handler.LastPath);
        }

        /// <summary>
        /// A long silence starts a new flight even without the flag.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_LongGapStartsNewFlight()
        {
            string body = "{\"timestamp\":1000000000,\"trace\":[[0,50.0,20.0,35000,1,1,0,0],[10,50.1,20.0,35000,1,1,0,0],[7200,51.0,20.0,3000,1,1,0,0]]}";

            FlightTrack? track = await GetAsync(HttpStatusCode.OK, body);

            Assert.Single(track!.Points);
            Assert.Equal(51.0, track.Points[0].Latitude);
        }

        /// <summary>
        /// Rows without a position are skipped and a file without usable rows gives no path.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_SkipsInvalidRows()
        {
            string body = "{\"timestamp\":1000000000,\"trace\":[[0,null,20.0,100],\"x\",[1,50.0],[5,50.0,20.0,null,1,1,0,0]]}";

            FlightTrack? track = await GetAsync(HttpStatusCode.OK, body);

            Assert.Single(track!.Points);
            Assert.Null(track.Points[0].AltitudeFeet);
            Assert.False(track.Points[0].OnGround);
            Assert.Null(await GetAsync(HttpStatusCode.OK, "{\"timestamp\":1,\"trace\":[]}"));
            Assert.Null(await GetAsync(HttpStatusCode.OK, "[]"));
            Assert.Null(await GetAsync(HttpStatusCode.OK, "{\"trace\":[]}"));
        }

        /// <summary>
        /// A missing file means the source has no recording of the aircraft.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_NotFound_ReturnsNull()
        {
            Assert.Null(await GetAsync(HttpStatusCode.NotFound, string.Empty));
        }

        /// <summary>
        /// A limit response is reported with the pause requested by the server.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_TooManyRequests_ThrowsRateLimited()
        {
            await Assert.ThrowsAsync<RateLimitedException>(() => GetAsync(HttpStatusCode.TooManyRequests, string.Empty));
        }

        /// <summary>
        /// Other failure codes are reported as network errors.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_ServerError_ThrowsHttpRequestException()
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(HttpStatusCode.InternalServerError, string.Empty));
        }

        /// <summary>
        /// An address that is too short gives no path and no request.
        /// </summary>
        [Fact]
        public async Task GetTrackAsync_ShortHex_ReturnsNull()
        {
            using HttpClient http = new HttpClient(new StubHandler(HttpStatusCode.OK, TwoFlights)) { BaseAddress = new Uri(AdsbLolTrackClient.DefaultBaseAddress) };

            Assert.Null(await new AdsbLolTrackClient(http).GetTrackAsync(" a ", CancellationToken.None));
        }
    }
}

using System.Net;
using AdsbCarMap.DataSources.AdsbLol;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AdsbLol.Tests
{
    /// <summary>
    /// Tests of the adsb.lol route client.
    /// </summary>
    public class AdsbLolRouteClientTests
    {
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
        /// Requests a route through a stub handler.
        /// </summary>
        /// <param name="status">Status code to return.</param>
        /// <param name="body">Response body.</param>
        /// <param name="handler">Handler used, to inspect the request.</param>
        /// <returns>The route or null.</returns>
        private static async Task<FlightRoute?> GetAsync(HttpStatusCode status, string body, StubHandler? handler = null)
        {
            AdsbLolRouteClient client = new AdsbLolRouteClient(new HttpClient(handler ?? new StubHandler(status, body)) { BaseAddress = new Uri("https://example.test/") });

            return await client.GetRouteAsync("LOT281", CancellationToken.None);
        }

        /// <summary>
        /// The airports with their codes, cities and coordinates become the route.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_FullResponse_ParsesRoute()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, "{\"callsign\":\"LOT281\",\"airline_code\":\"LOT\",\"airport_codes\":\"EPWA-EGLL\",\"_airports\":[{\"name\":\"Warsaw Chopin Airport\",\"icao\":\"EPWA\",\"iata\":\"WAW\",\"location\":\"Warsaw\",\"lat\":52.165699,\"lon\":20.9671},{\"name\":\"London Heathrow Airport\",\"icao\":\"EGLL\",\"iata\":\"LHR\",\"location\":\"London\",\"lat\":51.4706,\"lon\":-0.461941}]}");

            FlightRoute? route = await GetAsync(HttpStatusCode.OK, string.Empty, handler);

            Assert.NotNull(route);
            Assert.Equal("WAW", route.OriginCode);
            Assert.Equal("Warsaw", route.OriginCity);
            Assert.Equal("LHR", route.DestinationCode);
            Assert.Equal("London", route.DestinationCity);
            Assert.Null(route.AirlineName);
            Assert.True(route.HasCoordinates);
            Assert.Equal(51.4706, route.DestinationLatitude);
            Assert.Equal("/api/0/route/LOT281", handler.LastPath);
        }

        /// <summary>
        /// A route with stops goes from the first to the last airport and the ICAO code is used when there is no IATA code.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_StopsAndMissingIata_UseFirstAndLastWithIcaoFallback()
        {
            FlightRoute? route = await GetAsync(HttpStatusCode.OK, "{\"_airports\":[{\"icao\":\"EPXX\",\"iata\":\"\",\"location\":\"\"},{\"icao\":\"EDDF\",\"iata\":\"FRA\"},{\"icao\":\"KJFK\",\"iata\":\"JFK\",\"location\":\"New York\"}]}");

            Assert.NotNull(route);
            Assert.Equal("EPXX", route.OriginCode);
            Assert.Null(route.OriginCity);
            Assert.Equal("JFK", route.DestinationCode);
            Assert.False(route.HasCoordinates);
        }

        /// <summary>
        /// A missing airport list, a single airport and an airport without a code give null.
        /// </summary>
        [Theory]
        [InlineData("{}")]
        [InlineData("{\"_airports\":[]}")]
        [InlineData("{\"_airports\":[{\"icao\":\"EPWA\"}]}")]
        [InlineData("{\"_airports\":[{\"name\":\"x\"},{\"icao\":\"EGLL\"}]}")]
        [InlineData("[1,2]")]
        public async Task GetRouteAsync_UnusableBody_ReturnsNull(string body)
        {
            Assert.Null(await GetAsync(HttpStatusCode.OK, body));
        }

        /// <summary>
        /// A 404 answer means that the flight is unknown.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_NotFound_ReturnsNull()
        {
            Assert.Null(await GetAsync(HttpStatusCode.NotFound, "{}"));
        }

        /// <summary>
        /// A 429 answer is reported as RateLimitedException and a server error as HttpRequestException.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_Errors_AreReported()
        {
            await Assert.ThrowsAsync<RateLimitedException>(() => GetAsync(HttpStatusCode.TooManyRequests, string.Empty));
            await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(HttpStatusCode.InternalServerError, string.Empty));
        }

        /// <summary>
        /// The client reports the name of the service.
        /// </summary>
        [Fact]
        public void Client_ReportsName()
        {
            Assert.Equal("adsb.lol", new AdsbLolRouteClient(new HttpClient()).Name);
        }
    }
}

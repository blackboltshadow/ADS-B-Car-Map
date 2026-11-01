using System.Net;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the adsbdb route client and the flight route model.
    /// </summary>
    public class RouteClientTests
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
            /// Path and query of the last request.
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
        /// <param name="callsign">Callsign to ask for.</param>
        /// <returns>The route or null.</returns>
        private static async Task<FlightRoute?> GetAsync(HttpStatusCode status, string body, string callsign = "LOT1")
        {
            RouteClient client = new RouteClient(new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("https://example.test/") });

            return await client.GetRouteAsync(callsign, CancellationToken.None);
        }

        /// <summary>
        /// A complete response gives codes, cities, airline and coordinates.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_FullResponse_ParsesRoute()
        {
            FlightRoute? route = await GetAsync(HttpStatusCode.OK, "{\"response\":{\"flightroute\":{\"airline\":{\"name\":\"LOT Polish Airlines\"},\"origin\":{\"iata_code\":\"WAW\",\"icao_code\":\"EPWA\",\"municipality\":\"Warsaw\",\"latitude\":52.16,\"longitude\":20.97},\"destination\":{\"iata_code\":\"JFK\",\"icao_code\":\"KJFK\",\"municipality\":\"New York\",\"latitude\":40.64,\"longitude\":-73.78}}}}");

            Assert.NotNull(route);
            Assert.Equal("WAW", route.OriginCode);
            Assert.Equal("Warsaw", route.OriginCity);
            Assert.Equal("JFK", route.DestinationCode);
            Assert.Equal("New York", route.DestinationCity);
            Assert.Equal("LOT Polish Airlines", route.AirlineName);
            Assert.True(route.HasCoordinates);
            Assert.Equal(52.16, route.OriginLatitude);
            Assert.Equal(-73.78, route.DestinationLongitude);
            Assert.Equal("WAW→JFK", route.ToShortString());
        }

        /// <summary>
        /// The ICAO code is used when the IATA code is missing or empty.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_MissingIata_FallsBackToIcao()
        {
            FlightRoute? route = await GetAsync(HttpStatusCode.OK, "{\"response\":{\"flightroute\":{\"origin\":{\"iata_code\":\"\",\"icao_code\":\"EPMB\"},\"destination\":{\"icao_code\":\"EPWA\"}}}}");

            Assert.NotNull(route);
            Assert.Equal("EPMB", route.OriginCode);
            Assert.Equal("EPWA", route.DestinationCode);
            Assert.Null(route.AirlineName);
            Assert.False(route.HasCoordinates);
        }

        /// <summary>
        /// An empty airline name is normalized to null.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_BlankAirline_IsNull()
        {
            FlightRoute? route = await GetAsync(HttpStatusCode.OK, "{\"response\":{\"flightroute\":{\"airline\":{\"name\":\"  \"},\"origin\":{\"iata_code\":\"A\"},\"destination\":{\"iata_code\":\"B\"}}}}");

            Assert.NotNull(route);
            Assert.Null(route.AirlineName);
        }

        /// <summary>
        /// A 404 means there is no known route.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_NotFound_ReturnsNull()
        {
            Assert.Null(await GetAsync(HttpStatusCode.NotFound, "{}"));
        }

        /// <summary>
        /// Responses without a usable route give null.
        /// </summary>
        [Theory]
        [InlineData("{}")]
        [InlineData("{\"response\":\"unknown callsign\"}")]
        [InlineData("{\"response\":{}}")]
        [InlineData("{\"response\":{\"flightroute\":null}}")]
        [InlineData("{\"response\":{\"flightroute\":{\"origin\":{\"iata_code\":\"WAW\"}}}}")]
        [InlineData("{\"response\":{\"flightroute\":{\"origin\":{},\"destination\":{\"iata_code\":\"WAW\"}}}}")]
        public async Task GetRouteAsync_UnusableBody_ReturnsNull(string body)
        {
            Assert.Null(await GetAsync(HttpStatusCode.OK, body));
        }

        /// <summary>
        /// Server errors are thrown to the caller.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_ServerError_Throws()
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(HttpStatusCode.InternalServerError, string.Empty));
        }

        /// <summary>
        /// The callsign is escaped in the request path.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_EscapesCallsign()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.NotFound, "{}");
            RouteClient client = new RouteClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

            await client.GetRouteAsync("A B/C", CancellationToken.None);

            Assert.Equal("/v0/callsign/A%20B%2FC", handler.LastPath);
        }

        /// <summary>
        /// Coordinates are reported as present only when all four are known.
        /// </summary>
        [Fact]
        public void HasCoordinates_RequiresAllFour()
        {
            FlightRoute partial = new FlightRoute("A", null, "B", null, null, 1.0, 2.0, 3.0, null);
            FlightRoute full = new FlightRoute("A", null, "B", null, null, 1.0, 2.0, 3.0, 4.0);

            Assert.False(partial.HasCoordinates);
            Assert.True(full.HasCoordinates);
        }
    }
}

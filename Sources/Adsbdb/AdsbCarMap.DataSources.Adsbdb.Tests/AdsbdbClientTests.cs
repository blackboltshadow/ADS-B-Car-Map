using AdsbCarMap.DataSources.Models;
using System.Net;
using AdsbCarMap.DataSources.Adsbdb;
using Xunit;

namespace AdsbCarMap.DataSources.Adsbdb.Tests
{
    /// <summary>
    /// Tests of the adsbdb route client and the flight route model.
    /// </summary>
    public class AdsbdbClientTests
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
            AdsbdbClient client = new AdsbdbClient(new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("https://example.test/") });

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
            AdsbdbClient client = new AdsbdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

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

        /// <summary>
        /// Requests the data of an aircraft through a stub handler.
        /// </summary>
        /// <param name="status">Status code to return.</param>
        /// <param name="body">Response body.</param>
        /// <param name="handler">Handler used, to inspect the request.</param>
        /// <returns>The details or null.</returns>
        private static async Task<AircraftDetails?> GetDetailsAsync(HttpStatusCode status, string body, StubHandler? handler = null)
        {
            AdsbdbClient client = new AdsbdbClient(new HttpClient(handler ?? new StubHandler(status, body)) { BaseAddress = new Uri("https://example.test/") });

            return await client.GetAircraftDetailsAsync("48c9f3", CancellationToken.None);
        }

        /// <summary>
        /// The data of an aircraft gives the full type name, the type code, the manufacturer, the registration and the owner.
        /// </summary>
        [Fact]
        public async Task GetAircraftDetailsAsync_FullResponse_ParsesDetails()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, "{\"response\":{\"aircraft\":{\"type\":\"King Air B350\",\"icao_type\":\"B350\",\"manufacturer\":\"Beechcraft Corp\",\"mode_s\":\"48C9F3\",\"registration\":\"SP-TPU\",\"registered_owner\":\"Polish Air Navigation Services Agency\"}}}");

            AircraftDetails? details = await GetDetailsAsync(HttpStatusCode.OK, string.Empty, handler);

            Assert.NotNull(details);
            Assert.Equal("King Air B350", details.TypeName);
            Assert.Equal("B350", details.TypeCode);
            Assert.Equal("Beechcraft Corp", details.Manufacturer);
            Assert.Equal("SP-TPU", details.Registration);
            Assert.Equal("Polish Air Navigation Services Agency", details.Owner);
            Assert.Null(details.Category);
            Assert.Equal("/v0/aircraft/48c9f3", handler.LastPath);
        }

        /// <summary>
        /// Blank fields become null, an unknown aircraft gives null and an unusable body gives null.
        /// </summary>
        [Fact]
        public async Task GetAircraftDetailsAsync_BlankUnknownAndUnusable()
        {
            AircraftDetails? blank = await GetDetailsAsync(HttpStatusCode.OK, "{\"response\":{\"aircraft\":{\"type\":\"  \",\"icao_type\":\"\",\"registration\":\"N1\"}}}");

            Assert.NotNull(blank);
            Assert.Null(blank.TypeName);
            Assert.Null(blank.TypeCode);
            Assert.Equal("N1", blank.Registration);
            Assert.Null(await GetDetailsAsync(HttpStatusCode.NotFound, "{\"response\":\"unknown aircraft\"}"));
            Assert.Null(await GetDetailsAsync(HttpStatusCode.OK, "{\"response\":\"unknown aircraft\"}"));
        }

        /// <summary>
        /// A 429 answer is reported as RateLimitedException for the route and for the aircraft data.
        /// </summary>
        [Fact]
        public async Task TooManyRequests_ThrowsRateLimited()
        {
            AdsbdbClient client = new AdsbdbClient(new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, string.Empty)) { BaseAddress = new Uri("https://example.test/") });

            await Assert.ThrowsAsync<RateLimitedException>(() => client.GetRouteAsync("LOT1", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => client.GetAircraftDetailsAsync("48c9f3", CancellationToken.None));
            Assert.Equal("adsbdb.com", client.Name);
        }
    }
}

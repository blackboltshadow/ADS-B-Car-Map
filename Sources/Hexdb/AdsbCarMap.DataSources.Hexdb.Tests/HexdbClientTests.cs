using System.Net;
using AdsbCarMap.DataSources.Hexdb;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Hexdb.Tests
{
    /// <summary>
    /// Tests of the hexdb.io client.
    /// </summary>
    public class HexdbClientTests
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
        /// Creates a client over a stub handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <returns>The client.</returns>
        private static HexdbClient CreateClient(StubHandler handler)
        {
            return new HexdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// The pair of ICAO codes becomes the route, without names or coordinates.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_ParsesPairOfCodes()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, "{\"flight\": \"LOT281\", \"route\": \"EPWA-EGLL\", \"updatetime\": 1331463368}");

            FlightRoute? route = await CreateClient(handler).GetRouteAsync("LOT281", CancellationToken.None);

            Assert.NotNull(route);
            Assert.Equal("EPWA", route.OriginCode);
            Assert.Equal("EGLL", route.DestinationCode);
            Assert.Null(route.OriginCity);
            Assert.False(route.HasCoordinates);
            Assert.Equal("/api/v1/route/icao/LOT281", handler.LastPath);
        }

        /// <summary>
        /// A route with stops goes from the first to the last airport, and unusable routes give null.
        /// </summary>
        [Theory]
        [InlineData("{\"route\": \"EPWA-EDDF-KJFK\"}", "EPWA", "KJFK")]
        [InlineData("{\"route\": \"EPWA\"}", null, null)]
        [InlineData("{\"route\": \"\"}", null, null)]
        [InlineData("{}", null, null)]
        [InlineData("[]", null, null)]
        public async Task GetRouteAsync_StopsAndUnusableRoutes(string body, string? origin, string? destination)
        {
            FlightRoute? route = await CreateClient(new StubHandler(HttpStatusCode.OK, body)).GetRouteAsync("LOT281", CancellationToken.None);

            Assert.Equal(origin, route?.OriginCode);
            Assert.Equal(destination, route?.DestinationCode);
        }

        /// <summary>
        /// The data of an aircraft gives the type, the manufacturer, the registration and the owner.
        /// </summary>
        [Fact]
        public async Task GetAircraftDetailsAsync_ParsesDetails()
        {
            StubHandler handler = new StubHandler(HttpStatusCode.OK, "{\"ModeS\": \"48C9F3\", \"Registration\": \"SP-TPU\", \"Manufacturer\": \"Beechcraft Corp\", \"ICAOTypeCode\": \"B350\", \"Type\": \"King Air B350\", \"RegisteredOwners\": \"Polish Air Navigation Services Agency\", \"OperatorFlagCode\": \"PPU\"}");

            AircraftDetails? details = await CreateClient(handler).GetAircraftDetailsAsync("48c9f3", CancellationToken.None);

            Assert.NotNull(details);
            Assert.Equal("B350", details.TypeCode);
            Assert.Equal("King Air B350", details.TypeName);
            Assert.Equal("SP-TPU", details.Registration);
            Assert.Equal("Beechcraft Corp", details.Manufacturer);
            Assert.Equal("Polish Air Navigation Services Agency", details.Owner);
            Assert.Equal("/api/v1/aircraft/48c9f3", handler.LastPath);
        }

        /// <summary>
        /// A 404 answer means that the item is unknown, for both kinds of request.
        /// </summary>
        [Fact]
        public async Task NotFound_ReturnsNull()
        {
            HexdbClient client = CreateClient(new StubHandler(HttpStatusCode.NotFound, "{\"status\":\"404\",\"error\":\"Aircraft not found.\"}"));

            Assert.Null(await client.GetRouteAsync("XXX1", CancellationToken.None));
            Assert.Null(await client.GetAircraftDetailsAsync("000000", CancellationToken.None));
        }

        /// <summary>
        /// A 429 answer is reported as RateLimitedException and a server error as HttpRequestException.
        /// </summary>
        [Fact]
        public async Task Errors_AreReported()
        {
            HexdbClient limited = CreateClient(new StubHandler(HttpStatusCode.TooManyRequests, string.Empty));
            HexdbClient failing = CreateClient(new StubHandler(HttpStatusCode.InternalServerError, string.Empty));

            await Assert.ThrowsAsync<RateLimitedException>(() => limited.GetRouteAsync("LOT1", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => limited.GetAircraftDetailsAsync("48c9f3", CancellationToken.None));
            await Assert.ThrowsAsync<HttpRequestException>(() => failing.GetRouteAsync("LOT1", CancellationToken.None));
        }

        /// <summary>
        /// The client reports the name and the address of the service.
        /// </summary>
        [Fact]
        public void Client_ReportsNameAndAddress()
        {
            Assert.Equal("hexdb.io", new HexdbClient(new HttpClient()).Name);
            Assert.Equal("https://hexdb.io/", HexdbClient.DefaultBaseAddress);
        }
    }
}

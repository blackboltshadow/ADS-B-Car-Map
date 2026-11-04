using System.Net;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AdsbFi.Tests
{
    /// <summary>
    /// Tests of the request for aircraft by their ICAO addresses of the adsb.fi client.
    /// </summary>
    public class AdsbFiByHexTests
    {
        /// <summary>
        /// Handler that answers every request with one response and remembers the paths.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            /// <summary>
            /// Creates the response.
            /// </summary>
            private readonly Func<HttpResponseMessage> factory;

            /// <summary>
            /// Creates a handler.
            /// </summary>
            /// <param name="factory">Creates the response.</param>
            public FakeHandler(Func<HttpResponseMessage> factory)
            {
                this.factory = factory;
            }

            /// <summary>
            /// Paths and queries of the requests.
            /// </summary>
            public List<string> Paths { get; } = new List<string>();

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Paths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);

                return Task.FromResult(factory());
            }
        }

        /// <summary>
        /// Creates a client over a handler.
        /// </summary>
        /// <param name="handler">Handler to use.</param>
        /// <returns>The client.</returns>
        private static AdsbFiClient CreateClient(FakeHandler handler)
        {
            return new AdsbFiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// Creates a handler answering with a list of the given aircraft.
        /// </summary>
        /// <returns>The handler.</returns>
        private static FakeHandler CreateOkHandler()
        {
            string json = "{\"ac\":[{\"hex\":\"abc123\",\"lat\":50.0,\"lon\":20.0},{\"hex\":\"def456\"}],\"msg\":\"No error\"}";

            return new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }

        /// <summary>
        /// The addresses go into one request, lower-cased and without repeats, and aircraft without a position are skipped.
        /// </summary>
        [Fact]
        public async Task GetAircraftByHexAsync_AsksAllAddressesInOneRequest()
        {
            FakeHandler handler = CreateOkHandler();

            IReadOnlyList<Aircraft>? result = await CreateClient(handler).GetAircraftByHexAsync(new List<string> { "ABC123", " def456 ", "abc123" }, CancellationToken.None);

            Assert.Equal("/v2/hex/abc123,def456", Assert.Single(handler.Paths));
            Assert.Equal("abc123", Assert.Single(result!).Hex);
        }

        /// <summary>
        /// A long list is split into several requests.
        /// </summary>
        [Fact]
        public async Task GetAircraftByHexAsync_SplitsLongList()
        {
            FakeHandler handler = CreateOkHandler();
            List<string> hexes = Enumerable.Range(0, 250).Select(index => index.ToString("x6")).ToList();

            await CreateClient(handler).GetAircraftByHexAsync(hexes, CancellationToken.None);

            Assert.Equal(3, handler.Paths.Count);
        }

        /// <summary>
        /// An empty list makes no request.
        /// </summary>
        [Fact]
        public async Task GetAircraftByHexAsync_NoAddresses_MakesNoRequest()
        {
            FakeHandler handler = CreateOkHandler();

            IReadOnlyList<Aircraft>? result = await CreateClient(handler).GetAircraftByHexAsync(new List<string>(), CancellationToken.None);

            Assert.Empty(result!);
            Assert.Empty(handler.Paths);
        }

        /// <summary>
        /// A limit response is reported and another failure status is an error.
        /// </summary>
        [Fact]
        public async Task GetAircraftByHexAsync_FailureStatuses()
        {
            AdsbFiClient limited = CreateClient(new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
            AdsbFiClient broken = CreateClient(new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.BadGateway)));

            await Assert.ThrowsAsync<RateLimitedException>(() => limited.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
            await Assert.ThrowsAsync<HttpRequestException>(() => broken.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Pansa.Tests
{
    /// <summary>
    /// Tests of the client of the PAŻP airspace service.
    /// </summary>
    public class PansaClientTests
    {
        /// <summary>
        /// Map with one area, in the answer of the service.
        /// </summary>
        private const string OneZone = @"[{ ""properties"": { ""designator"": ""EPD1"", ""airspaceElementType"": ""D"" },
            ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[1,2],[3,4],[5,6]]] } }]";

        /// <summary>
        /// Handler that answers by path and remembers the requested paths.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            /// <summary>
            /// Creates the response for a path.
            /// </summary>
            private readonly Func<string, HttpResponseMessage> factory;

            /// <summary>
            /// Creates a handler.
            /// </summary>
            /// <param name="factory">Creates the response for a path.</param>
            public FakeHandler(Func<string, HttpResponseMessage> factory)
            {
                this.factory = factory;
            }

            /// <summary>
            /// Paths of the requests in the order they were made.
            /// </summary>
            public List<string> Paths { get; } = new List<string>();

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                Paths.Add(path);

                return Task.FromResult(factory(path));
            }
        }

        /// <summary>
        /// Creates a client over a handler.
        /// </summary>
        /// <param name="handler">Handler to use.</param>
        /// <returns>The client.</returns>
        private static PansaClient CreateClient(FakeHandler handler)
        {
            return new PansaClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// Creates a successful response with a text body.
        /// </summary>
        /// <param name="body">Body of the response.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage Ok(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }

        /// <summary>
        /// The client has the name shown to the user.
        /// </summary>
        [Fact]
        public void Name_IsPansa()
        {
            Assert.Equal("PAŻP", CreateClient(new FakeHandler(path => Ok("[]"))).Name);
        }

        /// <summary>
        /// The planned map is used when it has areas and the second one is not asked.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_UsesUupWhenItHasAreas()
        {
            FakeHandler handler = new FakeHandler(path => Ok(OneZone));

            IReadOnlyList<AirspaceZone> zones = await CreateClient(handler).GetZonesAsync(CancellationToken.None);

            Assert.Single(zones);
            Assert.Equal(new List<string> { "/map-configuration/uup" }, handler.Paths);
        }

        /// <summary>
        /// An empty planned map sends the client to the second map.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_FallsBackToAupWhenUupIsEmpty()
        {
            FakeHandler handler = new FakeHandler(path => Ok(path.EndsWith("uup", StringComparison.Ordinal) ? "[]" : OneZone));

            IReadOnlyList<AirspaceZone> zones = await CreateClient(handler).GetZonesAsync(CancellationToken.None);

            Assert.Single(zones);
            Assert.Equal(new List<string> { "/map-configuration/uup", "/map-configuration/aup" }, handler.Paths);
        }

        /// <summary>
        /// A server error or a damaged answer of the first map sends the client to the second map.
        /// </summary>
        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, "")]
        [InlineData(HttpStatusCode.OK, "<html>")]
        public async Task GetZonesAsync_FallsBackToAupOnFailure(HttpStatusCode status, string body)
        {
            FakeHandler handler = new FakeHandler(path => path.EndsWith("uup", StringComparison.Ordinal)
                ? new HttpResponseMessage(status) { Content = new StringContent(body) }
                : Ok(OneZone));

            IReadOnlyList<AirspaceZone> zones = await CreateClient(handler).GetZonesAsync(CancellationToken.None);

            Assert.Single(zones);
            Assert.Equal(2, handler.Paths.Count);
        }

        /// <summary>
        /// When both maps fail, the error of the second one reaches the caller.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_ThrowsWhenBothFail()
        {
            FakeHandler handler = new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.BadGateway));

            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetZonesAsync(CancellationToken.None));

            Assert.Equal(2, handler.Paths.Count);
        }

        /// <summary>
        /// A rate limit stops the client at once, with the pause the server asked for, and the second map is not asked.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_ThrowsRateLimitedWithoutFallback()
        {
            FakeHandler handler = new FakeHandler(path =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetZonesAsync(CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
            Assert.Single(handler.Paths);
        }

        /// <summary>
        /// A rate limit without the header, or with a date in the past, gives a zero pause.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetZonesAsync_RateLimitWithoutUsefulHeaderGivesZeroPause(bool pastDate)
        {
            FakeHandler handler = new FakeHandler(path =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

                if (pastDate)
                {
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));
                }

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetZonesAsync(CancellationToken.None));

            Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
        }

        /// <summary>
        /// A rate limit of the second map is also reported.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_ReportsRateLimitOfAup()
        {
            FakeHandler handler = new FakeHandler(path => path.EndsWith("uup", StringComparison.Ordinal)
                ? Ok("[]")
                : new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetZonesAsync(CancellationToken.None));

            Assert.Equal(2, handler.Paths.Count);
        }

        /// <summary>
        /// Cancellation is not mistaken for a failure of the first map.
        /// </summary>
        [Fact]
        public async Task GetZonesAsync_DoesNotFallBackOnCancellation()
        {
            FakeHandler handler = new FakeHandler(path => Ok(OneZone));
            using CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateClient(handler).GetZonesAsync(source.Token));

            Assert.Empty(handler.Paths);
        }
    }
}

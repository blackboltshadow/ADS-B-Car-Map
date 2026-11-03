using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of the client of the threats endpoint.
    /// </summary>
    public class NeptunRestClientTests
    {
        /// <summary>
        /// Handler that answers every request with one response and remembers the path.
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
            /// Path of the last request.
            /// </summary>
            public string? LastPath { get; private set; }

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastPath = request.RequestUri?.PathAndQuery;

                return Task.FromResult(factory());
            }
        }

        /// <summary>
        /// Creates a client over a handler.
        /// </summary>
        /// <param name="handler">Handler to use.</param>
        /// <returns>The client.</returns>
        private static NeptunRestClient CreateClient(FakeHandler handler)
        {
            return new NeptunRestClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// The client asks the threats endpoint and reads the answer.
        /// </summary>
        [Fact]
        public async Task GetThreatsAsync_ReadsThreats()
        {
            FakeHandler handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"threats\":[{\"id\":\"a\",\"lat\":1,\"lon\":2}]}") });

            IReadOnlyList<Threat> threats = await CreateClient(handler).GetThreatsAsync(CancellationToken.None);

            Assert.Single(threats);
            Assert.Equal("/api/v1/threats", handler.LastPath);
        }

        /// <summary>
        /// Answer 429 becomes an exception with the requested pause.
        /// </summary>
        [Fact]
        public async Task GetThreatsAsync_429_ThrowsRateLimited()
        {
            FakeHandler handler = new FakeHandler(() =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetThreatsAsync(CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
        }

        /// <summary>
        /// Answer 429 without the header gives zero pause.
        /// </summary>
        [Fact]
        public async Task GetThreatsAsync_429WithoutHeader_ZeroPause()
        {
            FakeHandler handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetThreatsAsync(CancellationToken.None));

            Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
        }

        /// <summary>
        /// A failure status throws an HTTP exception.
        /// </summary>
        [Fact]
        public async Task GetThreatsAsync_ServerError_Throws()
        {
            FakeHandler handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetThreatsAsync(CancellationToken.None));
        }
    }
}

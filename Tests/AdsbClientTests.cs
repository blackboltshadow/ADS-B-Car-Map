using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of response parsing and the 429 backoff in the ADS-B client.
    /// </summary>
    public class AdsbClientTests
    {
        /// <summary>
        /// Handler returning queued responses and counting requests.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            /// <summary>
            /// Responses returned in order, the last one repeats.
            /// </summary>
            private readonly Queue<Func<HttpResponseMessage>> responses = new Queue<Func<HttpResponseMessage>>();

            /// <summary>
            /// Number of requests that reached the handler.
            /// </summary>
            public int RequestCount { get; private set; }

            /// <summary>
            /// Path and query of the last request.
            /// </summary>
            public string? LastPath { get; private set; }

            /// <summary>
            /// Queues a response.
            /// </summary>
            /// <param name="factory">Factory creating the response.</param>
            public void Enqueue(Func<HttpResponseMessage> factory)
            {
                responses.Enqueue(factory);
            }

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestCount++;
                LastPath = request.RequestUri?.PathAndQuery;
                Func<HttpResponseMessage> factory = responses.Count > 1 ? responses.Dequeue() : responses.Peek();

                return Task.FromResult(factory());
            }
        }

        /// <summary>
        /// Creates a client over the fake handler.
        /// </summary>
        /// <param name="handler">Handler to use.</param>
        /// <returns>The client.</returns>
        private static AdsbClient CreateClient(FakeHandler handler)
        {
            return new AdsbClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// Creates a JSON response.
        /// </summary>
        /// <param name="json">Response body.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage Json(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }

        /// <summary>
        /// Creates a 429 response, optionally with Retry-After in seconds.
        /// </summary>
        /// <param name="retryAfterSeconds">Value of the Retry-After header or null.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage TooManyRequests(int? retryAfterSeconds = null)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            if (retryAfterSeconds.HasValue)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds.Value));
            }

            return response;
        }

        /// <summary>
        /// Aircraft without a position are skipped and the request path uses invariant formatting.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ParsesAircraftAndSkipsThoseWithoutPosition()
        {
            FakeHandler handler = new FakeHandler();
            handler.Enqueue(() => Json("{\"ac\":[{\"hex\":\"abc123\",\"flight\":\"LOT1 \",\"lat\":52.1,\"lon\":21.2,\"alt_baro\":35000,\"t\":\"B738\"},{\"hex\":\"def456\"}]}"));

            IReadOnlyList<Aircraft> result = await CreateClient(handler).GetAircraftAsync(52.5, 21.5, 100.0, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("abc123", result[0].Hex);
            Assert.Equal(35000, result[0].AltitudeFeet);
            Assert.Equal("B738", result[0].TypeCode);
            Assert.Equal("/v2/point/52.5/21.5/100", handler.LastPath);
        }

        /// <summary>
        /// The radius is clamped to the API limit.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ClampsRadius()
        {
            FakeHandler handler = new FakeHandler();
            handler.Enqueue(() => Json("{\"ac\":[]}"));

            await CreateClient(handler).GetAircraftAsync(0.0, 0.0, 100000.0, CancellationToken.None);

            Assert.EndsWith("/" + "250", handler.LastPath);
        }

        /// <summary>
        /// After a 429 further requests are blocked locally without reaching the server.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_After429_BlocksFurtherRequests()
        {
            FakeHandler handler = new FakeHandler();
            handler.Enqueue(() => TooManyRequests());
            AdsbClient client = CreateClient(handler);

            RateLimitedException first = await Assert.ThrowsAsync<RateLimitedException>(() => client.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));
            RateLimitedException second = await Assert.ThrowsAsync<RateLimitedException>(() => client.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(TimeSpan.FromSeconds(5), first.RetryAfter);
            Assert.True(second.RetryAfter > TimeSpan.Zero && second.RetryAfter <= first.RetryAfter);
        }

        /// <summary>
        /// A longer Retry-After from the server wins over the base backoff.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_RetryAfterHeader_ExtendsWait()
        {
            FakeHandler handler = new FakeHandler();
            handler.Enqueue(() => TooManyRequests(30));

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
        }

        /// <summary>
        /// Other HTTP errors are reported as HttpRequestException without starting a backoff.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ServerError_ThrowsHttpRequestException()
        {
            FakeHandler handler = new FakeHandler();
            handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            AdsbClient client = CreateClient(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(2, handler.RequestCount);
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AdsbLol.Tests
{
    /// <summary>
    /// Tests of the request for all the military aircraft of the adsb.lol client.
    /// </summary>
    public class AdsbLolMilitaryTests
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
            /// Path and query of the last request.
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
        private static AdsbLolClient CreateClient(FakeHandler handler)
        {
            return new AdsbLolClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// The client asks the military endpoint and returns the aircraft with a position, marked as military by the database flag; aircraft without a position are skipped.
        /// </summary>
        [Fact]
        public async Task GetMilitaryAircraftAsync_AsksMilEndpointAndReadsAircraft()
        {
            string json = "{\"ac\":[{\"hex\":\"ae109a\",\"flight\":\"C102    \",\"t\":\"GLF5\",\"dbFlags\":1,\"lat\":21.18,\"lon\":-160.82,\"alt_baro\":40000},{\"hex\":\"ae1111\",\"dbFlags\":1},{\"hex\":\"ae2222\",\"lat\":50.0,\"lon\":20.0,\"dbFlags\":0}],\"msg\":\"No error\",\"total\":3}";
            FakeHandler handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

            IReadOnlyList<Aircraft>? result = await CreateClient(handler).GetMilitaryAircraftAsync(CancellationToken.None);

            Assert.Equal("/v2/mil", handler.LastPath);
            Assert.Equal(2, result!.Count);
            Assert.Equal("ae109a", result[0].Hex);
            Assert.True(result[0].IsMilitary);
            Assert.False(result[1].IsMilitary);
        }

        /// <summary>
        /// An answer without the list of aircraft gives an empty list, which is different from no request at all.
        /// </summary>
        [Fact]
        public async Task GetMilitaryAircraftAsync_AnswerWithoutListIsEmpty()
        {
            FakeHandler handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"msg\":\"No error\"}") });

            IReadOnlyList<Aircraft>? result = await CreateClient(handler).GetMilitaryAircraftAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// A limit response is reported with the pause the server asked for, and another failure status is an error.
        /// </summary>
        [Fact]
        public async Task GetMilitaryAircraftAsync_ReportsLimitAndErrors()
        {
            FakeHandler limited = new FakeHandler(() =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(limited).GetMilitaryAircraftAsync(CancellationToken.None));
            Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.BadGateway))).GetMilitaryAircraftAsync(CancellationToken.None));
        }

        /// <summary>
        /// The client has the ability to return the military aircraft.
        /// </summary>
        [Fact]
        public void Client_IsAMilitarySource()
        {
            Assert.IsAssignableFrom<IMilitaryAircraftSource>(CreateClient(new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK))));
        }
    }
}

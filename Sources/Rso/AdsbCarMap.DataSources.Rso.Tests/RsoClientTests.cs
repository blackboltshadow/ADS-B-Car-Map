using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Rso.Tests
{
    /// <summary>
    /// Tests of the client of the feed of the regional warning system.
    /// </summary>
    public class RsoClientTests
    {
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
            /// <param name="factory">Creates the response for a path with the query.</param>
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
                string path = request.RequestUri?.PathAndQuery ?? string.Empty;
                Paths.Add(path);

                return Task.FromResult(factory(path));
            }
        }

        /// <summary>
        /// Creates a client over a handler.
        /// </summary>
        /// <param name="handler">Handler to use.</param>
        /// <returns>The client.</returns>
        private static RsoClient CreateClient(FakeHandler handler)
        {
            return new RsoClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// Creates a successful answer holding the given notice identifiers.
        /// </summary>
        /// <param name="ids">Identifiers of the notices.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage Notices(params int[] ids)
        {
            string items = string.Join(",", ids.Select((int id) => "{\"id\":" + id + ",\"valid_from\":\"2026-11-03 10:00:00\"}"));

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"pagination\":{},\"newses\":[" + items + "]}") };
        }

        /// <summary>
        /// The client has the name shown to the user.
        /// </summary>
        [Fact]
        public void Name_IsRso()
        {
            Assert.Equal("RSO (TVP)", CreateClient(new FakeHandler(path => Notices())).Name);
        }

        /// <summary>
        /// The path of a request holds the region, the category, the first page and the JSON format.
        /// </summary>
        [Fact]
        public void BuildPath_HoldsRegionAndCategory()
        {
            Assert.Equal("komunikatyxml/mazowieckie/ogolne/1?_format=json", RsoClient.BuildPath("mazowieckie", "ogolne"));
        }

        /// <summary>
        /// Without regions the client asks for all of them, once per category, and puts the category in the notices.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_AsksForAllRegionsPerCategory()
        {
            FakeHandler handler = new FakeHandler(path => path.Contains("/ogolne/") ? Notices(1, 2) : Notices(3));

            IReadOnlyList<Warning> warnings = await CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne", "hydrologiczne" }, new List<string>(), CancellationToken.None);

            Assert.Equal(new List<string> { "/komunikatyxml/wszystkie/ogolne/1?_format=json", "/komunikatyxml/wszystkie/hydrologiczne/1?_format=json" }, handler.Paths);
            Assert.Equal(3, warnings.Count);
            Assert.Equal(2, warnings.Count((Warning warning) => warning.Category == "ogolne"));
            Assert.Equal("hydrologiczne", warnings.Single((Warning warning) => warning.Id == "3").Category);
        }

        /// <summary>
        /// For a few regions the client asks for each of them separately, and a notice that appears for several regions is kept once per category.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_AsksForEachOfFewRegions()
        {
            FakeHandler handler = new FakeHandler(path => Notices(7));

            IReadOnlyList<Warning> warnings = await CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne" }, new List<string> { "slaskie", "opolskie" }, CancellationToken.None);

            Assert.Equal(new List<string> { "/komunikatyxml/slaskie/ogolne/1?_format=json", "/komunikatyxml/opolskie/ogolne/1?_format=json" }, handler.Paths);
            Assert.Single(warnings);
        }

        /// <summary>
        /// For more regions than the limit one request per category asks for all of them.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_UsesAllRegionsForManyRegions()
        {
            FakeHandler handler = new FakeHandler(path => Notices());
            List<string> many = new List<string> { "a", "b", "c", "d", "e" };

            await CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne" }, many, CancellationToken.None);

            Assert.Equal(new List<string> { "/komunikatyxml/wszystkie/ogolne/1?_format=json" }, handler.Paths);
            Assert.Equal(5, many.Count);
            Assert.Equal(4, RsoClient.MaximumSeparateProvinces);
        }

        /// <summary>
        /// Names that could change the address are not used.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_IgnoresUnsafeNames()
        {
            FakeHandler handler = new FakeHandler(path => Notices());

            await CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne", "../x", "A b", "", "ogolne" }, new List<string> { "slaskie/..", "mazowieckie" }, CancellationToken.None);

            Assert.Equal(new List<string> { "/komunikatyxml/mazowieckie/ogolne/1?_format=json" }, handler.Paths);
        }

        /// <summary>
        /// No categories mean no requests.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_WithoutCategoriesAsksForNothing()
        {
            FakeHandler handler = new FakeHandler(path => Notices(1));

            IReadOnlyList<Warning> warnings = await CreateClient(handler).GetWarningsAsync(new List<string>(), new List<string>(), CancellationToken.None);

            Assert.Empty(warnings);
            Assert.Empty(handler.Paths);
        }

        /// <summary>
        /// A rate limit stops the client at once and carries the pause the server asked for.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_ThrowsRateLimited()
        {
            FakeHandler handler = new FakeHandler(path =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne", "hydrologiczne" }, new List<string>(), CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(45), exception.RetryAfter);
            Assert.Single(handler.Paths);
        }

        /// <summary>
        /// A rate limit without a useful header gives a zero pause.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetWarningsAsync_RateLimitWithoutUsefulHeaderGivesZeroPause(bool pastDate)
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

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne" }, new List<string>(), CancellationToken.None));

            Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
        }

        /// <summary>
        /// A server error is reported to the caller.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_ThrowsForServerError()
        {
            FakeHandler handler = new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.BadGateway));

            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne" }, new List<string>(), CancellationToken.None));
        }

        /// <summary>
        /// An answer that is not JSON is reported to the caller.
        /// </summary>
        [Fact]
        public async Task GetWarningsAsync_ThrowsForInvalidAnswer()
        {
            FakeHandler handler = new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });

            await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => CreateClient(handler).GetWarningsAsync(new List<string> { "ogolne" }, new List<string>(), CancellationToken.None));
        }
    }
}

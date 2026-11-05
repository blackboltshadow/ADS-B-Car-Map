using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.AviationWeather;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AviationWeather.Tests
{
    /// <summary>
    /// Tests of the aviationweather.gov client.
    /// </summary>
    public class AviationWeatherClientTests
    {
        /// <summary>
        /// Handler that answers each request and remembers the requested addresses.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            /// <summary>
            /// Creates the response for a path with the query.
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
            /// Paths with queries of the requests in the order they were made.
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
        private static AviationWeatherClient CreateClient(FakeHandler handler)
        {
            return new AviationWeatherClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
        }

        /// <summary>
        /// Creates a successful answer with one report of each given airport.
        /// </summary>
        /// <param name="codes">ICAO codes.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage Reports(params string[] codes)
        {
            string items = string.Join(",", codes.Select((string code) => "{\"icaoId\":\"" + code + "\",\"obsTime\":1793867400,\"rawOb\":\"METAR " + code + "\"}"));

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + items + "]") };
        }

        /// <summary>
        /// The client has the name shown to the user.
        /// </summary>
        [Fact]
        public void Name_IsAviationWeather()
        {
            Assert.Equal("aviationweather.gov", CreateClient(new FakeHandler(path => Reports())).Name);
        }

        /// <summary>
        /// The request holds the JSON format and the codes in upper case separated with commas.
        /// </summary>
        [Fact]
        public async Task GetMetars_BuildsRequest()
        {
            FakeHandler handler = new FakeHandler(path => Reports("EPWA", "EPKK"));
            IReadOnlyList<Metar> result = await CreateClient(handler).GetMetarsAsync(new List<string> { "epwa", " EPKK " }, CancellationToken.None);

            Assert.Equal("/api/data/metar?format=json&ids=EPWA,EPKK", Assert.Single(handler.Paths));
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// Codes that are not four letters or digits and repeated codes are not sent.
        /// </summary>
        [Fact]
        public async Task GetMetars_DropsInvalidAndRepeatedCodes()
        {
            FakeHandler handler = new FakeHandler(path => Reports("EPWA"));
            await CreateClient(handler).GetMetarsAsync(new List<string> { "EPWA", "epwa", "EP", "EPWA,KJFK", "EP&A", string.Empty }, CancellationToken.None);

            Assert.Equal("/api/data/metar?format=json&ids=EPWA", Assert.Single(handler.Paths));
        }

        /// <summary>
        /// With no usable code no request is made.
        /// </summary>
        [Fact]
        public async Task GetMetars_NoCodes_MakesNoRequest()
        {
            FakeHandler handler = new FakeHandler(path => Reports());
            IReadOnlyList<Metar> result = await CreateClient(handler).GetMetarsAsync(new List<string> { "x" }, CancellationToken.None);

            Assert.Empty(result);
            Assert.Empty(handler.Paths);
        }

        /// <summary>
        /// A long list is split into requests of at most the allowed number of airports and the answers are joined.
        /// </summary>
        [Fact]
        public async Task GetMetars_SplitsLongList()
        {
            List<string> codes = Enumerable.Range(0, AviationWeatherClient.MaxCodesPerRequest + 5).Select((int index) => "E" + index.ToString("000")).ToList();
            FakeHandler handler = new FakeHandler(path => Reports(path.Substring(path.IndexOf("ids=") + 4).Split(',')));
            IReadOnlyList<Metar> result = await CreateClient(handler).GetMetarsAsync(codes, CancellationToken.None);

            Assert.Equal(2, handler.Paths.Count);
            Assert.Equal(codes.Count, result.Count);
        }

        /// <summary>
        /// Creates a successful answer with one forecast of each given airport.
        /// </summary>
        /// <param name="codes">ICAO codes.</param>
        /// <returns>The response.</returns>
        private static HttpResponseMessage Forecasts(params string[] codes)
        {
            string items = string.Join(",", codes.Select((string code) => "{\"icaoId\":\"" + code + "\",\"validTimeFrom\":1000,\"validTimeTo\":2000,\"rawTAF\":\"TAF " + code + "\"}"));

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + items + "]") };
        }

        /// <summary>
        /// A request for forecasts goes to the TAF endpoint with the JSON format and the valid codes only.
        /// </summary>
        [Fact]
        public async Task GetTafs_BuildsRequestAndDropsInvalidCodes()
        {
            FakeHandler handler = new FakeHandler(path => Forecasts("EPWA", "EPKK"));
            IReadOnlyList<Taf> result = await CreateClient(handler).GetTafsAsync(new List<string> { "epwa", " EPKK ", "EP", "EPWA", "EP&A" }, CancellationToken.None);

            Assert.Equal("/api/data/taf?format=json&ids=EPWA,EPKK", Assert.Single(handler.Paths));
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// With no usable code no request for forecasts is made, and a long list is split.
        /// </summary>
        [Fact]
        public async Task GetTafs_NoCodes_MakesNoRequest_LongListIsSplit()
        {
            FakeHandler none = new FakeHandler(path => Forecasts());

            Assert.Empty(await CreateClient(none).GetTafsAsync(new List<string> { "x" }, CancellationToken.None));
            Assert.Empty(none.Paths);

            List<string> codes = Enumerable.Range(0, AviationWeatherClient.MaxCodesPerRequest + 3).Select((int index) => "E" + index.ToString("000")).ToList();
            FakeHandler split = new FakeHandler(path => Forecasts(path.Substring(path.IndexOf("ids=") + 4).Split(',')));
            IReadOnlyList<Taf> result = await CreateClient(split).GetTafsAsync(codes, CancellationToken.None);

            Assert.Equal(2, split.Paths.Count);
            Assert.Equal(codes.Count, result.Count);
        }

        /// <summary>
        /// A request for forecasts gives nothing for 204, 404 and an empty body, reports a limit response and throws for a server error.
        /// </summary>
        [Fact]
        public async Task GetTafs_EmptyAnswers_LimitAndError()
        {
            Assert.Empty(await CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.NoContent))).GetTafsAsync(new List<string> { "EPML" }, CancellationToken.None));
            Assert.Empty(await CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.NotFound))).GetTafsAsync(new List<string> { "EPML" }, CancellationToken.None));
            Assert.Empty(await CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(" ") })).GetTafsAsync(new List<string> { "EPML" }, CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.TooManyRequests))).GetTafsAsync(new List<string> { "EPWA" }, CancellationToken.None));
            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.InternalServerError))).GetTafsAsync(new List<string> { "EPWA" }, CancellationToken.None));
        }

        /// <summary>
        /// A search around a point asks for the square around it, with the coordinates written with a dot.
        /// </summary>
        [Fact]
        public async Task GetMetarsNear_AsksForTheSquareAroundThePoint()
        {
            FakeHandler handler = new FakeHandler(path => Reports("EPRZ"));
            IReadOnlyList<Metar> result = await CreateClient(handler).GetMetarsNearAsync(50.3224, 21.4617, 60.0, CancellationToken.None);

            string path = Assert.Single(handler.Paths);

            Assert.StartsWith("/api/data/metar?format=json&bbox=", path);
            string[] box = path.Substring(path.IndexOf("bbox=") + 5).Split(',');

            Assert.Equal(4, box.Length);
            Assert.InRange(double.Parse(box[0], System.Globalization.CultureInfo.InvariantCulture), 49.7, 49.9);
            Assert.InRange(double.Parse(box[1], System.Globalization.CultureInfo.InvariantCulture), 20.6, 20.8);
            Assert.InRange(double.Parse(box[2], System.Globalization.CultureInfo.InvariantCulture), 50.7, 50.9);
            Assert.InRange(double.Parse(box[3], System.Globalization.CultureInfo.InvariantCulture), 22.2, 22.4);
            Assert.Single(result);
        }

        /// <summary>
        /// The square is cut at the poles and the date line, and a radius above the largest one is cut to it.
        /// </summary>
        [Fact]
        public async Task GetMetarsNear_CutsTheSquareAndTheRadius()
        {
            FakeHandler handler = new FakeHandler(path => Reports());

            await CreateClient(handler).GetMetarsNearAsync(89.9, 179.9, 5000.0, CancellationToken.None);

            string[] box = handler.Paths[0].Substring(handler.Paths[0].IndexOf("bbox=") + 5).Split(',');

            Assert.Equal(89.9 - AviationWeatherClient.MaxRadiusKilometers / 111.195, double.Parse(box[0], System.Globalization.CultureInfo.InvariantCulture), 2);
            Assert.Equal("90", box[2]);
            Assert.Equal("180", box[3]);
        }

        /// <summary>
        /// A position or radius that makes no sense sends no request.
        /// </summary>
        [Theory]
        [InlineData(91.0, 0.0, 10.0)]
        [InlineData(0.0, 181.0, 10.0)]
        [InlineData(50.0, 20.0, 0.0)]
        [InlineData(50.0, 20.0, -5.0)]
        [InlineData(double.NaN, 20.0, 10.0)]
        [InlineData(50.0, double.PositiveInfinity, 10.0)]
        [InlineData(50.0, 20.0, double.NaN)]
        public async Task GetMetarsNear_InvalidInput_MakesNoRequest(double latitude, double longitude, double radius)
        {
            FakeHandler handler = new FakeHandler(path => Reports());

            Assert.Empty(await CreateClient(handler).GetMetarsNearAsync(latitude, longitude, radius, CancellationToken.None));
            Assert.Empty(handler.Paths);
        }

        /// <summary>
        /// A search around a point reports a limit response too, and an empty area gives no reports.
        /// </summary>
        [Fact]
        public async Task GetMetarsNear_429AndNoContent()
        {
            await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.TooManyRequests))).GetMetarsNearAsync(50.0, 20.0, 60.0, CancellationToken.None));

            Assert.Empty(await CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.NoContent))).GetMetarsNearAsync(50.0, 20.0, 60.0, CancellationToken.None));
        }

        /// <summary>
        /// The answer without content (204) and the answer 404 give no reports.
        /// </summary>
        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetMetars_NoContent_GivesNothing(HttpStatusCode status)
        {
            IReadOnlyList<Metar> result = await CreateClient(new FakeHandler(path => new HttpResponseMessage(status))).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None);

            Assert.Empty(result);
        }

        /// <summary>
        /// An empty body of a successful answer gives no reports.
        /// </summary>
        [Fact]
        public async Task GetMetars_EmptyBody_GivesNothing()
        {
            FakeHandler handler = new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(" ") });

            Assert.Empty(await CreateClient(handler).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));
        }

        /// <summary>
        /// The answer 429 is reported with the pause from the Retry-After header.
        /// </summary>
        [Fact]
        public async Task GetMetars_429_ThrowsRateLimited()
        {
            FakeHandler handler = new FakeHandler(path =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

                return response;
            });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(handler).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
        }

        /// <summary>
        /// The answer 429 without the header gives a zero pause, and a Retry-After date in the past too.
        /// </summary>
        [Fact]
        public async Task GetMetars_429_WithoutOrPastHeader_GivesZero()
        {
            RateLimitedException none = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.TooManyRequests))).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));
            FakeHandler past = new FakeHandler(path =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));

                return response;
            });
            RateLimitedException old = await Assert.ThrowsAsync<RateLimitedException>(() => CreateClient(past).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));

            Assert.Equal(TimeSpan.Zero, none.RetryAfter);
            Assert.Equal(TimeSpan.Zero, old.RetryAfter);
        }

        /// <summary>
        /// Another failure status code throws an HTTP error.
        /// </summary>
        [Fact]
        public async Task GetMetars_ServerError_Throws()
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(new FakeHandler(path => new HttpResponseMessage(HttpStatusCode.InternalServerError))).GetMetarsAsync(new List<string> { "EPWA" }, CancellationToken.None));
        }
    }
}

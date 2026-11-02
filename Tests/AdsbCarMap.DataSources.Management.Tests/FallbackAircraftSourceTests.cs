using System.Net;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Management.Selection;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of asking additional aircraft sources when the preferred one gives no data.
    /// </summary>
    public class FallbackAircraftSourceTests
    {
        /// <summary>
        /// Source returning a fixed list or throwing a fixed exception.
        /// </summary>
        private sealed class StubSource : IRateLimitedAircraftSource
        {
            /// <summary>
            /// Creates a source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            public StubSource(string name)
            {
                Name = name;
            }

            /// <inheritdoc />
            public string Name { get; }

            /// <summary>
            /// Exception thrown by the query or null when the source answers.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Remaining block reported by the source.
            /// </summary>
            public TimeSpan Block { get; set; }

            /// <summary>
            /// Number of queries that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <inheritdoc />
            public RateLimitStatus GetRateLimitStatus()
            {
                return new RateLimitStatus(DateTime.MinValue, Block, Block > TimeSpan.Zero ? 1 : 0);
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                Calls++;

                if (Failure is RateLimitedException rateLimited)
                {
                    Block = rateLimited.RetryAfter;
                }

                if (Failure != null)
                {
                    throw Failure;
                }

                IReadOnlyList<Aircraft> result = new List<Aircraft> { new Aircraft(Name, null, latitude, longitude, null, false, null, null, 0.0, null, null) };

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Creates the composite over two sources.
        /// </summary>
        /// <param name="first">First source.</param>
        /// <param name="second">Second source.</param>
        /// <param name="preferred">Name of the preferred source.</param>
        /// <param name="fallback">Whether the fallback is enabled.</param>
        /// <returns>The composite source.</returns>
        private static FallbackAircraftSource Create(StubSource first, StubSource second, string preferred, bool fallback = true)
        {
            return new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { first, second }, () => new List<string> { preferred }, () => fallback);
        }

        /// <summary>
        /// A healthy preferred source is the only one asked.
        /// </summary>
        [Fact]
        public async Task PreferredWorks_OthersNotAsked()
        {
            StubSource first = new StubSource("a");
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "b");

            IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("b", result[0].Hex);
            Assert.Equal("b", source.Name);
            Assert.Equal("b", source.LastUsedSourceName);
            Assert.Equal(0, first.Calls);
        }

        /// <summary>
        /// A limit response of the preferred source makes the next source answer.
        /// </summary>
        [Fact]
        public async Task PreferredRateLimited_FallsBack()
        {
            StubSource first = new StubSource("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(5)) };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "a");

            IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("b", result[0].Hex);
            Assert.Equal("b", source.LastUsedSourceName);
        }

        /// <summary>
        /// A source that is already paused is skipped without a request.
        /// </summary>
        [Fact]
        public async Task PreferredBlocked_IsSkipped()
        {
            StubSource first = new StubSource("a") { Block = TimeSpan.FromSeconds(30) };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "a");

            await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal(0, first.Calls);
            Assert.Equal(1, second.Calls);
        }

        /// <summary>
        /// A network or server error of the preferred source also makes the next source answer.
        /// </summary>
        [Fact]
        public async Task PreferredHttpError_FallsBack()
        {
            StubSource first = new StubSource("a") { Failure = new HttpRequestException("boom", null, HttpStatusCode.InternalServerError) };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "a");

            IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("b", result[0].Hex);
        }

        /// <summary>
        /// With the fallback disabled the failure of the preferred source is reported and the others are not asked.
        /// </summary>
        [Fact]
        public async Task FallbackDisabled_ThrowsAndDoesNotAskOthers()
        {
            StubSource first = new StubSource("a") { Failure = new HttpRequestException("boom") };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "a", false);

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None));

            Assert.Equal(0, second.Calls);
        }

        /// <summary>
        /// When every source is paused the shortest pause is reported as a limit error.
        /// </summary>
        [Fact]
        public async Task AllRateLimited_ThrowsShortestWait()
        {
            StubSource first = new StubSource("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            StubSource second = new StubSource("b") { Block = TimeSpan.FromSeconds(12) };
            FallbackAircraftSource source = Create(first, second, "a");

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
            Assert.False(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// A hard failure wins over limit responses of the other sources.
        /// </summary>
        [Fact]
        public async Task HardFailureAndRateLimit_ThrowsHardFailure()
        {
            StubSource first = new StubSource("a") { Failure = new HttpRequestException("boom") };
            StubSource second = new StubSource("b") { Failure = new RateLimitedException(TimeSpan.FromSeconds(5)) };
            FallbackAircraftSource source = Create(first, second, "a");

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None));
        }

        /// <summary>
        /// An unknown preferred name selects the first source.
        /// </summary>
        [Fact]
        public void UnknownPreferredName_UsesFirstSource()
        {
            FallbackAircraftSource source = Create(new StubSource("a"), new StubSource("b"), "zzz");

            Assert.Equal("a", source.Name);
        }

        /// <summary>
        /// Cancellation is not treated as a failure of the source.
        /// </summary>
        [Fact]
        public async Task Cancellation_IsNotSwallowed()
        {
            StubSource first = new StubSource("a") { Failure = new OperationCanceledException() };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = Create(first, second, "a");

            await Assert.ThrowsAsync<OperationCanceledException>(() => source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None));

            Assert.Equal(0, second.Calls);
        }

        /// <summary>
        /// The user's order decides which source is asked first and the sources it does not name come last.
        /// </summary>
        [Fact]
        public async Task Order_DecidesWhichSourceIsAskedFirst()
        {
            StubSource first = new StubSource("a");
            StubSource second = new StubSource("b");
            StubSource third = new StubSource("c");
            FallbackAircraftSource source = new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { first, second, third }, () => new List<string> { "c", "zzz", "a" }, () => true);

            IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("c", result[0].Hex);
            Assert.Equal(new List<string> { "c", "a", "b" }, source.GetAllInOrder().Select(item => item.Name).ToList());
            Assert.Equal("c", source.Name);
        }

        /// <summary>
        /// With the fallback disabled only the first source of the order is asked.
        /// </summary>
        [Fact]
        public async Task FallbackDisabled_AsksOnlyFirstOfOrder()
        {
            StubSource first = new StubSource("a") { Failure = new HttpRequestException("boom") };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { first, second }, () => new List<string> { "b", "a" }, () => false);

            IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("b", result[0].Hex);
            Assert.Equal(0, first.Calls);
        }

        /// <summary>
        /// A limit response and the switch to another source are reported with the same text every time.
        /// </summary>
        [Fact]
        public async Task Report_GetsLimitResponseAndSourceChange()
        {
            List<(SourceEventKind Kind, string Message, Exception? Exception)> events = new List<(SourceEventKind, string, Exception?)>();
            StubSource first = new StubSource("a");
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { first, second }, () => new List<string> { "a" }, () => true, (SourceEventKind kind, string message, Exception? exception) => events.Add((kind, message, exception)));

            await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);
            Assert.Empty(events);

            first.Failure = new RateLimitedException(TimeSpan.FromSeconds(5));
            await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal(2, events.Count);
            Assert.Equal(SourceEventKind.Failure, events[0].Kind);
            Assert.Equal("Aircraft source a answered with a limit response (429), paused", events[0].Message);
            Assert.Null(events[0].Exception);
            Assert.Equal(SourceEventKind.Information, events[1].Kind);
            Assert.Equal("Aircraft source changed from a to b", events[1].Message);
        }

        /// <summary>
        /// A network failure is reported with its exception, and with no listener the fallback still works.
        /// </summary>
        [Fact]
        public async Task Report_NetworkFailure_PassesException()
        {
            List<(SourceEventKind Kind, string Message, Exception? Exception)> events = new List<(SourceEventKind, string, Exception?)>();
            HttpRequestException failure = new HttpRequestException("boom");
            StubSource first = new StubSource("a") { Failure = failure };
            StubSource second = new StubSource("b");
            FallbackAircraftSource source = new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { first, second }, () => new List<string> { "a" }, () => true, (SourceEventKind kind, string message, Exception? exception) => events.Add((kind, message, exception)));

            await source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("Aircraft source a failed (HttpRequestException)", events[0].Message);
            Assert.Same(failure, events[0].Exception);

            FallbackAircraftSource silent = Create(new StubSource("a") { Failure = new HttpRequestException("boom") }, new StubSource("b"), "a");

            IReadOnlyList<Aircraft> result = await silent.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None);

            Assert.Equal("b", result[0].Hex);
        }
    }
}

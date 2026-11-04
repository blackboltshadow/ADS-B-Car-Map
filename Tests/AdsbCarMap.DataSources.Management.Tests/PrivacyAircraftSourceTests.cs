using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the source that rounds the position of its queries.
    /// </summary>
    public class PrivacyAircraftSourceTests
    {
        /// <summary>
        /// Source that remembers the queries that reached it.
        /// </summary>
        private sealed class CapturingSource : IAircraftSource
        {
            /// <summary>
            /// Queries received so far.
            /// </summary>
            public List<QueryArea> Queries { get; } = new List<QueryArea>();

            /// <summary>
            /// Exception thrown instead of an answer, or null.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <inheritdoc />
            public string Name => "capture";

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                Queries.Add(new QueryArea(latitude, longitude, radiusNauticalMiles));

                if (Failure != null)
                {
                    throw Failure;
                }

                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// The wrapper takes the name of the wrapped source.
        /// </summary>
        [Fact]
        public void Name_IsTheNameOfTheWrappedSource()
        {
            Assert.Equal("capture", new PrivacyAircraftSource(new CapturingSource(), () => PrivacyOptions.Exact).Name);
        }

        /// <summary>
        /// Source that tracks limits and reports a fixed state.
        /// </summary>
        private sealed class LimitedSource : IAircraftSource, IRateLimitedSource
        {
            /// <inheritdoc />
            public string Name => "limited";

            /// <inheritdoc />
            public RateLimitStatus GetRateLimitStatus()
            {
                return new RateLimitStatus(DateTime.MinValue, TimeSpan.FromSeconds(30), 1);
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// The state of the limits comes from the wrapped source.
        /// </summary>
        [Fact]
        public void GetRateLimitStatus_ComesFromTheWrappedSource()
        {
            PrivacyAircraftSource source = new PrivacyAircraftSource(new LimitedSource(), () => PrivacyOptions.Exact);

            Assert.False(source.GetRateLimitStatus().CanQuery);
            Assert.Equal(TimeSpan.FromSeconds(30), source.GetRateLimitStatus().RemainingBlock);
        }

        /// <summary>
        /// A wrapped source that does not track limits is never reported as paused.
        /// </summary>
        [Fact]
        public void GetRateLimitStatus_UntrackedSource_CanQuery()
        {
            PrivacyAircraftSource source = new PrivacyAircraftSource(new CapturingSource(), () => PrivacyOptions.Exact);

            Assert.True(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// Missing parts are reported at once.
        /// </summary>
        [Fact]
        public void Constructor_MissingParts_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new PrivacyAircraftSource(null!, () => PrivacyOptions.Exact));
            Assert.Throws<ArgumentNullException>(() => new PrivacyAircraftSource(new CapturingSource(), null!));
        }

        /// <summary>
        /// The wrapped source receives the rounded query.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_PassesTheRoundedQuery()
        {
            CapturingSource inner = new CapturingSource();
            PrivacyOptions options = new PrivacyOptions(10.0);
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => options);

            await source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None);

            Assert.Equal(CoordinateRounder.Round(52.123456, 21.654321, 40.0, options), Assert.Single(inner.Queries));
        }

        /// <summary>
        /// The listener receives the same rounded area that reaches the wrapped source, also when the wrapped source then fails.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ReportsTheSentAreaToTheListener()
        {
            CapturingSource inner = new CapturingSource { Failure = new InvalidOperationException("down") };
            PrivacyOptions options = new PrivacyOptions(10.0);
            List<QueryArea> sent = new List<QueryArea>();
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => options, null, sent.Add);

            await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None));

            Assert.Equal(Assert.Single(inner.Queries), Assert.Single(sent));
        }

        /// <summary>
        /// A changed option applies to the next query without creating the source again.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ReadsTheOptionsAtEveryQuery()
        {
            CapturingSource inner = new CapturingSource();
            PrivacyOptions options = PrivacyOptions.Exact;
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => options);

            await source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None);
            options = new PrivacyOptions(25.0);
            await source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None);

            Assert.Equal(52.123456, inner.Queries[0].Latitude);
            Assert.NotEqual(52.123456, inner.Queries[1].Latitude);
        }

        /// <summary>
        /// The limit exception of the wrapped source reaches the caller unchanged.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_PassesExceptions()
        {
            CapturingSource inner = new CapturingSource { Failure = new RateLimitedException(TimeSpan.FromSeconds(7)) };
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => new PrivacyOptions(5.0));

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(52.0, 21.0, 40.0, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        }

        /// <summary>
        /// A query is reported with both positions only when the options ask for it.
        /// </summary>
        [Fact]
        public async Task GetAircraftAsync_ReportsQueriesOnlyWhenAsked()
        {
            List<string> messages = new List<string>();
            PrivacyOptions options = new PrivacyOptions(10.0, 0.0, true, false);
            PrivacyAircraftSource source = new PrivacyAircraftSource(new CapturingSource(), () => options, (SourceEventKind kind, string message, Exception? exception) => messages.Add(message));

            await source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None);
            Assert.Empty(messages);

            options = new PrivacyOptions(10.0, 0.0, true, true);
            await source.GetAircraftAsync(52.123456, 21.654321, 40.0, CancellationToken.None);

            string logged = Assert.Single(messages);
            Assert.Contains("52.12346", logged);
            Assert.Contains("21.65432", logged);
        }
    }
}

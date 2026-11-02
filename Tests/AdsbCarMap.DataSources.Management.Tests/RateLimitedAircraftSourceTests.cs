using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of pausing the requests of a source after its limit responses.
    /// </summary>
    public class RateLimitedAircraftSourceTests
    {
        /// <summary>
        /// Source that only reports limit responses and counts the requests, like a real client does.
        /// </summary>
        private sealed class ReportingSource : IAircraftSource
        {
            /// <inheritdoc />
            public string Name => "stub";

            /// <summary>
            /// Retry-After reported with a limit response, or null when the source answers normally.
            /// </summary>
            public TimeSpan? LimitedFor { get; set; }

            /// <summary>
            /// Number of requests that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                Calls++;

                if (LimitedFor.HasValue)
                {
                    throw new RateLimitedException(LimitedFor.Value);
                }

                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// The wrapper takes the name of the wrapped source and is not paused at the start.
        /// </summary>
        [Fact]
        public void New_UsesNameAndCanQuery()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new ReportingSource());

            Assert.Equal("stub", source.Name);
            Assert.True(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// After a limit response further requests are paused locally and do not reach the source.
        /// </summary>
        [Fact]
        public async Task After429_BlocksFurtherRequests()
        {
            ReportingSource inner = new ReportingSource { LimitedFor = TimeSpan.Zero };
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            RateLimitedException first = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));
            RateLimitedException second = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(1, inner.Calls);
            Assert.Equal(TimeSpan.FromSeconds(5), first.RetryAfter);
            Assert.True(second.RetryAfter > TimeSpan.Zero && second.RetryAfter <= first.RetryAfter);
            Assert.False(source.GetRateLimitStatus().CanQuery);
            Assert.Equal(1, source.GetRateLimitStatus().ConsecutiveRateLimits);
        }

        /// <summary>
        /// A longer Retry-After reported by the source wins over the base pause.
        /// </summary>
        [Fact]
        public async Task RetryAfter_ExtendsPause()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new ReportingSource { LimitedFor = TimeSpan.FromSeconds(30) });

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
        }

        /// <summary>
        /// A successful answer after the pause clears the pause state.
        /// </summary>
        [Fact]
        public async Task SuccessAfterPause_ClearsState()
        {
            DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            ReportingSource inner = new ReportingSource { LimitedFor = TimeSpan.Zero };
            BackoffRateLimiter limiter = new BackoffRateLimiter(BackoffRateLimiter.DefaultBaseBackoff, BackoffRateLimiter.DefaultMaxBackoff, BackoffRateLimiter.DefaultMaxRetryAfter, () => now);
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner, limiter);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            now += TimeSpan.FromSeconds(6);
            inner.LimitedFor = null;
            await source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None);

            Assert.True(source.GetRateLimitStatus().CanQuery);
            Assert.Equal(0, source.GetRateLimitStatus().ConsecutiveRateLimits);
            Assert.Equal(2, inner.Calls);
        }

        /// <summary>
        /// Errors other than limit responses pass through and do not start a pause.
        /// </summary>
        [Fact]
        public async Task OtherError_PassesThroughWithoutPause()
        {
            FailingSource inner = new FailingSource();
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));
            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(2, inner.Calls);
            Assert.True(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// A refusal (403) is remembered with its status code and cleared by the next successful answer.
        /// </summary>
        [Fact]
        public async Task Refusal_IsRemembered_AndClearedBySuccess()
        {
            RefusingSource inner = new RefusingSource();
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            SourceFailure? failure = source.GetRateLimitStatus().LastFailure;

            Assert.NotNull(failure);
            Assert.Equal(403, failure.StatusCode);
            Assert.True(source.GetRateLimitStatus().CanQuery);

            inner.Refuse = false;
            await source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None);

            Assert.Null(source.GetRateLimitStatus().LastFailure);
        }

        /// <summary>
        /// A limit response is remembered as a failure with status 429.
        /// </summary>
        [Fact]
        public async Task LimitResponse_IsRememberedAs429()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new ReportingSource { LimitedFor = TimeSpan.Zero });

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None));

            Assert.Equal(429, source.GetRateLimitStatus().LastFailure?.StatusCode);
        }

        /// <summary>
        /// Source that answers with a refusal (403) until told otherwise.
        /// </summary>
        private sealed class RefusingSource : IAircraftSource
        {
            /// <inheritdoc />
            public string Name => "refusing";

            /// <summary>
            /// Whether the source refuses the requests.
            /// </summary>
            public bool Refuse { get; set; } = true;

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                if (Refuse)
                {
                    throw new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden);
                }

                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Source that always fails with a network error.
        /// </summary>
        private sealed class FailingSource : IAircraftSource
        {
            /// <inheritdoc />
            public string Name => "failing";

            /// <summary>
            /// Number of requests that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                Calls++;

                throw new HttpRequestException("boom");
            }
        }

        /// <summary>
        /// Source that supports lookups by the ICAO address and reports limit responses.
        /// </summary>
        private sealed class LookupReportingSource : IAircraftSource, IAircraftLookupSource
        {
            /// <inheritdoc />
            public string Name => "lookup";

            /// <summary>
            /// Whether the lookup reports a limit response.
            /// </summary>
            public bool Limited { get; set; }

            /// <summary>
            /// Number of lookups that reached the source.
            /// </summary>
            public int Lookups { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }

            /// <inheritdoc />
            public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
            {
                Lookups++;

                if (Limited)
                {
                    throw new RateLimitedException(TimeSpan.Zero);
                }

                return Task.FromResult<AircraftDetails?>(new AircraftDetails(hex, "A320", null, null));
            }
        }

        /// <summary>
        /// A source without lookups answers a lookup with null and is not asked.
        /// </summary>
        [Fact]
        public async Task Lookup_SourceWithoutLookups_ReturnsNull()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new ReportingSource());

            Assert.Null(await source.GetAircraftDetailsAsync("abc123", CancellationToken.None));
        }

        /// <summary>
        /// A limit response to a lookup pauses the source and further lookups do not reach it.
        /// </summary>
        [Fact]
        public async Task Lookup_After429_PausesSource()
        {
            LookupReportingSource inner = new LookupReportingSource { Limited = true };
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftDetailsAsync("abc123", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftDetailsAsync("abc123", CancellationToken.None));

            Assert.Equal(1, inner.Lookups);
            Assert.False(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// A successful lookup returns the details of the wrapped source.
        /// </summary>
        [Fact]
        public async Task Lookup_Success_ReturnsDetails()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new LookupReportingSource());

            AircraftDetails? details = await source.GetAircraftDetailsAsync("abc123", CancellationToken.None);

            Assert.NotNull(details);
            Assert.Equal("A320", details.TypeCode);
        }
    }
}

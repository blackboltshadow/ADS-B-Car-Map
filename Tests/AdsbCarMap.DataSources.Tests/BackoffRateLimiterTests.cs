using System.Net.Http.Headers;
using AdsbCarMap.DataSources.RateLimiting;
using Xunit;

namespace AdsbCarMap.DataSources.Tests
{
    /// <summary>
    /// Tests of the exponential pause after limit responses.
    /// </summary>
    public class BackoffRateLimiterTests
    {
        /// <summary>
        /// Current time returned to the limiter.
        /// </summary>
        private DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a limiter with the default pauses over the controlled clock.
        /// </summary>
        /// <returns>The limiter.</returns>
        private BackoffRateLimiter Create()
        {
            return new BackoffRateLimiter(BackoffRateLimiter.DefaultBaseBackoff, BackoffRateLimiter.DefaultMaxBackoff, BackoffRateLimiter.DefaultMaxRetryAfter, () => now);
        }

        /// <summary>
        /// A new limiter does not block.
        /// </summary>
        [Fact]
        public void New_IsNotBlocked()
        {
            BackoffRateLimiter limiter = Create();

            Assert.Equal(TimeSpan.Zero, limiter.GetRemainingBlock());
            Assert.True(limiter.GetStatus().CanQuery);
            Assert.Equal(0, limiter.GetStatus().ConsecutiveRateLimits);
        }

        /// <summary>
        /// The pause doubles on every limit response and stops at the maximum.
        /// </summary>
        [Fact]
        public void RegisterRateLimit_DoublesUpToMaximum()
        {
            BackoffRateLimiter limiter = Create();

            Assert.Equal(TimeSpan.FromSeconds(5), limiter.RegisterRateLimit(null));
            Assert.Equal(TimeSpan.FromSeconds(10), limiter.RegisterRateLimit(null));
            Assert.Equal(TimeSpan.FromSeconds(20), limiter.RegisterRateLimit(null));
            Assert.Equal(TimeSpan.FromSeconds(40), limiter.RegisterRateLimit(null));
            Assert.Equal(TimeSpan.FromSeconds(60), limiter.RegisterRateLimit(null));
            Assert.Equal(TimeSpan.FromSeconds(60), limiter.RegisterRateLimit(null));
        }

        /// <summary>
        /// The block ends when the clock passes the pause.
        /// </summary>
        [Fact]
        public void GetStatus_AfterPause_CanQuery()
        {
            BackoffRateLimiter limiter = Create();
            limiter.RegisterRateLimit(null);

            Assert.False(limiter.GetStatus().CanQuery);
            Assert.Equal(TimeSpan.FromSeconds(5), limiter.GetStatus().RemainingBlock);

            now += TimeSpan.FromSeconds(3);
            Assert.Equal(TimeSpan.FromSeconds(2), limiter.GetRemainingBlock());

            now += TimeSpan.FromSeconds(3);
            Assert.True(limiter.GetStatus().CanQuery);
            Assert.Equal(TimeSpan.Zero, limiter.GetRemainingBlock());
        }

        /// <summary>
        /// A longer Retry-After wins and is capped at the maximum.
        /// </summary>
        [Fact]
        public void RegisterRateLimit_RetryAfter_WinsAndIsCapped()
        {
            BackoffRateLimiter limiter = Create();

            Assert.Equal(TimeSpan.FromSeconds(30), limiter.RegisterRateLimit(new RetryConditionHeaderValue(TimeSpan.FromSeconds(30))));
            limiter.Reset();
            Assert.Equal(TimeSpan.FromMinutes(5), limiter.RegisterRateLimit(new RetryConditionHeaderValue(TimeSpan.FromHours(2))));
            limiter.Reset();
            Assert.Equal(TimeSpan.FromSeconds(5), limiter.RegisterRateLimit(new RetryConditionHeaderValue(TimeSpan.FromSeconds(1))));
        }

        /// <summary>
        /// A Retry-After given as a date is measured from the controlled clock.
        /// </summary>
        [Fact]
        public void RegisterRateLimit_RetryAfterDate_UsesClock()
        {
            BackoffRateLimiter limiter = Create();
            RetryConditionHeaderValue header = new RetryConditionHeaderValue(new DateTimeOffset(now.AddSeconds(45), TimeSpan.Zero));

            Assert.Equal(TimeSpan.FromSeconds(45), limiter.RegisterRateLimit(header));
        }

        /// <summary>
        /// Reset clears the block and the counter.
        /// </summary>
        [Fact]
        public void Reset_ClearsState()
        {
            BackoffRateLimiter limiter = Create();
            limiter.RegisterRateLimit(null);
            limiter.RegisterRateLimit(null);

            Assert.Equal(2, limiter.GetStatus().ConsecutiveRateLimits);

            limiter.Reset();

            Assert.True(limiter.GetStatus().CanQuery);
            Assert.Equal(0, limiter.GetStatus().ConsecutiveRateLimits);
            Assert.Equal(DateTime.MinValue, limiter.GetStatus().BlockedUntilUtc);
        }

        /// <summary>
        /// The counter stops growing at ten so the pause stays at the maximum.
        /// </summary>
        [Fact]
        public void RegisterRateLimit_CounterIsCapped()
        {
            BackoffRateLimiter limiter = Create();

            for (int i = 0; i < 30; i++)
            {
                limiter.RegisterRateLimit(null);
            }

            Assert.Equal(10, limiter.GetStatus().ConsecutiveRateLimits);
        }
    }
}

using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.RateLimiting
{
    /// <summary>
    /// Pauses the requests of one data source after limit responses (429), lengthening the pause exponentially and honoring the Retry-After header.
    /// </summary>
    public sealed class BackoffRateLimiter
    {
        /// <summary>
        /// Default first pause after a limit response.
        /// </summary>
        public static readonly TimeSpan DefaultBaseBackoff = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Default longest pause resulting from the exponential growth.
        /// </summary>
        public static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Default longest pause requested by the server with the Retry-After header.
        /// </summary>
        public static readonly TimeSpan DefaultMaxRetryAfter = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Lock protecting the state.
        /// </summary>
        private readonly object stateLock = new object();

        /// <summary>
        /// First pause after a limit response, doubled on every following one.
        /// </summary>
        private readonly TimeSpan baseBackoff;

        /// <summary>
        /// Longest pause resulting from the exponential growth.
        /// </summary>
        private readonly TimeSpan maxBackoff;

        /// <summary>
        /// Longest pause requested by the server with the Retry-After header.
        /// </summary>
        private readonly TimeSpan maxRetryAfter;

        /// <summary>
        /// Source of the current time in UTC, replaceable in tests.
        /// </summary>
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Moment until which the requests are paused, in UTC.
        /// </summary>
        private DateTime blockedUntilUtc = DateTime.MinValue;

        /// <summary>
        /// Number of consecutive limit responses without a successful request between them.
        /// </summary>
        private int consecutiveRateLimits;

        /// <summary>
        /// Creates a limiter with the default pauses and the system clock.
        /// </summary>
        public BackoffRateLimiter()
            : this(DefaultBaseBackoff, DefaultMaxBackoff, DefaultMaxRetryAfter, () => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Creates a limiter with the given pauses and clock.
        /// </summary>
        /// <param name="baseBackoff">First pause after a limit response.</param>
        /// <param name="maxBackoff">Longest pause resulting from the exponential growth.</param>
        /// <param name="maxRetryAfter">Longest pause requested by the server with the Retry-After header.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        public BackoffRateLimiter(TimeSpan baseBackoff, TimeSpan maxBackoff, TimeSpan maxRetryAfter, Func<DateTime> utcNow)
        {
            this.baseBackoff = baseBackoff;
            this.maxBackoff = maxBackoff;
            this.maxRetryAfter = maxRetryAfter;
            this.utcNow = utcNow;
        }

        /// <summary>
        /// Returns how long the requests are still paused.
        /// </summary>
        /// <returns>The remaining pause or zero when the requests are allowed.</returns>
        public TimeSpan GetRemainingBlock()
        {
            lock (stateLock)
            {
                TimeSpan remaining = blockedUntilUtc - utcNow();

                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Remembers a limit response and lengthens the pause of the requests exponentially, honoring the Retry-After header.
        /// </summary>
        /// <param name="retryAfter">Retry-After header of the response or null.</param>
        /// <returns>The pause of the requests.</returns>
        public TimeSpan RegisterRateLimit(RetryConditionHeaderValue? retryAfter)
        {
            lock (stateLock)
            {
                consecutiveRateLimits = Math.Min(consecutiveRateLimits + 1, 10);

                double backoffSeconds = Math.Min(maxBackoff.TotalSeconds, baseBackoff.TotalSeconds * Math.Pow(2.0, consecutiveRateLimits - 1));
                TimeSpan wait = TimeSpan.FromSeconds(backoffSeconds);
                TimeSpan? requested = retryAfter?.Delta ?? (retryAfter?.Date - utcNow());

                if (requested.HasValue && requested.Value > wait)
                {
                    wait = requested.Value > maxRetryAfter ? maxRetryAfter : requested.Value;
                }

                blockedUntilUtc = utcNow() + wait;

                return wait;
            }
        }

        /// <summary>
        /// Clears the pause of the requests after a successful response.
        /// </summary>
        public void Reset()
        {
            lock (stateLock)
            {
                consecutiveRateLimits = 0;
                blockedUntilUtc = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reads the current state of the limiter.
        /// </summary>
        /// <returns>The state of the limits at the moment of the call.</returns>
        public RateLimitStatus GetStatus()
        {
            lock (stateLock)
            {
                TimeSpan remaining = blockedUntilUtc - utcNow();

                return new RateLimitStatus(blockedUntilUtc, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, consecutiveRateLimits);
            }
        }
    }
}

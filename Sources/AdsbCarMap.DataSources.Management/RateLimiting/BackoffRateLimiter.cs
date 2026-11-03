
namespace AdsbCarMap.DataSources.Management.RateLimiting
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
        /// Failure of the last request when no successful request followed it, otherwise null.
        /// </summary>
        private SourceFailure? lastFailure;

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
        /// <param name="retryAfter">Pause requested by the server with the Retry-After header; zero or less when the server did not say.</param>
        /// <returns>The pause of the requests.</returns>
        public TimeSpan RegisterRateLimit(TimeSpan retryAfter)
        {
            lock (stateLock)
            {
                consecutiveRateLimits = Math.Min(consecutiveRateLimits + 1, 10);

                double backoffSeconds = Math.Min(maxBackoff.TotalSeconds, baseBackoff.TotalSeconds * Math.Pow(2.0, consecutiveRateLimits - 1));
                TimeSpan wait = TimeSpan.FromSeconds(backoffSeconds);
                TimeSpan requested = retryAfter;

                if (requested > wait)
                {
                    wait = requested > maxRetryAfter ? maxRetryAfter : requested;
                }

                blockedUntilUtc = utcNow() + wait;
                lastFailure = new SourceFailure(utcNow(), 429, "Przekroczono limit zapytań (429)");

                return wait;
            }
        }

        /// <summary>
        /// Remembers a failure that was not a limit response, so that it can be shown. It does not pause the requests.
        /// </summary>
        /// <param name="exception">The failure: an HTTP error (with its status code, if any), an invalid answer or another error of the connection.</param>
        public void RegisterFailure(Exception exception)
        {
            int? statusCode = exception is HttpRequestException http && http.StatusCode.HasValue ? (int)http.StatusCode.Value : null;
            string description = statusCode.HasValue ? "Błąd HTTP " + statusCode.Value : exception is System.Text.Json.JsonException ? "Nieprawidłowa odpowiedź" : "Błąd sieci";

            lock (stateLock)
            {
                lastFailure = new SourceFailure(utcNow(), statusCode, description);
            }
        }

        /// <summary>
        /// Clears the pause of the requests and the remembered failure after a successful response.
        /// </summary>
        public void Reset()
        {
            lock (stateLock)
            {
                consecutiveRateLimits = 0;
                blockedUntilUtc = DateTime.MinValue;
                lastFailure = null;
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

                return new RateLimitStatus(blockedUntilUtc, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, consecutiveRateLimits, lastFailure);
            }
        }
    }
}

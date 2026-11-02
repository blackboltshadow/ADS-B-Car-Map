using System.Globalization;
using System.Net;

namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Error thrown when the server answered with 429 or a client pauses the requests after such an answer.
    /// </summary>
    public sealed class RateLimitedException : HttpRequestException
    {
        /// <summary>
        /// Creates the exception with the time after which the request can be repeated.
        /// </summary>
        /// <param name="retryAfter">Time to wait before the next request; zero when the server did not say.</param>
        public RateLimitedException(TimeSpan retryAfter)
            : base(string.Format(CultureInfo.InvariantCulture, "Przekroczono limit zapytań (429), ponowienie za {0:0} s", retryAfter.TotalSeconds), null, HttpStatusCode.TooManyRequests)
        {
            RetryAfter = retryAfter;
        }

        /// <summary>
        /// Time to wait before the next request; zero when the server did not say.
        /// </summary>
        public TimeSpan RetryAfter { get; }
    }
}

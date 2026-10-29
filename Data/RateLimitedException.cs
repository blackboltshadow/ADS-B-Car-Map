using System.Globalization;
using System.Net;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Błąd zgłaszany, gdy serwer odpowiedział kodem 429 albo klient wstrzymuje zapytania po takiej odpowiedzi.
    /// </summary>
    public sealed class RateLimitedException : HttpRequestException
    {
        /// <summary>
        /// Tworzy wyjątek z czasem, po którym można ponowić zapytanie.
        /// </summary>
        /// <param name="retryAfter">Czas oczekiwania przed kolejnym zapytaniem.</param>
        public RateLimitedException(TimeSpan retryAfter)
            : base(string.Format(CultureInfo.InvariantCulture, "Przekroczono limit zapytań (429), ponowienie za {0:0} s", retryAfter.TotalSeconds), null, HttpStatusCode.TooManyRequests)
        {
            RetryAfter = retryAfter;
        }

        /// <summary>
        /// Czas oczekiwania przed kolejnym zapytaniem.
        /// </summary>
        public TimeSpan RetryAfter { get; }
    }
}

using System.Diagnostics;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// HTTP handler that records every request and its outcome in a <see cref="RequestLog"/> and passes the request on unchanged.
    /// </summary>
    public sealed class RequestLoggingHandler : DelegatingHandler
    {
        /// <summary>
        /// Log that receives the requests.
        /// </summary>
        private readonly RequestLog log;

        /// <summary>
        /// Creates the handler.
        /// </summary>
        /// <param name="log">Log that receives the requests.</param>
        /// <param name="inner">Handler that actually sends the requests.</param>
        public RequestLoggingHandler(RequestLog log, HttpMessageHandler inner)
            : base(inner)
        {
            ArgumentNullException.ThrowIfNull(log);
            this.log = log;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            DateTime started = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                Record(request, started, stopwatch, (int)response.StatusCode, null, false);

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Record(request, started, stopwatch, null, null, true);

                throw;
            }
            catch (Exception exception)
            {
                Record(request, started, stopwatch, null, exception is TaskCanceledException ? "Przekroczono czas oczekiwania" : exception.GetType().Name + ": " + exception.Message, false);

                throw;
            }
        }

        /// <summary>
        /// Adds a request to the log.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="started">Moment at which the request was sent.</param>
        /// <param name="stopwatch">Stopwatch started when the request was sent.</param>
        /// <param name="statusCode">Status code of the answer, or null.</param>
        /// <param name="error">Description of the failure, or null.</param>
        /// <param name="cancelled">Whether the application cancelled the request.</param>
        private void Record(HttpRequestMessage request, DateTime started, Stopwatch stopwatch, int? statusCode, string? error, bool cancelled)
        {
            Uri? uri = request.RequestUri;

            log.Add(new RequestLogEntry(started, request.Method.Method, uri?.Host ?? string.Empty, uri?.PathAndQuery ?? string.Empty, statusCode, stopwatch.Elapsed, error, cancelled));
        }
    }
}

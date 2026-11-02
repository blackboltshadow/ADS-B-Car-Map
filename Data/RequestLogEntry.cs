namespace AdsbCarMap.Data
{
    /// <summary>
    /// One request that the application sent to an external server.
    /// </summary>
    /// <param name="TimeUtc">Moment at which the request was sent, in UTC.</param>
    /// <param name="Method">HTTP method, e.g. GET.</param>
    /// <param name="Host">Host the request was sent to.</param>
    /// <param name="PathAndQuery">Path and query of the request, as sent.</param>
    /// <param name="StatusCode">HTTP status code of the answer, or null when there was no answer.</param>
    /// <param name="Duration">Time from sending the request to the answer or the failure.</param>
    /// <param name="Error">Description of the failure when there was no answer (network error, timeout), otherwise null.</param>
    /// <param name="Cancelled">Whether the application cancelled the request itself, e.g. because the map moved on.</param>
    public sealed record RequestLogEntry(DateTime TimeUtc, string Method, string Host, string PathAndQuery, int? StatusCode, TimeSpan Duration, string? Error, bool Cancelled)
    {
        /// <summary>
        /// Whether the request failed: the server answered with an error status or there was no answer. A request cancelled by the application is not a failure.
        /// </summary>
        public bool IsError => !Cancelled && (Error != null || StatusCode >= 400);
    }
}

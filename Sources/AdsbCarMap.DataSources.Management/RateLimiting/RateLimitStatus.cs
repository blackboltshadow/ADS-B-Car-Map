namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// State of the request limits of a data source at a given moment.
    /// </summary>
    /// <param name="BlockedUntilUtc">Moment until which the source must not be queried, in UTC, or DateTime.MinValue when it is not blocked.</param>
    /// <param name="RemainingBlock">Time left until the source can be queried again, zero when it can be queried now.</param>
    /// <param name="ConsecutiveRateLimits">Number of consecutive limit responses without a successful request between them.</param>
    public sealed record RateLimitStatus(DateTime BlockedUntilUtc, TimeSpan RemainingBlock, int ConsecutiveRateLimits)
    {
        /// <summary>
        /// State of a source that is not limited.
        /// </summary>
        public static RateLimitStatus None { get; } = new RateLimitStatus(DateTime.MinValue, TimeSpan.Zero, 0);

        /// <summary>
        /// Whether the source can be queried now.
        /// </summary>
        public bool CanQuery => RemainingBlock <= TimeSpan.Zero;
    }
}

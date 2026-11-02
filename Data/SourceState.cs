using AdsbCarMap.DataSources.Management.RateLimiting;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// State of one data source as shown on the source status card.
    /// </summary>
    /// <param name="Group">What the source is used for, e.g. "Samoloty" or "Trasy".</param>
    /// <param name="Name">Name of the source.</param>
    /// <param name="Status">State of its request limits and its last failure.</param>
    /// <param name="IsLastUsed">Whether the source gave the last successful answer of its group.</param>
    public sealed record SourceState(string Group, string Name, RateLimitStatus Status, bool IsLastUsed);
}

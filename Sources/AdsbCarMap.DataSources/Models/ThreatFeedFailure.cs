namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// The last failure of a threat feed, kept after the feed stopped so that the state of the source can be shown later.
    /// </summary>
    /// <param name="Description">Short description of what failed.</param>
    /// <param name="OccurredUtc">Time of the failure.</param>
    public sealed record ThreatFeedFailure(string Description, DateTime OccurredUtc);
}

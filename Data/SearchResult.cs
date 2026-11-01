namespace AdsbCarMap.Data
{
    /// <summary>
    /// One row of the combined search results: either an airport or an aircraft.
    /// </summary>
    /// <param name="Airport">The airport, or null when the row is an aircraft.</param>
    /// <param name="Aircraft">The aircraft, or null when the row is an airport.</param>
    /// <param name="Quality">How well the row matches the query: 3 exact code, 2 beginning of a word, 1 somewhere inside, 0 when there was no query.</param>
    public sealed record SearchResult(Airport? Airport, Aircraft? Aircraft, int Quality);
}

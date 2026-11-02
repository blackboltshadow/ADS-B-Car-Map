namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// The last request of a data source that gave no usable answer.
    /// </summary>
    /// <param name="OccurredUtc">Moment of the failure, in UTC.</param>
    /// <param name="StatusCode">HTTP status code of the answer (e.g. 403 or 429), or null when there was no answer or it was invalid.</param>
    /// <param name="Description">Short description of the failure, the same for the same cause.</param>
    public sealed record SourceFailure(DateTime OccurredUtc, int? StatusCode, string Description);
}

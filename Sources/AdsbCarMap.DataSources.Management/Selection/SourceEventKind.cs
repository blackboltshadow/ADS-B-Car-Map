namespace AdsbCarMap.DataSources.Management.Selection
{
    /// <summary>
    /// Kind of an event that the fallback sources report to the application, for example to its diagnostic log.
    /// </summary>
    public enum SourceEventKind
    {
        /// <summary>
        /// Worth knowing, but nothing went wrong, for example the application switched to another source.
        /// </summary>
        Information,

        /// <summary>
        /// A source did not answer: a limit response, a refusal, a network error or an invalid answer.
        /// </summary>
        Failure,
    }
}

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Result of evaluating a filter expression for one aircraft, in three-valued logic.
    /// </summary>
    public enum FilterResult
    {
        /// <summary>
        /// The aircraft does not satisfy the condition.
        /// </summary>
        False,

        /// <summary>
        /// The aircraft satisfies the condition.
        /// </summary>
        True,

        /// <summary>
        /// The data needed to decide is missing, for example the route has not been downloaded yet.
        /// </summary>
        Unknown,
    }
}

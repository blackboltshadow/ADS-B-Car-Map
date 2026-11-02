namespace AdsbCarMap.Data
{
    /// <summary>
    /// What an active filter does with the aircraft that satisfy its expression.
    /// </summary>
    public enum FilterMode
    {
        /// <summary>
        /// Only the matching aircraft are shown. When several such filters are active an aircraft is shown if it matches any of them.
        /// </summary>
        Show,

        /// <summary>
        /// The matching aircraft are hidden.
        /// </summary>
        Hide,
    }
}

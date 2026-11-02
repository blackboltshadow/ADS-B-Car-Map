namespace AdsbCarMap.Data
{
    /// <summary>
    /// Way a group of filter conditions is combined.
    /// </summary>
    public enum FilterGroupMode
    {
        /// <summary>
        /// All conditions must be satisfied (logical AND).
        /// </summary>
        All,

        /// <summary>
        /// At least one condition must be satisfied (logical OR).
        /// </summary>
        Any,
    }
}

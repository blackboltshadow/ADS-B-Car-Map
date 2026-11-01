namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Category of airports that can be switched on or off on the map. The values are the bits of the airport types setting. A military airport belongs to the military category whatever its size.
    /// </summary>
    [Flags]
    public enum AirportCategory
    {
        /// <summary>
        /// No category.
        /// </summary>
        None = 0,

        /// <summary>
        /// Large civil airports.
        /// </summary>
        Large = 1,

        /// <summary>
        /// Medium civil airports.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// Small civil airports and airfields.
        /// </summary>
        Small = 4,

        /// <summary>
        /// Military airports of any size.
        /// </summary>
        Military = 8,

        /// <summary>
        /// Civil heliports and helipads.
        /// </summary>
        Heliport = 16,
    }
}

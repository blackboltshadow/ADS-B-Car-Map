namespace AdsbCarMap.Data
{
    /// <summary>
    /// Named filter: an expression together with the way its result is used.
    /// </summary>
    public sealed class AircraftFilter
    {
        /// <summary>
        /// Creates a filter.
        /// </summary>
        /// <param name="id">Stable identifier of the filter.</param>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="mode">Whether the matching aircraft are shown or hidden.</param>
        /// <param name="expression">Expression deciding which aircraft match.</param>
        public AircraftFilter(string id, string name, FilterMode mode, FilterNode expression)
        {
            Id = id;
            Name = name;
            Mode = mode;
            Expression = expression;
        }

        /// <summary>
        /// Stable identifier of the filter.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Name shown to the user.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether the matching aircraft are shown or hidden.
        /// </summary>
        public FilterMode Mode { get; }

        /// <summary>
        /// Expression deciding which aircraft match.
        /// </summary>
        public FilterNode Expression { get; }
    }
}

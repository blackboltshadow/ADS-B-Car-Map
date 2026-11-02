namespace AdsbCarMap.Data
{
    /// <summary>
    /// Kind of value a filter condition needs next to its field.
    /// </summary>
    public enum FilterOperandType
    {
        /// <summary>
        /// The condition has no operand.
        /// </summary>
        None,

        /// <summary>
        /// Free text, for example a type code or an airport.
        /// </summary>
        Text,

        /// <summary>
        /// One of the aircraft kinds.
        /// </summary>
        Kind,

        /// <summary>
        /// One of the known manufacturers.
        /// </summary>
        Manufacturer,

        /// <summary>
        /// A range of numbers with optional bounds.
        /// </summary>
        Range,
    }
}

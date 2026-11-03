namespace AdsbCarMap.Data
{
    /// <summary>
    /// State of an airspace area at a given moment. The values are bits, so the same type also describes a set of states the user wants to see.
    /// </summary>
    [Flags]
    public enum AirspaceZoneState
    {
        /// <summary>
        /// No state; an empty set.
        /// </summary>
        None = 0,

        /// <summary>
        /// A reservation confirmed as active is in force now.
        /// </summary>
        Active = 1,

        /// <summary>
        /// Nothing is confirmed as active now, but a reservation is announced for now or for later.
        /// </summary>
        Planned = 2,

        /// <summary>
        /// All reservations are over, or there are none.
        /// </summary>
        Inactive = 4,

        /// <summary>
        /// All the states.
        /// </summary>
        All = Active | Planned | Inactive,
    }
}

namespace AdsbCarMap.Data
{
    /// <summary>
    /// State of the flight route lookup for a callsign.
    /// </summary>
    public enum RouteLookupState
    {
        /// <summary>
        /// There is no callsign, so the route cannot be looked up.
        /// </summary>
        NotApplicable,

        /// <summary>
        /// The route is queued or being downloaded right now.
        /// </summary>
        Pending,

        /// <summary>
        /// The route is known.
        /// </summary>
        Found,

        /// <summary>
        /// The server answered that it does not know the route of this flight.
        /// </summary>
        NotFound,

        /// <summary>
        /// The last download failed, e.g. because of a network error or the request limit.
        /// </summary>
        Failed,
    }
}

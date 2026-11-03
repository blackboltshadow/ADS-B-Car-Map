namespace AdsbCarMap.Data
{
    /// <summary>
    /// Way in which the area of an alert is defined.
    /// </summary>
    public enum AlertAreaKind
    {
        /// <summary>
        /// Circle around the current position of the user.
        /// </summary>
        AroundMe,

        /// <summary>
        /// Circle around an airport identified by its ICAO code.
        /// </summary>
        AroundAirport,

        /// <summary>
        /// Circle with a fixed center chosen on the map.
        /// </summary>
        Circle,

        /// <summary>
        /// Polygon (or rectangle) with the vertices drawn on the map.
        /// </summary>
        Polygon,
    }
}

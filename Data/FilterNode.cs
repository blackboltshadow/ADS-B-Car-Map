using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Node of a filter expression tree.
    /// </summary>
    public abstract class FilterNode
    {
        /// <summary>
        /// Whether evaluating the node may need the route of the flight, which is downloaded in the background and is expensive compared with the other conditions.
        /// </summary>
        public abstract bool NeedsRoute { get; }

        /// <summary>
        /// Evaluates the node for an aircraft.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet. It may start downloading the route in the background.</param>
        /// <returns>Result in three-valued logic.</returns>
        public abstract FilterResult Evaluate(Aircraft aircraft, Func<string?, FlightRoute?> routeLookup);
    }
}

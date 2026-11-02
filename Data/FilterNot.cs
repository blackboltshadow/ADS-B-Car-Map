using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Negation of a filter node. An unknown result stays unknown.
    /// </summary>
    public sealed class FilterNot : FilterNode
    {
        /// <summary>
        /// Creates a negation.
        /// </summary>
        /// <param name="inner">Negated node.</param>
        public FilterNot(FilterNode inner)
        {
            Inner = inner;
        }

        /// <summary>
        /// Negated node.
        /// </summary>
        public FilterNode Inner { get; }

        /// <summary>
        /// Whether the negated node may need the route of the flight.
        /// </summary>
        public override bool NeedsRoute => Inner.NeedsRoute;

        /// <summary>
        /// Evaluates the negation.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>Opposite of the inner result, or unknown when the inner result is unknown.</returns>
        public override FilterResult Evaluate(Aircraft aircraft, Func<string?, FlightRoute?> routeLookup)
        {
            FilterResult result = Inner.Evaluate(aircraft, routeLookup);

            return result switch
            {
                FilterResult.True => FilterResult.False,
                FilterResult.False => FilterResult.True,
                _ => FilterResult.Unknown,
            };
        }
    }
}

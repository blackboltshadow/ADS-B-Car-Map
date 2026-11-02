using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Group of filter nodes combined with AND or OR. Evaluation stops at the first child that decides the result. AND and OR do not depend on the order of the children, so the children that need the route of the flight are evaluated last, which avoids downloading routes of aircraft already decided by the cheap conditions.
    /// </summary>
    public sealed class FilterGroup : FilterNode
    {
        /// <summary>
        /// The children in the order they are evaluated: those that do not need the route first. The order inside both parts is the original one.
        /// </summary>
        private readonly FilterNode[] evaluationOrder;

        /// <summary>
        /// Creates a group.
        /// </summary>
        /// <param name="mode">Way the children are combined.</param>
        /// <param name="children">Child nodes.</param>
        public FilterGroup(FilterGroupMode mode, IReadOnlyList<FilterNode> children)
        {
            Mode = mode;
            Children = children;
            evaluationOrder = children.OrderBy((FilterNode child) => child.NeedsRoute ? 1 : 0).ToArray();
            NeedsRoute = children.Any((FilterNode child) => child.NeedsRoute);
        }

        /// <summary>
        /// Way the children are combined.
        /// </summary>
        public FilterGroupMode Mode { get; }

        /// <summary>
        /// Child nodes in the order given by the author of the filter.
        /// </summary>
        public IReadOnlyList<FilterNode> Children { get; }

        /// <summary>
        /// Whether any child may need the route of the flight.
        /// </summary>
        public override bool NeedsRoute { get; }

        /// <summary>
        /// Creates a group in which all conditions must be satisfied.
        /// </summary>
        /// <param name="children">Child nodes.</param>
        /// <returns>The group.</returns>
        public static FilterGroup All(params FilterNode[] children)
        {
            return new FilterGroup(FilterGroupMode.All, children);
        }

        /// <summary>
        /// Creates a group in which at least one condition must be satisfied.
        /// </summary>
        /// <param name="children">Child nodes.</param>
        /// <returns>The group.</returns>
        public static FilterGroup Any(params FilterNode[] children)
        {
            return new FilterGroup(FilterGroupMode.Any, children);
        }

        /// <summary>
        /// Evaluates the group. An empty group is true for All and false for Any.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>Result in three-valued logic.</returns>
        public override FilterResult Evaluate(Aircraft aircraft, Func<string?, FlightRoute?> routeLookup)
        {
            FilterResult decisive = Mode == FilterGroupMode.All ? FilterResult.False : FilterResult.True;
            FilterResult neutral = Mode == FilterGroupMode.All ? FilterResult.True : FilterResult.False;
            FilterResult result = neutral;

            foreach (FilterNode child in evaluationOrder)
            {
                FilterResult childResult = child.Evaluate(aircraft, routeLookup);

                if (childResult == decisive)
                {
                    return decisive;
                }

                if (childResult == FilterResult.Unknown)
                {
                    result = FilterResult.Unknown;
                }
            }

            return result;
        }
    }
}

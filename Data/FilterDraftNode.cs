namespace AdsbCarMap.Data
{
    /// <summary>
    /// Node of the filter tree being edited in the filter editor: a condition or a group of nodes. Every node can be negated.
    /// </summary>
    public abstract class FilterDraftNode
    {
        /// <summary>
        /// Whether the node is negated (NOT).
        /// </summary>
        public bool Negated { get; set; }

        /// <summary>
        /// The group the node belongs to, or null for the root group and for a node that has not been added to a group yet.
        /// </summary>
        public FilterDraftGroup? Parent { get; internal set; }

        /// <summary>
        /// Builds the expression node, wrapped in a negation when the node is negated.
        /// </summary>
        /// <returns>The expression node.</returns>
        public abstract FilterNode ToNode();

        /// <summary>
        /// Creates a deep copy of the node that does not belong to any group.
        /// </summary>
        /// <returns>The copy.</returns>
        public abstract FilterDraftNode Clone();

        /// <summary>
        /// Reads a draft node from an expression node. Negations are folded into the <see cref="Negated"/> flag; two negations cancel out.
        /// </summary>
        /// <param name="node">Expression node.</param>
        /// <returns>The draft node, or null when the expression contains a node the editor does not know.</returns>
        public static FilterDraftNode? FromNode(FilterNode node)
        {
            bool negated = false;

            while (node is FilterNot negation)
            {
                negated = !negated;
                node = negation.Inner;
            }

            if (node is FilterGroup group)
            {
                FilterDraftGroup draftGroup = new FilterDraftGroup();

                draftGroup.Mode = group.Mode;
                draftGroup.Negated = negated;

                foreach (FilterNode child in group.Children)
                {
                    FilterDraftNode? draftChild = FromNode(child);

                    if (draftChild == null)
                    {
                        return null;
                    }

                    draftGroup.Add(draftChild);
                }

                return draftGroup;
            }

            if (node is FilterCondition condition)
            {
                FilterDraftCondition draftCondition = new FilterDraftCondition();

                draftCondition.Field = condition.Field;
                draftCondition.Negated = negated;
                draftCondition.Text = condition.Text ?? string.Empty;
                draftCondition.Kind = condition.Kind == AircraftKind.Unknown ? AircraftKind.Airliner : condition.Kind;
                draftCondition.Family = condition.Family;
                draftCondition.Minimum = condition.Minimum;
                draftCondition.Maximum = condition.Maximum;

                return draftCondition;
            }

            return null;
        }

        /// <summary>
        /// Wraps an expression node in a negation when the draft node is negated.
        /// </summary>
        /// <param name="node">Expression node built from this draft node.</param>
        /// <returns>The node or its negation.</returns>
        protected FilterNode ApplyNegation(FilterNode node)
        {
            return Negated ? new FilterNot(node) : node;
        }
    }
}

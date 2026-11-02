namespace AdsbCarMap.Data
{
    /// <summary>
    /// Group in the filter tree being edited: nodes combined with AND or OR. A group can contain other groups.
    /// </summary>
    public sealed class FilterDraftGroup : FilterDraftNode
    {
        /// <summary>
        /// Children of the group in the order shown in the editor.
        /// </summary>
        private readonly List<FilterDraftNode> children = new List<FilterDraftNode>();

        /// <summary>
        /// Way the children are combined.
        /// </summary>
        public FilterGroupMode Mode { get; set; } = FilterGroupMode.All;

        /// <summary>
        /// Whether the group is collapsed in the editor. It is not stored in the filter.
        /// </summary>
        public bool Collapsed { get; set; }

        /// <summary>
        /// Children of the group.
        /// </summary>
        public IReadOnlyList<FilterDraftNode> Children => children;

        /// <summary>
        /// Appends a node to the group.
        /// </summary>
        /// <param name="node">Node that does not belong to any group.</param>
        public void Add(FilterDraftNode node)
        {
            Insert(children.Count, node);
        }

        /// <summary>
        /// Inserts a node at a position in the group.
        /// </summary>
        /// <param name="index">Position, from 0 to the number of children.</param>
        /// <param name="node">Node that does not belong to any group.</param>
        public void Insert(int index, FilterDraftNode node)
        {
            node.Parent = this;
            children.Insert(index, node);
        }

        /// <summary>
        /// Removes a node from the group.
        /// </summary>
        /// <param name="node">Child to remove.</param>
        /// <returns>True when the node was a child of the group.</returns>
        public bool RemoveChild(FilterDraftNode node)
        {
            if (!children.Remove(node))
            {
                return false;
            }

            node.Parent = null;

            return true;
        }

        /// <summary>
        /// Returns the position of a child.
        /// </summary>
        /// <param name="node">Child.</param>
        /// <returns>Position, or -1 when the node is not a child of the group.</returns>
        public int IndexOf(FilterDraftNode node)
        {
            return children.IndexOf(node);
        }

        /// <summary>
        /// Builds the expression node of the group.
        /// </summary>
        /// <returns>The group, wrapped in a negation when the node is negated.</returns>
        public override FilterNode ToNode()
        {
            List<FilterNode> nodes = new List<FilterNode>();

            foreach (FilterDraftNode child in children)
            {
                nodes.Add(child.ToNode());
            }

            return ApplyNegation(new FilterGroup(Mode, nodes));
        }

        /// <summary>
        /// Creates a deep copy of the group.
        /// </summary>
        /// <returns>The copy.</returns>
        public override FilterDraftNode Clone()
        {
            FilterDraftGroup copy = new FilterDraftGroup();

            copy.Mode = Mode;
            copy.Negated = Negated;
            copy.Collapsed = Collapsed;

            foreach (FilterDraftNode child in children)
            {
                copy.Add(child.Clone());
            }

            return copy;
        }
    }
}

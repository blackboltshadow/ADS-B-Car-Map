using AdsbCarMap.Localization;
namespace AdsbCarMap.Data
{
    /// <summary>
    /// Filter being edited in the filter editor: a name, a mode and a tree of conditions and groups, each of which can be negated.
    /// </summary>
    public sealed class FilterDraft
    {
        /// <summary>
        /// Maximum length of the filter name.
        /// </summary>
        public const int MaxNameLength = 40;

        /// <summary>
        /// Maximum nesting depth of the tree that can be saved. It is far beyond practical use and guarantees that the saved filter can always be read back.
        /// </summary>
        public const int MaxDepth = 100;

        /// <summary>
        /// Name of the filter.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the matching aircraft are shown or hidden.
        /// </summary>
        public FilterMode Mode { get; set; } = FilterMode.Show;

        /// <summary>
        /// The top group of the tree. It cannot be removed or moved, but it can be negated and its mode can be changed.
        /// </summary>
        public FilterDraftGroup Root { get; private set; } = new FilterDraftGroup();

        /// <summary>
        /// Creates a draft of a new filter with one empty condition.
        /// </summary>
        /// <returns>The draft.</returns>
        public static FilterDraft CreateNew()
        {
            FilterDraft draft = new FilterDraft();

            draft.Root.Add(new FilterDraftCondition());

            return draft;
        }

        /// <summary>
        /// Reads a draft from an existing filter. Any expression can be read.
        /// </summary>
        /// <param name="filter">Filter to read.</param>
        /// <returns>The draft, or null when the expression contains a node the editor does not know.</returns>
        public static FilterDraft? FromFilter(AircraftFilter filter)
        {
            FilterDraftNode? node = FilterDraftNode.FromNode(filter.Expression);

            if (node == null)
            {
                return null;
            }

            FilterDraft draft = new FilterDraft();

            draft.Name = filter.Name;
            draft.Mode = filter.Mode;

            if (node is FilterDraftGroup group)
            {
                draft.Root = group;
            }
            else
            {
                draft.Root.Add(node);
            }

            return draft;
        }

        /// <summary>
        /// Builds the filter from the draft. The draft must be correct.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <returns>The filter.</returns>
        public AircraftFilter ToFilter(string id)
        {
            return new AircraftFilter(id, Name.Trim(), Mode, Root.ToNode());
        }

        /// <summary>
        /// Checks the draft.
        /// </summary>
        /// <returns>Polish description of the first problem, or null when the draft is correct.</returns>
        public string? Validate()
        {
            return Validate(out FilterDraftNode? problemNode);
        }

        /// <summary>
        /// Checks the draft.
        /// </summary>
        /// <param name="problemNode">The node the problem refers to, or null when the problem concerns the whole filter or there is none.</param>
        /// <returns>Polish description of the first problem, or null when the draft is correct.</returns>
        public string? Validate(out FilterDraftNode? problemNode)
        {
            problemNode = null;

            string name = Name.Trim();

            if (name.Length == 0)
            {
                return Loc.T("Podaj nazwę filtru.");
            }

            if (name.Length > MaxNameLength)
            {
                return Loc.T("Nazwa filtru może mieć najwyżej ") + MaxNameLength + Loc.T(" znaków.");
            }

            (string Message, FilterDraftNode Node)? problem = FindProblem(Root, 0);

            if (problem == null)
            {
                return null;
            }

            problemNode = problem.Value.Node;

            return problem.Value.Message;
        }

        /// <summary>
        /// Returns the depth of a node in the tree; the root group has depth 0.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>Number of groups above the node.</returns>
        public static int GetDepth(FilterDraftNode node)
        {
            int depth = 0;

            for (FilterDraftGroup? parent = node.Parent; parent != null; parent = parent.Parent)
            {
                depth++;
            }

            return depth;
        }

        /// <summary>
        /// Lists all groups of the tree, the root first, in the order they are shown.
        /// </summary>
        /// <returns>The groups.</returns>
        public IReadOnlyList<FilterDraftGroup> GetGroups()
        {
            List<FilterDraftGroup> groups = new List<FilterDraftGroup>();

            CollectGroups(Root, groups);

            return groups;
        }

        /// <summary>
        /// Removes a node from its group. The root cannot be removed.
        /// </summary>
        /// <param name="node">Node to remove.</param>
        /// <returns>True when the node was removed.</returns>
        public bool Remove(FilterDraftNode node)
        {
            return node.Parent != null && node.Parent.RemoveChild(node);
        }

        /// <summary>
        /// Tells whether a node can be moved one position up inside its group.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>True when the node is not the first child of its group.</returns>
        public bool CanMoveUp(FilterDraftNode node)
        {
            return node.Parent != null && node.Parent.IndexOf(node) > 0;
        }

        /// <summary>
        /// Tells whether a node can be moved one position down inside its group.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>True when the node is not the last child of its group.</returns>
        public bool CanMoveDown(FilterDraftNode node)
        {
            return node.Parent != null && node.Parent.IndexOf(node) < node.Parent.Children.Count - 1;
        }

        /// <summary>
        /// Moves a node one position up inside its group.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>True when the node was moved.</returns>
        public bool MoveUp(FilterDraftNode node)
        {
            return Shift(node, -1);
        }

        /// <summary>
        /// Moves a node one position down inside its group.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>True when the node was moved.</returns>
        public bool MoveDown(FilterDraftNode node)
        {
            return Shift(node, 1);
        }

        /// <summary>
        /// Tells whether a node can be moved to the end of another group. A node cannot be moved into itself or into a group inside it, and the root cannot be moved.
        /// </summary>
        /// <param name="node">Node to move.</param>
        /// <param name="target">Target group.</param>
        /// <returns>True when the move is allowed.</returns>
        public bool CanMoveTo(FilterDraftNode node, FilterDraftGroup target)
        {
            if (node.Parent == null || ReferenceEquals(node, target))
            {
                return false;
            }

            for (FilterDraftGroup? ancestor = target.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ReferenceEquals(ancestor, node))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Moves a node to the end of another group.
        /// </summary>
        /// <param name="node">Node to move.</param>
        /// <param name="target">Target group.</param>
        /// <returns>True when the node was moved.</returns>
        public bool MoveTo(FilterDraftNode node, FilterDraftGroup target)
        {
            if (!CanMoveTo(node, target))
            {
                return false;
            }

            node.Parent!.RemoveChild(node);
            target.Add(node);

            return true;
        }

        /// <summary>
        /// Inserts a copy of a node right after it. The root cannot be copied.
        /// </summary>
        /// <param name="node">Node to copy.</param>
        /// <returns>The copy, or null when the node is the root.</returns>
        public FilterDraftNode? Duplicate(FilterDraftNode node)
        {
            if (node.Parent == null)
            {
                return null;
            }

            FilterDraftNode copy = node.Clone();

            node.Parent.Insert(node.Parent.IndexOf(node) + 1, copy);

            return copy;
        }

        /// <summary>
        /// Moves a node by a number of positions inside its group.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <param name="offset">-1 moves it up, 1 moves it down.</param>
        /// <returns>True when the node was moved.</returns>
        private static bool Shift(FilterDraftNode node, int offset)
        {
            FilterDraftGroup? parent = node.Parent;

            if (parent == null)
            {
                return false;
            }

            int index = parent.IndexOf(node);
            int target = index + offset;

            if (target < 0 || target >= parent.Children.Count)
            {
                return false;
            }

            parent.RemoveChild(node);
            parent.Insert(target, node);

            return true;
        }

        /// <summary>
        /// Adds a group and all groups inside it to a list, depth first.
        /// </summary>
        /// <param name="group">Group to start from.</param>
        /// <param name="groups">List to fill.</param>
        private static void CollectGroups(FilterDraftGroup group, List<FilterDraftGroup> groups)
        {
            groups.Add(group);

            foreach (FilterDraftNode child in group.Children)
            {
                if (child is FilterDraftGroup childGroup)
                {
                    CollectGroups(childGroup, groups);
                }
            }
        }

        /// <summary>
        /// Looks for the first problem in a node and below it.
        /// </summary>
        /// <param name="node">Node to check.</param>
        /// <param name="depth">Depth of the node.</param>
        /// <returns>The problem with the node it refers to, or null when the node is correct.</returns>
        private static (string Message, FilterDraftNode Node)? FindProblem(FilterDraftNode node, int depth)
        {
            if (depth > MaxDepth)
            {
                return (Loc.T("Filtr jest zagnieżdżony zbyt głęboko (najwyżej ") + MaxDepth + Loc.T(" poziomów)."), node);
            }

            if (node is FilterDraftGroup group)
            {
                if (group.Children.Count == 0)
                {
                    return (group.Parent == null ? Loc.T("Dodaj co najmniej jeden warunek.") : Loc.T("Grupa jest pusta. Dodaj do niej warunek albo ją usuń."), group);
                }

                foreach (FilterDraftNode child in group.Children)
                {
                    (string Message, FilterDraftNode Node)? childProblem = FindProblem(child, depth + 1);

                    if (childProblem != null)
                    {
                        return childProblem;
                    }
                }

                return null;
            }

            string? conditionProblem = FindConditionProblem((FilterDraftCondition)node);

            return conditionProblem == null ? null : (conditionProblem, node);
        }

        /// <summary>
        /// Checks the operand of a condition.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>Polish description of the problem or null when the condition is correct.</returns>
        private static string? FindConditionProblem(FilterDraftCondition condition)
        {
            switch (FilterFieldInfo.GetOperandType(condition.Field))
            {
                case FilterOperandType.Text:
                case FilterOperandType.Manufacturer:
                    return string.IsNullOrWhiteSpace(condition.Text) ? Loc.T("Uzupełnij wartość: ") + FilterFieldInfo.GetLabel(condition.Field) + "." : null;
                case FilterOperandType.Range:
                    bool vertical = condition.Field == FilterField.VerticalRate;

                    if (!condition.Minimum.HasValue && !condition.Maximum.HasValue)
                    {
                        return vertical ? Loc.T("Podaj prędkość pionową „od” lub „do”.") : Loc.T("Podaj wysokość „od” lub „do”.");
                    }

                    if (condition.Minimum.HasValue && condition.Maximum.HasValue && condition.Minimum.Value > condition.Maximum.Value)
                    {
                        return vertical ? Loc.T("Prędkość pionowa „od” jest większa niż „do”.") : Loc.T("Wysokość „od” jest większa niż „do”.");
                    }

                    return null;
                default:
                    return null;
            }
        }
    }
}

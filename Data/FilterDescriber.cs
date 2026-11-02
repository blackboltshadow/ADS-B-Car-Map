using System.Globalization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Builds the Polish text that describes a filter, shown in the list of filters and in the filter editor.
    /// </summary>
    public static class FilterDescriber
    {
        /// <summary>
        /// Maximum nesting depth of groups for which a sentence is built. A deeper filter is better understood from the description with parentheses.
        /// </summary>
        public const int MaxSentenceDepth = 4;

        /// <summary>
        /// Describes a filter as a Polish sentence, for example "Pokazuje każdy samolot, który pochodzi od producenta Boeing oraz nie leci do lotniska WAW.". Negations are moved down to the conditions (De Morgan's laws) and nested groups of the same kind are merged, so the sentence is read the same way as the filter works.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <returns>The sentence, or null when the filter is nested deeper than <see cref="MaxSentenceDepth"/> groups.</returns>
        public static string? DescribeSentence(AircraftFilter filter)
        {
            SentenceNode tree = BuildSentence(filter.Expression, false);

            if (GetSentenceDepth(tree) > MaxSentenceDepth)
            {
                return null;
            }

            return (filter.Mode == FilterMode.Show ? "Pokazuje każdy samolot, który " : "Ukrywa każdy samolot, który ") + RenderSentence(tree, false) + ".";
        }

        /// <summary>
        /// Describes a whole filter: what it does with the matching aircraft and which aircraft match.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <returns>For example "Pokaż: producent Boeing ORAZ NIE lotnisko docelowe WAW".</returns>
        public static string Describe(AircraftFilter filter)
        {
            return (filter.Mode == FilterMode.Show ? "Pokaż: " : "Ukryj: ") + DescribeNode(filter.Expression, false);
        }

        /// <summary>
        /// Describes an expression node. A nested group is put in parentheses.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <param name="nested">True when the node is inside another group or negation.</param>
        /// <returns>Description of the node.</returns>
        public static string DescribeNode(FilterNode node, bool nested)
        {
            if (node is FilterNot negation)
            {
                return "NIE " + DescribeNode(negation.Inner, true);
            }

            if (node is FilterGroup group)
            {
                string connector = group.Mode == FilterGroupMode.All ? " ORAZ " : " LUB ";
                string text = string.Join(connector, group.Children.Select((FilterNode child) => DescribeNode(child, true)));

                if (group.Children.Count == 0)
                {
                    return "(pusta grupa)";
                }

                return nested && group.Children.Count > 1 ? "(" + text + ")" : text;
            }

            return node is FilterCondition condition ? DescribeCondition(condition) : string.Empty;
        }

        /// <summary>
        /// Describes a single condition.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>Description of the condition.</returns>
        public static string DescribeCondition(FilterCondition condition)
        {
            switch (condition.Field)
            {
                case FilterField.OnGround:
                    return "na ziemi";
                case FilterField.Military:
                    return "wojskowy";
                case FilterField.Kind:
                    return "rodzaj: " + AircraftClassifier.GetDisplayName(condition.Kind);
                case FilterField.Manufacturer:
                    return "producent " + condition.Text;
                case FilterField.TypeCode:
                    return (condition.Family ? "rodzina typu " : "typ ") + condition.Text;
                case FilterField.CallsignPrefix:
                    return "linia " + condition.Text;
                case FilterField.Altitude:
                    return DescribeRange(condition.Minimum, condition.Maximum);
                case FilterField.Origin:
                    return "lotnisko wylotu " + condition.Text;
                case FilterField.Destination:
                    return "lotnisko docelowe " + condition.Text;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Describes an altitude range in meters.
        /// </summary>
        /// <param name="minimum">Lower bound or null.</param>
        /// <param name="maximum">Upper bound or null.</param>
        /// <returns>For example "wysokość 1000–5000 m".</returns>
        private static string DescribeRange(double? minimum, double? maximum)
        {
            return "wysokość " + (FormatRange(minimum, maximum) ?? "dowolna");
        }

        /// <summary>
        /// Formats the bounds of an altitude range in meters.
        /// </summary>
        /// <param name="minimum">Lower bound or null.</param>
        /// <param name="maximum">Upper bound or null.</param>
        /// <returns>For example "1000–5000 m", "od 300 m" or "do 3000 m"; null when there are no bounds.</returns>
        private static string? FormatRange(double? minimum, double? maximum)
        {
            if (minimum.HasValue && maximum.HasValue)
            {
                return Format(minimum.Value) + "–" + Format(maximum.Value) + " m";
            }

            if (minimum.HasValue)
            {
                return "od " + Format(minimum.Value) + " m";
            }

            if (maximum.HasValue)
            {
                return "do " + Format(maximum.Value) + " m";
            }

            return null;
        }

        /// <summary>
        /// Converts an expression to the tree used to build the sentence. A negation is pushed down: negating a group swaps AND and OR and negates its children. Children that form a group of the same kind are merged into the parent, and a group with one child is replaced by it.
        /// </summary>
        /// <param name="node">Expression node.</param>
        /// <param name="negate">Whether the node is under an odd number of negations.</param>
        /// <returns>The sentence node.</returns>
        private static SentenceNode BuildSentence(FilterNode node, bool negate)
        {
            if (node is FilterNot negation)
            {
                return BuildSentence(negation.Inner, !negate);
            }

            if (node is FilterCondition condition)
            {
                return new SentenceLeaf(condition, negate);
            }

            if (node is not FilterGroup filterGroup)
            {
                return new SentenceGroup(true);
            }

            SentenceGroup group = new SentenceGroup((filterGroup.Mode == FilterGroupMode.All) != negate);

            foreach (FilterNode child in filterGroup.Children)
            {
                SentenceNode built = BuildSentence(child, negate);

                if (built is SentenceGroup childGroup && childGroup.All == group.All)
                {
                    group.Children.AddRange(childGroup.Children);
                }
                else
                {
                    group.Children.Add(built);
                }
            }

            return group.Children.Count == 1 ? group.Children[0] : group;
        }

        /// <summary>
        /// Returns the number of nested groups on the deepest path of a sentence tree.
        /// </summary>
        /// <param name="node">Sentence node.</param>
        /// <returns>0 for a condition, otherwise 1 plus the depth of the deepest child.</returns>
        private static int GetSentenceDepth(SentenceNode node)
        {
            if (node is not SentenceGroup group)
            {
                return 0;
            }

            int deepest = 0;

            foreach (SentenceNode child in group.Children)
            {
                deepest = Math.Max(deepest, GetSentenceDepth(child));
            }

            return deepest + 1;
        }

        /// <summary>
        /// Renders a sentence tree. A nested group with more than one child is put in parentheses.
        /// </summary>
        /// <param name="node">Sentence node.</param>
        /// <param name="nested">True when the node is inside another group.</param>
        /// <returns>The text.</returns>
        private static string RenderSentence(SentenceNode node, bool nested)
        {
            if (node is SentenceLeaf leaf)
            {
                return DescribeLeaf(leaf.Condition, leaf.Negated);
            }

            SentenceGroup group = (SentenceGroup)node;

            if (group.Children.Count == 0)
            {
                return "(pusta grupa)";
            }

            string text = string.Join(group.All ? " oraz " : " lub ", group.Children.Select((SentenceNode child) => RenderSentence(child, true)));

            return nested ? "(" + text + ")" : text;
        }

        /// <summary>
        /// Describes a condition as a verb phrase that continues the words "samolot, który".
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="negated">Whether the condition is negated.</param>
        /// <returns>For example "nie leci do lotniska WAW".</returns>
        private static string DescribeLeaf(FilterCondition condition, bool negated)
        {
            switch (condition.Field)
            {
                case FilterField.OnGround:
                    return negated ? "nie jest na ziemi" : "jest na ziemi";
                case FilterField.Military:
                    return negated ? "nie jest wojskowy" : "jest wojskowy";
                case FilterField.Kind:
                    return (negated ? "nie ma rodzaju: " : "ma rodzaj: ") + AircraftClassifier.GetDisplayName(condition.Kind);
                case FilterField.Manufacturer:
                    return (negated ? "nie pochodzi od producenta " : "pochodzi od producenta ") + condition.Text;
                case FilterField.TypeCode:
                    if (condition.Family)
                    {
                        return (negated ? "nie należy do rodziny typu " : "należy do rodziny typu ") + condition.Text;
                    }

                    return (negated ? "nie ma typu " : "ma typ ") + condition.Text;
                case FilterField.CallsignPrefix:
                    return (negated ? "nie ma znaku zaczynającego się od " : "ma znak zaczynający się od ") + condition.Text;
                case FilterField.Altitude:
                    string? range = FormatRange(condition.Minimum, condition.Maximum);

                    if (range == null)
                    {
                        return negated ? "nie leci na dowolnej wysokości" : "leci na dowolnej wysokości";
                    }

                    return (negated ? "nie leci na wysokości " : "leci na wysokości ") + range;
                case FilterField.Origin:
                    return (negated ? "nie leci z lotniska " : "leci z lotniska ") + condition.Text;
                case FilterField.Destination:
                    return (negated ? "nie leci do lotniska " : "leci do lotniska ") + condition.Text;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Node of the tree used to build a sentence.
        /// </summary>
        private abstract class SentenceNode
        {
        }

        /// <summary>
        /// Condition in the tree used to build a sentence, with its own negation.
        /// </summary>
        private sealed class SentenceLeaf : SentenceNode
        {
            /// <summary>
            /// Creates a leaf.
            /// </summary>
            /// <param name="condition">Condition.</param>
            /// <param name="negated">Whether the condition is negated.</param>
            public SentenceLeaf(FilterCondition condition, bool negated)
            {
                Condition = condition;
                Negated = negated;
            }

            /// <summary>
            /// Condition.
            /// </summary>
            public FilterCondition Condition { get; }

            /// <summary>
            /// Whether the condition is negated.
            /// </summary>
            public bool Negated { get; }
        }

        /// <summary>
        /// Group without negation in the tree used to build a sentence.
        /// </summary>
        private sealed class SentenceGroup : SentenceNode
        {
            /// <summary>
            /// Creates a group.
            /// </summary>
            /// <param name="all">True when all children must be satisfied, false when any of them is enough.</param>
            public SentenceGroup(bool all)
            {
                All = all;
            }

            /// <summary>
            /// True when all children must be satisfied, false when any of them is enough.
            /// </summary>
            public bool All { get; }

            /// <summary>
            /// Children of the group.
            /// </summary>
            public List<SentenceNode> Children { get; } = new List<SentenceNode>();
        }

        /// <summary>
        /// Formats a number without trailing zeros.
        /// </summary>
        /// <param name="value">Number.</param>
        /// <returns>Text of the number.</returns>
        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

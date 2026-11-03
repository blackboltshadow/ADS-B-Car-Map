using AdsbCarMap.Localization;
using System.Globalization;
using AdsbCarMap.Settings;

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
        /// <param name="altitudeUnit">Unit in which altitude bounds are written; the filter stores them in meters.</param>
        /// <returns>The sentence, or null when the filter is nested deeper than <see cref="MaxSentenceDepth"/> groups.</returns>
        public static string? DescribeSentence(AircraftFilter filter, AltitudeUnit altitudeUnit = AltitudeUnit.Metric)
        {
            SentenceNode tree = BuildSentence(filter.Expression, false);

            if (GetSentenceDepth(tree) > MaxSentenceDepth)
            {
                return null;
            }

            return (filter.Mode == FilterMode.Show ? Loc.T("Pokazuje każdy samolot, który ") : Loc.T("Ukrywa każdy samolot, który ")) + RenderSentence(tree, false, altitudeUnit) + ".";
        }

        /// <summary>
        /// Describes a whole filter: what it does with the matching aircraft and which aircraft match.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="altitudeUnit">Unit in which altitude bounds are written; the filter stores them in meters.</param>
        /// <returns>For example "Pokaż: producent Boeing ORAZ NIE lotnisko docelowe WAW".</returns>
        public static string Describe(AircraftFilter filter, AltitudeUnit altitudeUnit = AltitudeUnit.Metric)
        {
            return (filter.Mode == FilterMode.Show ? Loc.T("Pokaż: ") : Loc.T("Ukryj: ")) + DescribeNode(filter.Expression, false, altitudeUnit);
        }

        /// <summary>
        /// Describes an expression node. A nested group is put in parentheses.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <param name="nested">True when the node is inside another group or negation.</param>
        /// <param name="altitudeUnit">Unit in which altitude bounds are written; the filter stores them in meters.</param>
        /// <returns>Description of the node.</returns>
        public static string DescribeNode(FilterNode node, bool nested, AltitudeUnit altitudeUnit = AltitudeUnit.Metric)
        {
            if (node is FilterNot negation)
            {
                return Loc.T("NIE ") + DescribeNode(negation.Inner, true, altitudeUnit);
            }

            if (node is FilterGroup group)
            {
                string connector = group.Mode == FilterGroupMode.All ? Loc.T(" ORAZ ") : Loc.T(" LUB ");
                string text = string.Join(connector, group.Children.Select((FilterNode child) => DescribeNode(child, true, altitudeUnit)));

                if (group.Children.Count == 0)
                {
                    return Loc.T("(pusta grupa)");
                }

                return nested && group.Children.Count > 1 ? "(" + text + ")" : text;
            }

            return node is FilterCondition condition ? DescribeCondition(condition, altitudeUnit) : string.Empty;
        }

        /// <summary>
        /// Describes a single condition.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="altitudeUnit">Unit in which altitude bounds are written; the filter stores them in meters.</param>
        /// <returns>Description of the condition.</returns>
        public static string DescribeCondition(FilterCondition condition, AltitudeUnit altitudeUnit = AltitudeUnit.Metric)
        {
            switch (condition.Field)
            {
                case FilterField.OnGround:
                    return Loc.T("na ziemi");
                case FilterField.Military:
                    return "wojskowy";
                case FilterField.Kind:
                    return Loc.T("rodzaj: ") + AircraftClassifier.GetDisplayName(condition.Kind);
                case FilterField.Manufacturer:
                    return Loc.T("producent ") + condition.Text;
                case FilterField.TypeCode:
                    return (condition.Family ? Loc.T("rodzina typu ") : Loc.T("typ ")) + condition.Text;
                case FilterField.CallsignPrefix:
                    return Loc.T("linia ") + condition.Text;
                case FilterField.Altitude:
                    return DescribeRange(condition.Minimum, condition.Maximum, altitudeUnit);
                case FilterField.Origin:
                    return Loc.T("lotnisko wylotu ") + condition.Text;
                case FilterField.Destination:
                    return Loc.T("lotnisko docelowe ") + condition.Text;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Describes an altitude range.
        /// </summary>
        /// <param name="minimum">Lower bound in meters or null.</param>
        /// <param name="maximum">Upper bound in meters or null.</param>
        /// <param name="unit">Unit in which the bounds are written.</param>
        /// <returns>For example "wysokość 1000–5000 m".</returns>
        private static string DescribeRange(double? minimum, double? maximum, AltitudeUnit unit)
        {
            return Loc.T("wysokość ") + (FormatRange(minimum, maximum, unit) ?? "dowolna");
        }

        /// <summary>
        /// Formats the bounds of an altitude range.
        /// </summary>
        /// <param name="minimum">Lower bound in meters or null.</param>
        /// <param name="maximum">Upper bound in meters or null.</param>
        /// <param name="unit">Unit in which the bounds are written.</param>
        /// <returns>For example "1000–5000 m", "od 300 ft", "do FL350"; null when there are no bounds.</returns>
        private static string? FormatRange(double? minimum, double? maximum, AltitudeUnit unit)
        {
            string suffix = unit == AltitudeUnit.FlightLevel ? string.Empty : " " + AltitudeFormatter.Symbol(unit);

            if (minimum.HasValue && maximum.HasValue)
            {
                return FormatBound(minimum.Value, unit) + "–" + FormatBound(maximum.Value, unit) + suffix;
            }

            if (minimum.HasValue)
            {
                return "od " + FormatBound(minimum.Value, unit) + suffix;
            }

            if (maximum.HasValue)
            {
                return "do " + FormatBound(maximum.Value, unit) + suffix;
            }

            return null;
        }

        /// <summary>
        /// Formats one bound of an altitude range.
        /// </summary>
        /// <param name="meters">Bound in meters.</param>
        /// <param name="unit">Unit in which the bound is written.</param>
        /// <returns>The number in meters or feet, or the flight level such as "FL100".</returns>
        private static string FormatBound(double meters, AltitudeUnit unit)
        {
            switch (unit)
            {
                case AltitudeUnit.Feet:
                    return Math.Round(AltitudeFormatter.MetersToUnit(meters, unit)).ToString("0", CultureInfo.InvariantCulture);
                case AltitudeUnit.FlightLevel:
                    return "FL" + Math.Round(AltitudeFormatter.MetersToUnit(meters, unit)).ToString("000", CultureInfo.InvariantCulture);
                default:
                    return Format(meters);
            }
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
        /// <param name="altitudeUnit">Unit in which altitude bounds are written.</param>
        /// <returns>The text.</returns>
        private static string RenderSentence(SentenceNode node, bool nested, AltitudeUnit altitudeUnit)
        {
            if (node is SentenceLeaf leaf)
            {
                return DescribeLeaf(leaf.Condition, leaf.Negated, altitudeUnit);
            }

            SentenceGroup group = (SentenceGroup)node;

            if (group.Children.Count == 0)
            {
                return Loc.T("(pusta grupa)");
            }

            string text = string.Join(group.All ? Loc.T(" oraz ") : Loc.T(" lub "), group.Children.Select((SentenceNode child) => RenderSentence(child, true, altitudeUnit)));

            return nested ? "(" + text + ")" : text;
        }

        /// <summary>
        /// Describes a condition as a verb phrase that continues the words "samolot, który".
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="negated">Whether the condition is negated.</param>
        /// <param name="altitudeUnit">Unit in which altitude bounds are written.</param>
        /// <returns>For example "nie leci do lotniska WAW".</returns>
        private static string DescribeLeaf(FilterCondition condition, bool negated, AltitudeUnit altitudeUnit)
        {
            switch (condition.Field)
            {
                case FilterField.OnGround:
                    return negated ? Loc.T("nie jest na ziemi") : Loc.T("jest na ziemi");
                case FilterField.Military:
                    return negated ? Loc.T("nie jest wojskowy") : Loc.T("jest wojskowy");
                case FilterField.Kind:
                    return (negated ? Loc.T("nie ma rodzaju: ") : Loc.T("ma rodzaj: ")) + AircraftClassifier.GetDisplayName(condition.Kind);
                case FilterField.Manufacturer:
                    return (negated ? Loc.T("nie pochodzi od producenta ") : Loc.T("pochodzi od producenta ")) + condition.Text;
                case FilterField.TypeCode:
                    if (condition.Family)
                    {
                        return (negated ? Loc.T("nie należy do rodziny typu ") : Loc.T("należy do rodziny typu ")) + condition.Text;
                    }

                    return (negated ? Loc.T("nie ma typu ") : Loc.T("ma typ ")) + condition.Text;
                case FilterField.CallsignPrefix:
                    return (negated ? Loc.T("nie ma znaku zaczynającego się od ") : Loc.T("ma znak zaczynający się od ")) + condition.Text;
                case FilterField.Altitude:
                    string? range = FormatRange(condition.Minimum, condition.Maximum, altitudeUnit);

                    if (range == null)
                    {
                        return negated ? Loc.T("nie leci na dowolnej wysokości") : Loc.T("leci na dowolnej wysokości");
                    }

                    return (negated ? Loc.T("nie leci na wysokości ") : Loc.T("leci na wysokości ")) + range;
                case FilterField.Origin:
                    return (negated ? Loc.T("nie leci z lotniska ") : Loc.T("leci z lotniska ")) + condition.Text;
                case FilterField.Destination:
                    return (negated ? Loc.T("nie leci do lotniska ") : Loc.T("leci do lotniska ")) + condition.Text;
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

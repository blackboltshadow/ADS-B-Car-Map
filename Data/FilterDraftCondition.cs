
namespace AdsbCarMap.Data
{
    /// <summary>
    /// Condition in the filter tree being edited: a test of one aircraft property.
    /// </summary>
    public sealed class FilterDraftCondition : FilterDraftNode
    {
        /// <summary>
        /// Tested property.
        /// </summary>
        public FilterField Field { get; set; } = FilterField.Destination;

        /// <summary>
        /// Text operand.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Aircraft kind operand.
        /// </summary>
        public AircraftKind Kind { get; set; } = AircraftKind.Airliner;

        /// <summary>
        /// Whether a type condition covers the whole family of the type instead of the exact type only.
        /// </summary>
        public bool Family { get; set; }

        /// <summary>
        /// Lower bound of the altitude in meters or null when there is none.
        /// </summary>
        public double? Minimum { get; set; }

        /// <summary>
        /// Upper bound of the altitude in meters or null when there is none.
        /// </summary>
        public double? Maximum { get; set; }

        /// <summary>
        /// Builds the expression node of the condition.
        /// </summary>
        /// <returns>The condition, wrapped in a negation when the node is negated.</returns>
        public override FilterNode ToNode()
        {
            return ApplyNegation(FilterCondition.Create(Field, Text.Trim(), Kind, Minimum, Maximum, Family));
        }

        /// <summary>
        /// Creates a copy of the condition.
        /// </summary>
        /// <returns>The copy.</returns>
        public override FilterDraftNode Clone()
        {
            FilterDraftCondition copy = new FilterDraftCondition();

            copy.Field = Field;
            copy.Negated = Negated;
            copy.Text = Text;
            copy.Kind = Kind;
            copy.Family = Family;
            copy.Minimum = Minimum;
            copy.Maximum = Maximum;

            return copy;
        }
    }
}

using AdsbCarMap.Settings;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the filter editor model, of the descriptions of filters and of their JSON form.
    /// </summary>
    public class FilterEditorTests
    {
        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Creates the draft of the example "Boeing and LOT and not heading to Warsaw".
        /// </summary>
        /// <returns>The draft.</returns>
        private static FilterDraft CreateExampleDraft()
        {
            FilterDraft draft = new FilterDraft();
            draft.Name = "Boeing LOT bez Warszawy";
            draft.Root.Add(new FilterDraftCondition { Field = FilterField.Manufacturer, Text = "Boeing" });
            draft.Root.Add(new FilterDraftCondition { Field = FilterField.CallsignPrefix, Text = "LOT" });
            draft.Root.Add(new FilterDraftCondition { Field = FilterField.Destination, Text = "WAW", Negated = true });

            return draft;
        }

        /// <summary>
        /// The description names the mode, the conditions and the connectors.
        /// </summary>
        [Fact]
        public void Describer_DescribesExample()
        {
            string text = FilterDescriber.Describe(CreateExampleDraft().ToFilter("x"));

            Assert.Equal("Pokaż: producent Boeing ORAZ linia LOT ORAZ NIE lotnisko docelowe WAW", text);
        }

        /// <summary>
        /// The description of an OR filter and of a hiding filter.
        /// </summary>
        [Fact]
        public void Describer_DescribesModesAndConnectors()
        {
            Assert.Equal("Ukryj: na ziemi", FilterDescriber.Describe(BuiltInFilters.HideOnGround));
            Assert.Equal("Pokaż: lotnisko docelowe RZE LUB lotnisko docelowe EPRZ LUB lotnisko docelowe Rzeszów", FilterDescriber.Describe(BuiltInFilters.ToRzeszow));
        }

        /// <summary>
        /// Altitude ranges are described with the bounds that are set.
        /// </summary>
        [Fact]
        public void Describer_DescribesAltitudeRanges()
        {
            Assert.Equal("wysokość 1000–5000.5 m", FilterDescriber.DescribeCondition(FilterCondition.AltitudeBetween(1000.0, 5000.5)));
            Assert.Equal("wysokość od 300 m", FilterDescriber.DescribeCondition(FilterCondition.AltitudeBetween(300.0, null)));
            Assert.Equal("wysokość do 3000 m", FilterDescriber.DescribeCondition(FilterCondition.AltitudeBetween(null, 3000.0)));
            Assert.Equal("wysokość dowolna", FilterDescriber.DescribeCondition(FilterCondition.AltitudeBetween(null, null)));
        }

        /// <summary>
        /// Altitude bounds are written in the unit chosen by the user, although the filter stores meters.
        /// </summary>
        [Fact]
        public void Describer_WritesAltitudeInChosenUnit()
        {
            FilterCondition range = FilterCondition.AltitudeBetween(3048.0, 10668.0);
            AircraftFilter filter = new AircraftFilter("5", "5", FilterMode.Show, range);

            Assert.Equal("wysokość 10000–35000 ft", FilterDescriber.DescribeCondition(range, AltitudeUnit.Feet));
            Assert.Equal("wysokość FL100–FL350", FilterDescriber.DescribeCondition(range, AltitudeUnit.FlightLevel));
            Assert.Equal("wysokość od FL100", FilterDescriber.DescribeCondition(FilterCondition.AltitudeBetween(3048.0, null), AltitudeUnit.FlightLevel));
            Assert.Equal("Pokaż: wysokość do 35000 ft", FilterDescriber.Describe(new AircraftFilter("5", "5", FilterMode.Show, FilterCondition.AltitudeBetween(null, 10668.0)), AltitudeUnit.Feet));
            Assert.Equal("Pokazuje każdy samolot, który leci na wysokości 10000–35000 ft.", FilterDescriber.DescribeSentence(filter, AltitudeUnit.Feet));
            Assert.Equal("Pokazuje każdy samolot, który nie leci na wysokości FL100–FL350.", FilterDescriber.DescribeSentence(new AircraftFilter("5", "5", FilterMode.Show, new FilterNot(range)), AltitudeUnit.FlightLevel));
            Assert.Equal("Wysokość [ft]", FilterFieldInfo.GetLabel(FilterField.Altitude, AltitudeUnit.Feet));
            Assert.Equal("Wysokość [m]", FilterFieldInfo.GetLabel(FilterField.Altitude));
        }

        /// <summary>
        /// Kind, type and origin conditions have readable descriptions, and nested groups are put in parentheses.
        /// </summary>
        [Fact]
        public void Describer_DescribesRemainingConditionsAndNesting()
        {
            Assert.Equal("rodzaj: śmigłowiec", FilterDescriber.DescribeCondition(FilterCondition.OfKind(AircraftKind.Helicopter)));
            Assert.Equal("typ B738", FilterDescriber.DescribeCondition(FilterCondition.OfType("B738")));
            Assert.Equal("lotnisko wylotu WAW", FilterDescriber.DescribeCondition(FilterCondition.FromAirport("WAW")));

            FilterNode nested = FilterGroup.All(FilterCondition.Military(), FilterGroup.Any(FilterCondition.OnGround(), FilterCondition.OfType("B738")));

            Assert.Equal("wojskowy ORAZ (na ziemi LUB typ B738)", FilterDescriber.DescribeNode(nested, false));
            Assert.Equal("(pusta grupa)", FilterDescriber.DescribeNode(FilterGroup.All(), false));
            Assert.Equal("NIE (na ziemi LUB typ B738)", FilterDescriber.DescribeNode(new FilterNot(FilterGroup.Any(FilterCondition.OnGround(), FilterCondition.OfType("B738"))), false));
        }

        /// <summary>
        /// Every field has a label and an operand type, and the editor offers every field.
        /// </summary>
        [Fact]
        public void FieldInfo_CoversAllFields()
        {
            foreach (FilterField field in Enum.GetValues<FilterField>())
            {
                Assert.Contains(field, FilterFieldInfo.All);
                Assert.False(string.IsNullOrWhiteSpace(FilterFieldInfo.GetLabel(field)));
            }

            Assert.Equal(Enum.GetValues<FilterField>().Length, FilterFieldInfo.All.Count);
            Assert.Equal(FilterOperandType.Range, FilterFieldInfo.GetOperandType(FilterField.Altitude));
            Assert.Equal(FilterOperandType.None, FilterFieldInfo.GetOperandType(FilterField.Military));
            Assert.Equal("kod lub miasto, np. RZE", FilterFieldInfo.GetHint(FilterField.Destination));
            Assert.Equal(string.Empty, FilterFieldInfo.GetHint(FilterField.Military));
        }

        /// <summary>
        /// The selectable kinds exclude the unknown kind, and each has a name.
        /// </summary>
        [Fact]
        public void FieldInfo_SelectableKindsHaveNames()
        {
            IReadOnlyList<AircraftKind> kinds = FilterFieldInfo.GetSelectableKinds();

            Assert.DoesNotContain(AircraftKind.Unknown, kinds);

            foreach (AircraftKind kind in kinds)
            {
                Assert.False(string.IsNullOrEmpty(AircraftClassifier.GetDisplayName(kind)));
            }
        }

        /// <summary>
        /// Creating a condition keeps only the operand its field uses.
        /// </summary>
        [Fact]
        public void Condition_CreateKeepsOnlyUsedOperand()
        {
            FilterCondition range = FilterCondition.Create(FilterField.Altitude, "ignored", AircraftKind.Fighter, 1.0, 2.0);
            FilterCondition text = FilterCondition.Create(FilterField.Destination, "RZE", AircraftKind.Fighter, 1.0, 2.0);
            FilterCondition kind = FilterCondition.Create(FilterField.Kind, "ignored", AircraftKind.Fighter, 1.0, 2.0);
            FilterCondition flag = FilterCondition.Create(FilterField.Military, "ignored", AircraftKind.Fighter, 1.0, 2.0);

            Assert.Null(range.Text);
            Assert.Equal(1.0, range.Minimum);
            Assert.Equal("RZE", text.Text);
            Assert.Null(text.Minimum);
            Assert.Equal(AircraftKind.Fighter, kind.Kind);
            Assert.Null(kind.Text);
            Assert.Null(flag.Text);
            Assert.Equal(AircraftKind.Unknown, flag.Kind);
        }

        /// <summary>
        /// Filters survive a trip through JSON, including Polish letters, negations, nested groups and every kind of operand.
        /// </summary>
        [Fact]
        public void Json_RoundTripsFilters()
        {
            FilterNode expression = FilterGroup.Any(
                new FilterNot(FilterCondition.ToAirport("Łódź")),
                FilterGroup.All(FilterCondition.OfKind(AircraftKind.Helicopter), FilterCondition.AltitudeBetween(null, 1500.5), FilterCondition.MadeBy("Boeing")),
                FilterCondition.OnGround());
            List<AircraftFilter> filters = new List<AircraftFilter>
            {
                new AircraftFilter("a", "Pierwszy żółw", FilterMode.Hide, expression),
                new AircraftFilter("b", "Drugi", FilterMode.Show, FilterCondition.CallsignStartsWith("LOT")),
            };

            List<AircraftFilter> parsed = FilterJson.Parse(FilterJson.Serialize(filters));

            Assert.Equal(2, parsed.Count);
            Assert.Equal("Pierwszy żółw", parsed[0].Name);
            Assert.Equal(FilterMode.Hide, parsed[0].Mode);
            Assert.Equal(FilterDescriber.Describe(filters[0]), FilterDescriber.Describe(parsed[0]));
            Assert.Equal(FilterDescriber.Describe(filters[1]), FilterDescriber.Describe(parsed[1]));
        }

        /// <summary>
        /// A filter read from JSON evaluates like the original.
        /// </summary>
        [Fact]
        public void Json_ParsedFilterEvaluates()
        {
            AircraftFilter original = CreateExampleDraft().ToFilter("x");
            AircraftFilter parsed = FilterJson.Parse(FilterJson.Serialize(new List<AircraftFilter> { original }))[0];
            Aircraft lot = new Aircraft("1", "LOT281", 52.0, 21.0, 35000, false, 450.0, 90.0, 0.0, "B738", null);
            FlightRoute toWarsaw = new FlightRoute("KRK", "Kraków", "WAW", "Warszawa");

            Assert.Equal(FilterResult.False, parsed.Expression.Evaluate(lot, (string? callsign) => toWarsaw));
        }

        /// <summary>
        /// Missing or damaged JSON gives no filters instead of an error.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("[1, 2]")]
        public void Json_ParseToleratesBadInput(string? json)
        {
            Assert.Empty(FilterJson.Parse(json));
        }

        /// <summary>
        /// A filter that is incomplete or invalid is skipped and the valid ones are kept.
        /// </summary>
        [Fact]
        public void Json_SkipsInvalidFilters()
        {
            string json = "[" +
                "{\"id\":\"ok\",\"name\":\"Dobry\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"Military\"}}," +
                "{\"id\":\"noname\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"Military\"}}," +
                "{\"id\":\"badmode\",\"name\":\"X\",\"mode\":\"Maybe\",\"expr\":{\"type\":\"cond\",\"field\":\"Military\"}}," +
                "{\"id\":\"badfield\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"Colour\"}}," +
                "{\"id\":\"badkind\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"Kind\",\"kind\":\"Spaceship\"}}," +
                "{\"id\":\"badtype\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"xor\"}}," +
                "{\"id\":\"noexpr\",\"name\":\"X\",\"mode\":\"Show\"}," +
                "{\"id\":\"badnot\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"not\"}}," +
                "{\"id\":\"badgroup\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"all\",\"items\":[{\"type\":\"cond\",\"field\":\"Nope\"}]}}," +
                "{\"id\":\"noitems\",\"name\":\"X\",\"mode\":\"Show\",\"expr\":{\"type\":\"any\"}}," +
                "42]";

            List<AircraftFilter> parsed = FilterJson.Parse(json);

            Assert.Single(parsed);
            Assert.Equal("ok", parsed[0].Id);
        }

        /// <summary>
        /// An expression nested deeper than the limit is rejected.
        /// </summary>
        [Fact]
        public void Json_RejectsTooDeepNesting()
        {
            string node = "{\"type\":\"cond\",\"field\":\"Military\"}";

            for (int level = 0; level < FilterJson.MaxDepth + 50; level++)
            {
                node = "{\"type\":\"not\",\"item\":" + node + "}";
            }

            string json = "[{\"id\":\"deep\",\"name\":\"Deep\",\"mode\":\"Show\",\"expr\":" + node + "}]";

            Assert.Empty(FilterJson.Parse(json));
        }

        /// <summary>
        /// The predefined filters can be serialized and read back.
        /// </summary>
        [Fact]
        public void Json_RoundTripsPredefinedFilters()
        {
            List<AircraftFilter> parsed = FilterJson.Parse(FilterJson.Serialize(BuiltInFilters.All));

            Assert.Equal(BuiltInFilters.All.Count, parsed.Count);

            for (int index = 0; index < parsed.Count; index++)
            {
                Assert.Equal(FilterDescriber.Describe(BuiltInFilters.All[index]), FilterDescriber.Describe(parsed[index]));
            }
        }
    }
}

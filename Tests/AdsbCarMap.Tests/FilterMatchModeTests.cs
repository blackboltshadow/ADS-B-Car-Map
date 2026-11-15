using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the match modes of text conditions: evaluation, JSON, drafts and descriptions.
    /// </summary>
    public class FilterMatchModeTests
    {
        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Creates an aircraft with the given registration, callsign and squawk.
        /// </summary>
        /// <param name="registration">Registration.</param>
        /// <param name="callsign">Callsign.</param>
        /// <param name="squawk">Squawk.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string? registration, string? callsign = "LOT281", string? squawk = null)
        {
            return new Aircraft("489123", callsign, 52.0, 21.0, 35000, false, 450.0, 90.0, 0.0, "B738", registration, "A3", false, null, squawk);
        }

        /// <summary>
        /// The registration SP-YBC under every mode, ignoring case and dashes.
        /// </summary>
        [Theory]
        [InlineData(FilterMatchMode.StartsWith, "SP-", FilterResult.True)]
        [InlineData(FilterMatchMode.StartsWith, "YB", FilterResult.False)]
        [InlineData(FilterMatchMode.Contains, "YB", FilterResult.True)]
        [InlineData(FilterMatchMode.Contains, "p-y", FilterResult.True)]
        [InlineData(FilterMatchMode.Contains, "ZZ", FilterResult.False)]
        [InlineData(FilterMatchMode.EndsWith, "BC", FilterResult.True)]
        [InlineData(FilterMatchMode.EndsWith, "YB", FilterResult.False)]
        [InlineData(FilterMatchMode.Exact, "sp-ybc", FilterResult.True)]
        [InlineData(FilterMatchMode.Exact, "SP-", FilterResult.False)]
        [InlineData(FilterMatchMode.Contains, "", FilterResult.Unknown)]
        [InlineData(FilterMatchMode.Contains, "--", FilterResult.Unknown)]
        public void Registration_Modes(FilterMatchMode mode, string wanted, FilterResult expected)
        {
            FilterCondition condition = FilterCondition.Create(FilterField.Registration, wanted, AircraftKind.Unknown, null, null, false, mode);

            Assert.Equal(expected, condition.Evaluate(CreateAircraft("SP-YBC"), NoRoutes));
        }

        /// <summary>
        /// An aircraft without a registration gives an unknown result in every mode.
        /// </summary>
        [Fact]
        public void Registration_Missing_IsUnknown()
        {
            FilterCondition condition = FilterCondition.Create(FilterField.Registration, "YB", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains);

            Assert.Equal(FilterResult.Unknown, condition.Evaluate(CreateAircraft(null), NoRoutes));
        }

        /// <summary>
        /// "Contains YB or ends with BC" is an Any group of two conditions.
        /// </summary>
        [Fact]
        public void Registration_ContainsOrEndsWith_ViaAnyGroup()
        {
            FilterNode node = FilterGroup.Any(
                FilterCondition.Create(FilterField.Registration, "YB", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains),
                FilterCondition.Create(FilterField.Registration, "BC", AircraftKind.Unknown, null, null, false, FilterMatchMode.EndsWith));

            Assert.Equal(FilterResult.True, node.Evaluate(CreateAircraft("SP-YBC"), NoRoutes));
            Assert.Equal(FilterResult.True, node.Evaluate(CreateAircraft("SP-ABC"), NoRoutes));
            Assert.Equal(FilterResult.True, node.Evaluate(CreateAircraft("SP-LYB"), NoRoutes));
            Assert.Equal(FilterResult.False, node.Evaluate(CreateAircraft("SP-LOT"), NoRoutes));
        }

        /// <summary>
        /// The callsign condition supports the contains, ends-with and exact modes and ignores letter case.
        /// </summary>
        [Theory]
        [InlineData(FilterMatchMode.StartsWith, "lot", FilterResult.True)]
        [InlineData(FilterMatchMode.Contains, "OT2", FilterResult.True)]
        [InlineData(FilterMatchMode.EndsWith, "281", FilterResult.True)]
        [InlineData(FilterMatchMode.EndsWith, "LOT", FilterResult.False)]
        [InlineData(FilterMatchMode.Exact, "lot281", FilterResult.True)]
        [InlineData(FilterMatchMode.Exact, "LOT", FilterResult.False)]
        public void Callsign_Modes(FilterMatchMode mode, string wanted, FilterResult expected)
        {
            FilterCondition condition = FilterCondition.Create(FilterField.CallsignPrefix, wanted, AircraftKind.Unknown, null, null, false, mode);

            Assert.Equal(expected, condition.Evaluate(CreateAircraft("SP-YBC"), NoRoutes));
        }

        /// <summary>
        /// The squawk condition is exact by default and can match the beginning; other modes fall back to the default.
        /// </summary>
        [Fact]
        public void Squawk_Modes()
        {
            Aircraft aircraft = CreateAircraft("SP-YBC", "LOT281", "7700");

            FilterCondition starts = FilterCondition.Create(FilterField.Squawk, "77", AircraftKind.Unknown, null, null, false, FilterMatchMode.StartsWith);
            FilterCondition contains = FilterCondition.Create(FilterField.Squawk, "77", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains);

            Assert.Equal(FilterResult.True, starts.Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterMatchMode.Exact, contains.MatchMode);
            Assert.Equal(FilterResult.False, contains.Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.True, FilterCondition.SquawkIs("7700").Evaluate(aircraft, NoRoutes));
        }

        /// <summary>
        /// Fields without text modes always report the default mode.
        /// </summary>
        [Fact]
        public void UnsupportedField_UsesDefaultMode()
        {
            FilterCondition condition = FilterCondition.Create(FilterField.Destination, "WAW", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains);

            Assert.Equal(FilterMatchMode.Exact, condition.MatchMode);
            Assert.Empty(FilterFieldInfo.GetMatchModes(FilterField.TypeCode));
            Assert.Equal(FilterMatchMode.StartsWith, FilterCondition.RegistrationIs("SP-").MatchMode);
        }

        /// <summary>
        /// The mode is written to JSON only when it differs from the default and survives a round trip.
        /// </summary>
        [Fact]
        public void Json_WritesOnlyNonDefaultMode()
        {
            AircraftFilter plain = new AircraftFilter("1", "a", FilterMode.Show, FilterCondition.RegistrationIs("SP-"));
            AircraftFilter custom = new AircraftFilter("2", "b", FilterMode.Show, FilterCondition.Create(FilterField.Registration, "YB", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains));

            Assert.DoesNotContain("\"match\"", FilterJson.Serialize(new List<AircraftFilter> { plain }));

            string json = FilterJson.Serialize(new List<AircraftFilter> { custom });

            Assert.Contains("\"match\":\"Contains\"", json);
            Assert.Equal(FilterMatchMode.Contains, ((FilterCondition)FilterJson.Parse(json)[0].Expression).MatchMode);
        }

        /// <summary>
        /// A condition saved before modes existed, or with an unknown mode, keeps the default mode.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(",\"match\":\"Nonsense\"")]
        [InlineData(",\"match\":7")]
        public void Json_MissingOrInvalidMode_IsDefault(string extra)
        {
            string json = "[{\"id\":\"1\",\"name\":\"n\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"Registration\",\"text\":\"SP-\"" + extra + "}}]";

            Assert.Equal(FilterMatchMode.StartsWith, ((FilterCondition)FilterJson.Parse(json)[0].Expression).MatchMode);
        }

        /// <summary>
        /// The draft keeps the mode when it is cloned, built and read back from a node.
        /// </summary>
        [Fact]
        public void Draft_KeepsMode()
        {
            FilterDraftCondition draft = new FilterDraftCondition();

            draft.Field = FilterField.Registration;
            draft.Text = "BC";
            draft.MatchMode = FilterMatchMode.EndsWith;

            FilterCondition built = (FilterCondition)draft.Clone().ToNode();

            Assert.Equal(FilterMatchMode.EndsWith, built.MatchMode);

            FilterDraftCondition back = Assert.IsType<FilterDraftCondition>(FilterDraftNode.FromNode(built));

            Assert.Equal(FilterMatchMode.EndsWith, back.MatchMode);

            draft.Field = FilterField.Squawk;
            draft.MatchMode = null;

            Assert.Equal(FilterMatchMode.Exact, ((FilterCondition)draft.ToNode()).MatchMode);
        }

        /// <summary>
        /// Descriptions mention a non-default mode and stay unchanged for the default one.
        /// </summary>
        [Fact]
        public void Describer_MentionsMode()
        {
            FilterCondition contains = FilterCondition.Create(FilterField.Registration, "YB", AircraftKind.Unknown, null, null, false, FilterMatchMode.Contains);
            FilterCondition ends = FilterCondition.Create(FilterField.Registration, "BC", AircraftKind.Unknown, null, null, false, FilterMatchMode.EndsWith);

            Assert.Equal("rejestracja zawiera YB", FilterDescriber.DescribeCondition(contains));
            Assert.Equal("rejestracja SP-", FilterDescriber.DescribeCondition(FilterCondition.RegistrationIs("SP-")));
            Assert.Equal("Pokazuje każdy samolot, który ma rejestrację zawierającą YB.", FilterDescriber.DescribeSentence(new AircraftFilter("1", "1", FilterMode.Show, contains)));
            Assert.Equal("Pokazuje każdy samolot, który nie ma rejestracji kończącej się na BC.", FilterDescriber.DescribeSentence(new AircraftFilter("2", "2", FilterMode.Show, new FilterNot(ends))));
        }
    }
}

using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of exporting and importing alert rules and of the rule that watches one aircraft.
    /// </summary>
    public class AlertBundleTests
    {
        /// <summary>
        /// User-made filter used by the tests.
        /// </summary>
        private static readonly AircraftFilter Custom = new AircraftFilter("custom_1", "Moje", FilterMode.Show, FilterCondition.Military());

        /// <summary>
        /// Creates a rule that uses a filter.
        /// </summary>
        /// <param name="id">Identifier of the rule.</param>
        /// <param name="filterId">Identifier of the filter or null.</param>
        /// <returns>The rule.</returns>
        private static AlertRule CreateRule(string id, string? filterId)
        {
            return new AlertRule(id, "Alert " + id, true, new AlertArea(AlertAreaKind.AroundMe, 30.0), null, true, true, 30, filterId, null, AlertImportance.Low, true);
        }

        /// <summary>
        /// Finds a filter that exists only in the test.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <returns>The filter or null.</returns>
        private static AircraftFilter? Lookup(string id)
        {
            return id == Custom.Id ? Custom : null;
        }

        /// <summary>
        /// Rules and the user-made filters they use survive a round trip, and a filter used twice is written once.
        /// </summary>
        [Fact]
        public void RoundTrip_KeepsRulesAndUsedFilters()
        {
            List<AlertRule> rules = new List<AlertRule> { CreateRule("a", "custom_1"), CreateRule("b", "custom_1"), CreateRule("c", "military"), CreateRule("d", null) };

            AlertBundle? parsed = AlertBundle.Parse(AlertBundle.Serialize(rules, Lookup, (string id) => id == "military"));

            Assert.NotNull(parsed);
            Assert.Equal(4, parsed!.Rules.Count);
            Assert.Equal("Alert a", parsed.Rules[0].Name);
            Assert.Equal(AlertImportance.Low, parsed.Rules[0].Importance);
            Assert.True(parsed.Rules[0].NotifyOnExit);
            Assert.Single(parsed.Filters);
            Assert.Equal("Moje", parsed.Filters[0].Name);
        }

        /// <summary>
        /// A filter that no longer exists is simply not written.
        /// </summary>
        [Fact]
        public void Serialize_MissingFilter_IsSkipped()
        {
            AlertBundle? parsed = AlertBundle.Parse(AlertBundle.Serialize(new List<AlertRule> { CreateRule("a", "gone") }, Lookup, (string id) => false));

            Assert.Single(parsed!.Rules);
            Assert.Empty(parsed.Filters);
        }

        /// <summary>
        /// A bare array of rules, as stored on the device, is accepted.
        /// </summary>
        [Fact]
        public void Parse_BareArray_ReadsRules()
        {
            AlertBundle? parsed = AlertBundle.Parse(AlertRuleJson.Serialize(new List<AlertRule> { CreateRule("a", null) }));

            Assert.Single(parsed!.Rules);
            Assert.Empty(parsed.Filters);
        }

        /// <summary>
        /// Text that is not an alert document gives null.
        /// </summary>
        /// <param name="json">Text to read.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("{\"alerts\":5}")]
        [InlineData("42")]
        public void Parse_Invalid_ReturnsNull(string? json)
        {
            Assert.Null(AlertBundle.Parse(json));
        }

        /// <summary>
        /// An import gives every rule a new identifier and adds only the filters that are missing.
        /// </summary>
        [Fact]
        public void PrepareImport_NewIdentifiersAndMissingFiltersOnly()
        {
            AircraftFilter other = new AircraftFilter("custom_2", "Inny", FilterMode.Show, FilterCondition.Military());
            AlertBundle bundle = new AlertBundle(new List<AlertRule> { CreateRule("a", "custom_1"), CreateRule("a", "custom_2") }, new List<AircraftFilter> { Custom, other, other });
            int counter = 0;

            AlertBundle result = bundle.PrepareImport(() => "new_" + counter++, (string id) => id == "custom_1");

            Assert.Equal("new_0", result.Rules[0].Id);
            Assert.Equal("new_1", result.Rules[1].Id);
            Assert.Equal("custom_1", result.Rules[0].ConditionFilterId);
            Assert.Equal("Alert a", result.Rules[0].Name);
            Assert.Single(result.Filters);
            Assert.Equal("custom_2", result.Filters[0].Id);
        }

        /// <summary>
        /// Duplicating a rule changes only the identifier and the name.
        /// </summary>
        [Fact]
        public void WithIdentity_KeepsEverythingElse()
        {
            AlertRule original = CreateRule("a", "custom_1");

            AlertRule copy = original.WithIdentity("b", "Kopia");

            Assert.Equal("b", copy.Id);
            Assert.Equal("Kopia", copy.Name);
            Assert.Equal(original.Area, copy.Area);
            Assert.Equal("custom_1", copy.ConditionFilterId);
            Assert.Equal(AlertImportance.Low, copy.Importance);
            Assert.True(copy.NotifyOnExit);
        }

        /// <summary>
        /// The watch rule covers the watch radius around the user and watches one address.
        /// </summary>
        [Fact]
        public void CreateWatchRule_WatchesOneAircraftAroundMe()
        {
            AlertRule rule = AlertBundle.CreateWatchRule("w", "Obserwuj: LOT1", "48A124");

            Assert.True(rule.IsEnabled);
            Assert.Equal("48a124", rule.WatchedHex);
            Assert.Equal(AlertAreaKind.AroundMe, rule.Area.Kind);
            Assert.Equal(AlertBundle.WatchRadiusKilometers, rule.Area.RadiusKilometers);
            Assert.True(rule.NotifyOnEntry);
            Assert.True(rule.NotifyOnAppearance);
            Assert.False(rule.NotifyOnExit);
        }
    }
}

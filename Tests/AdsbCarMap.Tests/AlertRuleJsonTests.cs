using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the alert rule serialization and of the alert texts.
    /// </summary>
    public class AlertRuleJsonTests
    {
        /// <summary>
        /// Creates a rule with the given area.
        /// </summary>
        /// <param name="area">Area of the rule.</param>
        /// <param name="filterId">Identifier of the filter or null.</param>
        /// <returns>The rule.</returns>
        private static AlertRule CreateRule(AlertArea area, string? filterId = "custom_1")
        {
            return new AlertRule("id1", "Wojskowe", true, area, null, true, false, 45, filterId);
        }

        /// <summary>
        /// Every kind of area survives a round trip.
        /// </summary>
        [Fact]
        public void RoundTrip_KeepsAllFields()
        {
            List<AlertRule> rules = new List<AlertRule>
            {
                CreateRule(new AlertArea(AlertAreaKind.AroundMe, 30.0)),
                CreateRule(new AlertArea(AlertAreaKind.AroundAirport, 12.5, null, "EPWA"), null),
                CreateRule(new AlertArea(AlertAreaKind.Circle, 80.0, new GeoPoint(52.25, 21.5))),
            };

            List<AlertRule> parsed = AlertRuleJson.Parse(AlertRuleJson.Serialize(rules));

            Assert.Equal(3, parsed.Count);
            Assert.Equal(AlertAreaKind.AroundMe, parsed[0].Area.Kind);
            Assert.Equal(30.0, parsed[0].Area.RadiusKilometers);
            Assert.Equal("EPWA", parsed[1].Area.AirportIcao);
            Assert.Null(parsed[1].ConditionFilterId);
            Assert.Equal(52.25, parsed[2].Area.Center!.Latitude);
            Assert.Equal(21.5, parsed[2].Area.Center!.Longitude);
            Assert.Equal("id1", parsed[0].Id);
            Assert.Equal("Wojskowe", parsed[0].Name);
            Assert.True(parsed[0].IsEnabled);
            Assert.True(parsed[0].NotifyOnEntry);
            Assert.False(parsed[0].NotifyOnAppearance);
            Assert.Equal(45, parsed[0].CooldownMinutes);
            Assert.Equal("custom_1", parsed[0].ConditionFilterId);
            Assert.Null(parsed[0].Condition);
        }

        /// <summary>
        /// A polygon and a schedule survive a round trip, and a rule without a schedule stays unrestricted.
        /// </summary>
        [Fact]
        public void RoundTrip_KeepsPolygonAndSchedule()
        {
            AlertArea polygon = AlertArea.CreatePolygon(new List<GeoPoint> { new GeoPoint(51.0, 20.0), new GeoPoint(51.0, 22.0), new GeoPoint(53.0, 21.0) });
            AlertSchedule schedule = new AlertSchedule(0x3E, 22 * 60, 6 * 60);
            List<AlertRule> rules = new List<AlertRule>
            {
                new AlertRule("p", "Poly", true, polygon, null, true, true, 30, null, schedule),
                CreateRule(new AlertArea(AlertAreaKind.AroundMe, 30.0)),
            };

            List<AlertRule> parsed = AlertRuleJson.Parse(AlertRuleJson.Serialize(rules));

            Assert.Equal(2, parsed.Count);
            Assert.Equal(AlertAreaKind.Polygon, parsed[0].Area.Kind);
            Assert.Equal(3, parsed[0].Area.Vertices!.Count);
            Assert.Equal(53.0, parsed[0].Area.Vertices![2].Latitude);
            Assert.Equal(polygon.RadiusKilometers, parsed[0].Area.RadiusKilometers, 6);
            Assert.Equal(0x3E, parsed[0].Schedule.ActiveDays);
            Assert.Equal(22 * 60, parsed[0].Schedule.QuietStartMinute);
            Assert.Equal(6 * 60, parsed[0].Schedule.QuietEndMinute);
            Assert.True(parsed[1].Schedule.IsAlways);
        }

        /// <summary>
        /// A polygon with too few vertices or damaged vertices is dropped.
        /// </summary>
        [Theory]
        [InlineData("[{\"id\":\"a\",\"area\":\"Polygon\"}]")]
        [InlineData("[{\"id\":\"a\",\"area\":\"Polygon\",\"vertices\":[[1,2],[3,4]]}]")]
        [InlineData("[{\"id\":\"a\",\"area\":\"Polygon\",\"vertices\":[[1,2],[3,4],[5]]}]")]
        [InlineData("[{\"id\":\"a\",\"area\":\"Polygon\",\"vertices\":[[1,2],[3,4],[\"x\",6]]}]")]
        public void Parse_InvalidPolygon_IsDropped(string json)
        {
            Assert.Empty(AlertRuleJson.Parse(json));
        }

        /// <summary>
        /// The polygon, the rectangle and the schedule are described in Polish.
        /// </summary>
        [Fact]
        public void DescribePolygonRectangleAndSchedule()
        {
            AlertArea rectangle = AlertArea.CreateRectangle(new GeoPoint(51.0, 20.0), new GeoPoint(51.5, 20.5));
            AlertArea triangle = AlertArea.CreatePolygon(new List<GeoPoint> { new GeoPoint(51.0, 20.0), new GeoPoint(51.0, 20.1), new GeoPoint(51.1, 20.0) });

            Assert.StartsWith("Prostokąt, ", AlertText.DescribeArea(rectangle, DistanceUnit.Kilometers));
            Assert.StartsWith("Wielokąt (3 wierzchołków), ", AlertText.DescribeArea(triangle, DistanceUnit.Kilometers));
            Assert.Equal(string.Empty, AlertText.DescribeSchedule(AlertSchedule.Always));
            Assert.Equal("pn, wt · godziny ciszy 22:00–06:00", AlertText.DescribeSchedule(new AlertSchedule((1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Tuesday), 22 * 60, 6 * 60)));
            Assert.Equal("żaden dzień", AlertText.DescribeSchedule(new AlertSchedule(0, null, null)));
        }

        /// <summary>
        /// Missing or damaged text gives an empty list.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        public void Parse_InvalidText_IsEmpty(string? json)
        {
            Assert.Empty(AlertRuleJson.Parse(json));
        }

        /// <summary>
        /// Rules that are incomplete or invalid are skipped, the valid ones stay.
        /// </summary>
        [Fact]
        public void Parse_SkipsInvalidRules()
        {
            string json = "[1, {\"id\":\"a\"}, {\"id\":\"b\",\"area\":\"Circle\",\"radius\":10}, {\"id\":\"c\",\"area\":\"AroundAirport\",\"radius\":10}, {\"id\":\"d\",\"area\":\"Nope\",\"radius\":10}, {\"id\":\"e\",\"area\":\"AroundMe\",\"radius\":0}, {\"id\":\"f\",\"area\":\"AroundMe\",\"radius\":10}]";

            List<AlertRule> parsed = AlertRuleJson.Parse(json);

            Assert.Single(parsed);
            Assert.Equal("f", parsed[0].Id);
            Assert.True(parsed[0].IsEnabled);
            Assert.True(parsed[0].NotifyOnEntry);
            Assert.True(parsed[0].NotifyOnAppearance);
            Assert.Equal(30, parsed[0].CooldownMinutes);
            Assert.Equal(string.Empty, parsed[0].Name);
        }

        /// <summary>
        /// WithCondition and WithEnabled copy the rule and change one thing.
        /// </summary>
        [Fact]
        public void With_CopiesRule()
        {
            AlertRule rule = CreateRule(new AlertArea(AlertAreaKind.AroundMe, 30.0));
            AlertRule disabled = rule.WithEnabled(false);
            AlertRule conditioned = rule.WithCondition(FilterCondition.Military());

            Assert.False(disabled.IsEnabled);
            Assert.Equal(rule.CooldownMinutes, disabled.CooldownMinutes);
            Assert.Equal(rule.ConditionFilterId, disabled.ConditionFilterId);
            Assert.NotNull(conditioned.Condition);
            Assert.True(conditioned.IsEnabled);
        }

        /// <summary>
        /// The area is described in Polish for each kind.
        /// </summary>
        [Fact]
        public void DescribeArea_AllKinds()
        {
            Assert.Equal("Wokół mojej pozycji, 30 km", AlertText.DescribeArea(new AlertArea(AlertAreaKind.AroundMe, 30.0), DistanceUnit.Kilometers));
            Assert.Equal("Wokół lotniska EPWA, 7,5 km", AlertText.DescribeArea(new AlertArea(AlertAreaKind.AroundAirport, 7.5, null, "EPWA"), DistanceUnit.Kilometers));
            Assert.Equal("Okrąg 52.250, 21.500, 80 km", AlertText.DescribeArea(new AlertArea(AlertAreaKind.Circle, 80.0, new GeoPoint(52.25, 21.5)), DistanceUnit.Kilometers));
            Assert.Equal("Okrąg, 80 km", AlertText.DescribeArea(new AlertArea(AlertAreaKind.Circle, 80.0), DistanceUnit.Kilometers));
        }

        /// <summary>
        /// The events are listed together with the cooldown.
        /// </summary>
        [Fact]
        public void DescribeEvents_ListsEventsAndCooldown()
        {
            AlertArea area = new AlertArea(AlertAreaKind.AroundMe, 30.0);

            Assert.Equal("wlot, pojawienie się · cisza 30 min", AlertText.DescribeEvents(new AlertRule("a", "n", true, area, null, true, true, 30)));
            Assert.Equal("wlot · cisza 5 min", AlertText.DescribeEvents(new AlertRule("a", "n", true, area, null, true, false, 5)));
            Assert.Equal("bez zdarzeń · cisza 0 min", AlertText.DescribeEvents(new AlertRule("a", "n", true, area, null, false, false, 0)));
        }

        /// <summary>
        /// The notification title depends on the event and falls back when the rule has no name.
        /// </summary>
        [Fact]
        public void Title_DependsOnTrigger()
        {
            Aircraft aircraft = new Aircraft("48a123", "LOT1", 52.0, 21.0, 35000, false, null, null, 0.0, "B738", null);
            AlertArea area = new AlertArea(AlertAreaKind.AroundMe, 30.0);

            Assert.Equal("Moje – wlot do obszaru", AlertText.Title(new AlertMatch(new AlertRule("a", "Moje", true, area, null, true, true, 30), aircraft, AlertTrigger.Entered, 5.0)));
            Assert.Equal("Alert – w obszarze", AlertText.Title(new AlertMatch(new AlertRule("a", " ", true, area, null, true, true, 30), aircraft, AlertTrigger.Appeared, 5.0)));
        }

        /// <summary>
        /// The notification body contains the callsign or address, type, altitude and distance.
        /// </summary>
        [Fact]
        public void Body_ContainsAircraftDetails()
        {
            AlertArea area = new AlertArea(AlertAreaKind.AroundMe, 30.0);
            AlertRule rule = new AlertRule("a", "n", true, area, null, true, true, 30);
            Aircraft flying = new Aircraft("48a123", " LOT1 ", 52.0, 21.0, 35000, false, null, null, 0.0, "B738", null);
            Aircraft onGround = new Aircraft("48a124", null, 52.0, 21.0, 0, true, null, null, 0.0, null, null, typeName: "Boeing 737-800");
            Aircraft noAltitude = new Aircraft("48a125", null, 52.0, 21.0, null, false, null, null, 0.0, null, null);

            Assert.Equal("LOT1 · B738 · FL350 · 3,5 km", AlertText.Body(new AlertMatch(rule, flying, AlertTrigger.Entered, 3.5), new DisplayUnits(AltitudeUnit.FlightLevel, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute)));
            Assert.Equal("48A124 · Boeing 737-800 · na ziemi · 12 km", AlertText.Body(new AlertMatch(rule, onGround, AlertTrigger.Appeared, 12.4), new DisplayUnits(AltitudeUnit.Feet, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute)));
            Assert.Equal("48A125 · 20 km", AlertText.Body(new AlertMatch(rule, noAltitude, AlertTrigger.Appeared, 20.0), DisplayUnits.Default));
        }
    }
}

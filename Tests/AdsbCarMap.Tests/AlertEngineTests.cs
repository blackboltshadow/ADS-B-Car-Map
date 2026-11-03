using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the alert engine: areas, conditions, triggers, cooldown and state handling.
    /// </summary>
    public class AlertEngineTests
    {
        /// <summary>
        /// Center used by the tests.
        /// </summary>
        private static readonly GeoPoint Center = new GeoPoint(52.0, 21.0);

        /// <summary>
        /// Start time used by the tests.
        /// </summary>
        private static readonly DateTime Start = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Airport lookup that knows only EPWA.
        /// </summary>
        private static readonly Func<string, GeoPoint?> Airports = (string icao) => icao == "EPWA" ? new GeoPoint(52.1657, 20.9671) : null;

        /// <summary>
        /// Creates an aircraft at the given latitude offset from the center (one degree is about 111 km).
        /// </summary>
        /// <param name="hex">ICAO address.</param>
        /// <param name="latitudeOffset">Latitude offset from the center in degrees.</param>
        /// <param name="military">Whether the aircraft is military.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string hex, double latitudeOffset, bool military = false)
        {
            return new Aircraft(hex, "TEST1", Center.Latitude + latitudeOffset, Center.Longitude, 30000, false, 400.0, 90.0, 0.0, "B738", null, "A3", military);
        }

        /// <summary>
        /// Creates a circle rule with a 50 km radius around the center.
        /// </summary>
        /// <param name="condition">Condition or null.</param>
        /// <param name="entry">Whether to notify on entry.</param>
        /// <param name="appearance">Whether to notify on appearance.</param>
        /// <param name="cooldownMinutes">Cooldown in minutes.</param>
        /// <param name="enabled">Whether the rule is enabled.</param>
        /// <returns>The rule.</returns>
        private static AlertRule CreateRule(FilterNode? condition = null, bool entry = true, bool appearance = true, int cooldownMinutes = 30, bool enabled = true)
        {
            AlertArea area = new AlertArea(AlertAreaKind.Circle, 50.0, Center);

            return new AlertRule("r1", "Rule", enabled, area, condition, entry, appearance, cooldownMinutes);
        }

        /// <summary>
        /// Runs one evaluation.
        /// </summary>
        /// <param name="engine">Engine to use.</param>
        /// <param name="rule">Rule to evaluate.</param>
        /// <param name="aircraft">Aircraft in the data.</param>
        /// <param name="minutes">Minutes since the start.</param>
        /// <returns>The matches.</returns>
        private static List<AlertMatch> Run(AlertEngine engine, AlertRule rule, List<Aircraft> aircraft, int minutes = 0)
        {
            return engine.Evaluate(new List<AlertRule> { rule }, aircraft, null, Airports, NoRoutes, Start.AddMinutes(minutes));
        }

        /// <summary>
        /// An aircraft inside the area on the first evaluation counts as appeared.
        /// </summary>
        [Fact]
        public void Evaluate_FirstSightingInside_IsAppeared()
        {
            AlertEngine engine = new AlertEngine();

            List<AlertMatch> matches = Run(engine, CreateRule(), new List<Aircraft> { CreateAircraft("A1", 0.1) });

            Assert.Single(matches);
            Assert.Equal(AlertTrigger.Appeared, matches[0].Trigger);
            Assert.InRange(matches[0].DistanceKilometers, 10.0, 12.0);
        }

        /// <summary>
        /// An aircraft outside the area never fires.
        /// </summary>
        [Fact]
        public void Evaluate_Outside_DoesNotFire()
        {
            AlertEngine engine = new AlertEngine();

            Assert.Empty(Run(engine, CreateRule(), new List<Aircraft> { CreateAircraft("A1", 2.0) }));
        }

        /// <summary>
        /// An aircraft that was seen outside and then moves inside counts as entered.
        /// </summary>
        [Fact]
        public void Evaluate_MovesInside_IsEntered()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule();

            Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 2.0) }, 0);
            List<AlertMatch> matches = Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 1);

            Assert.Single(matches);
            Assert.Equal(AlertTrigger.Entered, matches[0].Trigger);
        }

        /// <summary>
        /// An aircraft staying inside fires only once.
        /// </summary>
        [Fact]
        public void Evaluate_StaysInside_FiresOnce()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule();

            Assert.Single(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 0));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 1));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.2) }, 2));
        }

        /// <summary>
        /// Leaving and coming back within the cooldown does not fire again, after the cooldown it does.
        /// </summary>
        [Fact]
        public void Evaluate_ReentryRespectsCooldown()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule(cooldownMinutes: 30);

            Assert.Single(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 0));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 2.0) }, 5));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 10));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 2.0) }, 20));
            Assert.Single(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 31));
        }

        /// <summary>
        /// An aircraft that disappears from the data and returns inside the area counts as appeared again once the cooldown has passed.
        /// </summary>
        [Fact]
        public void Evaluate_DisappearsAndReturns_AppearsAgainAfterCooldown()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule(cooldownMinutes: 10);

            Assert.Single(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 0));
            Assert.Empty(Run(engine, rule, new List<Aircraft>(), 1));
            Assert.Empty(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 2));
            Assert.Empty(Run(engine, rule, new List<Aircraft>(), 3));
            Assert.Single(Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 15));
        }

        /// <summary>
        /// A disabled trigger is not reported, but the state is still tracked.
        /// </summary>
        [Fact]
        public void Evaluate_DisabledTriggers_AreNotReported()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule onlyEntry = CreateRule(entry: true, appearance: false);

            Assert.Empty(Run(engine, onlyEntry, new List<Aircraft> { CreateAircraft("A1", 2.0) }, 0));
            Assert.Single(Run(engine, onlyEntry, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 1));

            AlertEngine other = new AlertEngine();
            AlertRule onlyAppearance = CreateRule(entry: false, appearance: true);

            Assert.Empty(Run(other, onlyAppearance, new List<Aircraft> { CreateAircraft("A1", 2.0) }, 0));
            Assert.Empty(Run(other, onlyAppearance, new List<Aircraft> { CreateAircraft("A1", 0.1) }, 1));
            Assert.Single(Run(other, onlyAppearance, new List<Aircraft> { CreateAircraft("A2", 0.1) }, 2));
        }

        /// <summary>
        /// The condition filters aircraft, and an aircraft that later starts to satisfy it fires as appeared.
        /// </summary>
        [Fact]
        public void Evaluate_Condition_Filters()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule(FilterCondition.Military());

            List<AlertMatch> matches = Run(engine, rule, new List<Aircraft> { CreateAircraft("A1", 0.1), CreateAircraft("A2", 0.1, military: true) });

            Assert.Single(matches);
            Assert.Equal("A2", matches[0].Aircraft.Hex);
        }

        /// <summary>
        /// A condition with an unknown result does not fire, and fires when it becomes true.
        /// </summary>
        [Fact]
        public void Evaluate_UnknownCondition_FiresWhenKnown()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule(FilterCondition.ToAirport("EPWA"));
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("A1", 0.1) };
            FlightRoute route = new FlightRoute("EPKK", "Krakow", "EPWA", "Warszawa");

            Assert.Empty(engine.Evaluate(new List<AlertRule> { rule }, aircraft, null, Airports, NoRoutes, Start));

            List<AlertMatch> matches = engine.Evaluate(new List<AlertRule> { rule }, aircraft, null, Airports, (string? callsign) => route, Start.AddMinutes(1));

            Assert.Single(matches);
            Assert.Equal(AlertTrigger.Appeared, matches[0].Trigger);
        }

        /// <summary>
        /// A disabled rule never fires and forgets its state.
        /// </summary>
        [Fact]
        public void Evaluate_DisabledRule_DoesNotFire()
        {
            AlertEngine engine = new AlertEngine();

            Assert.Empty(Run(engine, CreateRule(enabled: false), new List<Aircraft> { CreateAircraft("A1", 0.1) }));
            Assert.Single(Run(engine, CreateRule(), new List<Aircraft> { CreateAircraft("A1", 0.1) }, 1));
        }

        /// <summary>
        /// The around-me area needs a position and follows it.
        /// </summary>
        [Fact]
        public void Evaluate_AroundMe_UsesPosition()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = new AlertRule("r1", "Me", true, new AlertArea(AlertAreaKind.AroundMe, 50.0), null, true, true, 30);
            List<AlertRule> rules = new List<AlertRule> { rule };
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("A1", 0.1) };

            Assert.Empty(engine.Evaluate(rules, aircraft, null, Airports, NoRoutes, Start));
            Assert.Empty(engine.Evaluate(rules, aircraft, new GeoPoint(40.0, 10.0), Airports, NoRoutes, Start.AddMinutes(1)));
            Assert.Single(engine.Evaluate(rules, aircraft, Center, Airports, NoRoutes, Start.AddMinutes(2)));
        }

        /// <summary>
        /// The airport area is resolved through the lookup, and an unknown airport is skipped.
        /// </summary>
        [Fact]
        public void Evaluate_AroundAirport_UsesLookup()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule known = new AlertRule("r1", "Waw", true, new AlertArea(AlertAreaKind.AroundAirport, 20.0, null, "EPWA"), null, true, true, 30);
            AlertRule unknown = new AlertRule("r2", "Xxx", true, new AlertArea(AlertAreaKind.AroundAirport, 20.0, null, "XXXX"), null, true, true, 30);
            AlertRule missing = new AlertRule("r3", "None", true, new AlertArea(AlertAreaKind.AroundAirport, 20.0), null, true, true, 30);
            Aircraft nearWarsaw = new Aircraft("A1", null, 52.17, 20.97, 3000, false, null, null, 0.0, null, null);

            List<AlertMatch> matches = engine.Evaluate(new List<AlertRule> { known, unknown, missing }, new List<Aircraft> { nearWarsaw }, null, Airports, NoRoutes, Start);

            Assert.Single(matches);
            Assert.Equal("r1", matches[0].Rule.Id);
        }

        /// <summary>
        /// The same aircraft can fire for two different rules.
        /// </summary>
        [Fact]
        public void Evaluate_TwoRules_TrackedIndependently()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule first = new AlertRule("r1", "A", true, new AlertArea(AlertAreaKind.Circle, 50.0, Center), null, true, true, 30);
            AlertRule second = new AlertRule("r2", "B", true, new AlertArea(AlertAreaKind.Circle, 50.0, Center), null, true, true, 30);

            List<AlertMatch> matches = engine.Evaluate(new List<AlertRule> { first, second }, new List<Aircraft> { CreateAircraft("A1", 0.1) }, null, Airports, NoRoutes, Start);

            Assert.Equal(2, matches.Count);
        }

        /// <summary>
        /// The cooldown belongs to one rule: an aircraft reported by one rule is still reported by another rule that it starts to satisfy.
        /// </summary>
        [Fact]
        public void Evaluate_CooldownIsPerRule()
        {
            AlertEngine engine = new AlertEngine();
            AlertArea area = new AlertArea(AlertAreaKind.Circle, 50.0, Center);
            AlertRule any = new AlertRule("any", "Any", true, area, null, true, true, 30);
            AlertRule military = new AlertRule("mil", "Military", true, area, FilterCondition.Military(), true, true, 30);
            List<AlertRule> rules = new List<AlertRule> { any, military };

            List<AlertMatch> first = engine.Evaluate(rules, new List<Aircraft> { CreateAircraft("A1", 0.1) }, null, Airports, NoRoutes, Start);

            Assert.Single(first);
            Assert.Equal("any", first[0].Rule.Id);

            List<AlertMatch> second = engine.Evaluate(rules, new List<Aircraft> { CreateAircraft("A1", 0.1, military: true) }, null, Airports, NoRoutes, Start.AddMinutes(1));

            Assert.Single(second);
            Assert.Equal("mil", second[0].Rule.Id);

            Assert.Empty(engine.Evaluate(rules, new List<Aircraft> { CreateAircraft("A1", 0.1, military: true) }, null, Airports, NoRoutes, Start.AddMinutes(2)));
        }

        /// <summary>
        /// Removing a rule drops its state, so a rule with the same identifier added later starts fresh.
        /// </summary>
        [Fact]
        public void Evaluate_RemovedRule_StateIsDropped()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule();
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("A1", 0.1) };

            Assert.Single(Run(engine, rule, aircraft, 0));
            engine.Evaluate(new List<AlertRule>(), aircraft, null, Airports, NoRoutes, Start.AddMinutes(1));
            Assert.Single(Run(engine, rule, aircraft, 2));
        }

        /// <summary>
        /// Reset makes every aircraft new again.
        /// </summary>
        [Fact]
        public void Reset_ForgetsState()
        {
            AlertEngine engine = new AlertEngine();
            AlertRule rule = CreateRule();
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("A1", 0.1) };

            Assert.Single(Run(engine, rule, aircraft, 0));
            engine.Reset();
            Assert.Single(Run(engine, rule, aircraft, 1));
        }

        /// <summary>
        /// CountMatching counts the aircraft inside the area that satisfy the condition, without touching the state.
        /// </summary>
        [Fact]
        public void CountMatching_CountsInsideAndSatisfying()
        {
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("A1", 0.1), CreateAircraft("A2", 0.1, military: true), CreateAircraft("A3", 2.0, military: true) };

            Assert.Equal(2, AlertEngine.CountMatching(CreateRule(), aircraft, null, Airports, NoRoutes));
            Assert.Equal(1, AlertEngine.CountMatching(CreateRule(FilterCondition.Military()), aircraft, null, Airports, NoRoutes));
        }

        /// <summary>
        /// CountMatching returns zero when the center of the area cannot be determined.
        /// </summary>
        [Fact]
        public void CountMatching_NoCenter_IsZero()
        {
            AlertRule rule = new AlertRule("r1", "Me", true, new AlertArea(AlertAreaKind.AroundMe, 50.0), null, true, true, 30);

            Assert.Equal(0, AlertEngine.CountMatching(rule, new List<Aircraft> { CreateAircraft("A1", 0.1) }, null, Airports, NoRoutes));
        }
    }
}

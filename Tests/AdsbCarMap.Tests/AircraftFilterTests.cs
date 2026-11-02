using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the filter expressions, the evaluator and the predefined filters.
    /// </summary>
    public class AircraftFilterTests
    {
        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Creates an aircraft with the given properties.
        /// </summary>
        /// <param name="callsign">Callsign.</param>
        /// <param name="typeCode">ICAO type code.</param>
        /// <param name="altitudeFeet">Altitude in feet.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="military">Whether the aircraft is military.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string? callsign = "LOT281", string? typeCode = "B738", int? altitudeFeet = 35000, bool onGround = false, bool military = false)
        {
            return new Aircraft("489123", callsign, 52.0, 21.0, altitudeFeet, onGround, 450.0, 90.0, 0.0, typeCode, null, "A3", military);
        }

        /// <summary>
        /// Creates a route lookup that returns one route for every callsign.
        /// </summary>
        /// <param name="destinationCode">Destination code.</param>
        /// <param name="destinationCity">Destination city.</param>
        /// <returns>The lookup.</returns>
        private static Func<string?, FlightRoute?> RouteTo(string destinationCode, string? destinationCity)
        {
            FlightRoute route = new FlightRoute("WAW", "Warszawa", destinationCode, destinationCity);

            return (string? callsign) => route;
        }

        /// <summary>
        /// The ground and military conditions test the corresponding flags.
        /// </summary>
        [Fact]
        public void Condition_FlagsReflectAircraftState()
        {
            Assert.Equal(FilterResult.True, FilterCondition.OnGround().Evaluate(CreateAircraft(onGround: true), NoRoutes));
            Assert.Equal(FilterResult.False, FilterCondition.OnGround().Evaluate(CreateAircraft(), NoRoutes));
            Assert.Equal(FilterResult.True, FilterCondition.Military().Evaluate(CreateAircraft(military: true), NoRoutes));
            Assert.Equal(FilterResult.False, FilterCondition.Military().Evaluate(CreateAircraft(), NoRoutes));
        }

        /// <summary>
        /// The kind condition compares with the classified kind.
        /// </summary>
        [Fact]
        public void Condition_KindUsesClassifier()
        {
            Aircraft helicopter = CreateAircraft(typeCode: "R44");

            Assert.Equal(FilterResult.True, FilterCondition.OfKind(AircraftKind.Helicopter).Evaluate(helicopter, NoRoutes));
            Assert.Equal(FilterResult.False, FilterCondition.OfKind(AircraftKind.Fighter).Evaluate(helicopter, NoRoutes));
        }

        /// <summary>
        /// The manufacturer condition is unknown when the type is missing or not in the table.
        /// </summary>
        [Theory]
        [InlineData("B738", "Boeing", FilterResult.True)]
        [InlineData("b738", "boeing", FilterResult.True)]
        [InlineData("A320", "Boeing", FilterResult.False)]
        [InlineData(null, "Boeing", FilterResult.Unknown)]
        [InlineData("ZZZZ", "Boeing", FilterResult.Unknown)]
        public void Condition_Manufacturer(string? typeCode, string manufacturer, FilterResult expected)
        {
            Assert.Equal(expected, FilterCondition.MadeBy(manufacturer).Evaluate(CreateAircraft(typeCode: typeCode), NoRoutes));
        }

        /// <summary>
        /// The type code condition ignores letter case and is unknown for a missing type.
        /// </summary>
        [Theory]
        [InlineData("B738", "b738", FilterResult.True)]
        [InlineData("A320", "B738", FilterResult.False)]
        [InlineData(null, "B738", FilterResult.Unknown)]
        [InlineData("  ", "B738", FilterResult.Unknown)]
        public void Condition_TypeCode(string? typeCode, string wanted, FilterResult expected)
        {
            Assert.Equal(expected, FilterCondition.OfType(wanted).Evaluate(CreateAircraft(typeCode: typeCode), NoRoutes));
        }

        /// <summary>
        /// The callsign condition matches the beginning, ignores letter case and is unknown without a callsign.
        /// </summary>
        [Theory]
        [InlineData("LOT281", "LOT", FilterResult.True)]
        [InlineData("lot281", "LOT", FilterResult.True)]
        [InlineData("RYR12", "LOT", FilterResult.False)]
        [InlineData(null, "LOT", FilterResult.Unknown)]
        [InlineData("LOT281", "", FilterResult.Unknown)]
        public void Condition_CallsignPrefix(string? callsign, string prefix, FilterResult expected)
        {
            Assert.Equal(expected, FilterCondition.CallsignStartsWith(prefix).Evaluate(CreateAircraft(callsign: callsign), NoRoutes));
        }

        /// <summary>
        /// The altitude condition converts feet to meters, treats the ground as 0 m and is unknown without altitude.
        /// </summary>
        [Fact]
        public void Condition_AltitudeRange()
        {
            FilterCondition above = FilterCondition.AltitudeBetween(10000.0, null);
            FilterCondition below = FilterCondition.AltitudeBetween(null, 3000.0);

            Assert.Equal(FilterResult.True, above.Evaluate(CreateAircraft(altitudeFeet: 35000), NoRoutes));
            Assert.Equal(FilterResult.False, above.Evaluate(CreateAircraft(altitudeFeet: 5000), NoRoutes));
            Assert.Equal(FilterResult.True, below.Evaluate(CreateAircraft(altitudeFeet: 5000), NoRoutes));
            Assert.Equal(FilterResult.False, below.Evaluate(CreateAircraft(altitudeFeet: 35000), NoRoutes));
            Assert.Equal(FilterResult.True, below.Evaluate(CreateAircraft(altitudeFeet: null, onGround: true), NoRoutes));
            Assert.Equal(FilterResult.Unknown, below.Evaluate(CreateAircraft(altitudeFeet: null), NoRoutes));
        }

        /// <summary>
        /// The altitude bounds are inclusive.
        /// </summary>
        [Fact]
        public void Condition_AltitudeBoundsAreInclusive()
        {
            FilterCondition exact = FilterCondition.AltitudeBetween(3048.0, 3048.0);

            Assert.Equal(FilterResult.True, exact.Evaluate(CreateAircraft(altitudeFeet: 10000), NoRoutes));
        }

        /// <summary>
        /// The destination condition matches the code or the city without regard to case and diacritics, and is unknown while the route is not known.
        /// </summary>
        [Theory]
        [InlineData("RZE", "Rzeszów", "RZE", FilterResult.True)]
        [InlineData("RZE", "Rzeszów", "rze", FilterResult.True)]
        [InlineData("RZE", "Rzeszów", "rzeszow", FilterResult.True)]
        [InlineData("RZE", null, "Rzeszów", FilterResult.False)]
        [InlineData("WAW", "Warszawa", "RZE", FilterResult.False)]
        public void Condition_Destination(string code, string? city, string wanted, FilterResult expected)
        {
            Assert.Equal(expected, FilterCondition.ToAirport(wanted).Evaluate(CreateAircraft(), RouteTo(code, city)));
        }

        /// <summary>
        /// The origin condition tests the departure airport and not the destination.
        /// </summary>
        [Fact]
        public void Condition_OriginUsesDeparture()
        {
            Func<string?, FlightRoute?> lookup = RouteTo("RZE", "Rzeszów");

            Assert.Equal(FilterResult.True, FilterCondition.FromAirport("WAW").Evaluate(CreateAircraft(), lookup));
            Assert.Equal(FilterResult.False, FilterCondition.FromAirport("RZE").Evaluate(CreateAircraft(), lookup));
        }

        /// <summary>
        /// Airport conditions are unknown without a route or with an empty operand.
        /// </summary>
        [Fact]
        public void Condition_AirportUnknownWithoutRoute()
        {
            Assert.Equal(FilterResult.Unknown, FilterCondition.ToAirport("RZE").Evaluate(CreateAircraft(), NoRoutes));
            Assert.Equal(FilterResult.Unknown, FilterCondition.ToAirport(" ").Evaluate(CreateAircraft(), RouteTo("RZE", null)));
        }

        /// <summary>
        /// The lookup receives the callsign of the aircraft.
        /// </summary>
        [Fact]
        public void Condition_AirportPassesCallsignToLookup()
        {
            string? received = null;
            Func<string?, FlightRoute?> lookup = (string? callsign) =>
            {
                received = callsign;

                return null;
            };

            FilterCondition.ToAirport("RZE").Evaluate(CreateAircraft(callsign: "LOT281"), lookup);

            Assert.Equal("LOT281", received);
        }

        /// <summary>
        /// The AND group follows three-valued logic: false dominates, then unknown.
        /// </summary>
        [Fact]
        public void Group_AllUsesThreeValuedLogic()
        {
            Aircraft aircraft = CreateAircraft();
            FilterNode yes = FilterCondition.OfType("B738");
            FilterNode no = FilterCondition.OfType("A320");
            FilterNode unknown = FilterCondition.ToAirport("RZE");

            Assert.Equal(FilterResult.True, FilterGroup.All(yes, yes).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.False, FilterGroup.All(yes, no).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.Unknown, FilterGroup.All(yes, unknown).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.False, FilterGroup.All(unknown, no).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.True, FilterGroup.All().Evaluate(aircraft, NoRoutes));
        }

        /// <summary>
        /// The OR group follows three-valued logic: true dominates, then unknown.
        /// </summary>
        [Fact]
        public void Group_AnyUsesThreeValuedLogic()
        {
            Aircraft aircraft = CreateAircraft();
            FilterNode yes = FilterCondition.OfType("B738");
            FilterNode no = FilterCondition.OfType("A320");
            FilterNode unknown = FilterCondition.ToAirport("RZE");

            Assert.Equal(FilterResult.True, FilterGroup.Any(no, yes).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.False, FilterGroup.Any(no, no).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.Unknown, FilterGroup.Any(no, unknown).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.True, FilterGroup.Any(unknown, yes).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.False, FilterGroup.Any().Evaluate(aircraft, NoRoutes));
        }

        /// <summary>
        /// A group stops at the first deciding child, so later route conditions are not looked up.
        /// </summary>
        [Fact]
        public void Group_StopsAtDecidingChild()
        {
            int lookups = 0;
            Func<string?, FlightRoute?> lookup = (string? callsign) =>
            {
                lookups++;

                return null;
            };

            FilterGroup.All(FilterCondition.OfType("A320"), FilterCondition.ToAirport("RZE")).Evaluate(CreateAircraft(), lookup);
            FilterGroup.Any(FilterCondition.OfType("B738"), FilterCondition.ToAirport("RZE")).Evaluate(CreateAircraft(), lookup);

            Assert.Equal(0, lookups);
        }

        /// <summary>
        /// Negation swaps true and false and leaves unknown unchanged.
        /// </summary>
        [Fact]
        public void Not_SwapsTrueAndFalseAndKeepsUnknown()
        {
            Aircraft aircraft = CreateAircraft();

            Assert.Equal(FilterResult.False, new FilterNot(FilterCondition.OfType("B738")).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.True, new FilterNot(FilterCondition.OfType("A320")).Evaluate(aircraft, NoRoutes));
            Assert.Equal(FilterResult.Unknown, new FilterNot(FilterCondition.ToAirport("RZE")).Evaluate(aircraft, NoRoutes));
        }

        /// <summary>
        /// The example expression "Boeing and LOT and not heading to Warsaw" evaluates as intended, and an unknown route does not let the aircraft through.
        /// </summary>
        [Fact]
        public void Expression_BoeingLotNotToWarsaw()
        {
            AircraftFilter filter = new AircraftFilter(
                "test",
                "Test",
                FilterMode.Show,
                FilterGroup.All(FilterCondition.MadeBy("Boeing"), FilterCondition.CallsignStartsWith("LOT"), new FilterNot(FilterCondition.ToAirport("WAW"))));
            Aircraft aircraft = CreateAircraft();
            List<AircraftFilter> active = new List<AircraftFilter> { filter };

            Assert.True(AircraftFilterEvaluator.Passes(aircraft, active, RouteTo("RZE", "Rzeszów")));
            Assert.False(AircraftFilterEvaluator.Passes(aircraft, active, RouteTo("WAW", "Warszawa")));
            Assert.False(AircraftFilterEvaluator.Passes(aircraft, active, NoRoutes));
            Assert.False(AircraftFilterEvaluator.Passes(CreateAircraft(typeCode: "A320"), active, RouteTo("RZE", "Rzeszów")));
        }

        /// <summary>
        /// With no active filter every aircraft passes.
        /// </summary>
        [Fact]
        public void Evaluator_PassesEverythingWithoutFilters()
        {
            Assert.True(AircraftFilterEvaluator.Passes(CreateAircraft(onGround: true), new List<AircraftFilter>(), NoRoutes));
        }

        /// <summary>
        /// A hiding filter removes the matching aircraft and leaves the others.
        /// </summary>
        [Fact]
        public void Evaluator_HideFilterRemovesMatches()
        {
            List<AircraftFilter> active = new List<AircraftFilter> { BuiltInFilters.HideOnGround };

            Assert.False(AircraftFilterEvaluator.Passes(CreateAircraft(onGround: true), active, NoRoutes));
            Assert.True(AircraftFilterEvaluator.Passes(CreateAircraft(), active, NoRoutes));
        }

        /// <summary>
        /// Several showing filters are combined with OR.
        /// </summary>
        [Fact]
        public void Evaluator_ShowFiltersAreUnion()
        {
            List<AircraftFilter> active = new List<AircraftFilter> { BuiltInFilters.Military, BuiltInFilters.ToRzeszow };
            Func<string?, FlightRoute?> toRzeszow = RouteTo("RZE", "Rzeszów");

            Assert.True(AircraftFilterEvaluator.Passes(CreateAircraft(military: true), active, NoRoutes));
            Assert.True(AircraftFilterEvaluator.Passes(CreateAircraft(), active, toRzeszow));
            Assert.False(AircraftFilterEvaluator.Passes(CreateAircraft(), active, RouteTo("WAW", "Warszawa")));
            Assert.False(AircraftFilterEvaluator.Passes(CreateAircraft(), active, NoRoutes));
        }

        /// <summary>
        /// A hiding filter wins over a showing filter.
        /// </summary>
        [Fact]
        public void Evaluator_HideWinsOverShow()
        {
            List<AircraftFilter> active = new List<AircraftFilter> { BuiltInFilters.Military, BuiltInFilters.HideOnGround };

            Assert.False(AircraftFilterEvaluator.Passes(CreateAircraft(military: true, onGround: true), active, NoRoutes));
            Assert.True(AircraftFilterEvaluator.Passes(CreateAircraft(military: true), active, NoRoutes));
        }

        /// <summary>
        /// Routes are not looked up for an aircraft already removed by a hiding filter or already accepted by an earlier showing filter.
        /// </summary>
        [Fact]
        public void Evaluator_SkipsRouteLookupWhenDecided()
        {
            int lookups = 0;
            Func<string?, FlightRoute?> lookup = (string? callsign) =>
            {
                lookups++;

                return null;
            };
            List<AircraftFilter> active = new List<AircraftFilter> { BuiltInFilters.HideOnGround, BuiltInFilters.Military, BuiltInFilters.ToRzeszow };

            AircraftFilterEvaluator.Passes(CreateAircraft(onGround: true), active, lookup);
            AircraftFilterEvaluator.Passes(CreateAircraft(military: true), active, lookup);

            Assert.Equal(0, lookups);
        }

        /// <summary>
        /// The predefined filter "to Rzeszów" accepts the IATA code, the ICAO code and the city.
        /// </summary>
        [Theory]
        [InlineData("RZE", null)]
        [InlineData("EPRZ", null)]
        [InlineData("XXX", "Rzeszów")]
        [InlineData("XXX", "Rzeszow")]
        public void BuiltIn_ToRzeszowRecognizesAirport(string code, string? city)
        {
            Assert.Equal(FilterResult.True, BuiltInFilters.ToRzeszow.Expression.Evaluate(CreateAircraft(), RouteTo(code, city)));
        }

        /// <summary>
        /// The predefined filter "to Rzeszów" rejects other destinations and stays unknown without a route.
        /// </summary>
        [Fact]
        public void BuiltIn_ToRzeszowRejectsOthers()
        {
            Assert.Equal(FilterResult.False, BuiltInFilters.ToRzeszow.Expression.Evaluate(CreateAircraft(), RouteTo("KRK", "Kraków")));
            Assert.Equal(FilterResult.Unknown, BuiltInFilters.ToRzeszow.Expression.Evaluate(CreateAircraft(), NoRoutes));
        }

        /// <summary>
        /// The predefined filters have distinct identifiers and the expected modes.
        /// </summary>
        [Fact]
        public void BuiltIn_ModesAndIdentifiers()
        {
            Assert.Equal(FilterMode.Show, BuiltInFilters.Military.Mode);
            Assert.Equal(FilterMode.Hide, BuiltInFilters.HideOnGround.Mode);
            Assert.Equal(FilterMode.Show, BuiltInFilters.ToRzeszow.Mode);
            Assert.Equal(3, new HashSet<string> { BuiltInFilters.Military.Id, BuiltInFilters.HideOnGround.Id, BuiltInFilters.ToRzeszow.Id }.Count);
        }
    }
}

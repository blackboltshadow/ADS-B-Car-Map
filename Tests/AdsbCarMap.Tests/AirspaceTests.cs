using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the logic of the airspace layer: states of the areas, the configuration of the layer and the texts.
    /// </summary>
    public class AirspaceTests
    {
        /// <summary>
        /// The moment used by the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a reservation that starts and ends the given number of hours from now.
        /// </summary>
        /// <param name="status">State of the reservation.</param>
        /// <param name="startHours">Start, in hours from now.</param>
        /// <param name="endHours">End, in hours from now.</param>
        /// <param name="remarks">Remarks, or null.</param>
        /// <param name="unit">Unit text.</param>
        /// <returns>The reservation.</returns>
        private static AirspaceReservation Reservation(AirspaceReservationStatus status, double startHours, double endHours, string? remarks = null, string unit = "EPWA")
        {
            return new AirspaceReservation(
                Now.AddHours(startHours),
                Now.AddHours(endHours),
                new AirspaceLimit(0, AirspaceLimitReference.Ground),
                new AirspaceLimit(24500, AirspaceLimitReference.FlightLevel),
                status,
                unit,
                remarks);
        }

        /// <summary>
        /// Creates an area with the given reservations.
        /// </summary>
        /// <param name="type">Kind of the area.</param>
        /// <param name="reservations">Reservations of the area.</param>
        /// <returns>The area.</returns>
        private static AirspaceZone Zone(string type, params AirspaceReservation[] reservations)
        {
            List<AirspaceVertex> outline = new List<AirspaceVertex> { new AirspaceVertex(50, 20), new AirspaceVertex(51, 20), new AirspaceVertex(51, 21) };

            return new AirspaceZone("EP" + type + "1", type, new List<IReadOnlyList<AirspaceVertex>> { outline }, new AirspaceBounds(50, 51, 20, 21), reservations);
        }

        /// <summary>
        /// A confirmed reservation in force makes the area active.
        /// </summary>
        [Fact]
        public void GetState_ActiveWhenActivatedReservationIsInForce()
        {
            AirspaceZone zone = Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 1));

            Assert.Equal(AirspaceZoneState.Active, AirspaceClassifier.GetState(zone, Now));
        }

        /// <summary>
        /// The start belongs to the reservation and the end does not.
        /// </summary>
        [Fact]
        public void GetState_StartIsIncludedAndEndIsNot()
        {
            AirspaceZone zone = Zone("D", Reservation(AirspaceReservationStatus.Activated, 0, 1));
            AirspaceZone ending = Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 0));

            Assert.Equal(AirspaceZoneState.Active, AirspaceClassifier.GetState(zone, Now));
            Assert.Equal(AirspaceZoneState.Inactive, AirspaceClassifier.GetState(ending, Now));
        }

        /// <summary>
        /// A reservation not confirmed, or one that starts later, makes the area planned.
        /// </summary>
        [Theory]
        [InlineData(AirspaceReservationStatus.Planned, -1, 1)]
        [InlineData(AirspaceReservationStatus.Planned, 2, 3)]
        [InlineData(AirspaceReservationStatus.Activated, 2, 3)]
        [InlineData(AirspaceReservationStatus.Other, -1, 1)]
        public void GetState_PlannedWhenNothingIsConfirmedNow(AirspaceReservationStatus status, double startHours, double endHours)
        {
            AirspaceZone zone = Zone("D", Reservation(status, startHours, endHours));

            Assert.Equal(AirspaceZoneState.Planned, AirspaceClassifier.GetState(zone, Now));
        }

        /// <summary>
        /// An area whose reservations are over, or that has none, is inactive.
        /// </summary>
        [Fact]
        public void GetState_InactiveWhenAllIsOver()
        {
            Assert.Equal(AirspaceZoneState.Inactive, AirspaceClassifier.GetState(Zone("D", Reservation(AirspaceReservationStatus.Activated, -5, -1)), Now));
            Assert.Equal(AirspaceZoneState.Inactive, AirspaceClassifier.GetState(Zone("D"), Now));
        }

        /// <summary>
        /// Any active reservation makes the area active, whatever the others are.
        /// </summary>
        [Fact]
        public void GetState_ActiveWinsOverPlanned()
        {
            AirspaceZone zone = Zone("D", Reservation(AirspaceReservationStatus.Planned, 1, 2), Reservation(AirspaceReservationStatus.Activated, -1, 1));

            Assert.Equal(AirspaceZoneState.Active, AirspaceClassifier.GetState(zone, Now));
        }

        /// <summary>
        /// For the card the active reservation is chosen first, and among the active ones the one that ends first.
        /// </summary>
        [Fact]
        public void GetRelevantReservation_PrefersActiveThatEndsFirst()
        {
            AirspaceReservation later = Reservation(AirspaceReservationStatus.Activated, -1, 5);
            AirspaceReservation sooner = Reservation(AirspaceReservationStatus.Activated, -1, 2);
            AirspaceReservation planned = Reservation(AirspaceReservationStatus.Planned, -1, 1);

            Assert.Same(sooner, AirspaceClassifier.GetRelevantReservation(Zone("D", planned, later, sooner), Now));
        }

        /// <summary>
        /// Without an active reservation, one in force that is not confirmed is chosen before those that start later, and the one that starts first among those.
        /// </summary>
        [Fact]
        public void GetRelevantReservation_OrdersPlannedOnes()
        {
            AirspaceReservation inForce = Reservation(AirspaceReservationStatus.Planned, -1, 1);
            AirspaceReservation next = Reservation(AirspaceReservationStatus.Planned, 2, 3);
            AirspaceReservation afterNext = Reservation(AirspaceReservationStatus.Planned, 4, 5);

            Assert.Same(inForce, AirspaceClassifier.GetRelevantReservation(Zone("D", afterNext, next, inForce), Now));
            Assert.Same(next, AirspaceClassifier.GetRelevantReservation(Zone("D", afterNext, next), Now));
        }

        /// <summary>
        /// For an inactive area the reservation that ended last is chosen; an area without reservations gives null.
        /// </summary>
        [Fact]
        public void GetRelevantReservation_ChoosesLastEndedAndHandlesNone()
        {
            AirspaceReservation old = Reservation(AirspaceReservationStatus.Activated, -9, -8);
            AirspaceReservation recent = Reservation(AirspaceReservationStatus.Activated, -3, -2);

            Assert.Same(recent, AirspaceClassifier.GetRelevantReservation(Zone("D", old, recent), Now));
            Assert.Same(recent, AirspaceClassifier.GetRelevantReservation(Zone("D", recent, old), Now));
            Assert.Null(AirspaceClassifier.GetRelevantReservation(Zone("D"), Now));
        }

        /// <summary>
        /// The default configuration shows the restricting kinds, active areas only, and hides new kinds.
        /// </summary>
        [Fact]
        public void Default_ShowsRestrictingKindsOfActiveAreas()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default;

            Assert.True(config.IsTypeShown("D"));
            Assert.True(config.IsTypeShown("r"));
            Assert.False(config.IsTypeShown("TRA"));
            Assert.False(config.IsTypeShown("NEWKIND"));
            Assert.Equal(AirspaceZoneState.Active, config.States);
            Assert.True(config.Allows("D", AirspaceZoneState.Active));
            Assert.False(config.Allows("D", AirspaceZoneState.Planned));
            Assert.False(config.Allows("TRA", AirspaceZoneState.Active));
        }

        /// <summary>
        /// A kind without a decision follows the setting for new kinds; a decision wins over it.
        /// </summary>
        [Fact]
        public void ShowNewTypes_AppliesOnlyToKindsWithoutDecision()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithShowNewTypes(true).WithType("D", false);

            Assert.True(config.ShowNewTypes);
            Assert.True(config.IsTypeShown("NEWKIND"));
            Assert.True(config.IsTypeShown("TRA"));
            Assert.False(config.IsTypeShown("D"));
        }

        /// <summary>
        /// Changing the configuration gives a new object and leaves the old one unchanged.
        /// </summary>
        [Fact]
        public void With_DoesNotChangeTheOriginal()
        {
            AirspaceLayerConfig original = AirspaceLayerConfig.Default;

            AirspaceLayerConfig changed = original.WithType("TRA", true).WithStates(AirspaceZoneState.All);

            Assert.False(original.IsTypeShown("TRA"));
            Assert.Equal(AirspaceZoneState.Active, original.States);
            Assert.True(changed.IsTypeShown("TRA"));
            Assert.Equal(AirspaceZoneState.All, changed.States);
        }

        /// <summary>
        /// Bits outside the known states are dropped.
        /// </summary>
        [Fact]
        public void WithStates_DropsUnknownBits()
        {
            Assert.Equal(AirspaceZoneState.Planned, AirspaceLayerConfig.Default.WithStates((AirspaceZoneState)(2 | 64)).States);
        }

        /// <summary>
        /// Selecting keeps the order of the input and drops areas of hidden kinds and of hidden states.
        /// </summary>
        [Fact]
        public void Select_FiltersByKindAndState()
        {
            AirspaceZone active = Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 1));
            AirspaceZone planned = Zone("R", Reservation(AirspaceReservationStatus.Planned, 1, 2));
            AirspaceZone hiddenKind = Zone("TRA", Reservation(AirspaceReservationStatus.Activated, -1, 1));
            AirspaceZone inactive = Zone("TSA", Reservation(AirspaceReservationStatus.Activated, -5, -4));
            List<AirspaceZone> zones = new List<AirspaceZone> { inactive, planned, hiddenKind, active };

            IReadOnlyList<AirspaceZoneView> onlyActive = AirspaceLayerConfig.Default.Select(zones, Now);
            IReadOnlyList<AirspaceZoneView> all = AirspaceLayerConfig.Default.WithStates(AirspaceZoneState.All).Select(zones, Now);

            AirspaceZoneView view = Assert.Single(onlyActive);
            Assert.Same(active, view.Zone);
            Assert.Equal(AirspaceZoneState.Active, view.State);
            Assert.NotNull(view.Reservation);
            Assert.Equal(new List<AirspaceZone> { inactive, planned, active }, all.Select((AirspaceZoneView item) => item.Zone).ToList());
            Assert.Equal(AirspaceZoneState.Inactive, all[0].State);
        }

        /// <summary>
        /// Turning a kind on shows its areas at once, also a kind that was not known when the configuration was made.
        /// </summary>
        [Fact]
        public void Select_ShowsKindAfterItIsSwitchedOn()
        {
            AirspaceZone zone = Zone("NEWKIND", Reservation(AirspaceReservationStatus.Activated, -1, 1));
            List<AirspaceZone> zones = new List<AirspaceZone> { zone };

            Assert.Empty(AirspaceLayerConfig.Default.Select(zones, Now));
            Assert.Single(AirspaceLayerConfig.Default.WithType("newkind", true).Select(zones, Now));
        }

        /// <summary>
        /// The configuration is read back from its stored text.
        /// </summary>
        [Fact]
        public void SerializeAndParse_RoundTrip()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithType("TRA", true).WithType("D", false).WithStates(AirspaceZoneState.Active | AirspaceZoneState.Planned).WithShowNewTypes(true);

            AirspaceLayerConfig back = AirspaceLayerConfig.Parse(config.Serialize());

            Assert.Equal(config.Serialize(), back.Serialize());
            Assert.Equal(AirspaceZoneState.Active | AirspaceZoneState.Planned, back.States);
            Assert.True(back.ShowNewTypes);
            Assert.True(back.IsTypeShown("tra"));
            Assert.False(back.IsTypeShown("D"));
        }

        /// <summary>
        /// The stored text is written in a fixed, sorted form.
        /// </summary>
        [Fact]
        public void Serialize_IsSortedAndStable()
        {
            AirspaceLayerConfig config = new AirspaceLayerConfig(new Dictionary<string, bool> { { "R", true }, { "D", false } }, AirspaceZoneState.All, false);

            Assert.Equal("v2;7;0;D-,R+;", config.Serialize());
        }

        /// <summary>
        /// Missing, foreign or damaged text gives the default configuration.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("v3;1;0;D+;")]
        [InlineData("v2;1;0;D+")]
        [InlineData("v1;1;0")]
        [InlineData("v1;1;0;D+;")]
        [InlineData("v1;x;0;D+")]
        [InlineData("v1;-1;0;D+")]
        [InlineData("v1;1;2;D+")]
        public void Parse_ReturnsDefaultForBadText(string? text)
        {
            Assert.Equal(AirspaceLayerConfig.Default.Serialize(), AirspaceLayerConfig.Parse(text).Serialize());
        }

        /// <summary>
        /// A damaged entry of a kind is skipped and the rest is kept.
        /// </summary>
        [Fact]
        public void Parse_SkipsDamagedEntries()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Parse("v2;2;1;D+,,X,+,R?, TSA- ;");

            Assert.Equal(AirspaceZoneState.Planned, config.States);
            Assert.True(config.IsTypeShown("D"));
            Assert.False(config.IsTypeShown("TSA"));
            Assert.True(config.IsTypeShown("X"));
        }

        /// <summary>
        /// An empty set of kinds and states can be stored: the user may switch everything off.
        /// </summary>
        [Fact]
        public void Parse_KeepsEverythingOff()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Parse("v2;0;0;;");

            Assert.Equal(AirspaceZoneState.None, config.States);
            Assert.False(config.IsTypeShown("D"));
            Assert.Empty(config.Select(new List<AirspaceZone> { Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 1)) }, Now));
        }

        /// <summary>
        /// The first version of the stored text, which had no band of heights, is still read.
        /// </summary>
        [Fact]
        public void Parse_ReadsTheFirstFormat()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Parse("v1;3;1;D-,TRA+");

            Assert.Equal(AirspaceZoneState.Active | AirspaceZoneState.Planned, config.States);
            Assert.True(config.ShowNewTypes);
            Assert.False(config.IsTypeShown("D"));
            Assert.True(config.IsTypeShown("TRA"));
            Assert.Null(config.AltitudeRange);
        }

        /// <summary>
        /// The band of heights is stored and read back; a damaged or reversed band is dropped.
        /// </summary>
        [Fact]
        public void AltitudeRange_RoundTripsAndRejectsBadText()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithAltitudeRange(new AirspaceAltitudeRange(1000, 6500));

            AirspaceLayerConfig back = AirspaceLayerConfig.Parse(config.Serialize());

            Assert.EndsWith(";1000-6500", config.Serialize());
            Assert.Equal(new AirspaceAltitudeRange(1000, 6500), back.AltitudeRange);
            Assert.Null(AirspaceLayerConfig.Parse("v2;1;0;D+;5000-1000").AltitudeRange);
            Assert.Null(AirspaceLayerConfig.Parse("v2;1;0;D+;abc").AltitudeRange);
            Assert.Null(AirspaceLayerConfig.Parse("v2;1;0;D+;1-2-3").AltitudeRange);
            Assert.Null(AirspaceLayerConfig.Parse("v2;1;0;D+;-5-10").AltitudeRange);
            Assert.True(AirspaceLayerConfig.Parse("v2;1;0;D+;abc").IsTypeShown("D"));
        }

        /// <summary>
        /// Setting the band fixes negative heights and edges the wrong way round, and null removes the band. Other changes keep the band.
        /// </summary>
        [Fact]
        public void WithAltitudeRange_FixesEdgesAndIsKeptByOtherChanges()
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithAltitudeRange(new AirspaceAltitudeRange(8000, -500));

            Assert.Equal(new AirspaceAltitudeRange(0, 8000), config.AltitudeRange);
            Assert.Equal(new AirspaceAltitudeRange(0, 8000), config.WithType("TRA", true).WithStates(AirspaceZoneState.All).WithShowNewTypes(true).AltitudeRange);
            Assert.Null(config.WithAltitudeRange(null).AltitudeRange);
            Assert.Null(AirspaceLayerConfig.Default.AltitudeRange);
        }

        /// <summary>
        /// An area passes the band when the space it reserves overlaps the band, edges included; an area without a reservation always passes.
        /// </summary>
        [Theory]
        [InlineData(0, 2000, 0, 500, true)]
        [InlineData(0, 2000, 2001, 5000, false)]
        [InlineData(0, 2000, 1000, 5000, true)]
        [InlineData(0, 2000, 6000, 9000, false)]
        [InlineData(3000, 9000, 0, 3000, true)]
        [InlineData(3000, 9000, 0, 2999, false)]
        [InlineData(0, 24500, 8000, 8000, true)]
        public void PassesAltitude_ComparesTheBands(int bandMin, int bandMax, int lowerFeet, int upperFeet, bool expected)
        {
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithAltitudeRange(new AirspaceAltitudeRange(bandMin, bandMax));
            AirspaceReservation reservation = new AirspaceReservation(Now, Now.AddHours(1), new AirspaceLimit(lowerFeet, AirspaceLimitReference.Altitude), new AirspaceLimit(upperFeet, AirspaceLimitReference.FlightLevel), AirspaceReservationStatus.Activated, string.Empty, null);

            Assert.Equal(expected, config.PassesAltitude(reservation));
            Assert.True(config.PassesAltitude(null));
            Assert.True(AirspaceLayerConfig.Default.PassesAltitude(reservation));
        }

        /// <summary>
        /// Selecting drops the areas that do not reach into the band.
        /// </summary>
        [Fact]
        public void Select_AppliesTheBandOfHeights()
        {
            AirspaceReservation low = new AirspaceReservation(Now.AddHours(-1), Now.AddHours(1), new AirspaceLimit(0, AirspaceLimitReference.Ground), new AirspaceLimit(2000, AirspaceLimitReference.Altitude), AirspaceReservationStatus.Activated, string.Empty, null);
            AirspaceReservation high = new AirspaceReservation(Now.AddHours(-1), Now.AddHours(1), new AirspaceLimit(15000, AirspaceLimitReference.FlightLevel), new AirspaceLimit(24500, AirspaceLimitReference.FlightLevel), AirspaceReservationStatus.Activated, string.Empty, null);
            AirspaceZone lowZone = Zone("D", low);
            AirspaceZone highZone = Zone("R", high);
            List<AirspaceZone> zones = new List<AirspaceZone> { lowZone, highZone };

            IReadOnlyList<AirspaceZoneView> all = AirspaceLayerConfig.Default.Select(zones, Now);
            IReadOnlyList<AirspaceZoneView> near = AirspaceLayerConfig.Default.WithAltitudeRange(new AirspaceAltitudeRange(0, 5000)).Select(zones, Now);

            Assert.Equal(2, all.Count);
            Assert.Same(lowZone, Assert.Single(near).Zone);
        }

        /// <summary>
        /// Heights are converted between feet and the unit chosen by the user.
        /// </summary>
        [Fact]
        public void FeetAndUnitConversions_RoundTrip()
        {
            Assert.Equal(1000.0, AirspaceText.FeetToUnit(1000, AltitudeUnit.Feet), 6);
            Assert.Equal(304.8, AirspaceText.FeetToUnit(1000, AltitudeUnit.Metric), 6);
            Assert.Equal(10.0, AirspaceText.FeetToUnit(1000, AltitudeUnit.FlightLevel), 6);
            Assert.Equal(1000, AirspaceText.UnitToFeet(304.8, AltitudeUnit.Metric));
            Assert.Equal(9500, AirspaceText.UnitToFeet(95, AltitudeUnit.FlightLevel));
            Assert.Equal(2500, AirspaceText.UnitToFeet(2500, AltitudeUnit.Feet));
            Assert.Equal(0, AirspaceText.UnitToFeet(-5, AltitudeUnit.Feet));
        }

        /// <summary>
        /// The counting lists known kinds first in their order, then the others by code, with the number of active areas.
        /// </summary>
        [Fact]
        public void Count_OrdersKindsAndCountsActive()
        {
            List<AirspaceZone> zones = new List<AirspaceZone>
            {
                Zone("ZZZ"),
                Zone("TRA", Reservation(AirspaceReservationStatus.Activated, -1, 1)),
                Zone("AAA"),
                Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 1)),
                Zone("d", Reservation(AirspaceReservationStatus.Planned, 1, 2)),
                Zone("R"),
            };

            IReadOnlyList<AirspaceTypeCount> counts = AirspaceTypes.Count(zones, Now);

            Assert.Equal(new List<string> { "R", "D", "TRA", "AAA", "ZZZ" }, counts.Select((AirspaceTypeCount item) => item.Type).ToList());
            AirspaceTypeCount d = counts[1];
            Assert.Equal(2, d.Total);
            Assert.Equal(1, d.Active);
            Assert.Empty(AirspaceTypes.Count(new List<AirspaceZone>(), Now));
        }

        /// <summary>
        /// Known kinds have a name with the code, an unknown one shows its code, and a missing one a question mark.
        /// </summary>
        [Fact]
        public void GetName_NamesKnownKindsAndFallsBackToCode()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Strefa niebezpieczna (D)", AirspaceTypes.GetName("d"));
            Assert.Equal("Strefa ograniczona (R)", AirspaceTypes.GetName("R"));
            Assert.Equal("Strefa zakazana (P)", AirspaceTypes.GetName("P"));
            Assert.Equal("Strefa czasowo wydzielona (TSA)", AirspaceTypes.GetName("TSA"));
            Assert.Equal("Strefa czasowo zarezerwowana (TRA)", AirspaceTypes.GetName("TRA"));
            Assert.Equal("Rezerwacja doraźna (ADHOC)", AirspaceTypes.GetName("ADHOC"));
            Assert.Equal("Strefa ruchu lotniskowego (ATZ)", AirspaceTypes.GetName("ATZ"));
            Assert.Equal("MRT", AirspaceTypes.GetName("MRT"));
            Assert.Equal("?", AirspaceTypes.GetName(" "));
        }

        /// <summary>
        /// Every default kind and every known kind has a place in the order, and an unknown kind comes after them.
        /// </summary>
        [Fact]
        public void GetOrder_PutsUnknownKindsLast()
        {
            foreach (string type in AirspaceTypes.DefaultShown)
            {
                Assert.True(AirspaceTypes.GetOrder(type) < AirspaceTypes.GetOrder("UNKNOWN"));
            }

            Assert.True(AirspaceTypes.GetOrder("p") < AirspaceTypes.GetOrder("R"));
        }

        /// <summary>
        /// Heights are written in the unit chosen by the user, and an altitude is never written as a flight level.
        /// </summary>
        [Fact]
        public void FormatLimit_FollowsTheUnit()
        {
            AirspaceLimit ground = new AirspaceLimit(0, AirspaceLimitReference.Ground);
            AirspaceLimit level = new AirspaceLimit(9500, AirspaceLimitReference.FlightLevel);
            AirspaceLimit altitude = new AirspaceLimit(3500, AirspaceLimitReference.Altitude);

            Assert.Equal("GND", AirspaceText.FormatLimit(ground, AltitudeUnit.Metric));
            Assert.Equal("FL095", AirspaceText.FormatLimit(level, AltitudeUnit.FlightLevel));
            Assert.Equal("FL095", AirspaceText.FormatLimit(level, AltitudeUnit.FlightLevel));
            Assert.Equal(AltitudeFormatter.Format(9500, AltitudeUnit.Metric) + " (FL095)", AirspaceText.FormatLimit(level, AltitudeUnit.Metric));
            Assert.Equal(AltitudeFormatter.Format(9500, AltitudeUnit.Feet) + " (FL095)", AirspaceText.FormatLimit(level, AltitudeUnit.Feet));
            Assert.Equal(AltitudeFormatter.Format(3500, AltitudeUnit.Metric) + " AMSL", AirspaceText.FormatLimit(altitude, AltitudeUnit.Metric));
            Assert.Equal(AltitudeFormatter.Format(3500, AltitudeUnit.Feet) + " AMSL", AirspaceText.FormatLimit(altitude, AltitudeUnit.FlightLevel));
        }

        /// <summary>
        /// The period is shown in the given time zone.
        /// </summary>
        [Fact]
        public void FormatPeriod_UsesTheTimeZone()
        {
            AirspaceReservation reservation = new AirspaceReservation(
                new DateTime(2026, 11, 3, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 3, 21, 30, 0, DateTimeKind.Utc),
                new AirspaceLimit(0, AirspaceLimitReference.Ground),
                new AirspaceLimit(900, AirspaceLimitReference.Altitude),
                AirspaceReservationStatus.Activated,
                string.Empty,
                null);
            TimeZoneInfo plusOne = TimeZoneInfo.CreateCustomTimeZone("test+1", TimeSpan.FromHours(1), "test", "test");

            Assert.Equal("03.11 07:00 – 03.11 21:30", AirspaceText.FormatPeriod(reservation, TimeZoneInfo.Utc));
            Assert.Equal("03.11 08:00 – 03.11 22:30", AirspaceText.FormatPeriod(reservation, plusOne));
        }

        /// <summary>
        /// The card lists the state, the heights and the period, and leaves out the unit "ZZZZ" and missing remarks.
        /// </summary>
        [Fact]
        public void GetLines_BuildsTheCard()
        {
            Loc.Language = AppLanguage.Polish;
            AirspaceZone zone = Zone("D", Reservation(AirspaceReservationStatus.Activated, -1, 1, "NOT.D7851/26", "EPWA"));
            AirspaceZoneView view = new AirspaceZoneView(zone, AirspaceZoneState.Active, zone.Reservations[0]);

            IReadOnlyList<string> lines = AirspaceText.GetLines(view, DisplayUnits.Default, TimeZoneInfo.Utc);

            Assert.Equal("Stan: Aktywna", lines[0]);
            Assert.Equal("Wysokość: GND – " + AltitudeFormatter.Format(24500, AltitudeUnit.Metric) + " (FL245)", lines[1]);
            Assert.Equal("Czas: 03.11 11:00 – 03.11 13:00", lines[2]);
            Assert.Equal("Jednostka: EPWA", lines[3]);
            Assert.Equal("Uwagi: NOT.D7851/26", lines[4]);
            Assert.Equal(5, lines.Count);

            AirspaceZone bare = Zone("D", Reservation(AirspaceReservationStatus.Planned, 1, 2, null, "ZZZZ"));
            IReadOnlyList<string> bareLines = AirspaceText.GetLines(new AirspaceZoneView(bare, AirspaceZoneState.Planned, bare.Reservations[0]), DisplayUnits.Default, TimeZoneInfo.Utc);

            Assert.Equal(3, bareLines.Count);
        }

        /// <summary>
        /// An area without reservations gives only its state.
        /// </summary>
        [Fact]
        public void GetLines_WithoutReservationGivesOnlyState()
        {
            Loc.Language = AppLanguage.Polish;
            AirspaceZone zone = Zone("D");

            IReadOnlyList<string> lines = AirspaceText.GetLines(new AirspaceZoneView(zone, AirspaceZoneState.Inactive, null), DisplayUnits.Default, TimeZoneInfo.Utc);

            Assert.Equal(new List<string> { "Stan: Nieaktywna" }, lines.ToList());
        }

        /// <summary>
        /// The title, the states, the attribution and the warning have texts in both languages. The language is global state shared by the tests that run in parallel, so the English texts are read from the table directly.
        /// </summary>
        [Fact]
        public void Texts_AreTranslated()
        {
            Loc.Language = AppLanguage.Polish;
            AirspaceZone zone = Zone("D");

            Assert.Equal("EPD1 · Strefa niebezpieczna (D)", AirspaceText.GetTitle(zone));
            Assert.Equal("Aktywna", AirspaceText.GetStateName(AirspaceZoneState.Active));
            Assert.Equal("Planowana", AirspaceText.GetStateName(AirspaceZoneState.Planned));
            Assert.Equal("Nieaktywna", AirspaceText.GetStateName(AirspaceZoneState.Inactive));
            Assert.Equal("Dane: PAŻP", AirspaceText.Attribution);

            Assert.Equal("Danger area", Loc.Translate("Strefa niebezpieczna", AppLanguage.English));
            Assert.Equal("Active", Loc.Translate("Aktywna", AppLanguage.English));
            Assert.Equal("Data: PAŻP", Loc.Translate("Dane: PAŻP", AppLanguage.English));
            Assert.Contains("unofficial", Loc.Translate("Strefy pochodzą z mapy PAŻP, są nieoficjalne i mogą być spóźnione lub niepełne. Nie służą do planowania ani wykonywania lotów.", AppLanguage.English));
        }
    }
}

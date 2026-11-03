using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the notifications about airspace areas that become active near the user: the options, the engine that decides what to report, the distance to an area and the texts.
    /// </summary>
    public class AirspaceAlertTests
    {
        /// <summary>
        /// The moment used by the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Position of the user used by the tests.
        /// </summary>
        private static readonly GeoPoint User = new GeoPoint(50.0, 20.0);

        /// <summary>
        /// Options with the notifications on, 100 km and 30 minutes.
        /// </summary>
        private static readonly AirspaceAlertOptions On = new AirspaceAlertOptions(true, 100.0, 30);

        /// <summary>
        /// Creates a reservation.
        /// </summary>
        /// <param name="status">State of the reservation.</param>
        /// <param name="startMinutes">Start, in minutes from now.</param>
        /// <param name="endMinutes">End, in minutes from now.</param>
        /// <param name="lowerFeet">Lower limit in feet.</param>
        /// <param name="upperFeet">Upper limit in feet.</param>
        /// <returns>The reservation.</returns>
        private static AirspaceReservation Reservation(AirspaceReservationStatus status, double startMinutes, double endMinutes, int lowerFeet = 0, int upperFeet = 5000)
        {
            return new AirspaceReservation(Now.AddMinutes(startMinutes), Now.AddMinutes(endMinutes), new AirspaceLimit(lowerFeet, AirspaceLimitReference.Altitude), new AirspaceLimit(upperFeet, AirspaceLimitReference.Altitude), status, "EPWA", null);
        }

        /// <summary>
        /// Creates a square area around a point.
        /// </summary>
        /// <param name="designator">Designation of the area.</param>
        /// <param name="type">Kind of the area.</param>
        /// <param name="latitude">Latitude of the middle.</param>
        /// <param name="longitude">Longitude of the middle.</param>
        /// <param name="half">Half of the side in degrees.</param>
        /// <param name="reservations">Reservations of the area.</param>
        /// <returns>The area.</returns>
        private static AirspaceZone Zone(string designator, string type, double latitude, double longitude, double half, params AirspaceReservation[] reservations)
        {
            List<AirspaceVertex> outline = new List<AirspaceVertex>
            {
                new AirspaceVertex(latitude - half, longitude - half),
                new AirspaceVertex(latitude - half, longitude + half),
                new AirspaceVertex(latitude + half, longitude + half),
                new AirspaceVertex(latitude + half, longitude - half),
            };

            return new AirspaceZone(designator, type, new List<IReadOnlyList<AirspaceVertex>> { outline }, new AirspaceBounds(latitude - half, latitude + half, longitude - half, longitude + half), reservations);
        }

        /// <summary>
        /// The options are written and read back; a radius and a lead time out of range are moved to the nearest allowed value; damaged text gives the defaults.
        /// </summary>
        [Fact]
        public void Options_RoundTripAndClamp()
        {
            AirspaceAlertOptions options = new AirspaceAlertOptions(true, 37.5, 45);

            Assert.Equal("v1;1;37.5;45", options.Serialize());
            Assert.Equal(options, AirspaceAlertOptions.Parse(options.Serialize()));
            Assert.Equal(new AirspaceAlertOptions(true, 1000.0, 720), AirspaceAlertOptions.Parse("v1;1;99999;5000"));
            Assert.Equal(new AirspaceAlertOptions(false, 1.0, 0), AirspaceAlertOptions.Parse("v1;0;-5;0"));
            Assert.False(AirspaceAlertOptions.Default.Enabled);
            Assert.Equal(100.0, AirspaceAlertOptions.Default.RadiusKilometers);
            Assert.Equal(30, AirspaceAlertOptions.Default.LeadMinutes);
        }

        /// <summary>
        /// Missing or damaged text gives the default options.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("v2;1;10;10")]
        [InlineData("v1;2;10;10")]
        [InlineData("v1;1;abc;10")]
        [InlineData("v1;1;NaN;10")]
        [InlineData("v1;1;10;-1")]
        [InlineData("v1;1;10")]
        public void Options_ParseReturnsDefaultForBadText(string? text)
        {
            Assert.Equal(AirspaceAlertOptions.Default, AirspaceAlertOptions.Parse(text));
        }

        /// <summary>
        /// An area that is in force near the user is reported once, with its distance.
        /// </summary>
        [Fact]
        public void Evaluate_ReportsActiveAreaOnce()
        {
            AirspaceZone zone = Zone("EPD1", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);

            IReadOnlyList<AirspaceAlert> first = engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, On, Now, false);
            IReadOnlyList<AirspaceAlert> second = engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, On, Now.AddMinutes(1), false);

            AirspaceAlert alert = Assert.Single(first);
            Assert.True(alert.InForce);
            Assert.Equal(0.0, alert.DistanceKilometers);
            Assert.Empty(second);
        }

        /// <summary>
        /// A reservation that starts within the lead time is reported before it starts, and not again when it starts.
        /// </summary>
        [Fact]
        public void Evaluate_ReportsUpcomingAreaWithinLeadTimeOnly()
        {
            AirspaceZone soon = Zone("EPD1", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, 20, 120));
            AirspaceZone later = Zone("EPD2", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, 45, 120));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);

            IReadOnlyList<AirspaceAlert> early = engine.Evaluate(new List<AirspaceZone> { soon, later }, AirspaceLayerConfig.Default, User, On, Now, false);
            IReadOnlyList<AirspaceAlert> afterStart = engine.Evaluate(new List<AirspaceZone> { soon, later }, AirspaceLayerConfig.Default, User, On, Now.AddMinutes(25), false);

            AirspaceAlert alert = Assert.Single(early);
            Assert.False(alert.InForce);
            Assert.Equal("EPD1", alert.Zone.Designator);
            Assert.Equal("EPD2", Assert.Single(afterStart).Zone.Designator);
        }

        /// <summary>
        /// A lead time of zero reports an area only when it is in force.
        /// </summary>
        [Fact]
        public void Evaluate_ZeroLeadReportsOnlyInForce()
        {
            AirspaceZone zone = Zone("EPD1", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, 5, 60));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);
            AirspaceAlertOptions noLead = new AirspaceAlertOptions(true, 100.0, 0);

            Assert.Empty(engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, noLead, Now, false));
            Assert.Single(engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, noLead, Now.AddMinutes(5), false));
        }

        /// <summary>
        /// Only reservations confirmed as active are reported; planned ones, ones that are over and ones of hidden kinds are not.
        /// </summary>
        [Fact]
        public void Evaluate_IgnoresUnconfirmedOverAndHiddenKinds()
        {
            List<AirspaceZone> zones = new List<AirspaceZone>
            {
                Zone("PLANNED", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Planned, -10, 60)),
                Zone("OTHER", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Other, -10, 60)),
                Zone("OVER", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -60, -10)),
                Zone("HIDDEN", "TRA", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60)),
                Zone("OK", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60)),
            };

            IReadOnlyList<AirspaceAlert> alerts = new AirspaceAlertEngine(null).Evaluate(zones, AirspaceLayerConfig.Default, User, On, Now, false);

            Assert.Equal("OK", Assert.Single(alerts).Zone.Designator);
            Assert.Single(new AirspaceAlertEngine(null).Evaluate(zones, AirspaceLayerConfig.Default.WithType("TRA", true), User, On, Now, false), (AirspaceAlert alert) => alert.Zone.Designator == "HIDDEN");
        }

        /// <summary>
        /// The radius decides: a distant area is not reported and is not remembered, so it is reported when the user comes near.
        /// </summary>
        [Fact]
        public void Evaluate_UsesTheRadiusAndReportsLaterWhenUserComesNear()
        {
            AirspaceZone zone = Zone("EPD1", "D", 51.0, 20.0, 0.1, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);
            AirspaceAlertOptions near = new AirspaceAlertOptions(true, 40.0, 30);

            Assert.Empty(engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, near, Now, false));

            IReadOnlyList<AirspaceAlert> closer = engine.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, new GeoPoint(50.6, 20.0), near, Now.AddMinutes(10), false);

            AirspaceAlert alert = Assert.Single(closer);
            Assert.InRange(alert.DistanceKilometers, 25.0, 40.0);
        }

        /// <summary>
        /// The band of heights set for the layer also applies to the notifications.
        /// </summary>
        [Fact]
        public void Evaluate_AppliesTheBandOfHeights()
        {
            AirspaceZone high = Zone("HIGH", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60, 15000, 24500));
            AirspaceZone low = Zone("LOW", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60, 0, 3000));
            AirspaceLayerConfig config = AirspaceLayerConfig.Default.WithAltitudeRange(new AirspaceAltitudeRange(0, 5000));

            IReadOnlyList<AirspaceAlert> alerts = new AirspaceAlertEngine(null).Evaluate(new List<AirspaceZone> { high, low }, config, User, On, Now, false);

            Assert.Equal("LOW", Assert.Single(alerts).Zone.Designator);
        }

        /// <summary>
        /// Nothing is reported when the notifications are off or the position is not known, and nothing is remembered then.
        /// </summary>
        [Fact]
        public void Evaluate_NeedsOptionsOnAndPosition()
        {
            AirspaceZone zone = Zone("EPD1", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);
            List<AirspaceZone> zones = new List<AirspaceZone> { zone };

            Assert.Empty(engine.Evaluate(zones, AirspaceLayerConfig.Default, User, AirspaceAlertOptions.Default, Now, false));
            Assert.Empty(engine.Evaluate(zones, AirspaceLayerConfig.Default, null, On, Now, false));
            Assert.Empty(engine.Reported);
            Assert.Single(engine.Evaluate(zones, AirspaceLayerConfig.Default, User, On, Now, false));
        }

        /// <summary>
        /// At the first check the areas already in force are remembered without a notification, while the ones that start later are reported.
        /// </summary>
        [Fact]
        public void Evaluate_SilentInForceRemembersWithoutReporting()
        {
            AirspaceZone active = Zone("ACTIVE", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -120, 60));
            AirspaceZone upcoming = Zone("SOON", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, 10, 60));
            AirspaceAlertEngine engine = new AirspaceAlertEngine(null);
            List<AirspaceZone> zones = new List<AirspaceZone> { active, upcoming };

            IReadOnlyList<AirspaceAlert> first = engine.Evaluate(zones, AirspaceLayerConfig.Default, User, On, Now, true);
            IReadOnlyList<AirspaceAlert> second = engine.Evaluate(zones, AirspaceLayerConfig.Default, User, On, Now.AddMinutes(1), false);

            Assert.Equal("SOON", Assert.Single(first).Zone.Designator);
            Assert.Empty(second);
            Assert.Equal(2, engine.Reported.Count);
        }

        /// <summary>
        /// The memory can be restored, so that a restart does not repeat a notification; entries that ended more than a day ago are forgotten.
        /// </summary>
        [Fact]
        public void Evaluate_RestoresAndPrunesMemory()
        {
            AirspaceZone zone = Zone("EPD1", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceAlertEngine first = new AirspaceAlertEngine(null);
            first.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, On, Now, false);
            Dictionary<string, DateTime> saved = new Dictionary<string, DateTime>(first.Reported);
            saved["OLD|1"] = Now.AddDays(-3);

            AirspaceAlertEngine restored = new AirspaceAlertEngine(saved);
            IReadOnlyList<AirspaceAlert> again = restored.Evaluate(new List<AirspaceZone> { zone }, AirspaceLayerConfig.Default, User, On, Now.AddMinutes(1), false);

            Assert.Empty(again);
            Assert.Single(restored.Reported);
            Assert.False(restored.Reported.ContainsKey("OLD|1"));
        }

        /// <summary>
        /// Several reservations of one area give an alert each, and the alerts come nearest first.
        /// </summary>
        [Fact]
        public void Evaluate_SortsByDistance()
        {
            AirspaceZone far = Zone("FAR", "D", 50.6, 20.0, 0.05, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceZone inside = Zone("IN", "D", 50.0, 20.0, 0.5, Reservation(AirspaceReservationStatus.Activated, -10, 60));
            AirspaceZone middle = Zone("MID", "D", 50.3, 20.0, 0.05, Reservation(AirspaceReservationStatus.Activated, -10, 60));

            IReadOnlyList<AirspaceAlert> alerts = new AirspaceAlertEngine(null).Evaluate(new List<AirspaceZone> { far, inside, middle }, AirspaceLayerConfig.Default, User, On, Now, false);

            Assert.Equal(new List<string> { "IN", "MID", "FAR" }, alerts.Select((AirspaceAlert alert) => alert.Zone.Designator).ToList());
        }

        /// <summary>
        /// The distance to an area is zero inside and the distance to the nearest edge outside, also for a corner.
        /// </summary>
        [Fact]
        public void DistanceKilometers_MeasuresToTheNearestEdge()
        {
            AirspaceZone zone = Zone("A", "D", 50.0, 20.0, 0.5);

            Assert.Equal(0.0, AirspaceGeometry.DistanceKilometers(zone, 50.0, 20.0));
            Assert.InRange(AirspaceGeometry.DistanceKilometers(zone, 51.0, 20.0), 55.0, 56.5);
            Assert.InRange(AirspaceGeometry.DistanceKilometers(zone, 50.0, 21.5), 70.0, 73.0);
            Assert.InRange(AirspaceGeometry.DistanceKilometers(zone, 51.0, 21.0), 64.0, 68.0);
        }

        /// <summary>
        /// The distance to a degenerate edge (two equal corners) is still the distance to that point.
        /// </summary>
        [Fact]
        public void DistanceKilometers_HandlesRepeatedCorners()
        {
            List<AirspaceVertex> outline = new List<AirspaceVertex> { new AirspaceVertex(50, 20), new AirspaceVertex(50, 20), new AirspaceVertex(50, 20) };
            AirspaceZone zone = new AirspaceZone("P", "D", new List<IReadOnlyList<AirspaceVertex>> { outline }, new AirspaceBounds(50, 50, 20, 20), new List<AirspaceReservation>());

            Assert.InRange(AirspaceGeometry.DistanceKilometers(zone, 51.0, 20.0), 110.0, 112.0);
        }

        /// <summary>
        /// The feed downloads only when the pause after the last download is over, and a failure or a rate limit lengthens the pause.
        /// </summary>
        [Fact]
        public async Task RefreshIfDueAsync_RespectsThePause()
        {
            int calls = 0;
            DateTime clock = Now;
            Func<IReadOnlyList<AirspaceZone>> answer = () => new List<AirspaceZone>();
            AirspaceFeed feed = new AirspaceFeed(new CountingSource(() => { calls++; return answer(); }), () => clock, (TimeSpan time, CancellationToken token) => Task.CompletedTask, () => TimeSpan.FromMinutes(5), null);

            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            Assert.False(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(4);
            Assert.False(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(5);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            Assert.Equal(2, calls);

            answer = () => throw new HttpRequestException("down");
            clock = Now.AddMinutes(10);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(10).AddSeconds(29);
            Assert.False(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(10).AddSeconds(30);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
        }

        /// <summary>
        /// Source that counts and answers with a function.
        /// </summary>
        private sealed class CountingSource : IAirspaceSource
        {
            /// <summary>
            /// Gives the answer or throws.
            /// </summary>
            private readonly Func<IReadOnlyList<AirspaceZone>> answer;

            /// <summary>
            /// Creates a source.
            /// </summary>
            /// <param name="answer">Gives the answer or throws.</param>
            public CountingSource(Func<IReadOnlyList<AirspaceZone>> answer)
            {
                this.answer = answer;
            }

            /// <inheritdoc />
            public string Name => "counting";

            /// <inheritdoc />
            public Task<IReadOnlyList<AirspaceZone>> GetZonesAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(answer());
            }
        }

        /// <summary>
        /// The title tells whether the area is active or when it will be, and the text lists the kind, the distance, the heights and the period.
        /// </summary>
        [Fact]
        public void AlertTexts_DescribeTheArea()
        {
            Loc.Language = AppLanguage.Polish;
            AirspaceZone zone = Zone("EPD21", "D", 50.0, 20.0, 0.5);
            AirspaceReservation started = Reservation(AirspaceReservationStatus.Activated, -10, 60);
            AirspaceReservation upcoming = Reservation(AirspaceReservationStatus.Activated, 24.2, 120);

            AirspaceAlert inForce = new AirspaceAlert(zone, started, 0.0, true);
            AirspaceAlert soon = new AirspaceAlert(zone, upcoming, 35.4, false);

            Assert.Equal("Strefa EPD21 jest aktywna", AirspaceText.GetAlertTitle(inForce, Now));
            Assert.Equal("Strefa EPD21 zostanie aktywowana za 25 min", AirspaceText.GetAlertTitle(soon, Now));
            Assert.StartsWith("Strefa niebezpieczna (D) · jesteś w strefie · ", AirspaceText.GetAlertBody(inForce, DisplayUnits.Default, TimeZoneInfo.Utc));
            Assert.Contains("35 km od Ciebie", AirspaceText.GetAlertBody(soon, DisplayUnits.Default, TimeZoneInfo.Utc));
            Assert.EndsWith("03.11 12:24 – 03.11 14:00", AirspaceText.GetAlertBody(soon, DisplayUnits.Default, TimeZoneInfo.Utc));
        }

        /// <summary>
        /// The titles and texts have an English translation. The language is global state shared by the tests that run in parallel, so the table is asked directly.
        /// </summary>
        [Fact]
        public void AlertTexts_AreTranslated()
        {
            Assert.Equal("Area {0} is active", Loc.Translate("Strefa {0} jest aktywna", AppLanguage.English));
            Assert.Equal("Area {0} will be activated in {1} min", Loc.Translate("Strefa {0} zostanie aktywowana za {1} min", AppLanguage.English));
            Assert.Equal("{0} from you", Loc.Translate("{0} od Ciebie", AppLanguage.English));
            Assert.Equal("you are inside the area", Loc.Translate("jesteś w strefie", AppLanguage.English));
            Assert.Equal("Airspace alerts", Loc.Translate("Alerty o strefach", AppLanguage.English));
        }
    }
}

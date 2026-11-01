using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the texts of the aircraft card and the airport card.
    /// </summary>
    public class CardTextTests
    {
        /// <summary>
        /// Replaces the non-breaking spaces used as digit group separators with plain spaces.
        /// </summary>
        /// <param name="text">Formatted text.</param>
        /// <returns>Text with plain spaces.</returns>
        private static string Plain(string text)
        {
            return text.Replace(' ', ' ').Replace(' ', ' ');
        }

        /// <summary>
        /// Creates an aircraft over the point (0, 1).
        /// </summary>
        /// <param name="altitude">Altitude in feet or null.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="speed">Ground speed in knots or null.</param>
        /// <param name="track">Track in degrees or null.</param>
        /// <param name="verticalRate">Vertical rate in feet per minute or null.</param>
        /// <param name="squawk">Squawk or null.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(int? altitude = 35000, bool onGround = false, double? speed = 400.0, double? track = 90.0, int? verticalRate = null, string? squawk = null, string? callsign = "LOT123")
        {
            return new Aircraft("abc123", callsign, 0.0, 1.0, altitude, onGround, speed, track, 2.0, "B738", null, "A3", false, verticalRate, squawk);
        }

        /// <summary>
        /// Creates the selection data for an aircraft over the point (0, 1).
        /// </summary>
        /// <param name="aircraft">Aircraft or null.</param>
        /// <param name="hasUser">Whether the user position is known.</param>
        /// <param name="count">Number of selected aircraft.</param>
        /// <returns>The selection data.</returns>
        private static SelectedAircraftInfo CreateInfo(Aircraft? aircraft, bool hasUser = false, int count = 1)
        {
            return new SelectedAircraftInfo("abc123", aircraft, 0.0, 1.0, 1.0, hasUser, 0.0, 0.0, count);
        }

        /// <summary>
        /// Creates an airport.
        /// </summary>
        /// <param name="iata">IATA code or empty.</param>
        /// <param name="city">City or empty.</param>
        /// <param name="elevation">Elevation in feet or null.</param>
        /// <param name="type">Airport type.</param>
        /// <param name="military">Whether the airport is military.</param>
        /// <returns>The airport.</returns>
        private static Airport CreateAirport(string iata = "WAW", string city = "Warsaw", int? elevation = 362, AirportType type = AirportType.Large, bool military = false)
        {
            return new Airport("EPWA", iata, type, "Warsaw Chopin Airport", city, "PL", 52.1657, 20.9671, elevation, true, military);
        }

        /// <summary>
        /// Creates a runway of the given length.
        /// </summary>
        /// <param name="low">Name of the low end.</param>
        /// <param name="high">Name of the high end.</param>
        /// <param name="lengthFeet">Length in feet.</param>
        /// <returns>The runway.</returns>
        private static Runway CreateRunway(string low, string high, int lengthFeet)
        {
            return new Runway(lengthFeet, 150, "ASP", true, low, 52.0, 20.9, high, 52.1, 21.0);
        }

        /// <summary>
        /// The descriptors list the airline, the type code and the kind.
        /// </summary>
        [Fact]
        public void BuildDescriptors_ListsAirlineTypeAndKind()
        {
            FlightRoute route = new FlightRoute("WAW", null, "JFK", null, "LOT Polish Airlines");

            Assert.Equal("LOT Polish Airlines · B738 · samolot pasażerski", AircraftCardText.BuildDescriptors(Create(), route));
            Assert.Equal("LOT · B738 · samolot pasażerski", AircraftCardText.BuildDescriptors(Create(), null));
            Assert.Equal("B738 · samolot pasażerski", AircraftCardText.BuildDescriptors(Create(callsign: null), null));
        }

        /// <summary>
        /// A known route shows both airports with their cities when available.
        /// </summary>
        [Fact]
        public void BuildRouteText_WithRoute_ShowsAirports()
        {
            FlightRoute full = new FlightRoute("WAW", "Warsaw", "JFK", "New York");
            FlightRoute noCities = new FlightRoute("WAW", null, "JFK", string.Empty);

            Assert.Equal("WAW Warsaw → JFK New York", AircraftCardText.BuildRouteText(full, RouteLookupState.Found));
            Assert.Equal("WAW → JFK", AircraftCardText.BuildRouteText(noCities, RouteLookupState.Found));
        }

        /// <summary>
        /// Without a route the text depends on the lookup state.
        /// </summary>
        [Theory]
        [InlineData(RouteLookupState.NotApplicable, "Trasa: samolot nie nadaje znaku wywoławczego")]
        [InlineData(RouteLookupState.NotFound, "Trasa: baza adsbdb nie zna tego lotu")]
        [InlineData(RouteLookupState.Failed, "Trasa: nie udało się pobrać (błąd sieci lub limit zapytań)")]
        [InlineData(RouteLookupState.Pending, "Trasa: pobieranie…")]
        [InlineData(RouteLookupState.Found, "Trasa: pobieranie…")]
        public void BuildRouteText_WithoutRoute_DependsOnState(RouteLookupState state, string expected)
        {
            Assert.Equal(expected, AircraftCardText.BuildRouteText(null, state));
        }

        /// <summary>
        /// The route action button text depends on the lookup state.
        /// </summary>
        [Theory]
        [InlineData(RouteLookupState.NotApplicable, null)]
        [InlineData(RouteLookupState.NotFound, "Sprawdź ponownie")]
        [InlineData(RouteLookupState.Failed, "Spróbuj ponownie")]
        [InlineData(RouteLookupState.Pending, "Pobierz teraz")]
        public void BuildRouteActionText_DependsOnState(RouteLookupState state, string? expected)
        {
            Assert.Equal(expected, AircraftCardText.BuildRouteActionText(state));
        }

        /// <summary>
        /// The progress needs a route with all four coordinates.
        /// </summary>
        [Fact]
        public void TryBuildProgress_WithoutCoordinates_ReturnsFalse()
        {
            Aircraft aircraft = Create();
            FlightRoute noCoordinates = new FlightRoute("WAW", null, "JFK", null);

            Assert.False(AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, null, out float _, out string _));
            Assert.False(AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, noCoordinates, out float _, out string _));
        }

        /// <summary>
        /// Halfway along the route the progress is 50% and the arrival estimate follows from the speed.
        /// </summary>
        [Fact]
        public void TryBuildProgress_Halfway_ShowsPercentDistanceAndEstimate()
        {
            Aircraft aircraft = Create(speed: 400.0);
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 0.0, 0.0, 2.0);

            bool result = AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float fraction, out string text);

            Assert.True(result);
            Assert.Equal(0.5f, fraction, 3);
            Assert.Equal("przeleciano 50%  ·  do celu 111 km  ·  ok. 9 min", Plain(text));
        }

        /// <summary>
        /// A long remaining time is shown in hours and minutes.
        /// </summary>
        [Fact]
        public void TryBuildProgress_LongRemainingTime_ShowsHours()
        {
            Aircraft aircraft = Create(speed: 400.0);
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 0.0, 0.0, 10.0);

            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string text);

            Assert.EndsWith("ok. 1 h 21 min", text);
        }

        /// <summary>
        /// No estimate is given for an aircraft on the ground, without speed, or below the minimum speed.
        /// </summary>
        [Fact]
        public void TryBuildProgress_NoEstimateWhenSlowOrOnGround()
        {
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 0.0, 0.0, 2.0);

            foreach (Aircraft aircraft in new Aircraft[] { Create(onGround: true), Create(speed: null), Create(speed: 79.0) })
            {
                AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string text);

                Assert.DoesNotContain("ok.", text);
            }
        }

        /// <summary>
        /// A route that starts and ends at the aircraft position gives zero progress instead of dividing by zero.
        /// </summary>
        [Fact]
        public void TryBuildProgress_ZeroLengthRoute_GivesZero()
        {
            Aircraft aircraft = Create();
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 1.0, 0.0, 1.0);

            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float fraction, out string _);

            Assert.Equal(0.0f, fraction);
        }

        /// <summary>
        /// The left column shows altitude in the unit, speed in two units, track with the compass point and the data age.
        /// </summary>
        [Fact]
        public void BuildLeftLines_ShowsAltitudeSpeedTrackAndAge()
        {
            List<string> lines = AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(), AltitudeUnit.Metric);

            Assert.Equal(4, lines.Count);
            Assert.Equal("Wysokość: 10 668 m (35 000 ft)", Plain(lines[0]));
            Assert.Equal("Prędkość: 400 kt (741 km/h)", Plain(lines[1]));
            Assert.Equal("Kurs: 90° E", lines[2]);
            Assert.Equal("Dane sprzed 3 s", lines[3]);
        }

        /// <summary>
        /// The altitude line follows the chosen unit and handles the ground and missing data.
        /// </summary>
        [Fact]
        public void BuildLeftLines_AltitudeDependsOnUnitAndState()
        {
            Assert.Equal("Wysokość: FL350 (10 668 m)", Plain(AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(), AltitudeUnit.FlightLevel)[0]));
            Assert.Equal("Wysokość: na ziemi", AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(onGround: true), AltitudeUnit.Metric)[0]);
            Assert.Equal("Wysokość: brak danych", AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(altitude: null), AltitudeUnit.Metric)[0]);
        }

        /// <summary>
        /// Missing speed and track are reported.
        /// </summary>
        [Fact]
        public void BuildLeftLines_MissingSpeedAndTrack()
        {
            List<string> lines = AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(speed: null, track: null), AltitudeUnit.Metric);

            Assert.Equal("Prędkość: brak danych", lines[1]);
            Assert.Equal("Kurs: brak danych", lines[2]);
        }

        /// <summary>
        /// The right column shows the vertical rate in the right words.
        /// </summary>
        [Theory]
        [InlineData(1500, "Wznoszenie: 1 500 ft/min")]
        [InlineData(-800, "Opadanie: 800 ft/min")]
        [InlineData(99, "Wznoszenie: lot poziomy")]
        [InlineData(-99, "Wznoszenie: lot poziomy")]
        [InlineData(100, "Wznoszenie: 100 ft/min")]
        public void BuildRightLines_VerticalRate(int rate, string expected)
        {
            Assert.Equal(expected, Plain(AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(verticalRate: rate))[0]));
        }

        /// <summary>
        /// The vertical rate line handles the ground and missing data.
        /// </summary>
        [Fact]
        public void BuildRightLines_VerticalRateOnGroundAndMissing()
        {
            Assert.Equal("Wznoszenie: –", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(onGround: true, verticalRate: 500))[0]);
            Assert.Equal("Wznoszenie: brak danych", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create())[0]);
        }

        /// <summary>
        /// The distance from the user is shown only when the GPS position is known.
        /// </summary>
        [Fact]
        public void BuildRightLines_DistanceFromUser()
        {
            Assert.Equal("Od Ciebie: 111,2 km, E (90°)", Plain(AircraftCardText.BuildRightLines(CreateInfo(Create(), hasUser: true), Create())[1]));
            Assert.Equal("Od Ciebie: brak pozycji GPS", AircraftCardText.BuildRightLines(CreateInfo(Create(), hasUser: false), Create())[1]);
        }

        /// <summary>
        /// The squawk is shown or reported missing.
        /// </summary>
        [Fact]
        public void BuildRightLines_Squawk()
        {
            Assert.Equal("Squawk: 7700", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(squawk: "7700"))[2]);
            Assert.Equal("Squawk: brak danych", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create())[2]);
        }

        /// <summary>
        /// The title is the name of the aircraft, the address when there is no data, and the number of other selected aircraft.
        /// </summary>
        [Fact]
        public void BuildTitle_NameAddressAndCount()
        {
            Assert.Equal("LOT123", AircraftCardText.BuildTitle(CreateInfo(Create())));
            Assert.Equal("ABC123", AircraftCardText.BuildTitle(CreateInfo(null)));
            Assert.Equal("LOT123  (+2)", AircraftCardText.BuildTitle(CreateInfo(Create(), count: 3)));
            Assert.Equal("ABC123  (+1)", AircraftCardText.BuildTitle(CreateInfo(null, count: 2)));
        }

        /// <summary>
        /// The airport title and descriptor show the code, name, size class and military mark.
        /// </summary>
        [Fact]
        public void AirportTitleAndDescriptor()
        {
            Assert.Equal("WAW  ·  Warsaw Chopin Airport", AirportCardText.BuildTitle(CreateAirport()));
            Assert.Equal("EPWA  ·  Warsaw Chopin Airport", AirportCardText.BuildTitle(CreateAirport(iata: string.Empty)));
            Assert.Equal("Duże lotnisko", AirportCardText.BuildDescriptor(CreateAirport()));
            Assert.Equal("Średnie lotnisko", AirportCardText.BuildDescriptor(CreateAirport(type: AirportType.Medium)));
            Assert.Equal("Małe lotnisko · wojskowe", AirportCardText.BuildDescriptor(CreateAirport(type: AirportType.Small, military: true)));
            Assert.Equal("Helipad", AirportCardText.BuildDescriptor(CreateAirport(type: AirportType.Heliport)));
        }

        /// <summary>
        /// The detail lines show codes, place and elevation.
        /// </summary>
        [Fact]
        public void AirportLines_CodesPlaceAndElevation()
        {
            List<string> lines = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, AltitudeUnit.Metric);

            Assert.Equal(3, lines.Count);
            Assert.Equal("IATA WAW  ·  ICAO EPWA", lines[0]);
            Assert.Equal("Warsaw, PL", lines[1]);
            Assert.Equal("Wysokość: 110 m (362 ft)", Plain(lines[2]));
        }

        /// <summary>
        /// Missing IATA, city and elevation are left out.
        /// </summary>
        [Fact]
        public void AirportLines_MissingFieldsAreLeftOut()
        {
            List<string> lines = AirportCardText.BuildLines(CreateAirport(iata: string.Empty, city: string.Empty, elevation: null), Array.Empty<Runway>(), false, 0.0, 0.0, AltitudeUnit.Metric);

            Assert.Equal(new string[] { "ICAO EPWA", "PL" }, lines.ToArray());
        }

        /// <summary>
        /// The distance and direction from the user are shown only when the position is known.
        /// </summary>
        [Fact]
        public void AirportLines_DistanceFromUser()
        {
            List<string> with = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), true, 52.1657, 19.9671, AltitudeUnit.Metric);
            List<string> without = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 52.1657, 19.9671, AltitudeUnit.Metric);

            Assert.Contains(with, line => Plain(line).StartsWith("Odległość: 68 km, kierunek E", StringComparison.Ordinal));
            Assert.DoesNotContain(without, line => line.StartsWith("Odległość", StringComparison.Ordinal));
        }

        /// <summary>
        /// Runways are listed longest first, at most four, in meters unless the unit is feet.
        /// </summary>
        [Fact]
        public void AirportLines_RunwaysLongestFirstAndLimited()
        {
            List<Runway> runways = new List<Runway>
            {
                CreateRunway("01", "19", 3000),
                CreateRunway("15", "33", 11500),
                CreateRunway("05", "23", 5000),
                CreateRunway("09", "27", 7000),
                CreateRunway("12", "30", 1000),
            };

            string metric = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, AltitudeUnit.Metric).Last();
            string feet = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, AltitudeUnit.Feet).Last();
            string flightLevel = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, AltitudeUnit.FlightLevel).Last();

            Assert.Equal("Pasy: 15/33 · 3 505 m, 09/27 · 2 134 m, 05/23 · 1 524 m, 01/19 · 914 m", Plain(metric));
            Assert.Equal("Pasy: 15/33 · 11 500 ft, 09/27 · 7 000 ft, 05/23 · 5 000 ft, 01/19 · 3 000 ft", Plain(feet));
            Assert.Equal(Plain(metric), Plain(flightLevel));
        }

        /// <summary>
        /// A runway of unknown length is listed by name only, and no runway line is added without runways.
        /// </summary>
        [Fact]
        public void AirportLines_RunwayWithoutLength()
        {
            string line = AirportCardText.BuildLines(CreateAirport(elevation: null), new Runway[] { CreateRunway("09", "27", 0) }, false, 0.0, 0.0, AltitudeUnit.Metric).Last();

            Assert.Equal("Pasy: 09/27", line);
            Assert.DoesNotContain(AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, AltitudeUnit.Metric), item => item.StartsWith("Pasy", StringComparison.Ordinal));
        }
    }
}

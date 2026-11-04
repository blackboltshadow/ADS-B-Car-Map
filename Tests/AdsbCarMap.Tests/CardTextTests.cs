using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the texts of the aircraft card and the airport card.
    /// </summary>
    [Collection("TypeTable")]
    public class CardTextTests
    {
        /// <summary>
        /// Without a name from the data sources the type text uses the name from the built-in type table.
        /// </summary>
        [Fact]
        public void BuildTypeText_FallsBackToTypeTableName()
        {
            AircraftTypeTable previous = AircraftTypeTable.Current;

            try
            {
                AircraftTypeTable.Current = AircraftTypeTable.Parse(new string[] { "iata\ticao\tfamily\tname", "32Q\tA21N\tA320\tAirbus A321neo" });
                Aircraft neo = new Aircraft("abc125", "LOT2", 0.0, 1.0, 5000, false, 200.0, 90.0, 1.0, "A21N", "SP-XYZ");

                Assert.Equal("Airbus A321neo (A21N)", AircraftCardText.BuildTypeText(neo));
            }
            finally
            {
                AircraftTypeTable.Current = previous;
            }
        }

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
        [InlineData(RouteLookupState.NotFound, "Trasa: żadne źródło nie zna tego lotu")]
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

            Assert.False(AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, null, out float _, out string _, DisplayUnits.Default));
            Assert.False(AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, noCoordinates, out float _, out string _, DisplayUnits.Default));
        }

        /// <summary>
        /// Halfway along the route the progress is 50% and the arrival estimate follows from the speed.
        /// </summary>
        [Fact]
        public void TryBuildProgress_Halfway_ShowsPercentDistanceAndEstimate()
        {
            Aircraft aircraft = Create(speed: 400.0);
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 0.0, 0.0, 2.0);

            bool result = AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float fraction, out string text, DisplayUnits.Default);

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

            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string text, DisplayUnits.Default);

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
                AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string text, DisplayUnits.Default);

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

            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float fraction, out string _, DisplayUnits.Default);

            Assert.Equal(0.0f, fraction);
        }

        /// <summary>
        /// The left column shows altitude and speed in the default units, track with the compass point and the data age.
        /// </summary>
        [Fact]
        public void BuildLeftLines_ShowsAltitudeSpeedTrackAndAge()
        {
            List<string> lines = AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(), DisplayUnits.Default);

            Assert.Equal(4, lines.Count);
            Assert.Equal("Wysokość: 10 668 m", Plain(lines[0]));
            Assert.Equal("Prędkość: 741 km/h", Plain(lines[1]));
            Assert.Equal("Kurs: 90° E", lines[2]);
            Assert.Equal("Dane sprzed 3 s", lines[3]);
        }

        /// <summary>
        /// The altitude line follows the chosen unit and handles the ground and missing data.
        /// </summary>
        [Fact]
        public void BuildLeftLines_AltitudeDependsOnUnitAndState()
        {
            Assert.Equal("Wysokość: FL350", Plain(AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(), Units(AltitudeUnit.FlightLevel))[0]));
            Assert.Equal("Wysokość: na ziemi", AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(onGround: true), DisplayUnits.Default)[0]);
            Assert.Equal("Wysokość: brak danych", AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(altitude: null), DisplayUnits.Default)[0]);
        }

        /// <summary>
        /// Missing speed and track are reported.
        /// </summary>
        [Fact]
        public void BuildLeftLines_MissingSpeedAndTrack()
        {
            List<string> lines = AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(speed: null, track: null), DisplayUnits.Default);

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
            Assert.Equal(expected, Plain(AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(verticalRate: rate), DisplayUnits.Default)[0]));
        }

        /// <summary>
        /// The vertical rate line handles the ground and missing data.
        /// </summary>
        [Fact]
        public void BuildRightLines_VerticalRateOnGroundAndMissing()
        {
            Assert.Equal("Wznoszenie: –", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(onGround: true, verticalRate: 500), DisplayUnits.Default)[0]);
            Assert.Equal("Wznoszenie: brak danych", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(), DisplayUnits.Default)[0]);
        }

        /// <summary>
        /// The distance from the user is shown only when the GPS position is known.
        /// </summary>
        [Fact]
        public void BuildRightLines_DistanceFromUser()
        {
            Assert.Equal("Od Ciebie: 111,2 km, E (90°)", Plain(AircraftCardText.BuildRightLines(CreateInfo(Create(), hasUser: true), Create(), DisplayUnits.Default)[1]));
            Assert.Equal("Od Ciebie: brak pozycji GPS", AircraftCardText.BuildRightLines(CreateInfo(Create(), hasUser: false), Create(), DisplayUnits.Default)[1]);
        }

        /// <summary>
        /// The squawk is shown or reported missing.
        /// </summary>
        [Fact]
        public void BuildRightLines_Squawk()
        {
            Assert.Equal("Squawk: 7700", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(squawk: "7700"), DisplayUnits.Default)[2]);
            Assert.Equal("Squawk: brak danych", AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(), DisplayUnits.Default)[2]);
        }

        /// <summary>
        /// Creates units that differ from the defaults only in the altitude.
        /// </summary>
        /// <param name="altitude">Altitude unit.</param>
        /// <returns>The units.</returns>
        private static DisplayUnits Units(AltitudeUnit altitude)
        {
            return new DisplayUnits(altitude, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute);
        }

        /// <summary>
        /// The speed line follows the chosen speed unit.
        /// </summary>
        [Theory]
        [InlineData(SpeedUnit.KilometersPerHour, "Prędkość: 741 km/h")]
        [InlineData(SpeedUnit.Knots, "Prędkość: 400 kt")]
        [InlineData(SpeedUnit.MilesPerHour, "Prędkość: 460 mph")]
        public void BuildLeftLines_SpeedFollowsUnit(SpeedUnit speed, string expected)
        {
            DisplayUnits units = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, speed, VerticalSpeedUnit.FeetPerMinute);

            Assert.Equal(expected, AircraftCardText.BuildLeftLines(CreateInfo(Create()), Create(), units)[1]);
        }

        /// <summary>
        /// The vertical rate follows the chosen unit, and the level flight threshold does not depend on it.
        /// </summary>
        [Theory]
        [InlineData(1500, "Wznoszenie: 7,6 m/s")]
        [InlineData(-800, "Opadanie: 4,1 m/s")]
        [InlineData(99, "Wznoszenie: lot poziomy")]
        public void BuildRightLines_VerticalRateFollowsUnit(int rate, string expected)
        {
            DisplayUnits units = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.MetersPerSecond);

            Assert.Equal(expected, AircraftCardText.BuildRightLines(CreateInfo(Create()), Create(verticalRate: rate), units)[0]);
        }

        /// <summary>
        /// The distance from the user and the distance to the destination follow the chosen distance unit, while the arrival estimate stays the same.
        /// </summary>
        [Fact]
        public void Distances_FollowDistanceUnit()
        {
            DisplayUnits nautical = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.NauticalMiles, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);
            DisplayUnits miles = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Miles, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);
            Aircraft aircraft = Create(speed: 400.0);
            FlightRoute route = new FlightRoute("AAA", null, "BBB", null, null, 0.0, 0.0, 0.0, 2.0);

            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string progress, nautical);
            AircraftCardText.TryBuildProgress(CreateInfo(aircraft), aircraft, route, out float _, out string milesProgress, miles);

            Assert.Equal("przeleciano 50%  ·  do celu 60 NM  ·  ok. 9 min", Plain(progress));
            Assert.Equal("przeleciano 50%  ·  do celu 69 mi  ·  ok. 9 min", Plain(milesProgress));
            Assert.Equal("Od Ciebie: 60,0 NM, E (90°)", Plain(AircraftCardText.BuildRightLines(CreateInfo(Create(), hasUser: true), Create(), nautical)[1]));
        }

        /// <summary>
        /// The distance from the airport follows the chosen distance unit.
        /// </summary>
        [Fact]
        public void AirportLines_DistanceFollowsUnit()
        {
            DisplayUnits miles = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Miles, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute);

            List<string> lines = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), true, 52.1657, 19.9671, miles);

            Assert.Contains(lines, line => Plain(line).StartsWith("Odległość: 42 mi, kierunek E", StringComparison.Ordinal));
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
            List<string> lines = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default);

            Assert.Equal(3, lines.Count);
            Assert.Equal("IATA WAW  ·  ICAO EPWA", lines[0]);
            Assert.Equal("Warsaw, PL", lines[1]);
            Assert.Equal("Wysokość: 110 m", Plain(lines[2]));
        }

        /// <summary>
        /// Missing IATA, city and elevation are left out.
        /// </summary>
        [Fact]
        public void AirportLines_MissingFieldsAreLeftOut()
        {
            List<string> lines = AirportCardText.BuildLines(CreateAirport(iata: string.Empty, city: string.Empty, elevation: null), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default);

            Assert.Equal(new string[] { "ICAO EPWA", "PL" }, lines.ToArray());
        }

        /// <summary>
        /// The distance and direction from the user are shown only when the position is known.
        /// </summary>
        [Fact]
        public void AirportLines_DistanceFromUser()
        {
            List<string> with = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), true, 52.1657, 19.9671, DisplayUnits.Default);
            List<string> without = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 52.1657, 19.9671, DisplayUnits.Default);

            Assert.Contains(with, line => Plain(line).StartsWith("Odległość: 68 km, kierunek E", StringComparison.Ordinal));
            Assert.DoesNotContain(without, line => line.StartsWith("Odległość", StringComparison.Ordinal));
        }

        /// <summary>
        /// Runways are listed longest first, at most four, in meters for the metric unit and in feet otherwise.
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

            string metric = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, DisplayUnits.Default).Last();
            string feet = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, Units(AltitudeUnit.Feet)).Last();
            string flightLevel = AirportCardText.BuildLines(CreateAirport(elevation: null), runways, false, 0.0, 0.0, Units(AltitudeUnit.FlightLevel)).Last();

            Assert.Equal("Pasy: 15/33 · 3 505 m, 09/27 · 2 134 m, 05/23 · 1 524 m, 01/19 · 914 m", Plain(metric));
            Assert.Equal("Pasy: 15/33 · 11 500 ft, 09/27 · 7 000 ft, 05/23 · 5 000 ft, 01/19 · 3 000 ft", Plain(feet));
            Assert.Equal(Plain(feet), Plain(flightLevel));
        }

        /// <summary>
        /// A runway of unknown length is listed by name only, and no runway line is added without runways.
        /// </summary>
        [Fact]
        public void AirportLines_RunwayWithoutLength()
        {
            string line = AirportCardText.BuildLines(CreateAirport(elevation: null), new Runway[] { CreateRunway("09", "27", 0) }, false, 0.0, 0.0, DisplayUnits.Default).Last();

            Assert.Equal("Pasy: 09/27", line);
            Assert.DoesNotContain(AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default), item => item.StartsWith("Pasy", StringComparison.Ordinal));
        }

        /// <summary>
        /// All frequencies are listed one per line: main types in a fixed order, other types after them, repeats once.
        /// </summary>
        [Fact]
        public void AirportLines_AllFrequenciesOnePerLine()
        {
            List<AirportFrequency> frequencies = new List<AirportFrequency>
            {
                new AirportFrequency("GND", "Ground", 121.905),
                new AirportFrequency("TWR", "Tower 2", 119.0),
                new AirportFrequency("VOLMET", "Volmet", 127.6),
                new AirportFrequency("TWR", "Tower", 118.3),
                new AirportFrequency("TWR", "Tower again", 118.3),
                new AirportFrequency("ATIS", string.Empty, 120.455),
                new AirportFrequency("XYZ", "Unknown kind", 130.0),
            };

            List<string> lines = AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default, frequencies);
            int start = lines.IndexOf("Częstotliwości (MHz):");

            Assert.True(start >= 0);
            Assert.Equal(new string[] { "ATIS 120.455 · Automatyczna informacja o lotnisku", "TWR 118.300 · wieża · Tower", "TWR 119.000 · wieża · Tower 2", "GND 121.905 · kontrola ruchu naziemnego · Ground", "VOLMET 127.600 · pogoda dla załóg w locie (VOLMET) · Volmet", "XYZ 130.000 · Unknown kind" }, lines.Skip(start + 1).ToArray());
        }

        /// <summary>
        /// No frequency lines are added without frequencies.
        /// </summary>
        [Fact]
        public void AirportLines_NoFrequencyLinesWithoutFrequencies()
        {
            Assert.DoesNotContain(AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default, null), item => item.StartsWith("Częstotliwości", StringComparison.Ordinal));
            Assert.DoesNotContain(AirportCardText.BuildLines(CreateAirport(), Array.Empty<Runway>(), false, 0.0, 0.0, DisplayUnits.Default, Array.Empty<AirportFrequency>()), item => item.StartsWith("Częstotliwości", StringComparison.Ordinal));
        }

        /// <summary>
        /// The full type name is shown with the manufacturer and the type code, without repeating either of them.
        /// </summary>
        [Fact]
        public void BuildTypeText_CombinesManufacturerNameAndCode()
        {
            Aircraft king = new Aircraft("abc123", "SPTPU", 0.0, 1.0, 5000, false, 200.0, 90.0, 1.0, "B350", "SP-TPU", "A2", false, null, null, "King Air B350", "Beechcraft Corp", "Polish Air Navigation Services Agency");
            Aircraft airbus = new Aircraft("abc124", "LOT1", 0.0, 1.0, 5000, false, 200.0, 90.0, 1.0, "A320", "SP-XYZ", "A3", false, null, null, "Airbus A320-214", "Airbus", null);
            Aircraft boeing = new Aircraft("abc125", "LOT2", 0.0, 1.0, 5000, false, 200.0, 90.0, 1.0, "B738", "SP-XYZ", "A3", false, null, null, "737-8AS", "Boeing", null);

            Assert.Equal("Beechcraft King Air B350", AircraftCardText.BuildTypeText(king));
            Assert.Equal("Airbus A320-214", AircraftCardText.BuildTypeText(airbus));
            Assert.Equal("Boeing 737-8AS (B738)", AircraftCardText.BuildTypeText(boeing));
            Assert.Equal("B738", AircraftCardText.BuildTypeText(Create()));
        }

        /// <summary>
        /// Without an airline the registered owner is shown, and the full type name replaces the bare code.
        /// </summary>
        [Fact]
        public void BuildDescriptors_UsesOwnerWhenNoAirlineAndFullTypeName()
        {
            Aircraft king = new Aircraft("abc123", null, 0.0, 1.0, 5000, false, 200.0, 90.0, 1.0, "B350", "SP-TPU", "A2", false, null, null, "King Air B350", "Beechcraft Corp", "Polish Air Navigation Services Agency");

            Assert.Equal("Polish Air Navigation Services Agency · Beechcraft King Air B350 · mały odrzutowiec", AircraftCardText.BuildDescriptors(king, null));
        }
    }
}

using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the combined search of aircraft and airports and of the row texts.
    /// </summary>
    public class AircraftSearchTests
    {
        /// <summary>
        /// Formats altitude the way the tests expect.
        /// </summary>
        /// <param name="feet">Altitude in feet.</param>
        /// <returns>Text such as "10000 ft".</returns>
        private static string FormatFeet(double feet)
        {
            return feet.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ft";
        }

        /// <summary>
        /// Creates an aircraft.
        /// </summary>
        /// <param name="hex">ICAO address.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <param name="typeCode">Type code or null.</param>
        /// <param name="registration">Registration or null.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="altitude">Altitude in feet or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(string hex, string? callsign, string? typeCode = null, string? registration = null, bool onGround = false, int? altitude = 10000)
        {
            return new Aircraft(hex, callsign, 52.0, 21.0, altitude, onGround, 300.0, 90.0, 1.0, typeCode, registration);
        }

        /// <summary>
        /// Creates a database with a few airports.
        /// </summary>
        /// <returns>The loaded database.</returns>
        private static AirportDatabase CreateAirports()
        {
            AirportDatabase database = new AirportDatabase();
            database.LoadFrom(
                new string[]
                {
                    "EPWA\tWAW\tL\tWarsaw Chopin Airport\tWarsaw\tPL\t52.1657\t20.9671\t362\t1\t0",
                    "EPKK\tKRK\tL\tKraków Jana Pawła II\tKraków\tPL\t50.0777\t19.7848\t791\t1\t0",
                    "EPLL\tLCJ\tM\tŁódź Władysław Reymont\tŁódź\tPL\t51.7219\t19.3981\t604\t1\t0",
                    "EPWH\t\tH\tWarsaw Hospital Heliport\tWarsaw\tPL\t52.2\t21.0\t300\t0\t0",
                },
                Array.Empty<string>());

            return database;
        }

        /// <summary>
        /// Runs the combined search with the route lookup and favorites given.
        /// </summary>
        /// <param name="aircraft">Aircraft to search.</param>
        /// <param name="query">Query.</param>
        /// <param name="routes">Known routes by callsign.</param>
        /// <param name="favorites">ICAO codes of the favorite airports.</param>
        /// <param name="maxResults">Maximum number of rows.</param>
        /// <param name="reserved">Rows kept for aircraft.</param>
        /// <param name="includeHeliports">Whether heliports are searched.</param>
        /// <returns>Result rows.</returns>
        private static List<SearchResult> SearchAll(IReadOnlyList<Aircraft> aircraft, string query, Dictionary<string, FlightRoute>? routes = null, string[]? favorites = null, int maxResults = 10, int reserved = 2, bool includeHeliports = false)
        {
            Dictionary<string, FlightRoute> known = routes ?? new Dictionary<string, FlightRoute>();

            return AircraftSearch.SearchAll(
                aircraft,
                CreateAirports(),
                callsign => callsign != null && known.TryGetValue(callsign, out FlightRoute? route) ? route : null,
                favorites ?? Array.Empty<string>(),
                includeHeliports,
                query,
                maxResults,
                reserved,
                52.0,
                21.0);
        }

        /// <summary>
        /// The name is the callsign, or the upper-case address when there is no callsign.
        /// </summary>
        [Fact]
        public void GetName_UsesCallsignOrHex()
        {
            Assert.Equal("LOT1", AircraftSearch.GetName(Create("abc123", "LOT1")));
            Assert.Equal("ABC123", AircraftSearch.GetName(Create("abc123", null)));
            Assert.Equal("ABC123", AircraftSearch.GetName(Create("abc123", string.Empty)));
        }

        /// <summary>
        /// The airline is the route airline name, or the three-letter prefix of the callsign.
        /// </summary>
        [Fact]
        public void GetAirline_FromRouteOrCallsignPrefix()
        {
            FlightRoute route = new FlightRoute("WAW", null, "JFK", null, "LOT Polish Airlines");

            Assert.Equal("LOT Polish Airlines", AircraftSearch.GetAirline(Create("a", "LOT123"), route));
            Assert.Equal("LOT", AircraftSearch.GetAirline(Create("a", "LOT123"), null));
            Assert.Equal(string.Empty, AircraftSearch.GetAirline(Create("a", "SP-ABC"), null));
            Assert.Equal(string.Empty, AircraftSearch.GetAirline(Create("a", "LOT"), null));
            Assert.Equal(string.Empty, AircraftSearch.GetAirline(Create("a", "lot123"), null));
            Assert.Equal(string.Empty, AircraftSearch.GetAirline(Create("a", null), null));
        }

        /// <summary>
        /// The description joins type, route and altitude, or says that the aircraft is on the ground.
        /// </summary>
        [Fact]
        public void Describe_JoinsAvailableParts()
        {
            FlightRoute route = new FlightRoute("WAW", null, "JFK", null);

            Assert.Equal("B738 · WAW→JFK · 10000 ft", AircraftSearch.Describe(Create("a", "LOT1", "B738"), route, FormatFeet));
            Assert.Equal("B738 · na ziemi", AircraftSearch.Describe(Create("a", "LOT1", "B738", null, true, null), null, FormatFeet));
            Assert.Equal("10000 ft", AircraftSearch.Describe(Create("a", "LOT1"), null, FormatFeet));
            Assert.Equal(string.Empty, AircraftSearch.Describe(Create("a", "LOT1", null, null, false, null), null, FormatFeet));
        }

        /// <summary>
        /// The airport title has a star for a favorite.
        /// </summary>
        [Fact]
        public void GetAirportTitle_AddsStarForFavorite()
        {
            Airport airport = CreateAirports().Find("EPWA")!;

            Assert.Equal("WAW · Warsaw Chopin Airport", AircraftSearch.GetAirportTitle(airport, false));
            Assert.Equal("★ WAW · Warsaw Chopin Airport", AircraftSearch.GetAirportTitle(airport, true));
        }

        /// <summary>
        /// The airport description joins the place, the size class, the military mark and the elevation.
        /// </summary>
        [Fact]
        public void DescribeAirport_JoinsParts()
        {
            AirportDatabase airports = CreateAirports();

            Assert.Equal("Warsaw, PL · duże lotnisko · 362 ft", AircraftSearch.DescribeAirport(airports.Find("EPWA")!, FormatFeet));
            Assert.Equal("Łódź, PL · średnie lotnisko · 604 ft", AircraftSearch.DescribeAirport(airports.Find("EPLL")!, FormatFeet));
            Assert.Equal("Warsaw, PL · helipad · 300 ft", AircraftSearch.DescribeAirport(airports.Find("EPWH")!, FormatFeet));

            Airport military = new Airport("EPMB", string.Empty, AirportType.Small, "Malbork", string.Empty, "PL", 54.0, 19.1, null, false, true);
            Assert.Equal("PL · małe lotnisko · wojskowe", AircraftSearch.DescribeAirport(military, FormatFeet));
        }

        /// <summary>
        /// An empty query gives no aircraft in the simple search.
        /// </summary>
        [Fact]
        public void Search_EmptyQuery_ReturnsNothing()
        {
            Assert.Empty(AircraftSearch.Search(new Aircraft[] { Create("a", "LOT1") }, callsign => null, "  ", 10));
        }

        /// <summary>
        /// The simple search requires all words and also looks into the known route.
        /// </summary>
        [Fact]
        public void Search_RequiresAllWordsAndUsesRoute()
        {
            Aircraft first = Create("a1", "LOT1", "B738", "SP-LWA");
            Aircraft second = Create("a2", "RYR2", "B738", "EI-ABC");
            FlightRoute route = new FlightRoute("WAW", "Warsaw", "JFK", "New York", "LOT Polish Airlines");
            IReadOnlyList<Aircraft> aircraft = new Aircraft[] { second, first };

            Assert.Equal(2, AircraftSearch.Search(aircraft, callsign => null, "b738", 10).Count);
            Assert.Single(AircraftSearch.Search(aircraft, callsign => null, "b738 sp-lwa", 10));
            Assert.Empty(AircraftSearch.Search(aircraft, callsign => null, "b738 jfk", 10));
            Assert.Single(AircraftSearch.Search(aircraft, callsign => callsign == "LOT1" ? route : null, "b738 jfk", 10));
            Assert.Equal("LOT1", AircraftSearch.Search(aircraft, callsign => null, "b738", 10)[0].Callsign);
            Assert.Single(AircraftSearch.Search(aircraft, callsign => null, "b738", 1));
        }

        /// <summary>
        /// Listing everything sorts by name and limits the number of rows.
        /// </summary>
        [Fact]
        public void ListAll_SortsByNameAndLimits()
        {
            List<Aircraft> result = AircraftSearch.ListAll(new Aircraft[] { Create("a3", "ZZZ9"), Create("a1", "aaa1"), Create("a2", null) }, 2);

            Assert.Equal(2, result.Count);
            Assert.Equal("a2", result[0].Hex);
            Assert.Equal("aaa1", result[1].Callsign);
        }

        /// <summary>
        /// An exact code gives an airport row before a weaker aircraft match.
        /// </summary>
        [Fact]
        public void SearchAll_ExactAirportCode_ComesFirst()
        {
            List<SearchResult> result = SearchAll(new Aircraft[] { Create("a1", "WAW123") }, "waw");

            Assert.NotNull(result[0].Airport);
            Assert.Equal("EPWA", result[0].Airport!.Icao);
            Assert.Contains(result, row => row.Aircraft != null);
        }

        /// <summary>
        /// A query typed without Polish letters finds the airport.
        /// </summary>
        [Fact]
        public void SearchAll_FindsAirportWithoutDiacritics()
        {
            List<SearchResult> result = SearchAll(Array.Empty<Aircraft>(), "lodz");

            Assert.Single(result);
            Assert.Equal("EPLL", result[0].Airport!.Icao);
        }

        /// <summary>
        /// Aircraft match by callsign, registration, type and route fields, ranked by match quality.
        /// </summary>
        [Fact]
        public void SearchAll_AircraftMatchQuality()
        {
            Aircraft exact = Create("a1", "LOT1", "B738", "SP-LWA");
            Aircraft prefix = Create("a2", "LOT2", "B738", "SP-LWB");
            Aircraft contains = Create("a3", "XLOT3", "B738", "SP-LWC");
            Dictionary<string, FlightRoute> routes = new Dictionary<string, FlightRoute>
            {
                { "LOT1", new FlightRoute("WAW", "Warsaw", "JFK", "New York", "LOT Polish Airlines") },
            };

            List<SearchResult> result = SearchAll(new Aircraft[] { contains, prefix, exact }, "lot1", routes, maxResults: 10, reserved: 0);

            Assert.Equal(new string[] { "a1" }, result.Where(row => row.Aircraft != null).Select(row => row.Aircraft!.Hex).ToArray());

            result = SearchAll(new Aircraft[] { contains, prefix, exact }, "lot", routes, maxResults: 10, reserved: 0);
            List<string> hexes = result.Where(row => row.Aircraft != null).Select(row => row.Aircraft!.Hex).ToList();

            Assert.Equal(new string[] { "a1", "a2", "a3" }, hexes.ToArray());
            Assert.Equal(AirportDatabase.PrefixMatch, result.First(row => row.Aircraft!.Hex == "a1").Quality);
            Assert.Equal(AirportDatabase.PrefixMatch, result.First(row => row.Aircraft!.Hex == "a2").Quality);
            Assert.Equal(AirportDatabase.ContainsMatch, result.First(row => row.Aircraft!.Hex == "a3").Quality);
        }

        /// <summary>
        /// The route cities and airline are searched too.
        /// </summary>
        [Fact]
        public void SearchAll_MatchesRouteFields()
        {
            Dictionary<string, FlightRoute> routes = new Dictionary<string, FlightRoute>
            {
                { "LOT1", new FlightRoute("WAW", "Warsaw", "JFK", "New York", "LOT Polish Airlines") },
            };

            List<SearchResult> result = SearchAll(new Aircraft[] { Create("a1", "LOT1"), Create("a2", "RYR2") }, "york", routes, reserved: 0);

            Assert.Single(result);
            Assert.Equal("a1", result[0].Aircraft!.Hex);
        }

        /// <summary>
        /// Rows reserved for aircraft replace the weakest airport rows when the list is full of airports.
        /// </summary>
        [Fact]
        public void SearchAll_ReservesRowsForAircraft()
        {
            Aircraft plane = Create("a1", "Warsaw1");

            List<SearchResult> unreserved = SearchAll(new Aircraft[] { plane }, "warsaw", maxResults: 1, reserved: 0, includeHeliports: true);
            List<SearchResult> reserved = SearchAll(new Aircraft[] { plane }, "warsaw", maxResults: 1, reserved: 1, includeHeliports: true);

            Assert.NotNull(unreserved[0].Airport);
            Assert.Single(reserved);
            Assert.NotNull(reserved[0].Aircraft);
        }

        /// <summary>
        /// Heliports appear only when enabled.
        /// </summary>
        [Fact]
        public void SearchAll_Heliports_OnlyWhenEnabled()
        {
            Assert.DoesNotContain(SearchAll(Array.Empty<Aircraft>(), "hospital", includeHeliports: false), row => row.Airport?.Icao == "EPWH");
            Assert.Contains(SearchAll(Array.Empty<Aircraft>(), "hospital", includeHeliports: true), row => row.Airport?.Icao == "EPWH");
        }

        /// <summary>
        /// A favorite airport comes before other airports matching equally.
        /// </summary>
        [Fact]
        public void SearchAll_FavoriteComesFirst()
        {
            List<SearchResult> result = SearchAll(Array.Empty<Aircraft>(), "warsaw", favorites: new string[] { "EPWH" }, includeHeliports: true);

            Assert.Equal("EPWH", result[0].Airport!.Icao);
        }

        /// <summary>
        /// An empty query lists the favorites, the nearest airports without duplicates, then the aircraft.
        /// </summary>
        [Fact]
        public void SearchAll_EmptyQuery_ListsFavoritesNearestAndAircraft()
        {
            List<SearchResult> result = SearchAll(new Aircraft[] { Create("a1", "LOT1"), Create("a2", "LOT2") }, string.Empty, favorites: new string[] { "EPKK", "NOPE" }, maxResults: 20);

            List<string?> airportCodes = result.Where(row => row.Airport != null).Select(row => row.Airport!.Icao).ToList<string?>();

            Assert.Equal("EPKK", airportCodes[0]);
            Assert.Equal(airportCodes.Distinct().Count(), airportCodes.Count);
            Assert.Contains("EPWA", airportCodes);
            Assert.DoesNotContain("NOPE", airportCodes);
            Assert.Equal(2, result.Count(row => row.Aircraft != null));
        }

        /// <summary>
        /// The favorite codes are matched ignoring case.
        /// </summary>
        [Fact]
        public void SearchAll_FavoriteCodesIgnoreCase()
        {
            List<SearchResult> result = SearchAll(Array.Empty<Aircraft>(), "warsaw", favorites: new string[] { "epwh" }, includeHeliports: true);

            Assert.Equal("EPWH", result[0].Airport!.Icao);
        }
    }
}

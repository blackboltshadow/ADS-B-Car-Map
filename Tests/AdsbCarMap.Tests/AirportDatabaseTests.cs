using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of loading, indexing and searching the airport database.
    /// </summary>
    public class AirportDatabaseTests
    {
        /// <summary>
        /// Builds a line of the airports file.
        /// </summary>
        /// <param name="icao">ICAO code.</param>
        /// <param name="iata">IATA code or empty.</param>
        /// <param name="type">L, M, S or H.</param>
        /// <param name="name">Name.</param>
        /// <param name="city">City.</param>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="scheduled">Whether the airport has scheduled service.</param>
        /// <param name="military">Whether the airport is military.</param>
        /// <returns>The tab separated line.</returns>
        private static string AirportLine(string icao, string iata, string type, string name, string city, double latitude, double longitude, bool scheduled = false, bool military = false)
        {
            return string.Join("\t", icao, iata, type, name, city, "PL", latitude.ToString(System.Globalization.CultureInfo.InvariantCulture), longitude.ToString(System.Globalization.CultureInfo.InvariantCulture), "100", scheduled ? "1" : "0", military ? "1" : "0");
        }

        /// <summary>
        /// Builds a line of the runways file.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <param name="lengthFeet">Length in feet.</param>
        /// <param name="surface">Surface code.</param>
        /// <returns>The tab separated line.</returns>
        private static string RunwayLine(string icao, int lengthFeet, string surface)
        {
            return string.Join("\t", icao, lengthFeet.ToString(System.Globalization.CultureInfo.InvariantCulture), "100", surface, "1", "09", "52.0", "20.0", "27", "52.0", "20.1");
        }

        /// <summary>
        /// Creates a database with a small set of Polish airports.
        /// </summary>
        /// <param name="runwayLines">Lines of the runways file.</param>
        /// <returns>The loaded database.</returns>
        private static AirportDatabase CreateDatabase(params string[] runwayLines)
        {
            AirportDatabase database = new AirportDatabase();
            database.LoadFrom(
                new string[]
                {
                    AirportLine("EPWA", "WAW", "L", "Warsaw Chopin Airport", "Warsaw", 52.1657, 20.9671, true),
                    AirportLine("EPKK", "KRK", "L", "Kraków Jana Pawła II", "Kraków", 50.0777, 19.7848, true),
                    AirportLine("EPLL", "LCJ", "M", "Łódź Władysław Reymont", "Łódź", 51.7219, 19.3981, true),
                    AirportLine("EPMB", string.Empty, "S", "Malbork Air Base", "Malbork", 54.0267, 19.1344, false, true),
                    AirportLine("EPWH", string.Empty, "H", "Warsaw Hospital Heliport", "Warsaw", 52.2, 21.0),
                    AirportLine("EPXX", string.Empty, "S", "Warsaw Aeroclub Field", "Warsaw", 52.3, 21.1),
                    "broken line",
                    "A\tB\tL\tName\tCity\tPL\tnot-a-number\t20.0\t1\t1\t0",
                },
                runwayLines);

            return database;
        }

        /// <summary>
        /// Folds the query like the search does.
        /// </summary>
        /// <param name="query">Query, words separated by spaces.</param>
        /// <returns>Folded terms.</returns>
        private static List<string> Terms(string query)
        {
            return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(AirportDatabase.Fold).ToList();
        }

        /// <summary>
        /// Before loading the database is empty and every query returns nothing.
        /// </summary>
        [Fact]
        public void BeforeLoading_IsEmpty()
        {
            AirportDatabase database = new AirportDatabase();

            Assert.False(database.IsLoaded);
            Assert.Equal(0, database.Count);
            Assert.Null(database.Find("EPWA"));
            Assert.Null(database.FindByIcao("EPWA"));
            Assert.Empty(database.Search(Terms("warsaw"), 10, true));
            Assert.Empty(database.GetNearest(52.0, 21.0, 3, AirportType.Small));
        }

        /// <summary>
        /// Loading skips invalid lines, marks the data as ready and raises the event.
        /// </summary>
        [Fact]
        public void LoadFrom_SkipsInvalidLinesAndRaisesLoaded()
        {
            AirportDatabase database = new AirportDatabase();
            int raised = 0;
            database.Loaded += () => raised++;

            database.LoadFrom(new string[] { AirportLine("EPWA", "WAW", "L", "Chopin", "Warsaw", 52.0, 21.0), "broken", "A\tB\tL\tN\tC\tPL\tx\ty\t1\t1\t0" }, Array.Empty<string>());

            Assert.True(database.IsLoaded);
            Assert.Equal(1, database.Count);
            Assert.Equal(1, raised);
        }

        /// <summary>
        /// Airports are found by ICAO and by IATA code, ignoring case and spaces.
        /// </summary>
        [Fact]
        public void Find_ByIcaoOrIata()
        {
            AirportDatabase database = CreateDatabase();

            Assert.Equal("EPWA", database.Find("epwa")?.Icao);
            Assert.Equal("EPKK", database.Find(" krk ")?.Icao);
            Assert.Null(database.Find("XXXX"));
            Assert.Null(database.Find("  "));
            Assert.Null(database.Find(null));
            Assert.Equal("EPLL", database.FindByIcao("epll")?.Icao);
            Assert.Null(database.FindByIcao("KRK"));
        }

        /// <summary>
        /// Airport types are read from the file.
        /// </summary>
        [Fact]
        public void LoadFrom_ParsesTypesAndFlags()
        {
            AirportDatabase database = CreateDatabase();

            Assert.Equal(AirportType.Large, database.Find("EPWA")!.Type);
            Assert.Equal(AirportType.Medium, database.Find("EPLL")!.Type);
            Assert.Equal(AirportType.Small, database.Find("EPMB")!.Type);
            Assert.Equal(AirportType.Heliport, database.Find("EPWH")!.Type);
            Assert.True(database.Find("EPMB")!.IsMilitary);
            Assert.True(database.Find("EPWA")!.HasScheduledService);
            Assert.Equal(100, database.Find("EPWA")!.ElevationFeet);
        }

        /// <summary>
        /// A missing elevation stays null.
        /// </summary>
        [Fact]
        public void LoadFrom_MissingElevation_IsNull()
        {
            AirportDatabase database = new AirportDatabase();
            database.LoadFrom(new string[] { "EPAA\t\tS\tName\tCity\tPL\t52.0\t21.0\t\t0\t0" }, Array.Empty<string>());

            Assert.Null(database.Find("EPAA")!.ElevationFeet);
        }

        /// <summary>
        /// A word matches exactly a code, as a prefix, or as a substring, with decreasing quality.
        /// </summary>
        [Fact]
        public void GetMatchQuality_RanksExactPrefixAndContains()
        {
            AirportDatabase database = CreateDatabase();
            Airport warsaw = database.Find("EPWA")!;

            Assert.Equal(AirportDatabase.ExactMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("waw")));
            Assert.Equal(AirportDatabase.ExactMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("EPWA")));
            Assert.Equal(AirportDatabase.PrefixMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("chop")));
            Assert.Equal(AirportDatabase.ContainsMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("hopin")));
            Assert.Equal(0, AirportDatabase.GetMatchQuality(warsaw, Terms("berlin")));
        }

        /// <summary>
        /// With several words the weakest word decides, and one missing word rejects the airport.
        /// </summary>
        [Fact]
        public void GetMatchQuality_MultipleWords_UsesWeakest()
        {
            Airport warsaw = CreateDatabase().Find("EPWA")!;

            Assert.Equal(AirportDatabase.PrefixMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("waw chopin")));
            Assert.Equal(AirportDatabase.ContainsMatch, AirportDatabase.GetMatchQuality(warsaw, Terms("waw hopin")));
            Assert.Equal(0, AirportDatabase.GetMatchQuality(warsaw, Terms("waw berlin")));
            Assert.Equal(0, AirportDatabase.GetMatchQuality(warsaw, new List<string>()));
        }

        /// <summary>
        /// A word after a hyphen counts as the start of a word.
        /// </summary>
        [Fact]
        public void GetMatchQuality_WordAfterHyphen_IsPrefix()
        {
            Airport airport = new Airport("XXXX", string.Empty, AirportType.Small, "Saint-Exupery", "Lyon", "FR", 45.0, 5.0, null, false, false);

            Assert.Equal(AirportDatabase.PrefixMatch, AirportDatabase.GetMatchQuality(airport, Terms("exupery")));
        }

        /// <summary>
        /// Polish letters are found by the query typed without them.
        /// </summary>
        [Theory]
        [InlineData("lodz", "EPLL")]
        [InlineData("krakow", "EPKK")]
        [InlineData("wladyslaw", "EPLL")]
        [InlineData("pawla", "EPKK")]
        public void Search_FindsPolishNamesWithoutDiacritics(string query, string expectedIcao)
        {
            AirportDatabase database = CreateDatabase();

            List<(Airport Airport, int Quality)> result = database.Search(Terms(query), 10, true);

            Assert.Contains(result, item => item.Airport.Icao == expectedIcao);
        }

        /// <summary>
        /// Results are ordered by quality, then by size, then by scheduled service, then by name.
        /// </summary>
        [Fact]
        public void Search_OrdersBySizeAndService()
        {
            AirportDatabase database = CreateDatabase();

            List<(Airport Airport, int Quality)> result = database.Search(Terms("warsaw"), 10, true);

            Assert.Equal(new string[] { "EPWA", "EPXX", "EPWH" }, result.Select(item => item.Airport.Icao).ToArray());
        }

        /// <summary>
        /// A preferred airport comes before others of the same quality.
        /// </summary>
        [Fact]
        public void Search_PreferredAirportComesFirst()
        {
            AirportDatabase database = CreateDatabase();

            List<(Airport Airport, int Quality)> result = database.Search(Terms("warsaw"), 10, true, airport => airport.Icao == "EPXX");

            Assert.Equal("EPXX", result[0].Airport.Icao);
        }

        /// <summary>
        /// Exact quality beats everything else, even a preferred airport.
        /// </summary>
        [Fact]
        public void Search_ExactCodeComesBeforePreferred()
        {
            AirportDatabase database = CreateDatabase();

            List<(Airport Airport, int Quality)> result = database.Search(Terms("epwa"), 10, true, airport => airport.Icao == "EPXX");

            Assert.Equal("EPWA", result[0].Airport.Icao);
            Assert.Equal(AirportDatabase.ExactMatch, result[0].Quality);
        }

        /// <summary>
        /// Heliports are skipped unless they are enabled or matched exactly by code.
        /// </summary>
        [Fact]
        public void Search_Heliports_OnlyWhenEnabledOrExact()
        {
            AirportDatabase database = CreateDatabase();

            Assert.DoesNotContain(database.Search(Terms("warsaw"), 10, false), item => item.Airport.Icao == "EPWH");
            Assert.Contains(database.Search(Terms("epwh"), 10, false), item => item.Airport.Icao == "EPWH");
            Assert.Contains(database.Search(Terms("warsaw"), 10, true), item => item.Airport.Icao == "EPWH");
        }

        /// <summary>
        /// The number of results is limited, and an empty list of words gives nothing.
        /// </summary>
        [Fact]
        public void Search_LimitsResultsAndRejectsEmptyTerms()
        {
            AirportDatabase database = CreateDatabase();

            Assert.Single(database.Search(Terms("warsaw"), 1, true));
            Assert.Empty(database.Search(new List<string>(), 10, true));
        }

        /// <summary>
        /// A query returns airports inside the rectangle that satisfy the size filter.
        /// </summary>
        [Fact]
        public void Query_ReturnsAirportsInRectangle()
        {
            AirportDatabase database = CreateDatabase();
            List<Airport> result = new List<Airport>();

            database.Query(50.0, 53.0, 19.0, 22.0, AirportType.Medium, result);

            Assert.Equal(new string[] { "EPKK", "EPLL", "EPWA" }, result.Select(airport => airport.Icao).OrderBy(icao => icao, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// The rectangle is checked exactly, not only by grid cells.
        /// </summary>
        [Fact]
        public void Query_ExcludesAirportsOutsideRectangleInSameCell()
        {
            AirportDatabase database = CreateDatabase();
            List<Airport> result = new List<Airport>();

            database.Query(52.0, 52.17, 20.9, 20.97, delegate (Airport airport) { return true; }, result);
            Assert.Single(result);
            Assert.Equal("EPWA", result[0].Icao);

            result.Clear();
            database.Query(52.0, 52.1, 20.9, 20.96, delegate (Airport airport) { return true; }, result);
            Assert.Empty(result);
        }

        /// <summary>
        /// The query does nothing before the data is loaded and tolerates coordinates beyond the globe.
        /// </summary>
        [Fact]
        public void Query_HandlesNotLoadedAndExtremeCoordinates()
        {
            List<Airport> result = new List<Airport>();
            new AirportDatabase().Query(-90.0, 90.0, -180.0, 180.0, AirportType.Small, result);
            Assert.Empty(result);

            CreateDatabase().Query(-200.0, 200.0, -400.0, 400.0, AirportType.Heliport, result);
            Assert.Equal(6, result.Count);
        }

        /// <summary>
        /// The nearest airports are ordered by distance and limited by size class.
        /// </summary>
        [Fact]
        public void GetNearest_OrdersByDistanceAndFiltersBySize()
        {
            AirportDatabase database = CreateDatabase();

            List<Airport> result = database.GetNearest(52.1, 21.0, 3, AirportType.Medium);

            Assert.Equal("EPWA", result[0].Icao);
            Assert.All(result, airport => Assert.True(airport.Type <= AirportType.Medium));
            Assert.True(result.Count <= 3);
        }

        /// <summary>
        /// The search widens until it has enough airports.
        /// </summary>
        [Fact]
        public void GetNearest_WidensSearchArea()
        {
            AirportDatabase database = CreateDatabase();

            List<Airport> result = database.GetNearest(53.5, 18.0, 2, AirportType.Large);

            Assert.Equal(2, result.Count);
            Assert.Equal("EPWA", result[0].Icao);
        }

        /// <summary>
        /// Runways are returned for the airport and an empty list for an airport without them.
        /// </summary>
        [Fact]
        public void GetRunways_ReturnsRunwaysOfAirport()
        {
            AirportDatabase database = CreateDatabase(RunwayLine("EPWA", 11500, "ASP"), RunwayLine("EPWA", 9000, "CON"), "short\tline");

            Assert.Equal(2, database.GetRunways(database.Find("EPWA")!).Count);
            Assert.Empty(database.GetRunways(database.Find("EPLL")!));
            Assert.Equal(2, database.RunwayCount);
        }

        /// <summary>
        /// A runway line with invalid coordinates is skipped.
        /// </summary>
        [Fact]
        public void LoadFrom_RunwayWithInvalidCoordinates_IsSkipped()
        {
            string invalid = "EPWA\t9000\t100\tASP\t1\t09\tx\t20.0\t27\t52.0\t20.1";
            AirportDatabase database = CreateDatabase(invalid);

            Assert.Empty(database.GetRunways(database.Find("EPWA")!));
        }

        /// <summary>
        /// The runway fields are read in the right order.
        /// </summary>
        [Fact]
        public void LoadFrom_ParsesRunwayFields()
        {
            AirportDatabase database = CreateDatabase(RunwayLine("EPWA", 11500, "ASP"));

            Runway runway = database.GetRunways(database.Find("EPWA")!)[0];

            Assert.Equal(11500, runway.LengthFeet);
            Assert.Equal(100, runway.WidthFeet);
            Assert.Equal("ASP", runway.Surface);
            Assert.True(runway.IsLighted);
            Assert.Equal("09", runway.LowEndName);
            Assert.Equal("27", runway.HighEndName);
            Assert.Equal(20.1, runway.HighEndLongitude);
        }

        /// <summary>
        /// A long paved runway sets the flag, a short or unpaved one does not.
        /// </summary>
        [Theory]
        [InlineData(2625, "ASP", true)]
        [InlineData(2624, "ASP", false)]
        [InlineData(8000, "GRS", false)]
        [InlineData(8000, "con-g", true)]
        [InlineData(8000, "PEM", true)]
        [InlineData(8000, "BIT", true)]
        [InlineData(8000, "PAVED", true)]
        [InlineData(8000, "TAR", true)]
        [InlineData(8000, " asphalt ", true)]
        public void LoadFrom_LongPavedRunwayFlag(int lengthFeet, string surface, bool expected)
        {
            AirportDatabase database = CreateDatabase(RunwayLine("EPMB", lengthFeet, surface));

            Assert.Equal(expected, database.Find("EPMB")!.HasLongPavedRunway);
        }

        /// <summary>
        /// The flag is true when any of several runways qualifies.
        /// </summary>
        [Fact]
        public void LoadFrom_AnyQualifyingRunwaySetsFlag()
        {
            AirportDatabase database = CreateDatabase(RunwayLine("EPMB", 1000, "ASP"), RunwayLine("EPMB", 5000, "GRS"), RunwayLine("EPMB", 6000, "ASP"));

            Assert.True(database.Find("EPMB")!.HasLongPavedRunway);
        }
    }
}

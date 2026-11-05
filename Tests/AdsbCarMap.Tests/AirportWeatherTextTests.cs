using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the texts and colors of the weather panel of the airport card.
    /// </summary>
    public class AirportWeatherTextTests
    {
        /// <summary>
        /// Moment of the observation used by the tests.
        /// </summary>
        private static readonly DateTimeOffset Observed = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Units of the tests: kilometers, knots and feet.
        /// </summary>
        private static readonly DisplayUnits Knots = new DisplayUnits(AltitudeUnit.Feet, DistanceUnit.Kilometers, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);

        /// <summary>
        /// Creates a report.
        /// </summary>
        /// <param name="category">Flight category.</param>
        /// <param name="direction">Wind direction, or null.</param>
        /// <param name="variable">Whether the wind direction is variable.</param>
        /// <param name="speed">Wind speed, or null.</param>
        /// <param name="gust">Gust speed, or null.</param>
        /// <param name="visibility">Visibility in meters, or null.</param>
        /// <param name="lowerBound">Whether the visibility is a lower bound.</param>
        /// <param name="clouds">Cloud layers.</param>
        /// <param name="temperature">Temperature, or null.</param>
        /// <param name="dewPoint">Dew point, or null.</param>
        /// <param name="pressure">Pressure, or null.</param>
        /// <param name="weather">Present weather, or null.</param>
        /// <returns>The report.</returns>
        private static Metar Create(string? category = "VFR", int? direction = 270, bool variable = false, int? speed = 12, int? gust = 22, double? visibility = 9000.0, bool lowerBound = false, List<CloudLayer>? clouds = null, double? temperature = 8.0, double? dewPoint = 5.0, double? pressure = 1018.0, string? weather = null)
        {
            return new Metar("EPWA", Observed, "METAR EPWA 051000Z 27012G22KT 9000 BKN025 08/05 Q1018", category, direction, variable, speed, gust, visibility, lowerBound, clouds ?? new List<CloudLayer> { new CloudLayer("BKN", 2500) }, temperature, dewPoint, pressure, weather);
        }

        /// <summary>
        /// Creates a stored answer fetched at the time of the observation.
        /// </summary>
        /// <param name="report">The report, or null.</param>
        /// <returns>The answer.</returns>
        private static WeatherEntry Entry(Metar? report)
        {
            return new WeatherEntry(report, Observed.UtcDateTime);
        }

        /// <summary>
        /// Current time 14 minutes after the observation.
        /// </summary>
        private static readonly DateTime Now = Observed.UtcDateTime + TimeSpan.FromMinutes(14);

        /// <summary>
        /// The closed header holds the category, the wind, the visibility and the age.
        /// </summary>
        [Fact]
        public void Header_Closed_SummarizesTheReport()
        {
            Assert.Equal("Pogoda · VFR · 270° 12 kt · 9,0 km · 14 min temu", AirportWeatherText.BuildHeader(Entry(Create()), false, false, Knots, Now));
        }

        /// <summary>
        /// The open header holds only the category and the age.
        /// </summary>
        [Fact]
        public void Header_Open_HoldsCategoryAndAge()
        {
            Assert.Equal("Pogoda · VFR · 14 min temu", AirportWeatherText.BuildHeader(Entry(Create()), false, true, Knots, Now));
        }

        /// <summary>
        /// A report from the nearest station names the station and its distance in the unit chosen by the user, and the lines start with an explanation.
        /// </summary>
        [Fact]
        public void NearestStation_HeaderAndLinesNameTheStation()
        {
            WeatherEntry entry = new WeatherEntry(Create() with { IcaoCode = "EPRZ" }, Observed.UtcDateTime, 46.4, 60.0);
            DisplayUnits miles = new DisplayUnits(AltitudeUnit.Feet, DistanceUnit.Miles, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);

            Assert.Equal("Pogoda · stacja EPRZ, 46 km · VFR · 14 min temu", AirportWeatherText.BuildHeader(entry, false, true, Knots, Now));
            Assert.Equal("Pogoda · stacja EPRZ, 29 mi · VFR · 14 min temu", AirportWeatherText.BuildHeader(entry, false, true, miles, Now));
            Assert.Equal("Najbliższa stacja: EPRZ, 46 km od lotniska (to lotnisko nie ma własnego METAR)", AirportWeatherText.BuildLines(entry.Report!, Knots, 46.4)[0]);
            Assert.Equal("Wiatr: 270° 12 kt, w porywach 22 kt", AirportWeatherText.BuildLines(entry.Report!, Knots, 46.4)[1]);
            Assert.DoesNotContain("stacja", AirportWeatherText.BuildHeader(Entry(Create()), false, true, Knots, Now));
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(), Knots), line => line.StartsWith("Najbliższa", StringComparison.Ordinal));
        }

        /// <summary>
        /// A report that is more than 90 minutes old is marked as out of date.
        /// </summary>
        [Fact]
        public void Header_OldReport_IsMarked()
        {
            string header = AirportWeatherText.BuildHeader(Entry(Create()), false, true, Knots, Observed.UtcDateTime + TimeSpan.FromMinutes(125));

            Assert.Equal("Pogoda · VFR · 2 h 5 min temu · nieaktualne", header);
            Assert.DoesNotContain("nieaktualne", AirportWeatherText.BuildHeader(Entry(Create()), false, true, Knots, Observed.UtcDateTime + TimeSpan.FromMinutes(90)));
        }

        /// <summary>
        /// Without an answer the header says whether the weather is being downloaded.
        /// </summary>
        [Fact]
        public void Header_WithoutAnswer_ShowsLoadingOrNoData()
        {
            Assert.Equal("Pogoda · pobieranie…", AirportWeatherText.BuildHeader(null, true, false, Knots, Now));
            Assert.Equal("Pogoda · brak danych", AirportWeatherText.BuildHeader(null, false, false, Knots, Now));
        }

        /// <summary>
        /// An answer without a report says that the airport has no METAR.
        /// </summary>
        [Fact]
        public void Header_AnswerWithoutReport_SaysThereIsNoMetar()
        {
            Assert.Equal("Pogoda · brak raportu METAR dla tego lotniska", AirportWeatherText.BuildHeader(Entry(null), false, false, Knots, Now));
        }

        /// <summary>
        /// Parts that the report does not have are left out of the header.
        /// </summary>
        [Fact]
        public void Header_MissingParts_AreLeftOut()
        {
            Metar bare = Create(category: null, direction: null, speed: null, gust: null, visibility: null);

            Assert.Equal("Pogoda · 14 min temu", AirportWeatherText.BuildHeader(Entry(bare), false, false, Knots, Now));
        }

        /// <summary>
        /// The wind has a direction and gusts; units follow the settings.
        /// </summary>
        [Fact]
        public void Lines_Wind_FollowsUnitsAndShowsGusts()
        {
            Assert.Equal("Wiatr: 270° 12 kt, w porywach 22 kt", AirportWeatherText.BuildLines(Create(), Knots)[0]);
            Assert.Equal("Wiatr: 270° 22 km/h, w porywach 41 km/h", AirportWeatherText.BuildLines(Create(), DisplayUnits.Default)[0].Replace(' ', ' '));
        }

        /// <summary>
        /// A calm, a variable wind and gusts that are not stronger than the mean wind are written accordingly.
        /// </summary>
        [Fact]
        public void Lines_Wind_CalmVariableAndNoGusts()
        {
            Assert.Equal("Wiatr: cisza", AirportWeatherText.BuildLines(Create(speed: 0, gust: null, direction: 0), Knots)[0]);
            Assert.Equal("Wiatr: zmienny 3 kt", AirportWeatherText.BuildLines(Create(direction: null, variable: true, speed: 3, gust: null), Knots)[0]);
            Assert.Equal("Wiatr: 090° 5 kt", AirportWeatherText.BuildLines(Create(direction: 90, speed: 5, gust: 5), Knots)[0]);
            Assert.Equal("Wiatr: 5 kt", AirportWeatherText.BuildLines(Create(direction: null, speed: 5, gust: null), Knots)[0]);
        }

        /// <summary>
        /// A report without wind data has no wind line.
        /// </summary>
        [Fact]
        public void Lines_NoWind_HasNoWindLine()
        {
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(speed: null, gust: null), Knots), line => line.StartsWith("Wiatr", StringComparison.Ordinal));
        }

        /// <summary>
        /// The visibility follows the distance unit and a lower bound is described.
        /// </summary>
        [Fact]
        public void Lines_Visibility_UnitsAndLowerBound()
        {
            DisplayUnits miles = new DisplayUnits(AltitudeUnit.Feet, DistanceUnit.Miles, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);

            Assert.Equal("Widzialność: 9,0 km", AirportWeatherText.BuildLines(Create(), Knots)[1]);
            Assert.Equal("Widzialność: 5,6 mi", AirportWeatherText.BuildLines(Create(), miles)[1]);
            Assert.Equal("Widzialność: 10 km lub więcej", AirportWeatherText.BuildLines(Create(visibility: 9700.0, lowerBound: true), Knots)[1]);
            Assert.Equal("Widzialność: co najmniej 4,8 km", AirportWeatherText.BuildLines(Create(visibility: 4800.0, lowerBound: true), Knots)[1]);
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(visibility: null), Knots), line => line.StartsWith("Widzialność", StringComparison.Ordinal));
        }

        /// <summary>
        /// The cloud base follows the altitude unit, but flight levels are shown in feet because a base is a height above the airport.
        /// </summary>
        [Fact]
        public void Lines_Clouds_UnitsAndSpecialCases()
        {
            DisplayUnits metric = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);
            DisplayUnits level = new DisplayUnits(AltitudeUnit.FlightLevel, DistanceUnit.Kilometers, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);
            List<CloudLayer> layers = new List<CloudLayer> { new CloudLayer("BKN", 3900), new CloudLayer("OVC", 4800), new CloudLayer("VV", null) };

            Assert.Equal("Chmury: BKN 3 900 ft, OVC 4 800 ft, VV", Normalize(AirportWeatherText.BuildLines(Create(clouds: layers), Knots)[2]));
            Assert.Equal("Chmury: BKN 1 189 m, OVC 1 463 m, VV", Normalize(AirportWeatherText.BuildLines(Create(clouds: layers), metric)[2]));
            Assert.Equal("Chmury: BKN 3 900 ft, OVC 4 800 ft, VV", Normalize(AirportWeatherText.BuildLines(Create(clouds: layers), level)[2]));
            Assert.Equal("Chmury: brak istotnych", AirportWeatherText.BuildLines(Create(clouds: new List<CloudLayer>()), Knots)[2]);
        }

        /// <summary>
        /// Creates an east-west runway that is 27 when used from its eastern end.
        /// </summary>
        /// <returns>The runways of an airport.</returns>
        private static List<Runway> Runways()
        {
            return new List<Runway> { new Runway(9800, 150, "ASP", true, "09", 52.0, 20.0, "27", 52.0, 20.1) };
        }

        /// <summary>
        /// The lines of the open panel have a line with the runway that the wind favors after the clouds, with the headwind and the crosswind side.
        /// </summary>
        [Fact]
        public void Lines_RunwayByTheWind()
        {
            List<string> lines = AirportWeatherText.BuildLines(Create(direction: 290, speed: 10, gust: null), Knots, null, Runways());

            Assert.StartsWith("Chmury:", lines[2]);
            Assert.Equal("Pas wg wiatru: 27 · wiatr czołowy 9 kt, boczny 3 kt z prawej", lines[3]);
            Assert.Equal("Pas wg wiatru: 27 · wiatr czołowy 9 kt, boczny 3 kt z lewej", AirportWeatherText.BuildLines(Create(direction: 250, speed: 10, gust: null), Knots, null, Runways())[3]);
            Assert.Equal("Pas wg wiatru: 27 · wiatr czołowy 10 kt", AirportWeatherText.BuildLines(Create(direction: 270, speed: 10, gust: null), Knots, null, Runways())[3]);
            Assert.Equal("Pas wg wiatru: 09 · wiatr czołowy 19 km/h", Normalize(AirportWeatherText.BuildLines(Create(direction: 90, speed: 10, gust: null), DisplayUnits.Default, null, Runways())[3]));
        }

        /// <summary>
        /// The line is left out without runways, with a weak, variable or unknown wind, and for a report from the nearest station, because its wind is not the wind at the airport.
        /// </summary>
        [Fact]
        public void Lines_RunwayByTheWind_LeftOutWhenItCannotBeGiven()
        {
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(), Knots), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(), Knots, null, new List<Runway>()), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(speed: 2, gust: null), Knots, null, Runways()), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(direction: null, variable: true, speed: 8, gust: null), Knots, null, Runways()), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.DoesNotContain(AirportWeatherText.BuildLines(Create(), Knots, 46.0, Runways()), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.Equal(string.Empty, AirportWeatherText.BuildRunwayLine(Create(), null, Knots));
        }

        /// <summary>
        /// The popup has the line after the short lines, and the block of text for a dialog has the open header followed by everything the open panel shows.
        /// </summary>
        [Fact]
        public void PopupAndTextLines_HaveTheRunwayLine()
        {
            WeatherEntry entry = Entry(Create(direction: 270, speed: 10, gust: null));
            List<string> popup = AirportWeatherText.BuildPopupLines(entry, false, Knots, Now, Runways());
            List<string> text = AirportWeatherText.BuildTextLines(entry, false, Knots, Now, Runways());

            Assert.Equal("Pas wg wiatru: 27 · wiatr czołowy 10 kt", popup.Last());
            Assert.Equal(5, popup.Count);
            Assert.Equal("Pogoda · VFR · 14 min temu", text[0]);
            Assert.Contains("Pas wg wiatru: 27 · wiatr czołowy 10 kt", text);
            Assert.Equal("METAR EPWA 051000Z 27012G22KT 9000 BKN025 08/05 Q1018", text.Last());
            Assert.DoesNotContain(AirportWeatherText.BuildPopupLines(new WeatherEntry(entry.Report, entry.FetchedUtc, 46.0), false, Knots, Now, Runways()), line => line.StartsWith("Pas wg", StringComparison.Ordinal));
            Assert.Equal(new string[] { "Pogoda · pobieranie…" }, AirportWeatherText.BuildTextLines(null, true, Knots, Now, Runways()).ToArray());
        }

        /// <summary>
        /// A layer on the ground has its base written as zero, which differs from a layer without a height.
        /// </summary>
        [Fact]
        public void Lines_Clouds_BaseZeroIsWritten()
        {
            List<CloudLayer> layers = new List<CloudLayer> { new CloudLayer("BKN", 0), new CloudLayer("OVC", 100), new CloudLayer("VV", null) };

            Assert.Equal("Chmury: BKN 0 ft, OVC 100 ft, VV", Normalize(AirportWeatherText.BuildLines(Create(clouds: layers), Knots)[2]));
            Assert.Equal("Chmury: BKN 0 m, OVC 30 m, VV", Normalize(AirportWeatherText.BuildLines(Create(clouds: layers), new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute))[2]));
        }

        /// <summary>
        /// The open panel ends with present weather, temperature, pressure and the original text.
        /// </summary>
        [Fact]
        public void Lines_End_WithWeatherTemperaturePressureAndRawText()
        {
            List<string> lines = AirportWeatherText.BuildLines(Create(weather: "-RA BR"), Knots);

            Assert.Equal(new string[] { "Zjawiska: -RA BR", "Temperatura: 8 °C, punkt rosy 5 °C · QNH 1018 hPa", "METAR EPWA 051000Z 27012G22KT 9000 BKN025 08/05 Q1018" }, lines.Skip(3).ToArray());
        }

        /// <summary>
        /// Temperature, dew point and pressure are left out when the report does not have them, and a fractional temperature keeps one decimal.
        /// </summary>
        [Fact]
        public void Lines_TemperatureAndPressure_Optional()
        {
            List<string> bare = AirportWeatherText.BuildLines(Create(temperature: null, dewPoint: null, pressure: null), Knots);
            List<string> partial = AirportWeatherText.BuildLines(Create(temperature: -3.5, dewPoint: null, pressure: 1031.4), Knots);

            Assert.Equal(4, bare.Count);
            Assert.Equal("Temperatura: -3,5 °C · QNH 1031 hPa", partial[3]);
        }

        /// <summary>
        /// A report with only a pressure shows only the pressure.
        /// </summary>
        [Fact]
        public void Lines_OnlyPressure_ShowsPressure()
        {
            Assert.Equal("QNH 1018 hPa", AirportWeatherText.BuildLines(Create(temperature: null, dewPoint: null), Knots)[3]);
        }

        /// <summary>
        /// The short lines hold the wind, the visibility and the clouds only.
        /// </summary>
        [Fact]
        public void PopupLines_AreHeaderAndShortLines()
        {
            List<string> lines = AirportWeatherText.BuildPopupLines(Entry(Create()), false, Knots, Now);

            Assert.Equal(new string[] { "Pogoda · VFR · 14 min temu", "Wiatr: 270° 12 kt, w porywach 22 kt", "Widzialność: 9,0 km", "Chmury: BKN 2 500 ft" }, lines.Select(Normalize).ToArray());
        }

        /// <summary>
        /// The popup without a report has only the header.
        /// </summary>
        [Fact]
        public void PopupLines_WithoutReport_HaveOnlyHeader()
        {
            Assert.Equal(new string[] { "Pogoda · pobieranie…" }, AirportWeatherText.BuildPopupLines(null, true, Knots, Now).ToArray());
            Assert.Single(AirportWeatherText.BuildPopupLines(Entry(null), false, Knots, Now));
        }

        /// <summary>
        /// The age is written in minutes, hours or days; an observation from the future counts as just now.
        /// </summary>
        [Fact]
        public void FormatAge_Ranges()
        {
            Assert.Equal("przed chwilą", AirportWeatherText.FormatAge(TimeSpan.FromSeconds(30)));
            Assert.Equal("przed chwilą", AirportWeatherText.FormatAge(TimeSpan.FromMinutes(-5)));
            Assert.Equal("1 min temu", AirportWeatherText.FormatAge(TimeSpan.FromSeconds(75)));
            Assert.Equal("59 min temu", AirportWeatherText.FormatAge(TimeSpan.FromMinutes(59)));
            Assert.Equal("1 h temu", AirportWeatherText.FormatAge(TimeSpan.FromMinutes(60)));
            Assert.Equal("2 h 5 min temu", AirportWeatherText.FormatAge(TimeSpan.FromMinutes(125)));
            Assert.Equal("ponad dobę temu", AirportWeatherText.FormatAge(TimeSpan.FromHours(30)));
        }

        /// <summary>
        /// Each flight category has its own color, the letter case does not matter, and anything else has none.
        /// </summary>
        [Fact]
        public void CategoryColor_KnownAndUnknown()
        {
            int?[] colors = new string[] { "VFR", "MVFR", "IFR", "LIFR" }.Select(AirportWeatherText.GetCategoryColor).ToArray();

            Assert.All(colors, color => Assert.NotNull(color));
            Assert.Equal(4, colors.Distinct().Count());
            Assert.Equal(AirportWeatherText.GetCategoryColor("VFR"), AirportWeatherText.GetCategoryColor(" vfr "));
            Assert.Null(AirportWeatherText.GetCategoryColor(null));
            Assert.Null(AirportWeatherText.GetCategoryColor("XYZ"));
        }

        /// <summary>
        /// The texts have English translations, so that the English interface does not show Polish.
        /// </summary>
        [Fact]
        public void Texts_HaveEnglishTranslations()
        {
            string[] keys = new string[] { "Pogoda", "pobieranie…", "brak danych", "brak raportu METAR dla tego lotniska", "nieaktualne", "Wiatr: ", "Widzialność: ", "Chmury: ", "Zjawiska: ", "Temperatura: ", ", punkt rosy ", "cisza", "zmienny", ", w porywach ", " lub więcej", "co najmniej ", "brak istotnych", "przed chwilą", "ponad dobę temu", "{0} min temu", "{0} h temu", "{0} h {1} min temu", "Częstotliwości", "Częstotliwości (MHz)", "stacja {0}, {1}", "Najbliższa stacja: {0}, {1} od lotniska (to lotnisko nie ma własnego METAR)", "rozwiń", "zwiń", "Rozwiń panel", "Zwiń panel", "TYP", "OPIS", "Pas wg wiatru: {0} · wiatr czołowy {1}", ", boczny {0} {1}", "z prawej", "z lewej" };

            foreach (string key in keys)
            {
                Assert.NotEqual(key, Loc.Translate(key, AppLanguage.English));
            }
        }

        /// <summary>
        /// Replaces the non-breaking spaces used as digit group separators with plain spaces.
        /// </summary>
        /// <param name="text">Formatted text.</param>
        /// <returns>Text with plain spaces.</returns>
        private static string Normalize(string text)
        {
            return text.Replace(' ', ' ').Replace(' ', ' ');
        }
    }
}

using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the texts of the forecast panel of the airport card.
    /// </summary>
    public class AirportForecastTextTests
    {
        /// <summary>
        /// Beginning of the validity of the forecast used by the tests.
        /// </summary>
        private static readonly DateTimeOffset ValidFrom = new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Current time one hour after the beginning of the validity.
        /// </summary>
        private static readonly DateTime Now = ValidFrom.UtcDateTime + TimeSpan.FromHours(1);

        /// <summary>
        /// Units of the tests: kilometers, knots and feet.
        /// </summary>
        private static readonly DisplayUnits Knots = new DisplayUnits(AltitudeUnit.Feet, DistanceUnit.Kilometers, SpeedUnit.Knots, VerticalSpeedUnit.FeetPerMinute);

        /// <summary>
        /// Creates a period that starts at an hour offset from the beginning of the validity.
        /// </summary>
        /// <param name="fromHours">Beginning in hours from the beginning of the validity.</param>
        /// <param name="toHours">End in hours from the beginning of the validity.</param>
        /// <param name="change">Kind of the change.</param>
        /// <param name="probability">Probability.</param>
        /// <param name="direction">Wind direction.</param>
        /// <param name="speed">Wind speed.</param>
        /// <param name="visibility">Visibility in meters.</param>
        /// <param name="clouds">Cloud layers.</param>
        /// <param name="weather">Weather codes.</param>
        /// <returns>The period.</returns>
        private static TafPeriod Period(int fromHours, int toHours, string? change = null, int? probability = null, int? direction = null, int? speed = null, double? visibility = null, List<CloudLayer>? clouds = null, string? weather = null)
        {
            return new TafPeriod(ValidFrom.AddHours(fromHours), ValidFrom.AddHours(toHours), change, probability, direction, false, speed, null, visibility, false, clouds ?? new List<CloudLayer>(), weather);
        }

        /// <summary>
        /// Creates the forecast of the tests: valid for 24 hours with the basic conditions and three changes.
        /// </summary>
        /// <returns>The forecast.</returns>
        private static Taf CreateTaf()
        {
            List<TafPeriod> periods = new List<TafPeriod>
            {
                Period(0, 21, null, null, 280, 10, 8000.0, new List<CloudLayer> { new CloudLayer("NSC", null) }),
                Period(8, 21, "TEMPO", null, null, null, null, new List<CloudLayer> { new CloudLayer("BKN", 700), new CloudLayer("BKN", 1000) }),
                Period(11, 18, "TEMPO", 40, null, null, 4000.0, new List<CloudLayer> { new CloudLayer("BKN", 300) }, "BR"),
                Period(21, 24, "BECMG", null, 340, 10),
            };

            return new Taf("EPWA", ValidFrom.AddMinutes(-30), ValidFrom, ValidFrom.AddHours(24), "TAF EPWA 050830Z 0509/0609 28010KT 8000 NSC", periods);
        }

        /// <summary>
        /// Creates a stored answer.
        /// </summary>
        /// <param name="taf">The forecast, or null.</param>
        /// <returns>The answer.</returns>
        private static ForecastEntry Entry(Taf? taf)
        {
            return new ForecastEntry(taf, Now);
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

        /// <summary>
        /// The closed header holds the end of the validity with the zone and the wind and the visibility of the basic conditions.
        /// </summary>
        [Fact]
        public void Header_Closed_HoldsValidityAndBasicConditions()
        {
            Assert.Equal("Prognoza · do 06.11 09:00 UTC · 280° 10 kt · 8,0 km", AirportForecastText.BuildHeader(Entry(CreateTaf()), false, false, Knots, TimeZoneInfo.Utc, Now));
        }

        /// <summary>
        /// The open header holds the whole validity.
        /// </summary>
        [Fact]
        public void Header_Open_HoldsWholeValidity()
        {
            Assert.Equal("Prognoza (TAF) · ważna 05.11 09:00 – 06.11 09:00 UTC", AirportForecastText.BuildHeader(Entry(CreateTaf()), false, true, Knots, TimeZoneInfo.Utc, Now));
        }

        /// <summary>
        /// A forecast that is no longer valid is marked in both headers.
        /// </summary>
        [Fact]
        public void Header_Expired_IsMarked()
        {
            DateTime later = ValidFrom.UtcDateTime + TimeSpan.FromHours(25);

            Assert.Equal("Prognoza · wygasła 06.11 09:00 UTC", AirportForecastText.BuildHeader(Entry(CreateTaf()), false, false, Knots, TimeZoneInfo.Utc, later));
            Assert.Equal("Prognoza (TAF) · ważna 05.11 09:00 – 06.11 09:00 UTC · wygasła", AirportForecastText.BuildHeader(Entry(CreateTaf()), false, true, Knots, TimeZoneInfo.Utc, later));
        }

        /// <summary>
        /// Without an answer the header says whether the forecast is being downloaded, and an answer without a forecast says that there is none.
        /// </summary>
        [Fact]
        public void Header_WithoutAnswerOrForecast()
        {
            Assert.Equal("Prognoza · pobieranie…", AirportForecastText.BuildHeader(null, true, false, Knots, TimeZoneInfo.Utc, Now));
            Assert.Equal("Prognoza (TAF) · brak danych", AirportForecastText.BuildHeader(null, false, true, Knots, TimeZoneInfo.Utc, Now));
            Assert.Equal("Prognoza · brak prognozy TAF dla tego lotniska", AirportForecastText.BuildHeader(Entry(null), false, false, Knots, TimeZoneInfo.Utc, Now));
        }

        /// <summary>
        /// The times follow the chosen zone, which is named in the header.
        /// </summary>
        [Fact]
        public void Header_LocalZone_ShiftsTimesAndNamesTheZone()
        {
            TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

            Assert.Equal("Prognoza · do 06.11 11:00 czas lokalny · 280° 10 kt · 8,0 km", AirportForecastText.BuildHeader(Entry(CreateTaf()), false, false, Knots, zone, Now));
            Assert.Equal("05 11:00\n→ 06 08:00", AirportForecastText.BuildRows(CreateTaf(), Knots, zone)[0].Period);
        }

        /// <summary>
        /// The basic conditions are the first period without a change, or the first period when every period is a change; a forecast without periods has only the validity.
        /// </summary>
        [Fact]
        public void Header_Closed_BasicConditionsChoice()
        {
            Taf changesOnly = CreateTaf() with { Periods = new List<TafPeriod> { Period(0, 3, "FM", null, 90, 5, 9999.0), Period(3, 6, null, null, 180, 8) } };
            Taf onlyChanges = CreateTaf() with { Periods = new List<TafPeriod> { Period(0, 3, "FM", null, 90, 5, 9999.0) } };
            Taf none = CreateTaf() with { Periods = new List<TafPeriod>() };

            Assert.Equal("Prognoza · do 06.11 09:00 UTC · 180° 8 kt", AirportForecastText.BuildHeader(Entry(changesOnly), false, false, Knots, TimeZoneInfo.Utc, Now));
            Assert.Equal("Prognoza · do 06.11 09:00 UTC · 090° 5 kt · 10,0 km", AirportForecastText.BuildHeader(Entry(onlyChanges), false, false, Knots, TimeZoneInfo.Utc, Now));
            Assert.Equal("Prognoza · do 06.11 09:00 UTC", AirportForecastText.BuildHeader(Entry(none), false, false, Knots, TimeZoneInfo.Utc, Now));
        }

        /// <summary>
        /// There is a row for each period with the period on two lines, the kind of the change and only the conditions that the period gives.
        /// </summary>
        [Fact]
        public void Rows_DescribeEachPeriod()
        {
            List<ForecastRow> rows = AirportForecastText.BuildRows(CreateTaf(), Knots, TimeZoneInfo.Utc);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new ForecastRow("05 09:00\n→ 05 06:00".Replace("05 06:00", "06 06:00"), "bazowa", "280° 10 kt · 8,0 km · NSC"), rows[0]);
            Assert.Equal("05 17:00\n→ 06 06:00", rows[1].Period);
            Assert.Equal("czasowo", rows[1].Change);
            Assert.Equal("BKN 700 ft, BKN 1 000 ft", Normalize(rows[1].Conditions));
            Assert.Equal("40% czasowo", rows[2].Change);
            Assert.Equal("4,0 km · BKN 300 ft · BR", Normalize(rows[2].Conditions));
            Assert.Equal("stopniowo", rows[3].Change);
            Assert.Equal("340° 10 kt", rows[3].Conditions);
        }

        /// <summary>
        /// The kinds of the change are described, a probability alone is written as a percentage, an unknown kind is kept and a period without conditions has a dash.
        /// </summary>
        [Fact]
        public void Rows_ChangeKindsAndEmptyConditions()
        {
            Taf taf = CreateTaf() with
            {
                Periods = new List<TafPeriod>
                {
                    Period(0, 1, "FM"),
                    Period(1, 2, "PROB", 30),
                    Period(2, 3, null, 30),
                    Period(3, 4, "XX"),
                    Period(4, 5, "TEMPO", 40, 200, 4, 1500.0),
                },
            };

            List<ForecastRow> rows = AirportForecastText.BuildRows(taf, Knots, TimeZoneInfo.Utc);

            Assert.Equal(new string[] { "od", "30%", "30%", "XX", "40% czasowo" }, rows.Select((ForecastRow row) => row.Change).ToArray());
            Assert.Equal("–", rows[0].Conditions);
            Assert.Equal("200° 4 kt · 1,5 km", rows[4].Conditions);
        }

        /// <summary>
        /// The rows are in the order of time, and the units follow the settings.
        /// </summary>
        [Fact]
        public void Rows_AreOrderedAndFollowUnits()
        {
            Taf taf = CreateTaf() with { Periods = new List<TafPeriod> { Period(10, 12, "TEMPO", null, null, 10), Period(0, 24, null, null, 280, 10) } };
            DisplayUnits metric = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute);
            List<ForecastRow> rows = AirportForecastText.BuildRows(taf, metric, TimeZoneInfo.Utc);

            Assert.Equal("bazowa", rows[0].Change);
            Assert.Equal("280° 19 km/h", Normalize(rows[0].Conditions));
            Assert.Equal("19 km/h", Normalize(rows[1].Conditions));
        }

        /// <summary>
        /// The columns, the original text, the popup lines and the zone are provided.
        /// </summary>
        [Fact]
        public void Columns_Footer_PopupLines_Zone()
        {
            Assert.Equal(new string[] { "OKRES", "ZMIANA", "WARUNKI" }, AirportForecastText.BuildColumns().ToArray());
            Assert.Equal("TAF EPWA 050830Z 0509/0609 28010KT 8000 NSC", Assert.Single(AirportForecastText.BuildFooter(CreateTaf())));
            Assert.Equal("Prognoza · do 06.11 09:00 UTC · 280° 10 kt · 8,0 km", Assert.Single(AirportForecastText.BuildPopupLines(Entry(CreateTaf()), false, Knots, TimeZoneInfo.Utc, Now)));
            Assert.Equal(TimeZoneInfo.Utc, AirportForecastText.GetZone(false));
            Assert.Equal(TimeZoneInfo.Local, AirportForecastText.GetZone(true));
        }

        /// <summary>
        /// The block of text for a dialog has the open header, a line for each period on one line and the original text.
        /// </summary>
        [Fact]
        public void TextLines_HeaderPeriodsAndOriginalText()
        {
            List<string> lines = AirportForecastText.BuildTextLines(Entry(CreateTaf()), false, Knots, TimeZoneInfo.Utc, Now);

            Assert.Equal("Prognoza (TAF) · ważna 05.11 09:00 – 06.11 09:00 UTC", lines[0]);
            Assert.Equal(6, lines.Count);
            Assert.Equal("05 09:00 → 06 06:00 · bazowa · 280° 10 kt · 8,0 km · NSC", lines[1]);
            Assert.Equal("05 17:00 → 06 06:00 · czasowo · BKN 700 ft, BKN 1 000 ft", Normalize(lines[2]));
            Assert.Equal("TAF EPWA 050830Z 0509/0609 28010KT 8000 NSC", lines.Last());
            Assert.Equal(new string[] { "Prognoza · pobieranie…" }, AirportForecastText.BuildTextLines(null, true, Knots, TimeZoneInfo.Utc, Now).Select(line => line.Replace("(TAF) ", string.Empty)).ToArray());
        }

        /// <summary>
        /// The texts have English translations, so that the English interface does not show Polish.
        /// </summary>
        [Fact]
        public void Texts_HaveEnglishTranslations()
        {
            string[] keys = new string[] { "Prognoza", "Prognoza (TAF)", "brak prognozy TAF dla tego lotniska", "ważna {0} – {1} {2}", "wygasła", "wygasła {0} {1}", "do {0} {1}", "OKRES", "ZMIANA", "WARUNKI", "bazowa", "stopniowo", "czasowo", "czas lokalny" };

            foreach (string key in keys)
            {
                Assert.NotEqual(key, Loc.Translate(key, AppLanguage.English));
            }
        }
    }
}

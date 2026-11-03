using System.Text.Json;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Rso.Tests
{
    /// <summary>
    /// Tests of the reader of the notices of the regional warning system.
    /// </summary>
    public class RsoParserTests
    {
        /// <summary>
        /// A notice as the feed writes it, with the quirks: the alarm flag as text, extra keys with date formats and the regions keyed by a number.
        /// </summary>
        private const string Notice = @"{
            ""id"": 23466071, ""title"": ""Susza hydrologiczna"", ""shortcut"": ""podkarpackie, zlewnie Wieprza"", ""content"": ""Niskie przepływy wody."",
            ""rso_alarm"": ""1"", ""rso_icon"": null, ""valid_from"": ""2026-10-31 11:06:00"", ""0"": ""Y-m-d H:i:s"", ""valid_to"": ""2026-11-30 23:59:00"", ""1"": ""Y-m-d H:i:s"",
            ""repetition"": null, ""longitude"": null, ""latitude"": null, ""water_level_value"": null, ""river_name"": """", ""location_name"": """",
            ""created_at"": ""2026-10-31 11:07:12"", ""2"": ""Y-m-d H:i:s"", ""updated_at"": ""2026-10-31 11:07:20"", ""3"": ""Y-m-d H:i:s"",
            ""provinces"": { ""9"": { ""id"": ""9"", ""name"": ""Podkarpackie"", ""city"": """", ""slug_name"": ""podkarpackie"" }, ""12"": { ""id"": ""12"", ""name"": ""Lubelskie"", ""slug_name"": ""lubelskie"" } }
        }";

        /// <summary>
        /// A reading of a water gauge as the feed writes it: no title, the levels written as text.
        /// </summary>
        private const string Gauge = @"{
            ""id"": ""23475590"", ""title"": null, ""shortcut"": 0, ""content"": null, ""rso_alarm"": null, ""valid_from"": ""2026-11-03 23:50:00"", ""valid_to"": ""2026-11-05 23:50:00"",
            ""longitude"": ""17.67"", ""latitude"": ""51.575556"", ""water_level_value"": ""67"", ""water_level_warning_status_value"": ""120"", ""water_level_alarm_status_value"": ""150"", ""water_level_trend"": ""0"",
            ""river_name"": ""Barycz"", ""location_name"": ""Odolanów / Barycz"", ""created_at"": ""2026-11-03 23:50:00"", ""updated_at"": ""2026-11-04 00:00:48"", ""type"": ""z"",
            ""provinces"": { ""15"": { ""id"": ""15"", ""name"": ""Wielkopolskie"", ""slug_name"": ""wielkopolskie"" } }
        }";

        /// <summary>
        /// Wraps notices in the answer of the feed.
        /// </summary>
        /// <param name="items">Notices, separated by commas.</param>
        /// <returns>The JSON text.</returns>
        private static string Answer(string items)
        {
            return "{\"pagination\":{\"totalitems\":1,\"itemsperpage\":20},\"newses\":[" + items + "]}";
        }

        /// <summary>
        /// A notice is read with its texts, flags, regions and the category the caller asked for.
        /// </summary>
        [Fact]
        public void ParseWarnings_ReadsNotice()
        {
            Warning warning = Assert.Single(RsoParser.ParseWarnings(Answer(Notice), "hydrologiczne"));

            Assert.Equal("23466071", warning.Id);
            Assert.Equal("hydrologiczne", warning.Category);
            Assert.Equal("Susza hydrologiczna", warning.Title);
            Assert.Equal("podkarpackie, zlewnie Wieprza", warning.Summary);
            Assert.Equal("Niskie przepływy wody.", warning.Content);
            Assert.True(warning.IsAlarm);
            Assert.Equal(new List<string> { "podkarpackie", "lubelskie" }, warning.Provinces.Select((WarningProvince province) => province.Slug).ToList());
            Assert.Equal("Podkarpackie", warning.Provinces[0].Name);
            Assert.Equal(string.Empty, warning.RiverName);
            Assert.Null(warning.WaterLevel);
            Assert.Null(warning.Latitude);
        }

        /// <summary>
        /// The dates are written in the time of Poland and are converted to UTC, with the summer and the winter time.
        /// </summary>
        [Fact]
        public void ParseWarnings_ConvertsDatesToUtc()
        {
            Warning warning = Assert.Single(RsoParser.ParseWarnings(Answer(Notice), "ogolne"));

            Assert.Equal(new DateTime(2026, 10, 31, 10, 6, 0, DateTimeKind.Utc), warning.ValidFromUtc);
            Assert.Equal(new DateTime(2026, 11, 30, 22, 59, 0, DateTimeKind.Utc), warning.ValidToUtc);
            Assert.Equal(new DateTime(2026, 10, 31, 10, 7, 20, DateTimeKind.Utc), warning.UpdatedUtc);
            Assert.Equal(DateTimeKind.Utc, warning.ValidFromUtc.Kind);
            Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), RsoParser.ParseLocalDate("2026-07-01 12:00:00"));
            Assert.Equal(new DateTime(2026, 12, 1, 11, 0, 0, DateTimeKind.Utc), RsoParser.ParseLocalDate("2026-12-01 12:00:00"));
        }

        /// <summary>
        /// A moment that does not exist because the clocks are put forward is read as the moment after the change, and a bad text gives null.
        /// </summary>
        [Theory]
        [InlineData("2026-03-29 02:30:00")]
        [InlineData("2026-10-25 02:30:00")]
        public void ParseLocalDate_HandlesChangesOfTime(string text)
        {
            Assert.NotNull(RsoParser.ParseLocalDate(text));
        }

        /// <summary>
        /// Texts that are not dates give null.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Y-m-d H:i:s")]
        [InlineData("2026-13-45 00:00:00")]
        [InlineData("2026-11-03")]
        public void ParseLocalDate_ReturnsNullForBadText(string? text)
        {
            Assert.Null(RsoParser.ParseLocalDate(text));
        }

        /// <summary>
        /// A reading of a gauge is read with the levels written as text, the place and the position.
        /// </summary>
        [Fact]
        public void ParseWarnings_ReadsWaterGauge()
        {
            Warning warning = Assert.Single(RsoParser.ParseWarnings(Answer(Gauge), "stany-wod"));

            Assert.Equal("23475590", warning.Id);
            Assert.Equal(string.Empty, warning.Title);
            Assert.False(warning.IsAlarm);
            Assert.Equal("Barycz", warning.RiverName);
            Assert.Equal("Odolanów / Barycz", warning.LocationName);
            Assert.Equal(51.575556, warning.Latitude);
            Assert.Equal(17.67, warning.Longitude);
            Assert.Equal(new WarningWaterLevel(67.0, 120.0, 150.0), warning.WaterLevel);
        }

        /// <summary>
        /// The alarm is only a flag with the value 1 (as text or as a number); other values are not an alarm.
        /// </summary>
        [Theory]
        [InlineData("\"1\"", true)]
        [InlineData("1", true)]
        [InlineData("\"0\"", false)]
        [InlineData("0", false)]
        [InlineData("null", false)]
        [InlineData("\"yes\"", false)]
        public void ParseWarnings_ReadsAlarmFlag(string value, bool expected)
        {
            string json = Answer(@"{ ""id"": 1, ""valid_from"": ""2026-11-03 10:00:00"", ""rso_alarm"": " + value + " }");

            Assert.Equal(expected, Assert.Single(RsoParser.ParseWarnings(json, "ogolne")).IsAlarm);
        }

        /// <summary>
        /// A notice without an end has no end, and one without a start takes the time of the last change.
        /// </summary>
        [Fact]
        public void ParseWarnings_UsesUpdateWhenStartIsMissing()
        {
            string json = Answer(@"{ ""id"": 2, ""updated_at"": ""2026-11-03 10:00:00"" }");

            Warning warning = Assert.Single(RsoParser.ParseWarnings(json, "ogolne"));

            Assert.Equal(warning.UpdatedUtc, warning.ValidFromUtc);
            Assert.Null(warning.ValidToUtc);
            Assert.Empty(warning.Provinces);
        }

        /// <summary>
        /// Notices that cannot be read are skipped and the good one stays.
        /// </summary>
        [Fact]
        public void ParseWarnings_SkipsUnreadableNotices()
        {
            string json = Answer(Notice + @", 7, { ""title"": ""no id"", ""valid_from"": ""2026-11-03 10:00:00"" }, { ""id"": 3 }, { ""id"": """", ""valid_from"": ""2026-11-03 10:00:00"" }");

            Assert.Equal("23466071", Assert.Single(RsoParser.ParseWarnings(json, "ogolne")).Id);
        }

        /// <summary>
        /// Regions that are not objects or have no slug are skipped, a region without a name takes its slug, and a list of regions is also understood.
        /// </summary>
        [Fact]
        public void ParseWarnings_ReadsOddRegions()
        {
            string odd = Answer(@"{ ""id"": 4, ""valid_from"": ""2026-11-03 10:00:00"", ""provinces"": { ""1"": 5, ""2"": { ""name"": ""X"" }, ""3"": { ""slug_name"": ""opolskie"" } } }");
            string list = Answer(@"{ ""id"": 5, ""valid_from"": ""2026-11-03 10:00:00"", ""provinces"": [ { ""slug_name"": ""lodzkie"", ""name"": ""Łódzkie"" } ] }");
            string none = Answer(@"{ ""id"": 6, ""valid_from"": ""2026-11-03 10:00:00"", ""provinces"": ""x"" }");

            WarningProvince province = Assert.Single(Assert.Single(RsoParser.ParseWarnings(odd, "ogolne")).Provinces);
            Assert.Equal(new WarningProvince("opolskie", "opolskie"), province);
            Assert.Equal("Łódzkie", Assert.Single(Assert.Single(RsoParser.ParseWarnings(list, "ogolne")).Provinces).Name);
            Assert.Empty(Assert.Single(RsoParser.ParseWarnings(none, "ogolne")).Provinces);
        }

        /// <summary>
        /// Numbers that are empty, damaged or written with a comma are handled.
        /// </summary>
        [Fact]
        public void ParseWarnings_ReadsNumbersTolerantly()
        {
            string json = Answer(@"{ ""id"": 8, ""valid_from"": ""2026-11-03 10:00:00"", ""latitude"": ""51,5"", ""longitude"": ""abc"", ""water_level_value"": 12.5, ""water_level_alarm_status_value"": """" }");

            Warning warning = Assert.Single(RsoParser.ParseWarnings(json, "ogolne"));

            Assert.Equal(51.5, warning.Latitude);
            Assert.Null(warning.Longitude);
            Assert.Equal(new WarningWaterLevel(12.5, null, null), warning.WaterLevel);
        }

        /// <summary>
        /// Missing text, an object without the list and other JSON values give no notices.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("{}")]
        [InlineData("{\"newses\":5}")]
        [InlineData("[]")]
        [InlineData("5")]
        public void ParseWarnings_ReturnsEmptyForNothing(string? json)
        {
            Assert.Empty(RsoParser.ParseWarnings(json, "ogolne"));
        }

        /// <summary>
        /// Text that is not JSON is reported to the caller.
        /// </summary>
        [Fact]
        public void ParseWarnings_ThrowsForInvalidJson()
        {
            Assert.ThrowsAny<JsonException>(() => RsoParser.ParseWarnings("<html>", "ogolne"));
        }
    }
}

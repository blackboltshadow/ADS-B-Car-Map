using System.Text.Json;
using AdsbCarMap.DataSources.AviationWeather;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.AviationWeather.Tests
{
    /// <summary>
    /// Tests of the parser of the aviationweather.gov METAR answer.
    /// </summary>
    public class AviationWeatherParserTests
    {
        /// <summary>
        /// Real answer for three airports: clear sky, broken and overcast layers with present weather, and a "6+" visibility.
        /// </summary>
        private const string Sample = "[{\"icaoId\":\"EPWA\",\"obsTime\":1793867400,\"temp\":4,\"dewp\":2,\"wdir\":260,\"wspd\":5,\"visib\":5.59,\"altim\":1031,\"metarType\":\"METAR\",\"rawOb\":\"METAR EPWA 050830Z 26005KT 9000 NSC 04/02 Q1031 NOSIG\",\"cover\":\"CLR\",\"clouds\":[],\"fltCat\":\"VFR\"},"
            + "{\"icaoId\":\"KJFK\",\"obsTime\":1793865840,\"temp\":16.1,\"dewp\":16.1,\"wdir\":130,\"wspd\":4,\"visib\":3,\"altim\":1015.7,\"wxString\":\"BR\",\"metarType\":\"SPECI\",\"rawOb\":\"SPECI KJFK 050804Z 13004KT 3SM BR BKN039 OVC048 16/16 A2999\",\"clouds\":[{\"cover\":\"OVC\",\"base\":4800},{\"cover\":\"BKN\",\"base\":3900}],\"fltCat\":\"MVFR\"},"
            + "{\"icaoId\":\"LFPG\",\"obsTime\":1793867400,\"temp\":5,\"dewp\":2,\"wdir\":70,\"wspd\":9,\"visib\":\"6+\",\"altim\":1023,\"rawOb\":\"METAR LFPG 050830Z AUTO 07009KT CAVOK 05/02 Q1023 NOSIG\",\"clouds\":[],\"fltCat\":\"VFR\"}]";

        /// <summary>
        /// Returns the report of an airport from the sample.
        /// </summary>
        /// <param name="code">ICAO code.</param>
        /// <returns>The report.</returns>
        private static Metar Find(string code)
        {
            return AviationWeatherParser.Parse(Sample).Single((Metar metar) => metar.IcaoCode == code);
        }

        /// <summary>
        /// Real answer for Warsaw: a basic period, a temporary change, a temporary change with a probability and a gradual change.
        /// </summary>
        private const string TafSample = "[{\"icaoId\":\"EPWA\",\"issueTime\":\"2026-11-05T08:30:00.000Z\",\"validTimeFrom\":1793869200,\"validTimeTo\":1793955600,\"rawTAF\":\"TAF EPWA 050830Z 0509/0609 28010KT 8000 NSC TEMPO 0517/0606 BKN007 BKN010 PROB40 TEMPO 0520/0603 4000 BR BKN003 BECMG 0606/0609 34010KT\",\"fcsts\":["
            + "{\"timeFrom\":1793869200,\"timeTo\":1793944800,\"timeBec\":null,\"fcstChange\":null,\"probability\":null,\"wdir\":280,\"wspd\":10,\"wgst\":null,\"visib\":4.97,\"wxString\":null,\"clouds\":[{\"cover\":\"NSC\",\"base\":null,\"type\":null}]},"
            + "{\"timeFrom\":1793898000,\"timeTo\":1793944800,\"timeBec\":null,\"fcstChange\":\"TEMPO\",\"probability\":null,\"wdir\":null,\"wspd\":null,\"wgst\":null,\"visib\":\"\",\"wxString\":null,\"clouds\":[{\"cover\":\"BKN\",\"base\":1000},{\"cover\":\"BKN\",\"base\":700}]},"
            + "{\"timeFrom\":1793908800,\"timeTo\":1793934000,\"timeBec\":null,\"fcstChange\":\"TEMPO\",\"probability\":40,\"wdir\":null,\"wspd\":null,\"wgst\":null,\"visib\":2.49,\"wxString\":\"BR\",\"clouds\":[{\"cover\":\"BKN\",\"base\":300,\"type\":null}]},"
            + "{\"timeFrom\":1793944800,\"timeTo\":1793955600,\"timeBec\":1793955600,\"fcstChange\":\"BECMG\",\"probability\":null,\"wdir\":340,\"wspd\":10,\"wgst\":null,\"visib\":4.97,\"wxString\":null,\"clouds\":[{\"cover\":\"NSC\",\"base\":null}]}]}]";

        /// <summary>
        /// The validity, the text and the periods of a forecast are read, and the periods keep their kinds of change and probabilities.
        /// </summary>
        [Fact]
        public void ParseTafs_ReadsForecastAndPeriods()
        {
            Taf taf = AviationWeatherParser.ParseTafs(TafSample).Single();

            Assert.Equal("EPWA", taf.IcaoCode);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793869200), taf.ValidFrom);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793955600), taf.ValidTo);
            Assert.Equal(new DateTimeOffset(2026, 11, 5, 8, 30, 0, TimeSpan.Zero), taf.IssuedAt);
            Assert.StartsWith("TAF EPWA 050830Z", taf.RawText);
            Assert.Equal(4, taf.Periods.Count);
            Assert.Equal(new string?[] { null, "TEMPO", "TEMPO", "BECMG" }, taf.Periods.Select((TafPeriod period) => period.Change).ToArray());
            Assert.Equal(new int?[] { null, null, 40, null }, taf.Periods.Select((TafPeriod period) => period.Probability).ToArray());
        }

        /// <summary>
        /// The elements of a period are converted: the wind, the visibility in meters, the weather and the clouds from the lowest one.
        /// </summary>
        [Fact]
        public void ParseTafs_ConvertsTheElementsOfAPeriod()
        {
            Taf taf = AviationWeatherParser.ParseTafs(TafSample).Single();
            TafPeriod basic = taf.Periods[0];
            TafPeriod clouds = taf.Periods[1];
            TafPeriod mist = taf.Periods[2];

            Assert.Equal(280, basic.WindDirectionDegrees);
            Assert.Equal(10, basic.WindSpeedKnots);
            Assert.Equal(8000.0, basic.VisibilityMeters);
            Assert.Equal(new CloudLayer("NSC", null), Assert.Single(basic.Clouds));
            Assert.Null(clouds.WindSpeedKnots);
            Assert.Null(clouds.VisibilityMeters);
            Assert.Equal(new CloudLayer[] { new CloudLayer("BKN", 700), new CloudLayer("BKN", 1000) }, clouds.Clouds.ToArray());
            Assert.Equal(4000.0, mist.VisibilityMeters);
            Assert.Equal("BR", mist.WeatherText);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793908800), mist.From);
        }

        /// <summary>
        /// A variable wind, a visibility with a plus and gusts are read.
        /// </summary>
        [Fact]
        public void ParseTafs_VariableWindPlusVisibilityAndGusts()
        {
            string json = "[{\"icaoId\":\"EPKK\",\"validTimeFrom\":1000,\"validTimeTo\":2000,\"rawTAF\":\"TAF EPKK\",\"fcsts\":[{\"timeFrom\":1000,\"timeTo\":2000,\"wdir\":\"VRB\",\"wspd\":3,\"wgst\":12,\"visib\":\"6+\"}]}]";
            TafPeriod period = AviationWeatherParser.ParseTafs(json).Single().Periods.Single();

            Assert.True(period.IsWindVariable);
            Assert.Null(period.WindDirectionDegrees);
            Assert.Equal(12, period.WindGustKnots);
            Assert.True(period.IsVisibilityLowerBound);
            Assert.Null(period.Change);
            Assert.Empty(period.Clouds);
        }

        /// <summary>
        /// For a repeated airport only the forecast issued last is kept, and the issue time falls back to the beginning of the validity.
        /// </summary>
        [Fact]
        public void ParseTafs_RepeatedAirport_KeepsNewest()
        {
            string json = "[{\"icaoId\":\"EPWA\",\"issueTime\":\"2026-11-05T02:00:00Z\",\"validTimeFrom\":1000,\"validTimeTo\":2000,\"rawTAF\":\"old\"},{\"icaoId\":\"EPWA\",\"issueTime\":\"2026-11-05T08:00:00Z\",\"validTimeFrom\":1000,\"validTimeTo\":2000,\"rawTAF\":\"new\"},{\"icaoId\":\"EPKK\",\"validTimeFrom\":5000,\"validTimeTo\":6000,\"rawTAF\":\"no issue time\"}]";
            IReadOnlyList<Taf> tafs = AviationWeatherParser.ParseTafs(json);

            Assert.Equal(2, tafs.Count);
            Assert.Equal("new", tafs.Single((Taf taf) => taf.IcaoCode == "EPWA").RawText);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(5000), tafs.Single((Taf taf) => taf.IcaoCode == "EPKK").IssuedAt);
        }

        /// <summary>
        /// Forecasts without a code, a validity or a text, and periods without times, are skipped; an answer that is not an array gives nothing and invalid JSON throws.
        /// </summary>
        [Fact]
        public void ParseTafs_SkipsIncompleteAndRejectsInvalid()
        {
            string json = "[1,{\"validTimeFrom\":1,\"validTimeTo\":2,\"rawTAF\":\"x\"},{\"icaoId\":\"EPWA\",\"validTimeTo\":2,\"rawTAF\":\"x\"},{\"icaoId\":\"EPWA\",\"validTimeFrom\":1,\"validTimeTo\":2},{\"icaoId\":\"EPGD\",\"validTimeFrom\":1,\"validTimeTo\":2,\"rawTAF\":\"TAF EPGD\",\"fcsts\":[5,{\"timeTo\":2},{\"timeFrom\":1,\"timeTo\":2}]}]";
            Taf taf = Assert.Single(AviationWeatherParser.ParseTafs(json));

            Assert.Equal("EPGD", taf.IcaoCode);
            Assert.Single(taf.Periods);
            Assert.Empty(AviationWeatherParser.ParseTafs("{}"));
            Assert.ThrowsAny<JsonException>(() => AviationWeatherParser.ParseTafs("not json"));
            Assert.Empty(AviationWeatherParser.ParseTafs("[{\"icaoId\":\"EPGD\",\"validTimeFrom\":1,\"validTimeTo\":2,\"rawTAF\":\"x\",\"fcsts\":\"none\"}]").Single().Periods);
        }

        /// <summary>
        /// The source leaves out a base of zero, so a layer that covers the sky without a base is on the ground, while a code without a base such as NSC keeps none; the same holds in a forecast.
        /// </summary>
        [Fact]
        public void Parse_CloudWithoutBase_IsOnTheGroundOnlyForCoveringLayers()
        {
            string clouds = "\"clouds\":[{\"cover\":\"BKN\",\"base\":null},{\"cover\":\"OVC\",\"base\":100},{\"cover\":\"NSC\",\"base\":null},{\"cover\":\"VV\",\"base\":null}]";
            Metar metar = AviationWeatherParser.Parse("[{\"icaoId\":\"EPRZ\",\"obsTime\":1793867400,\"rawOb\":\"METAR EPRZ\"," + clouds + "}]").Single();
            TafPeriod period = AviationWeatherParser.ParseTafs("[{\"icaoId\":\"EPRZ\",\"validTimeFrom\":1000,\"validTimeTo\":2000,\"rawTAF\":\"TAF EPRZ\",\"fcsts\":[{\"timeFrom\":1000,\"timeTo\":2000," + clouds + "}]}]").Single().Periods.Single();

            Assert.Equal(new CloudLayer[] { new CloudLayer("BKN", 0), new CloudLayer("OVC", 100), new CloudLayer("NSC", null), new CloudLayer("VV", null) }.OrderBy((CloudLayer layer) => layer.BaseFeet ?? int.MaxValue).ToArray(), metar.Clouds.ToArray());
            Assert.Equal(metar.Clouds.ToArray(), period.Clouds.ToArray());
        }

        /// <summary>
        /// The position of the station is read when the source gives it, and is null otherwise.
        /// </summary>
        [Fact]
        public void Parse_ReadsStationPosition()
        {
            Metar metar = AviationWeatherParser.Parse("[{\"icaoId\":\"EPRZ\",\"obsTime\":1793867400,\"lat\":50.112,\"lon\":22.025,\"rawOb\":\"METAR EPRZ\"},{\"icaoId\":\"EPKK\",\"obsTime\":1793867400,\"rawOb\":\"METAR EPKK\"}]").Single((Metar item) => item.IcaoCode == "EPRZ");

            Assert.Equal(50.112, metar.Latitude);
            Assert.Equal(22.025, metar.Longitude);
            Assert.Null(AviationWeatherParser.Parse("[{\"icaoId\":\"EPKK\",\"obsTime\":1793867400,\"rawOb\":\"METAR EPKK\"}]").Single().Latitude);
        }

        /// <summary>
        /// All reports of the sample are read.
        /// </summary>
        [Fact]
        public void Parse_ReadsAllReports()
        {
            Assert.Equal(3, AviationWeatherParser.Parse(Sample).Count);
        }

        /// <summary>
        /// The basic fields of a report are converted.
        /// </summary>
        [Fact]
        public void Parse_ReadsBasicFields()
        {
            Metar metar = Find("EPWA");

            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793867400), metar.ObservedAt);
            Assert.Equal("VFR", metar.FlightCategory);
            Assert.Equal(260, metar.WindDirectionDegrees);
            Assert.False(metar.IsWindVariable);
            Assert.Equal(5, metar.WindSpeedKnots);
            Assert.Null(metar.WindGustKnots);
            Assert.Equal(4.0, metar.TemperatureCelsius);
            Assert.Equal(2.0, metar.DewPointCelsius);
            Assert.Equal(1031.0, metar.PressureHectopascals);
            Assert.Null(metar.WeatherText);
            Assert.StartsWith("METAR EPWA", metar.RawText);
            Assert.Empty(metar.Clouds);
        }

        /// <summary>
        /// A visibility in statute miles is converted to meters rounded to hundreds.
        /// </summary>
        [Fact]
        public void Parse_ConvertsVisibilityFromMiles()
        {
            Assert.Equal(9000.0, Find("EPWA").VisibilityMeters);
            Assert.Equal(4800.0, Find("KJFK").VisibilityMeters);
            Assert.False(Find("EPWA").IsVisibilityLowerBound);
        }

        /// <summary>
        /// A visibility with a plus is a lower bound.
        /// </summary>
        [Fact]
        public void Parse_VisibilityWithPlus_IsLowerBound()
        {
            Metar metar = Find("LFPG");

            Assert.True(metar.IsVisibilityLowerBound);
            Assert.Equal(9700.0, metar.VisibilityMeters);
        }

        /// <summary>
        /// Cloud layers are sorted from the lowest one, and present weather is kept.
        /// </summary>
        [Fact]
        public void Parse_SortsCloudsAndReadsWeather()
        {
            Metar metar = Find("KJFK");

            Assert.Equal(2, metar.Clouds.Count);
            Assert.Equal(new CloudLayer("BKN", 3900), metar.Clouds[0]);
            Assert.Equal(new CloudLayer("OVC", 4800), metar.Clouds[1]);
            Assert.Equal("BR", metar.WeatherText);
            Assert.Equal("MVFR", metar.FlightCategory);
            Assert.Equal(16.1, metar.TemperatureCelsius);
        }

        /// <summary>
        /// A variable wind direction is reported by the flag, not by a number.
        /// </summary>
        [Fact]
        public void Parse_VariableWind_SetsFlag()
        {
            string json = "[{\"icaoId\":\"EPKK\",\"obsTime\":1793867400,\"wdir\":\"VRB\",\"wspd\":3,\"wgst\":12,\"rawOb\":\"METAR EPKK\"}]";
            Metar metar = AviationWeatherParser.Parse(json).Single();

            Assert.True(metar.IsWindVariable);
            Assert.Null(metar.WindDirectionDegrees);
            Assert.Equal(3, metar.WindSpeedKnots);
            Assert.Equal(12, metar.WindGustKnots);
        }

        /// <summary>
        /// Missing optional fields give nulls instead of an error.
        /// </summary>
        [Fact]
        public void Parse_MissingFields_GiveNulls()
        {
            Metar metar = AviationWeatherParser.Parse("[{\"icaoId\":\"epgd\",\"obsTime\":1793867400,\"rawOb\":\"METAR EPGD\"}]").Single();

            Assert.Equal("EPGD", metar.IcaoCode);
            Assert.Null(metar.FlightCategory);
            Assert.Null(metar.WindSpeedKnots);
            Assert.Null(metar.VisibilityMeters);
            Assert.Null(metar.TemperatureCelsius);
            Assert.Null(metar.PressureHectopascals);
            Assert.Empty(metar.Clouds);
        }

        /// <summary>
        /// A number given as text is accepted, and text that is not a number is ignored.
        /// </summary>
        [Fact]
        public void Parse_NumbersAsText()
        {
            Metar metar = AviationWeatherParser.Parse("[{\"icaoId\":\"EPGD\",\"obsTime\":\"1793867400\",\"wspd\":\"7\",\"temp\":\"abc\",\"visib\":\"abc\",\"rawOb\":\"METAR EPGD\"}]").Single();

            Assert.Equal(7, metar.WindSpeedKnots);
            Assert.Null(metar.TemperatureCelsius);
            Assert.Null(metar.VisibilityMeters);
            Assert.False(metar.IsVisibilityLowerBound);
        }

        /// <summary>
        /// Visibility of another JSON type and cloud entries that are not usable are ignored.
        /// </summary>
        [Fact]
        public void Parse_UnusableValues_AreIgnored()
        {
            Metar metar = AviationWeatherParser.Parse("[{\"icaoId\":\"EPGD\",\"obsTime\":1793867400,\"visib\":true,\"clouds\":[1,{\"base\":100},{\"cover\":\"few\",\"base\":null}],\"rawOb\":\"METAR EPGD\"}]").Single();

            Assert.Null(metar.VisibilityMeters);
            Assert.Equal(new CloudLayer("FEW", 0), Assert.Single(metar.Clouds));
        }

        /// <summary>
        /// Entries without a code, a time or a text are skipped, as well as entries that are not objects.
        /// </summary>
        [Fact]
        public void Parse_SkipsIncompleteEntries()
        {
            string json = "[1,{\"obsTime\":1,\"rawOb\":\"x\"},{\"icaoId\":\"EPGD\",\"rawOb\":\"x\"},{\"icaoId\":\"EPGD\",\"obsTime\":1},{\"icaoId\":\" \",\"obsTime\":1,\"rawOb\":\"x\"}]";

            Assert.Empty(AviationWeatherParser.Parse(json));
        }

        /// <summary>
        /// For a repeated airport only the newest report is kept.
        /// </summary>
        [Fact]
        public void Parse_RepeatedAirport_KeepsNewest()
        {
            string json = "[{\"icaoId\":\"EPWA\",\"obsTime\":2000,\"rawOb\":\"new\"},{\"icaoId\":\"EPWA\",\"obsTime\":1000,\"rawOb\":\"old\"}]";
            Metar metar = AviationWeatherParser.Parse(json).Single();

            Assert.Equal("new", metar.RawText);
        }

        /// <summary>
        /// An answer that is not an array gives no reports, and invalid JSON throws.
        /// </summary>
        [Fact]
        public void Parse_NotAnArray_GivesNothing_InvalidJsonThrows()
        {
            Assert.Empty(AviationWeatherParser.Parse("{}"));
            Assert.ThrowsAny<JsonException>(() => AviationWeatherParser.Parse("not json"));
        }
    }
}

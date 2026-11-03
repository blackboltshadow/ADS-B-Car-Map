using System.Text.Json;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Pansa.Tests
{
    /// <summary>
    /// Tests of the reader of the PAŻP airspace map.
    /// </summary>
    public class PansaParserTests
    {
        /// <summary>
        /// One feature of the map with a polygon and two reservations, as the service sends it.
        /// </summary>
        private const string Feature = @"{
            ""type"": ""Feature"",
            ""properties"": {
                ""airspaceElementType"": ""D"",
                ""designator"": ""EPD62"",
                ""airspaceReservations"": [
                    { ""startDate"": ""2026-11-03T07:00:00Z"", ""endDate"": ""2026-11-03T21:00:00Z"", ""lowerAltitude"": ""GND"", ""upperAltitude"": ""A009"", ""altitudeUnit"": ""FL"", ""unit"": ""ZZZZ"", ""remarks"": ""NOT.D7851/26"", ""reservationStatus"": ""ACTIVATED"" },
                    { ""startDate"": ""2026-11-04T07:00:00Z"", ""endDate"": ""2026-11-04T21:00:00Z"", ""lowerAltitude"": ""F095"", ""upperAltitude"": ""F245"", ""altitudeUnit"": ""FL"", ""unit"": ""EPWA"", ""reservationStatus"": ""PLANNED"" }
                ]
            },
            ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[16.0, 53.0], [17.0, 53.0], [17.0, 54.0], [16.0, 54.0], [16.0, 53.0]]] }
        }";

        /// <summary>
        /// A list of features is read and the coordinates are turned from longitude-latitude into latitude and longitude.
        /// </summary>
        [Fact]
        public void ParseZones_ReadsZone()
        {
            IReadOnlyList<AirspaceZone> zones = PansaParser.ParseZones("[" + Feature + "]");

            AirspaceZone zone = Assert.Single(zones);
            Assert.Equal("EPD62", zone.Designator);
            Assert.Equal("D", zone.Type);
            IReadOnlyList<AirspaceVertex> outline = Assert.Single(zone.Outlines);
            Assert.Equal(5, outline.Count);
            Assert.Equal(new AirspaceVertex(53.0, 16.0), outline[0]);
            Assert.Equal(new AirspaceBounds(53.0, 54.0, 16.0, 17.0), zone.Bounds);
        }

        /// <summary>
        /// Both reservations are read with limits, period, state, unit and remarks.
        /// </summary>
        [Fact]
        public void ParseZones_ReadsReservations()
        {
            AirspaceZone zone = Assert.Single(PansaParser.ParseZones("[" + Feature + "]"));

            Assert.Equal(2, zone.Reservations.Count);
            AirspaceReservation first = zone.Reservations[0];
            Assert.Equal(new DateTime(2026, 11, 3, 7, 0, 0, DateTimeKind.Utc), first.StartUtc);
            Assert.Equal(DateTimeKind.Utc, first.EndUtc.Kind);
            Assert.Equal(new AirspaceLimit(0, AirspaceLimitReference.Ground), first.Lower);
            Assert.Equal(new AirspaceLimit(900, AirspaceLimitReference.Altitude), first.Upper);
            Assert.Equal(AirspaceReservationStatus.Activated, first.Status);
            Assert.Equal("ZZZZ", first.Unit);
            Assert.Equal("NOT.D7851/26", first.Remarks);
            AirspaceReservation second = zone.Reservations[1];
            Assert.Equal(new AirspaceLimit(9500, AirspaceLimitReference.FlightLevel), second.Lower);
            Assert.Equal(new AirspaceLimit(24500, AirspaceLimitReference.FlightLevel), second.Upper);
            Assert.Equal(AirspaceReservationStatus.Planned, second.Status);
            Assert.Null(second.Remarks);
        }

        /// <summary>
        /// An object with a "features" list is read like a bare list.
        /// </summary>
        [Fact]
        public void ParseZones_ReadsFeatureCollection()
        {
            IReadOnlyList<AirspaceZone> zones = PansaParser.ParseZones("{\"type\":\"FeatureCollection\",\"features\":[" + Feature + "]}");

            Assert.Single(zones);
        }

        /// <summary>
        /// A multi-polygon gives one outline per part, and the bounds cover all of them.
        /// </summary>
        [Fact]
        public void ParseZones_ReadsMultiPolygon()
        {
            string json = @"[{ ""properties"": { ""designator"": ""EPR1"", ""airspaceElementType"": ""R"" },
                ""geometry"": { ""type"": ""MultiPolygon"", ""coordinates"": [
                    [[[10.0, 50.0], [11.0, 50.0], [11.0, 51.0], [10.0, 50.0]]],
                    [[[20.0, 52.0], [21.0, 52.0], [21.0, 53.0], [20.0, 52.0]]] ] } }]";

            AirspaceZone zone = Assert.Single(PansaParser.ParseZones(json));

            Assert.Equal(2, zone.Outlines.Count);
            Assert.Equal(new AirspaceBounds(50.0, 53.0, 10.0, 21.0), zone.Bounds);
            Assert.Empty(zone.Reservations);
        }

        /// <summary>
        /// Only the outer ring of a polygon is kept.
        /// </summary>
        [Fact]
        public void ParseZones_IgnoresHoles()
        {
            string json = @"[{ ""properties"": { ""designator"": ""EPR2"" },
                ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [
                    [[10.0, 50.0], [12.0, 50.0], [12.0, 52.0], [10.0, 50.0]],
                    [[10.5, 50.5], [11.0, 50.5], [11.0, 51.0], [10.5, 50.5]] ] } }]";

            AirspaceZone zone = Assert.Single(PansaParser.ParseZones(json));

            Assert.Equal(4, Assert.Single(zone.Outlines).Count);
            Assert.Equal(string.Empty, zone.Type);
        }

        /// <summary>
        /// Features without a designation, an outline or with a bad outline are skipped, the good one stays.
        /// </summary>
        [Fact]
        public void ParseZones_SkipsUnreadableFeatures()
        {
            string json = "[" + Feature + @",
                42,
                { ""properties"": { ""designator"": """" }, ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[1,2],[3,4],[5,6]]] } },
                { ""properties"": { ""designator"": ""NOGEO"" } },
                { ""properties"": { ""designator"": ""LINE"" }, ""geometry"": { ""type"": ""LineString"", ""coordinates"": [[1,2],[3,4]] } },
                { ""properties"": { ""designator"": ""TWO"" }, ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[1,2],[3,4]]] } },
                { ""properties"": { ""designator"": ""BADPOS"" }, ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[1,2],[3,4],""x"",[5],[""a"",""b""]]] } },
                { ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [] } },
                { ""properties"": 5, ""geometry"": {} }]";

            AirspaceZone zone = Assert.Single(PansaParser.ParseZones(json));

            Assert.Equal("EPD62", zone.Designator);
        }

        /// <summary>
        /// A reservation with an unreadable date or limit is skipped, an unknown state becomes Other.
        /// </summary>
        [Fact]
        public void ParseZones_SkipsUnreadableReservations()
        {
            string json = @"[{ ""properties"": { ""designator"": ""EPD1"", ""airspaceReservations"": [
                    7,
                    { ""startDate"": ""bad"", ""endDate"": ""2026-11-03T21:00:00Z"", ""lowerAltitude"": ""GND"", ""upperAltitude"": ""A009"" },
                    { ""endDate"": ""2026-11-03T21:00:00Z"", ""lowerAltitude"": ""GND"", ""upperAltitude"": ""A009"" },
                    { ""startDate"": ""2026-11-03T07:00:00Z"", ""endDate"": ""2026-11-03T21:00:00Z"", ""lowerAltitude"": ""??"", ""upperAltitude"": ""A009"" },
                    { ""startDate"": ""2026-11-03T07:00:00Z"", ""endDate"": ""2026-11-03T21:00:00Z"", ""lowerAltitude"": ""GND"", ""upperAltitude"": ""A009"", ""reservationStatus"": ""CANCELLED"", ""remarks"": ""  "" } ] },
                ""geometry"": { ""type"": ""Polygon"", ""coordinates"": [[[1,2],[3,4],[5,6]]] } }]";

            AirspaceZone zone = Assert.Single(PansaParser.ParseZones(json));

            AirspaceReservation reservation = Assert.Single(zone.Reservations);
            Assert.Equal(AirspaceReservationStatus.Other, reservation.Status);
            Assert.Null(reservation.Remarks);
            Assert.Equal(string.Empty, reservation.Unit);
        }

        /// <summary>
        /// Missing text, an object without features and other JSON values give no areas.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{}")]
        [InlineData("{\"features\":5}")]
        [InlineData("5")]
        public void ParseZones_ReturnsEmptyForNothing(string? json)
        {
            Assert.Empty(PansaParser.ParseZones(json));
        }

        /// <summary>
        /// Text that is not JSON is reported to the caller.
        /// </summary>
        [Fact]
        public void ParseZones_ThrowsForInvalidJson()
        {
            Assert.ThrowsAny<JsonException>(() => PansaParser.ParseZones("<html>"));
        }

        /// <summary>
        /// The limits are read in all the notations the service uses.
        /// </summary>
        [Theory]
        [InlineData("GND", 0, AirspaceLimitReference.Ground)]
        [InlineData("gnd", 0, AirspaceLimitReference.Ground)]
        [InlineData("SFC", 0, AirspaceLimitReference.Ground)]
        [InlineData("A035", 3500, AirspaceLimitReference.Altitude)]
        [InlineData("A009", 900, AirspaceLimitReference.Altitude)]
        [InlineData("F095", 9500, AirspaceLimitReference.FlightLevel)]
        [InlineData("FL095", 9500, AirspaceLimitReference.FlightLevel)]
        [InlineData(" F660 ", 66000, AirspaceLimitReference.FlightLevel)]
        public void ParseLimit_ReadsNotations(string text, int feet, AirspaceLimitReference reference)
        {
            Assert.Equal(new AirspaceLimit(feet, reference), PansaParser.ParseLimit(text));
        }

        /// <summary>
        /// Text that is not a limit gives null.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("UNL")]
        [InlineData("A")]
        [InlineData("F-10")]
        [InlineData("Axyz")]
        public void ParseLimit_ReturnsNullForOtherText(string? text)
        {
            Assert.Null(PansaParser.ParseLimit(text));
        }
    }
}

using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of reading the Neptun JSON.
    /// </summary>
    public class NeptunParserTests
    {
        /// <summary>
        /// A threat as the real server sends it, with the fields the application does not use.
        /// </summary>
        private const string FullThreat = "{\"id\":\"trk_1\",\"type\":\"uav\",\"title\":\"БпЛА\",\"region\":\"Чернігівська область\",\"district\":\"\",\"locality\":\"Ніжин\",\"lat\":51.2,\"lon\":31.8,\"heading\":63.7,\"confidenceLevel\":\"high\",\"sourceCount\":3,\"updatedAt\":\"2026-11-03T17:21:19Z\",\"explanationShort\":\"opis\",\"status\":\"active\",\"confirmedAt\":\"2026-11-03T17:20:36Z\",\"uncertaintyKm\":10,\"positionQuality\":\"confirmed\",\"trail\":[{\"lat\":51.0,\"lon\":31.1,\"t\":\"2026-11-03T16:59:52Z\"},{\"lat\":51.2,\"lon\":31.8,\"t\":\"2026-11-03T17:20:36Z\"}],\"lifecycle\":\"confirmed\",\"displayConfidence\":\"high\",\"somethingNew\":{\"a\":1}}";

        /// <summary>
        /// All the fields of a threat are read.
        /// </summary>
        [Fact]
        public void ParseThreats_ReadsAllFields()
        {
            IReadOnlyList<Threat> threats = NeptunParser.ParseThreats("{\"epoch\":\"x\",\"threats\":[" + FullThreat + "]}");

            Assert.Single(threats);

            Threat threat = threats[0];

            Assert.Equal("trk_1", threat.Id);
            Assert.Equal(ThreatKind.Uav, threat.Kind);
            Assert.Equal("Ніжин", threat.Locality);
            Assert.Equal(51.2, threat.Latitude);
            Assert.Equal(31.8, threat.Longitude);
            Assert.Equal(63.7, threat.HeadingDegrees);
            Assert.Equal(ThreatConfidence.High, threat.Confidence);
            Assert.Equal(3, threat.SourceCount);
            Assert.Equal(new DateTime(2026, 11, 3, 17, 21, 19, DateTimeKind.Utc), threat.UpdatedUtc);
            Assert.Equal(ThreatStatus.Active, threat.Status);
            Assert.Equal(10.0, threat.UncertaintyKm);
            Assert.False(threat.IsApproximate);
            Assert.False(threat.Advisory);
            Assert.False(threat.AreaOnly);
            Assert.Equal(2, threat.Trail.Count);
            Assert.Equal(31.1, threat.Trail[0].Longitude);
        }

        /// <summary>
        /// A null heading and the flags written as true are read as the application expects.
        /// </summary>
        [Fact]
        public void ParseThreats_NullHeadingAndFlags()
        {
            string json = "{\"threats\":[{\"id\":\"a\",\"type\":\"missile\",\"lat\":1,\"lon\":2,\"heading\":null,\"positionQuality\":\"approx\",\"presumptiveCourse\":true,\"sea\":true,\"advisory\":true,\"areaOnly\":true,\"count\":4}]}";

            Threat threat = NeptunParser.ParseThreats(json)[0];

            Assert.Null(threat.HeadingDegrees);
            Assert.True(threat.IsApproximate);
            Assert.True(threat.IsPresumptiveCourse);
            Assert.True(threat.IsSea);
            Assert.True(threat.Advisory);
            Assert.True(threat.AreaOnly);
            Assert.Equal(4, threat.Count);
            Assert.Empty(threat.Trail);
        }

        /// <summary>
        /// The velocity object gives the speed and a missing heading.
        /// </summary>
        [Fact]
        public void ParseThreats_VelocityGivesSpeedAndHeading()
        {
            Threat threat = NeptunParser.ParseThreats("{\"threats\":[{\"id\":\"a\",\"lat\":1,\"lon\":2,\"velocity\":{\"bearingDeg\":42,\"speedKmh\":150}}]}")[0];

            Assert.Equal(150.0, threat.SpeedKmh);
            Assert.Equal(42.0, threat.HeadingDegrees);
        }

        /// <summary>
        /// Names that are not known get safe defaults.
        /// </summary>
        [Fact]
        public void ParseThreats_UnknownNamesGetDefaults()
        {
            Threat threat = NeptunParser.ParseThreats("{\"threats\":[{\"id\":\"a\",\"type\":\"newthing\",\"lat\":1,\"lon\":2,\"confidenceLevel\":\"??\",\"status\":\"??\"}]}")[0];

            Assert.Equal(ThreatKind.Unknown, threat.Kind);
            Assert.Equal(ThreatConfidence.Low, threat.Confidence);
            Assert.Equal(ThreatStatus.Active, threat.Status);
        }

        /// <summary>
        /// Every type name of the API has its kind.
        /// </summary>
        /// <param name="type">Type name.</param>
        /// <param name="expected">Expected kind.</param>
        [Theory]
        [InlineData("uav", ThreatKind.Uav)]
        [InlineData("fpv", ThreatKind.Fpv)]
        [InlineData("recon", ThreatKind.Recon)]
        [InlineData("missile", ThreatKind.Missile)]
        [InlineData("ballistic", ThreatKind.Ballistic)]
        [InlineData("kab", ThreatKind.Kab)]
        [InlineData("mig31k", ThreatKind.Mig31k)]
        [InlineData("UAV", ThreatKind.Uav)]
        [InlineData("unknown", ThreatKind.Unknown)]
        public void ParseThreats_MapsKinds(string type, ThreatKind expected)
        {
            Assert.Equal(expected, NeptunParser.ParseThreats("{\"threats\":[{\"id\":\"a\",\"type\":\"" + type + "\",\"lat\":1,\"lon\":2}]}")[0].Kind);
        }

        /// <summary>
        /// Threats without an identifier or with an invalid position are skipped, the valid ones stay.
        /// </summary>
        [Fact]
        public void ParseThreats_SkipsInvalidThreats()
        {
            string json = "{\"threats\":[{\"lat\":1,\"lon\":2},{\"id\":\"b\",\"lat\":91,\"lon\":2},{\"id\":\"c\",\"lat\":1,\"lon\":181},{\"id\":\"d\",\"lon\":2},5,{\"id\":\"ok\",\"lat\":1,\"lon\":2}]}";

            IReadOnlyList<Threat> threats = NeptunParser.ParseThreats(json);

            Assert.Single(threats);
            Assert.Equal("ok", threats[0].Id);
        }

        /// <summary>
        /// Invalid points of the trail are skipped and negative numbers are clamped.
        /// </summary>
        [Fact]
        public void ParseThreats_CleansTrailAndNumbers()
        {
            Threat threat = NeptunParser.ParseThreats("{\"threats\":[{\"id\":\"a\",\"lat\":1,\"lon\":2,\"sourceCount\":-5,\"uncertaintyKm\":-3,\"trail\":[{\"lat\":1,\"lon\":2},{\"lat\":99,\"lon\":2},\"x\"]}]}")[0];

            Assert.Single(threat.Trail);
            Assert.Null(threat.Trail[0].TimeUtc);
            Assert.Equal(0, threat.SourceCount);
            Assert.Equal(0.0, threat.UncertaintyKm);
        }

        /// <summary>
        /// Missing, empty and wrong documents give no threats, and a text that is not JSON throws.
        /// </summary>
        [Fact]
        public void ParseThreats_HandlesMissingAndWrongDocuments()
        {
            Assert.Empty(NeptunParser.ParseThreats(null));
            Assert.Empty(NeptunParser.ParseThreats(" "));
            Assert.Empty(NeptunParser.ParseThreats("{}"));
            Assert.Empty(NeptunParser.ParseThreats("[1,2]"));
            Assert.Empty(NeptunParser.ParseThreats("{\"threats\":5}"));
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => NeptunParser.ParseThreats("not json"));
        }

        /// <summary>
        /// Every kind of frame is recognized.
        /// </summary>
        [Fact]
        public void ParseFrame_RecognizesFrames()
        {
            NeptunFrame snapshot = NeptunParser.ParseFrame("{\"type\":\"snapshot\",\"ts\":\"x\",\"data\":{\"threats\":[" + FullThreat + "]}}");
            NeptunFrame upsert = NeptunParser.ParseFrame("{\"type\":\"upsert\",\"data\":" + FullThreat + "}");
            NeptunFrame remove = NeptunParser.ParseFrame("{\"type\":\"remove\",\"data\":{\"id\":\"trk_1\"}}");
            NeptunFrame heartbeat = NeptunParser.ParseFrame("{\"type\":\"heartbeat\",\"ts\":\"x\"}");
            NeptunFrame alerts = NeptunParser.ParseFrame("{\"type\":\"alerts\",\"data\":{\"raions\":[]}}");

            Assert.Equal(NeptunFrameKind.Snapshot, snapshot.Kind);
            Assert.Single(snapshot.Threats);
            Assert.Equal(NeptunFrameKind.Upsert, upsert.Kind);
            Assert.Equal("trk_1", upsert.Threats[0].Id);
            Assert.Equal(NeptunFrameKind.Remove, remove.Kind);
            Assert.Equal("trk_1", remove.RemovedId);
            Assert.Equal(NeptunFrameKind.Heartbeat, heartbeat.Kind);
            Assert.Equal(NeptunFrameKind.Alerts, alerts.Kind);
        }

        /// <summary>
        /// Frames that are damaged, incomplete or new are reported as unknown instead of throwing.
        /// </summary>
        /// <param name="json">Text of the frame.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("{\"type\":\"future\"}")]
        [InlineData("{\"type\":\"upsert\",\"data\":{\"id\":\"a\"}}")]
        [InlineData("{\"type\":\"upsert\"}")]
        [InlineData("{\"type\":\"remove\",\"data\":{}}")]
        [InlineData("{\"type\":\"remove\"}")]
        public void ParseFrame_UnknownOrDamaged_IsUnknown(string? json)
        {
            Assert.Equal(NeptunFrameKind.Unknown, NeptunParser.ParseFrame(json).Kind);
        }

        /// <summary>
        /// A snapshot without the list is an empty snapshot.
        /// </summary>
        [Fact]
        public void ParseFrame_SnapshotWithoutList_IsEmpty()
        {
            NeptunFrame frame = NeptunParser.ParseFrame("{\"type\":\"snapshot\"}");

            Assert.Equal(NeptunFrameKind.Snapshot, frame.Kind);
            Assert.Empty(frame.Threats);
        }
    }
}

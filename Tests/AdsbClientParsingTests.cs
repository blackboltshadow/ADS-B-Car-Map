using System.Net;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the field-level parsing of the ADS-B API response.
    /// </summary>
    public class AdsbClientParsingTests
    {
        /// <summary>
        /// Handler returning a fixed JSON body.
        /// </summary>
        private sealed class JsonHandler : HttpMessageHandler
        {
            /// <summary>
            /// Response body.
            /// </summary>
            private readonly string json;

            /// <summary>
            /// Creates the handler.
            /// </summary>
            /// <param name="json">Response body.</param>
            public JsonHandler(string json)
            {
                this.json = json;
            }

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
        }

        /// <summary>
        /// Parses a response body with the client.
        /// </summary>
        /// <param name="json">Response body.</param>
        /// <returns>Parsed aircraft.</returns>
        private static async Task<IReadOnlyList<Aircraft>> ParseAsync(string json)
        {
            AdsbClient client = new AdsbClient(new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://example.test/") });

            return await client.GetAircraftAsync(0.0, 0.0, 10.0, CancellationToken.None);
        }

        /// <summary>
        /// All supported fields are read from a complete record.
        /// </summary>
        [Fact]
        public async Task Parse_FullRecord_ReadsAllFields()
        {
            IReadOnlyList<Aircraft> result = await ParseAsync("{\"ac\":[{\"hex\":\"3c6444\",\"flight\":\"DLH4AB  \",\"lat\":50.1,\"lon\":8.6,\"alt_baro\":36000.4,\"gs\":455.5,\"track\":271.2,\"seen_pos\":2.5,\"t\":\" A320 \",\"r\":\"D-AIZZ\",\"category\":\"A3\",\"dbFlags\":1,\"baro_rate\":-1200.4,\"squawk\":\"1000\"}]}");

            Aircraft aircraft = Assert.Single(result);
            Assert.Equal("3c6444", aircraft.Hex);
            Assert.Equal("DLH4AB", aircraft.Callsign);
            Assert.Equal(50.1, aircraft.Latitude);
            Assert.Equal(8.6, aircraft.Longitude);
            Assert.Equal(36000, aircraft.AltitudeFeet);
            Assert.False(aircraft.OnGround);
            Assert.Equal(455.5, aircraft.GroundSpeedKnots);
            Assert.Equal(271.2, aircraft.TrackDegrees);
            Assert.Equal(2.5, aircraft.SecondsSincePosition);
            Assert.Equal("A320", aircraft.TypeCode);
            Assert.Equal("D-AIZZ", aircraft.Registration);
            Assert.Equal("A3", aircraft.Category);
            Assert.True(aircraft.IsMilitary);
            Assert.Equal(-1200, aircraft.VerticalRateFeetPerMinute);
            Assert.Equal("1000", aircraft.Squawk);
        }

        /// <summary>
        /// The string "ground" in the altitude field marks an aircraft on the ground without altitude.
        /// </summary>
        [Fact]
        public async Task Parse_GroundAltitude_SetsOnGround()
        {
            IReadOnlyList<Aircraft> result = await ParseAsync("{\"ac\":[{\"hex\":\"a1\",\"lat\":1,\"lon\":2,\"alt_baro\":\"ground\"}]}");

            Aircraft aircraft = Assert.Single(result);
            Assert.True(aircraft.OnGround);
            Assert.Null(aircraft.AltitudeFeet);
        }

        /// <summary>
        /// Missing optional fields stay null and empty strings are normalized to null.
        /// </summary>
        [Fact]
        public async Task Parse_MinimalRecord_LeavesOptionalFieldsNull()
        {
            IReadOnlyList<Aircraft> result = await ParseAsync("{\"ac\":[{\"hex\":\"a1\",\"lat\":1,\"lon\":2,\"flight\":\"   \",\"t\":\"\",\"r\":\" \",\"category\":\"\",\"squawk\":\"\"}]}");

            Aircraft aircraft = Assert.Single(result);
            Assert.Null(aircraft.Callsign);
            Assert.Null(aircraft.AltitudeFeet);
            Assert.Null(aircraft.GroundSpeedKnots);
            Assert.Null(aircraft.TrackDegrees);
            Assert.Null(aircraft.TypeCode);
            Assert.Null(aircraft.Registration);
            Assert.Null(aircraft.Category);
            Assert.Null(aircraft.Squawk);
            Assert.Null(aircraft.VerticalRateFeetPerMinute);
            Assert.False(aircraft.IsMilitary);
            Assert.Equal(0.0, aircraft.SecondsSincePosition);
        }

        /// <summary>
        /// The true heading replaces the track, and the geometric rate replaces the barometric one.
        /// </summary>
        [Fact]
        public async Task Parse_FallbackFields_AreUsed()
        {
            IReadOnlyList<Aircraft> result = await ParseAsync("{\"ac\":[{\"hex\":\"a1\",\"lat\":1,\"lon\":2,\"true_heading\":45.0,\"geom_rate\":640}]}");

            Aircraft aircraft = Assert.Single(result);
            Assert.Equal(45.0, aircraft.TrackDegrees);
            Assert.Equal(640, aircraft.VerticalRateFeetPerMinute);
        }

        /// <summary>
        /// Only the lowest bit of dbFlags marks a military aircraft.
        /// </summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        public async Task Parse_DbFlags_LowestBitMeansMilitary(int flags, bool expected)
        {
            IReadOnlyList<Aircraft> result = await ParseAsync("{\"ac\":[{\"hex\":\"a1\",\"lat\":1,\"lon\":2,\"dbFlags\":" + flags + "}]}");

            Assert.Equal(expected, Assert.Single(result).IsMilitary);
        }

        /// <summary>
        /// Records without hex or coordinates are dropped.
        /// </summary>
        [Theory]
        [InlineData("{\"ac\":[{\"lat\":1,\"lon\":2}]}")]
        [InlineData("{\"ac\":[{\"hex\":\"\",\"lat\":1,\"lon\":2}]}")]
        [InlineData("{\"ac\":[{\"hex\":\"a1\",\"lon\":2}]}")]
        [InlineData("{\"ac\":[{\"hex\":\"a1\",\"lat\":1}]}")]
        [InlineData("{\"ac\":[{\"hex\":\"a1\",\"lat\":\"x\",\"lon\":2}]}")]
        public async Task Parse_IncompleteRecord_IsDropped(string json)
        {
            Assert.Empty(await ParseAsync(json));
        }

        /// <summary>
        /// A body without the aircraft list gives an empty result.
        /// </summary>
        [Theory]
        [InlineData("{}")]
        [InlineData("{\"ac\":null}")]
        [InlineData("{\"ac\":{}}")]
        public async Task Parse_NoAircraftArray_IsEmpty(string json)
        {
            Assert.Empty(await ParseAsync(json));
        }
    }
}

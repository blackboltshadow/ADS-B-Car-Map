using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of aircraft kind classification.
    /// </summary>
    public class AircraftClassifierTests
    {
        /// <summary>
        /// Creates an aircraft with only the fields relevant to classification.
        /// </summary>
        /// <param name="typeCode">ICAO type code or null.</param>
        /// <param name="category">ADS-B emitter category or null.</param>
        /// <param name="isMilitary">Military flag.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(string? typeCode, string? category, bool isMilitary = false)
        {
            return new Aircraft("abc123", null, 52.0, 21.0, 10000, false, 400.0, 90.0, 1.0, typeCode, null, category, isMilitary);
        }

        /// <summary>
        /// Without any data the kind is unknown.
        /// </summary>
        [Fact]
        public void Classify_NoData_IsUnknown()
        {
            Assert.Equal(AircraftKind.Unknown, AircraftClassifier.Classify(Create(null, null)));
        }

        /// <summary>
        /// Category A0 carries no information.
        /// </summary>
        [Fact]
        public void Classify_CategoryA0WithoutType_IsUnknown()
        {
            Assert.Equal(AircraftKind.Unknown, AircraftClassifier.Classify(Create(null, "A0")));
        }

        /// <summary>
        /// Emitter categories map to kinds.
        /// </summary>
        [Theory]
        [InlineData("A1", AircraftKind.LightPlane)]
        [InlineData("A2", AircraftKind.SmallJet)]
        [InlineData("A3", AircraftKind.Airliner)]
        [InlineData("A5", AircraftKind.Heavy)]
        [InlineData("A6", AircraftKind.Fighter)]
        [InlineData("A7", AircraftKind.Helicopter)]
        [InlineData("B1", AircraftKind.Glider)]
        [InlineData("B2", AircraftKind.Balloon)]
        [InlineData("B6", AircraftKind.Drone)]
        [InlineData("C1", AircraftKind.Ground)]
        public void Classify_ByCategory(string category, AircraftKind expected)
        {
            Assert.Equal(expected, AircraftClassifier.Classify(Create(null, category)));
        }

        /// <summary>
        /// Type codes map to kinds, regardless of letter case.
        /// </summary>
        [Theory]
        [InlineData("B738", AircraftKind.Airliner)]
        [InlineData("b738", AircraftKind.Airliner)]
        [InlineData("C172", AircraftKind.LightPlane)]
        [InlineData("B06", AircraftKind.Helicopter)]
        [InlineData("TWR", AircraftKind.Ground)]
        [InlineData("GLID", AircraftKind.Glider)]
        public void Classify_ByTypeCode(string typeCode, AircraftKind expected)
        {
            Assert.Equal(expected, AircraftClassifier.Classify(Create(typeCode, null)));
        }

        /// <summary>
        /// A military heavy transport without a known type is a military transport.
        /// </summary>
        [Fact]
        public void Classify_MilitaryLargeCategory_IsMilitaryTransport()
        {
            Assert.Equal(AircraftKind.MilitaryTransport, AircraftClassifier.Classify(Create(null, "A5", true)));
        }

        /// <summary>
        /// The same category without the military flag stays a heavy aircraft.
        /// </summary>
        [Fact]
        public void Classify_CivilLargeCategory_IsHeavy()
        {
            Assert.Equal(AircraftKind.Heavy, AircraftClassifier.Classify(Create(null, "A5", false)));
        }

        /// <summary>
        /// The classification exposed by the aircraft agrees with the static method.
        /// </summary>
        [Fact]
        public void Kind_MatchesClassify()
        {
            Aircraft aircraft = Create("C172", "A1");

            Assert.Equal(AircraftClassifier.Classify(aircraft), aircraft.Kind);
            Assert.Equal(aircraft.Kind, aircraft.Kind);
        }

        /// <summary>
        /// Every real kind has a display name and the unknown kind has none.
        /// </summary>
        [Fact]
        public void GetDisplayName_CoversAllKinds()
        {
            foreach (AircraftKind kind in Enum.GetValues<AircraftKind>())
            {
                string name = AircraftClassifier.GetDisplayName(kind);

                if (kind == AircraftKind.Unknown)
                {
                    Assert.Equal(string.Empty, name);
                }
                else
                {
                    Assert.NotEqual(string.Empty, name);
                }
            }
        }
    }
}

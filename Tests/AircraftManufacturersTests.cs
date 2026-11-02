using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the aircraft type to manufacturer table.
    /// </summary>
    public class AircraftManufacturersTests
    {
        /// <summary>
        /// Common types resolve to the expected manufacturer.
        /// </summary>
        [Theory]
        [InlineData("B738", "Boeing")]
        [InlineData("B38M", "Boeing")]
        [InlineData("B77W", "Boeing")]
        [InlineData("A320", "Airbus")]
        [InlineData("A21N", "Airbus")]
        [InlineData("A359", "Airbus")]
        [InlineData("BCS3", "Airbus")]
        [InlineData("E190", "Embraer")]
        [InlineData("E295", "Embraer")]
        [InlineData("E55P", "Embraer")]
        [InlineData("AT76", "ATR")]
        [InlineData("DH8D", "De Havilland Canada")]
        [InlineData("CRJ9", "Bombardier")]
        [InlineData("GLEX", "Bombardier")]
        [InlineData("GLF6", "Gulfstream")]
        [InlineData("C172", "Cessna")]
        [InlineData("C560", "Cessna")]
        [InlineData("PA28", "Piper")]
        [InlineData("PC12", "Pilatus")]
        [InlineData("PC21", "Pilatus")]
        [InlineData("SR22", "Cirrus")]
        [InlineData("EC35", "Airbus Helicopters")]
        [InlineData("H145", "Airbus Helicopters")]
        [InlineData("H25B", "Hawker")]
        [InlineData("MIG2", "Mikoyan")]
        [InlineData("MI8", "Mil")]
        [InlineData("SU95", "Sukhoi")]
        [InlineData("MD11", "McDonnell Douglas")]
        public void Find_ReturnsManufacturerOfCommonTypes(string typeCode, string expected)
        {
            Assert.Equal(expected, AircraftManufacturers.Find(typeCode));
        }

        /// <summary>
        /// Exact codes override a prefix rule that would assign another manufacturer.
        /// </summary>
        [Theory]
        [InlineData("C130", "Lockheed Martin")]
        [InlineData("C295", "Airbus")]
        [InlineData("C160", "Transall")]
        [InlineData("A124", "Antonov")]
        [InlineData("A225", "Antonov")]
        [InlineData("A400", "Airbus")]
        [InlineData("A109", "Leonardo")]
        [InlineData("FA18", "Boeing")]
        [InlineData("B190", "Beechcraft")]
        [InlineData("B06", "Bell")]
        [InlineData("SF50", "Cirrus")]
        [InlineData("ASTR", "IAI")]
        public void Find_ExactCodeOverridesPrefix(string typeCode, string expected)
        {
            Assert.Equal(expected, AircraftManufacturers.Find(typeCode));
        }

        /// <summary>
        /// Lookup ignores letter case and surrounding spaces.
        /// </summary>
        [Fact]
        public void Find_IgnoresCaseAndSpaces()
        {
            Assert.Equal("Boeing", AircraftManufacturers.Find(" b738 "));
        }

        /// <summary>
        /// Unknown, empty and missing codes give no manufacturer.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ZZZZ")]
        [InlineData("C46")]
        public void Find_ReturnsNullForUnknownType(string? typeCode)
        {
            Assert.Null(AircraftManufacturers.Find(typeCode));
        }

        /// <summary>
        /// The manufacturer comparison ignores letter case and fails for unknown types.
        /// </summary>
        [Fact]
        public void IsMadeBy_ComparesManufacturer()
        {
            Assert.True(AircraftManufacturers.IsMadeBy("B738", "boeing"));
            Assert.False(AircraftManufacturers.IsMadeBy("A320", "Boeing"));
            Assert.False(AircraftManufacturers.IsMadeBy(null, "Boeing"));
        }

        /// <summary>
        /// The list of names is sorted, has no duplicates and contains the main manufacturers.
        /// </summary>
        [Fact]
        public void Names_AreSortedAndDistinct()
        {
            IReadOnlyList<string> names = AircraftManufacturers.Names;

            Assert.Contains("Boeing", names);
            Assert.Contains("Airbus", names);
            Assert.Equal(names.Count, new HashSet<string>(names).Count);

            List<string> sorted = new List<string>(names);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            Assert.Equal(sorted, new List<string>(names));
        }
    }
}

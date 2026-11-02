using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of filling the missing descriptive data of an aircraft.
    /// </summary>
    public class AircraftMergerTests
    {
        /// <summary>
        /// Creates an aircraft with the given descriptive data.
        /// </summary>
        /// <param name="typeCode">Type code or null.</param>
        /// <param name="registration">Registration or null.</param>
        /// <param name="category">Category or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(string? typeCode, string? registration, string? category)
        {
            return new Aircraft("abc123", "LOT1", 52.0, 21.0, 35000, false, 450.0, 90.0, 1.5, typeCode, registration, category, true, -500, "1000");
        }

        /// <summary>
        /// A complete aircraft has no gaps, and "A0" or an empty category counts as missing.
        /// </summary>
        [Fact]
        public void HasGaps_DetectsEachMissingField()
        {
            Assert.False(AircraftMerger.HasGaps(Create("B738", "SP-LWA", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create(null, "SP-LWA", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create("B738", "", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create("B738", "SP-LWA", "A0")));
            Assert.True(AircraftMerger.HasGaps(Create("B738", "SP-LWA", null)));
        }

        /// <summary>
        /// Missing fields are taken from the details and everything else stays untouched.
        /// </summary>
        [Fact]
        public void FillMissing_FillsGapsAndKeepsTheRest()
        {
            Aircraft original = Create(null, null, "A0");

            Aircraft filled = AircraftMerger.FillMissing(original, new AircraftDetails("abc123", "A320", "D-AIZZ", "A3"));

            Assert.Equal("A320", filled.TypeCode);
            Assert.Equal("D-AIZZ", filled.Registration);
            Assert.Equal("A3", filled.Category);
            Assert.Equal("LOT1", filled.Callsign);
            Assert.Equal(52.0, filled.Latitude);
            Assert.Equal(35000, filled.AltitudeFeet);
            Assert.True(filled.IsMilitary);
            Assert.Equal(-500, filled.VerticalRateFeetPerMinute);
            Assert.Equal("1000", filled.Squawk);
        }

        /// <summary>
        /// Data that the aircraft already has is never replaced.
        /// </summary>
        [Fact]
        public void FillMissing_DoesNotReplaceExistingData()
        {
            Aircraft original = Create("B738", "SP-LWA", "A3");

            Aircraft result = AircraftMerger.FillMissing(original, new AircraftDetails("abc123", "A320", "D-AIZZ", "A5"));

            Assert.Same(original, result);
        }

        /// <summary>
        /// When the details add nothing the same object is returned.
        /// </summary>
        [Fact]
        public void FillMissing_NothingToAdd_ReturnsSameObject()
        {
            Aircraft original = Create(null, null, null);

            Assert.Same(original, AircraftMerger.FillMissing(original, new AircraftDetails("abc123", null, null, "A0")));
        }
    }
}

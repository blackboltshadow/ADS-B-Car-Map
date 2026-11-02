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
        /// <param name="typeName">Full type name or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(string? typeCode, string? registration, string? category, string? typeName = "Full name")
        {
            return new Aircraft("abc123", "LOT1", 52.0, 21.0, 35000, false, 450.0, 90.0, 1.5, typeCode, registration, category, true, -500, "1000", typeName);
        }

        /// <summary>
        /// A complete aircraft has no gaps. The type code, the registration and the full type name decide; the category alone does not, because most sources do not know it.
        /// </summary>
        [Fact]
        public void HasGaps_DetectsEachMissingField()
        {
            Assert.False(AircraftMerger.HasGaps(Create("B738", "SP-LWA", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create(null, "SP-LWA", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create("B738", "", "A3")));
            Assert.True(AircraftMerger.HasGaps(Create("B738", "SP-LWA", "A3", null)));
            Assert.False(AircraftMerger.HasGaps(Create("B738", "SP-LWA", "A0")));
            Assert.False(AircraftMerger.HasGaps(Create("B738", "SP-LWA", null)));
        }

        /// <summary>
        /// The full type name, the manufacturer and the owner are filled from the details and never replaced.
        /// </summary>
        [Fact]
        public void FillMissing_FillsNameManufacturerAndOwner()
        {
            Aircraft original = Create("B350", "SP-TPU", "A2", null);

            Aircraft filled = AircraftMerger.FillMissing(original, new AircraftDetails("abc123", "B350", "SP-TPU", null, "King Air B350", "Beechcraft Corp", "Polish Air Navigation Services Agency"));
            Aircraft again = AircraftMerger.FillMissing(filled, new AircraftDetails("abc123", null, null, null, "Other name", "Other maker", "Other owner"));

            Assert.Equal("King Air B350", filled.TypeName);
            Assert.Equal("Beechcraft Corp", filled.Manufacturer);
            Assert.Equal("Polish Air Navigation Services Agency", filled.Owner);
            Assert.Same(filled, again);
        }

        /// <summary>
        /// Two sets of details are merged keeping what the first one has.
        /// </summary>
        [Fact]
        public void Combine_KeepsFirstAndFillsGaps()
        {
            AircraftDetails first = new AircraftDetails("abc123", "B350", null, "A0", "King Air B350");
            AircraftDetails second = new AircraftDetails("abc123", "XXXX", "SP-TPU", "A2", "Other", "Beechcraft Corp", "Owner");

            AircraftDetails combined = AircraftMerger.Combine(first, second);

            Assert.Equal("B350", combined.TypeCode);
            Assert.Equal("SP-TPU", combined.Registration);
            Assert.Equal("A2", combined.Category);
            Assert.Equal("King Air B350", combined.TypeName);
            Assert.Equal("Beechcraft Corp", combined.Manufacturer);
            Assert.Same(first, AircraftMerger.Combine(first, new AircraftDetails("abc123", null, null, null)));
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

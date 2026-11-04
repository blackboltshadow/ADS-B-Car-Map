using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the history of the selected and watched aircraft.
    /// </summary>
    public class AircraftHistoryTests
    {
        /// <summary>
        /// Time used as the start in the tests.
        /// </summary>
        private static readonly DateTime Start = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A repeated notice within the gap updates the time but does not count again; after the gap it counts.
        /// </summary>
        [Fact]
        public void Record_CountsOnlyAfterTheGap()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("48A124", "LOT1", null, null, Start, false);
            history.Record("48a124", "LOT1", null, null, Start.AddMinutes(5), false);
            history.Record("48a124", "LOT1", null, null, Start.AddMinutes(30), false);

            AircraftHistoryEntry entry = Assert.Single(history.GetForDisplay());

            Assert.Equal("48a124", entry.Hex);
            Assert.Equal(2, entry.Count);
            Assert.Equal(Start, entry.FirstSeenUtc);
            Assert.Equal(Start.AddMinutes(30), entry.LastSeenUtc);
        }

        /// <summary>
        /// Empty details do not erase the known ones and new details replace them.
        /// </summary>
        [Fact]
        public void Record_KeepsKnownDetails()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("abc123", "LOT1", "SP-LWA", "B738", Start, false);
            history.Record("abc123", null, " ", "B739", Start.AddMinutes(1), true);

            AircraftHistoryEntry entry = history.GetForDisplay()[0];

            Assert.Equal("LOT1", entry.Callsign);
            Assert.Equal("SP-LWA", entry.Registration);
            Assert.Equal("B739", entry.TypeCode);
            Assert.True(entry.Watched);
        }

        /// <summary>
        /// An empty address is ignored.
        /// </summary>
        [Fact]
        public void Record_IgnoresEmptyHex()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record(" ", null, null, null, Start, false);

            Assert.Equal(0, history.Count);
        }

        /// <summary>
        /// The limit drops the least recently seen unpinned entries and never the pinned ones.
        /// </summary>
        [Fact]
        public void Capacity_DropsOldestUnpinned()
        {
            AircraftHistory history = new AircraftHistory(2);

            history.Record("a1", null, null, null, Start, false);
            history.SetPinned("a1", true);
            history.Record("a2", null, null, null, Start.AddMinutes(1), false);
            history.Record("a3", null, null, null, Start.AddMinutes(2), false);
            history.Record("a4", null, null, null, Start.AddMinutes(3), false);

            List<string> hexes = history.GetForDisplay().Select((AircraftHistoryEntry entry) => entry.Hex).ToList();

            Assert.Equal(new List<string> { "a1", "a4", "a3" }, hexes);
        }

        /// <summary>
        /// Pinned entries come first in the display order, then the most recently seen.
        /// </summary>
        [Fact]
        public void GetForDisplay_PinnedFirst()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("old", null, null, null, Start, false);
            history.Record("new", null, null, null, Start.AddHours(1), false);
            history.SetPinned("old", true);

            Assert.Equal(new List<string> { "old", "new" }, history.GetForDisplay().Select((AircraftHistoryEntry entry) => entry.Hex).ToList());
        }

        /// <summary>
        /// Clearing removes the unpinned entries and Remove removes one entry, including a pinned one.
        /// </summary>
        [Fact]
        public void Clear_KeepsPinned_RemoveDeletes()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("a1", null, null, null, Start, false);
            history.Record("a2", null, null, null, Start, false);
            history.SetPinned("a1", true);
            history.ClearUnpinned();

            Assert.Equal(new List<string> { "a1" }, history.GetForDisplay().Select((AircraftHistoryEntry entry) => entry.Hex).ToList());

            history.Remove("A1");

            Assert.Equal(0, history.Count);
        }

        /// <summary>
        /// MarkWatched sets the flag only for a known aircraft.
        /// </summary>
        [Fact]
        public void MarkWatched_SetsFlag()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("a1", null, null, null, Start, false);
            history.MarkWatched("A1");
            history.MarkWatched("zz");

            Assert.True(history.GetForDisplay()[0].Watched);
            Assert.Equal(1, history.Count);
        }

        /// <summary>
        /// The text format keeps all the fields; damaged text gives an empty history and damaged entries are skipped.
        /// </summary>
        [Fact]
        public void SerializeParse_RoundTripAndDamagedText()
        {
            AircraftHistory history = new AircraftHistory();

            history.Record("a1", "LOT1", "SP-LWA", "B738", Start, true);
            history.Record("a1", "LOT1", "SP-LWA", "B738", Start.AddHours(1), false);
            history.SetPinned("a1", true);
            history.Record("a2", null, null, null, Start.AddHours(2), false);

            AircraftHistory loaded = AircraftHistory.Parse(history.Serialize());
            AircraftHistoryEntry first = loaded.GetForDisplay()[0];

            Assert.Equal(2, loaded.Count);
            Assert.Equal("a1", first.Hex);
            Assert.Equal("SP-LWA", first.Registration);
            Assert.Equal("B738", first.TypeCode);
            Assert.Equal(2, first.Count);
            Assert.True(first.Pinned);
            Assert.True(first.Watched);
            Assert.Equal(Start, first.FirstSeenUtc);
            Assert.Equal(Start.AddHours(1), first.LastSeenUtc);

            Assert.Equal(0, AircraftHistory.Parse("not json").Count);
            Assert.Equal(0, AircraftHistory.Parse(null).Count);
            Assert.Equal(0, AircraftHistory.Parse("{}").Count);
            Assert.Equal(0, AircraftHistory.Parse("[1, {\"hex\":\"a\"}, {\"hex\":\"\",\"first\":1,\"last\":1}]").Count);
        }
    }
}

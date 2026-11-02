using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the log of aircraft types missing from the type table.
    /// </summary>
    public class MissingTypeLogTests
    {
        /// <summary>
        /// Moment used as the current time.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Table that knows only the 737-800.
        /// </summary>
        private static readonly AircraftTypeTable Table = AircraftTypeTable.Parse(new string[] { "iata\ticao\tfamily\tname", "738\tB738\tB737\tBoeing 737-800" });

        /// <summary>
        /// Creates an aircraft.
        /// </summary>
        /// <param name="hex">ICAO address.</param>
        /// <param name="typeCode">Type code or null.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <param name="military">Whether the aircraft is military.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string hex, string? typeCode, string? callsign = "LOT281", bool military = false)
        {
            return new Aircraft(hex, callsign, 52.0, 21.0, 35000, false, 450.0, 90.0, 0.0, typeCode, "SP-XYZ", "A3", military);
        }

        /// <summary>
        /// Known types and aircraft without a type are not logged.
        /// </summary>
        [Fact]
        public void Record_IgnoresKnownAndMissingTypes()
        {
            MissingTypeLog log = new MissingTypeLog();
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("1", "B738"), CreateAircraft("2", "b738"), CreateAircraft("3", null), CreateAircraft("4", "  ") };

            Assert.False(log.Record(aircraft, Table, Now));
            Assert.Equal(0, log.Count);
        }

        /// <summary>
        /// An unknown type is logged once with an example aircraft, and every aircraft is counted once.
        /// </summary>
        [Fact]
        public void Record_CountsEachAircraftOnce()
        {
            MissingTypeLog log = new MissingTypeLog();
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("1", "e190"), CreateAircraft("2", "E190", "ENT12", true) };

            Assert.True(log.Record(aircraft, Table, Now));
            Assert.False(log.Record(aircraft, Table, Now.AddMinutes(1)));

            MissingTypeEntry entry = Assert.Single(log.GetEntries());

            Assert.Equal("E190", entry.Icao);
            Assert.Equal(2, entry.Count);
            Assert.Equal("LOT281", entry.Callsign);
            Assert.Equal("SP-XYZ", entry.Registration);
            Assert.Equal("A3", entry.Category);
            Assert.True(entry.IsMilitary);
            Assert.Equal(Now, entry.FirstSeenUtc);
            Assert.Equal(Now, entry.LastSeenUtc);
        }

        /// <summary>
        /// A new aircraft of a logged type moves the last seen time and changes the log.
        /// </summary>
        [Fact]
        public void Record_NewAircraftUpdatesLastSeen()
        {
            MissingTypeLog log = new MissingTypeLog();

            log.Record(new List<Aircraft> { CreateAircraft("1", "E190") }, Table, Now);

            Assert.True(log.Record(new List<Aircraft> { CreateAircraft("2", "E190") }, Table, Now.AddHours(1)));

            MissingTypeEntry entry = Assert.Single(log.GetEntries());

            Assert.Equal(Now, entry.FirstSeenUtc);
            Assert.Equal(Now.AddHours(1), entry.LastSeenUtc);
        }

        /// <summary>
        /// The most frequent type comes first, equal counts are ordered by the code.
        /// </summary>
        [Fact]
        public void GetEntries_SortsByCountThenCode()
        {
            MissingTypeLog log = new MissingTypeLog();

            log.Record(new List<Aircraft> { CreateAircraft("1", "ZZZ1"), CreateAircraft("2", "AAA1"), CreateAircraft("3", "MMM1"), CreateAircraft("4", "MMM1") }, Table, Now);

            Assert.Equal(new string[] { "MMM1", "AAA1", "ZZZ1" }, log.GetEntries().Select((MissingTypeEntry entry) => entry.Icao).ToArray());
        }

        /// <summary>
        /// The log stops growing at the limit of distinct types.
        /// </summary>
        [Fact]
        public void Record_StopsAtMaximumEntries()
        {
            MissingTypeLog log = new MissingTypeLog();
            List<Aircraft> aircraft = new List<Aircraft>();

            for (int index = 0; index < MissingTypeLog.MaxEntries + 10; index++)
            {
                aircraft.Add(CreateAircraft(index.ToString(), "T" + index));
            }

            log.Record(aircraft, Table, Now);

            Assert.Equal(MissingTypeLog.MaxEntries, log.Count);
        }

        /// <summary>
        /// Clearing removes everything, and an aircraft seen earlier is counted again.
        /// </summary>
        [Fact]
        public void Clear_RemovesEntriesAndForgetsCountedAircraft()
        {
            MissingTypeLog log = new MissingTypeLog();
            List<Aircraft> aircraft = new List<Aircraft> { CreateAircraft("1", "E190") };

            log.Record(aircraft, Table, Now);
            log.Clear();

            Assert.Equal(0, log.Count);
            Assert.True(log.Record(aircraft, Table, Now));
            Assert.Equal(1, Assert.Single(log.GetEntries()).Count);
        }

        /// <summary>
        /// Saving and loading keeps every field, and damaged lines are skipped.
        /// </summary>
        [Fact]
        public void SerializeAndLoad_RoundTrip()
        {
            MissingTypeLog log = new MissingTypeLog();

            log.Record(new List<Aircraft> { CreateAircraft("1", "E190", "ENT12", true), CreateAircraft("2", "E190") }, Table, Now);

            MissingTypeLog restored = new MissingTypeLog();

            restored.Load(log.Serialize() + "broken\nX\tnot-a-number\t1\t2\t\t\t\t0\n");

            MissingTypeEntry entry = Assert.Single(restored.GetEntries());

            Assert.Equal("E190", entry.Icao);
            Assert.Equal(2, entry.Count);
            Assert.Equal("ENT12", entry.Callsign);
            Assert.True(entry.IsMilitary);
            Assert.Equal(Now, entry.FirstSeenUtc);
            Assert.Equal(DateTimeKind.Utc, entry.FirstSeenUtc.Kind);
        }

        /// <summary>
        /// Loading a missing or empty text leaves an empty log.
        /// </summary>
        [Fact]
        public void Load_EmptyText_ClearsLog()
        {
            MissingTypeLog log = new MissingTypeLog();

            log.Record(new List<Aircraft> { CreateAircraft("1", "E190") }, Table, Now);
            log.Load(null);

            Assert.Equal(0, log.Count);
        }

        /// <summary>
        /// Tabs and line breaks in the example data cannot break the saved text.
        /// </summary>
        [Fact]
        public void Record_CleansSeparatorsInExampleData()
        {
            MissingTypeLog log = new MissingTypeLog();

            log.Record(new List<Aircraft> { CreateAircraft("1", "E190", "AB\tC\n1") }, Table, Now);

            MissingTypeLog restored = new MissingTypeLog();

            restored.Load(log.Serialize());

            Assert.Equal("AB C 1", Assert.Single(restored.GetEntries()).Callsign);
        }
    }
}

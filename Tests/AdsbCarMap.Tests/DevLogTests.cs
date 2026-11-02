using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the developer log.
    /// </summary>
    public class DevLogTests
    {
        /// <summary>
        /// Moment used as the current time.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a log that records everything and merges identical events.
        /// </summary>
        /// <returns>The log.</returns>
        private static DevLog CreateLog()
        {
            DevLog log = new DevLog();
            log.Configure(DevLogLevel.Info, 100, true);

            return log;
        }

        /// <summary>
        /// Creates an exception with a stack trace.
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <returns>The thrown exception.</returns>
        private static Exception CreateException(string message)
        {
            try
            {
                throw new InvalidOperationException(message);
            }
            catch (InvalidOperationException exception)
            {
                return exception;
            }
        }

        /// <summary>
        /// The default level records warnings and errors but not information.
        /// </summary>
        [Fact]
        public void Add_DefaultLevel_SkipsInformation()
        {
            DevLog log = new DevLog();

            Assert.False(log.Add(DevLogLevel.Info, "Tag", "info", null, Now));
            Assert.True(log.Add(DevLogLevel.Warning, "Tag", "warning", null, Now));
            Assert.True(log.Add(DevLogLevel.Error, "Tag", "error", null, Now));
            Assert.Equal(2, log.Count);
        }

        /// <summary>
        /// Each choice of the level setting maps to the expected minimum level.
        /// </summary>
        [Theory]
        [InlineData(0, DevLogLevel.Error)]
        [InlineData(1, DevLogLevel.Warning)]
        [InlineData(2, DevLogLevel.Info)]
        [InlineData(-5, DevLogLevel.Error)]
        [InlineData(9, DevLogLevel.Info)]
        public void GetMinimumLevel_MapsChoice(int index, DevLogLevel expected)
        {
            Assert.Equal(expected, DevLog.GetMinimumLevel(index));
        }

        /// <summary>
        /// Each choice of the size setting maps to a number of entries, and a wrong index is limited.
        /// </summary>
        [Fact]
        public void GetMaxEntries_MapsChoiceAndLimitsIndex()
        {
            Assert.Equal(100, DevLog.GetMaxEntries(0));
            Assert.Equal(500, DevLog.GetMaxEntries(2));
            Assert.Equal(2000, DevLog.GetMaxEntries(4));
            Assert.Equal(100, DevLog.GetMaxEntries(-1));
            Assert.Equal(2000, DevLog.GetMaxEntries(99));
        }

        /// <summary>
        /// Only errors are recorded at the strictest level.
        /// </summary>
        [Fact]
        public void Add_ErrorsOnly_SkipsWarnings()
        {
            DevLog log = new DevLog();
            log.Configure(DevLogLevel.Error, 100, true);

            Assert.False(log.IsEnabled(DevLogLevel.Warning));
            Assert.False(log.Add(DevLogLevel.Warning, "Tag", "warning", null, Now));
            Assert.True(log.IsEnabled(DevLogLevel.Error));
            Assert.True(log.Add(DevLogLevel.Error, "Tag", "error", null, Now));
        }

        /// <summary>
        /// Identical events are merged into one entry with a counter and the time of the last occurrence.
        /// </summary>
        [Fact]
        public void Add_Deduplicate_MergesIdenticalEvents()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now);
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now.AddMinutes(5));

            DevLogEntry entry = Assert.Single(log.GetEntries());

            Assert.Equal(2, entry.Count);
            Assert.Equal(Now, entry.FirstUtc);
            Assert.Equal(Now.AddMinutes(5), entry.LastUtc);
        }

        /// <summary>
        /// Events that differ in level, tag or message are not merged.
        /// </summary>
        [Fact]
        public void Add_Deduplicate_KeepsDifferentEventsApart()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now);
            log.Add(DevLogLevel.Error, "Tiles", "timeout", null, Now);
            log.Add(DevLogLevel.Warning, "Search", "timeout", null, Now);
            log.Add(DevLogLevel.Warning, "Tiles", "other", null, Now);

            Assert.Equal(4, log.Count);
        }

        /// <summary>
        /// The same message with different exceptions is not merged, so that no stack trace is lost.
        /// </summary>
        [Fact]
        public void Add_Deduplicate_KeepsDifferentExceptionsApart()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Error, "Map", "failure", CreateException("first"), Now);
            log.Add(DevLogLevel.Error, "Map", "failure", CreateException("second"), Now);

            Assert.Equal(2, log.Count);
        }

        /// <summary>
        /// An event that repeats moves to the end of the list, so it is the most recent one.
        /// </summary>
        [Fact]
        public void Add_Deduplicate_MovesRepeatedEventToTheNewest()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Warning, "A", "first", null, Now);
            log.Add(DevLogLevel.Warning, "B", "second", null, Now.AddMinutes(1));
            log.Add(DevLogLevel.Warning, "A", "first", null, Now.AddMinutes(2));

            List<DevLogEntry> entries = log.GetEntries();

            Assert.Equal("A", entries[0].Tag);
            Assert.Equal("B", entries[1].Tag);
        }

        /// <summary>
        /// With the merging switched off every event is a separate entry.
        /// </summary>
        [Fact]
        public void Add_NoDeduplication_KeepsEveryEvent()
        {
            DevLog log = new DevLog();
            log.Configure(DevLogLevel.Info, 100, false);

            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now);
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now.AddMinutes(1));

            Assert.Equal(2, log.Count);
            Assert.All(log.GetEntries(), (DevLogEntry entry) => Assert.Equal(1, entry.Count));
        }

        /// <summary>
        /// The oldest entries are dropped when the limit is exceeded.
        /// </summary>
        [Fact]
        public void Add_OverLimit_DropsOldest()
        {
            DevLog log = new DevLog();
            log.Configure(DevLogLevel.Info, 3, false);

            for (int index = 0; index < 5; index++)
            {
                log.Add(DevLogLevel.Warning, "Tag", "message " + index, null, Now.AddMinutes(index));
            }

            List<DevLogEntry> entries = log.GetEntries();

            Assert.Equal(3, entries.Count);
            Assert.Equal("message 4", entries[0].Message);
            Assert.Equal("message 2", entries[2].Message);
        }

        /// <summary>
        /// Lowering the limit removes the oldest entries at once and tells the listeners.
        /// </summary>
        [Fact]
        public void Configure_LowerLimit_TrimsAndRaisesChanged()
        {
            DevLog log = CreateLog();

            for (int index = 0; index < 10; index++)
            {
                log.Add(DevLogLevel.Warning, "Tag", "message " + index, null, Now);
            }

            int raised = 0;
            log.Changed += () => raised++;
            log.Configure(DevLogLevel.Info, 4, true);

            Assert.Equal(4, log.Count);
            Assert.Equal("message 9", log.GetEntries()[0].Message);
            Assert.Equal(1, raised);
        }

        /// <summary>
        /// A limit below one is raised to one, and a configuration that removes nothing does not raise the event.
        /// </summary>
        [Fact]
        public void Configure_ZeroLimit_KeepsOneEntryAndNoEventWithoutChange()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Warning, "Tag", "one", null, Now);

            int raised = 0;
            log.Changed += () => raised++;
            log.Configure(DevLogLevel.Info, 0, true);

            Assert.Equal(1, log.Count);
            Assert.Equal(0, raised);
        }

        /// <summary>
        /// Adding an entry and clearing the log raise the event, clearing an empty log does not.
        /// </summary>
        [Fact]
        public void Changed_RaisedByAddAndClear()
        {
            DevLog log = CreateLog();
            int raised = 0;
            log.Changed += () => raised++;

            log.Add(DevLogLevel.Warning, "Tag", "message", null, Now);
            log.Clear();
            log.Clear();

            Assert.Equal(2, raised);
            Assert.Equal(0, log.Count);
        }

        /// <summary>
        /// The text of the exception is stored with the stack trace.
        /// </summary>
        [Fact]
        public void Add_WithException_StoresDetails()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Error, "Map", "failure", CreateException("boom"), Now);

            DevLogEntry entry = Assert.Single(log.GetEntries());

            Assert.Contains("InvalidOperationException", entry.Details);
            Assert.Contains("boom", entry.Details);
            Assert.Contains("CreateException", entry.Details);
        }

        /// <summary>
        /// Very long texts are cut.
        /// </summary>
        [Fact]
        public void Add_LongTexts_AreCut()
        {
            DevLog log = CreateLog();

            log.Add(DevLogLevel.Warning, new string('t', 500), new string('m', 5000), CreateException(new string('x', 10000)), Now);

            DevLogEntry entry = Assert.Single(log.GetEntries());

            Assert.Equal(100, entry.Tag.Length);
            Assert.Equal(DevLog.MaxMessageLength, entry.Message.Length);
            Assert.Equal(DevLog.MaxDetailsLength, entry.Details.Length);
        }

        /// <summary>
        /// The copy returned by the log does not change the log.
        /// </summary>
        [Fact]
        public void GetEntries_ReturnsCopies()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Warning, "Tag", "message", null, Now);

            log.GetEntries()[0].Count = 99;

            Assert.Equal(1, log.GetEntries()[0].Count);
        }

        /// <summary>
        /// A saved log is restored with all its fields, including line breaks, tabs and backslashes.
        /// </summary>
        [Fact]
        public void SerializeAndLoad_RoundTrip()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Error, "Map", "a\ttab and a \\ backslash\nsecond line", CreateException("boom"), Now);
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now.AddMinutes(1));
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now.AddMinutes(2));

            DevLog restored = CreateLog();
            restored.Load(log.Serialize());

            List<DevLogEntry> expected = log.GetEntries();
            List<DevLogEntry> actual = restored.GetEntries();

            Assert.Equal(expected.Count, actual.Count);

            for (int index = 0; index < expected.Count; index++)
            {
                Assert.Equal(expected[index].Level, actual[index].Level);
                Assert.Equal(expected[index].Tag, actual[index].Tag);
                Assert.Equal(expected[index].Message, actual[index].Message);
                Assert.Equal(expected[index].Details.Replace("\r", string.Empty), actual[index].Details);
                Assert.Equal(expected[index].Count, actual[index].Count);
                Assert.Equal(expected[index].FirstUtc, actual[index].FirstUtc);
                Assert.Equal(expected[index].LastUtc, actual[index].LastUtc);
            }
        }

        /// <summary>
        /// Lines that cannot be read are skipped, and a missing text gives an empty log.
        /// </summary>
        [Fact]
        public void Load_SkipsBrokenLinesAndAcceptsEmptyText()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Warning, "Tag", "message", null, Now);
            string valid = log.Serialize();

            DevLog restored = CreateLog();
            restored.Load(valid + "broken\n9\tTag\t1\t" + Now.ToString("o") + "\t" + Now.ToString("o") + "\tmessage\t\nx\tTag\tnot-a-number\ta\tb\tmessage\t\n");

            Assert.Equal(1, restored.Count);

            restored.Load(null);
            Assert.Equal(0, restored.Count);

            restored.Load(valid);
            restored.Load("   ");
            Assert.Equal(0, restored.Count);
        }

        /// <summary>
        /// Loading keeps only the newest entries when the saved log is longer than the limit.
        /// </summary>
        [Fact]
        public void Load_OverLimit_KeepsNewest()
        {
            DevLog source = new DevLog();
            source.Configure(DevLogLevel.Info, 100, false);

            for (int index = 0; index < 6; index++)
            {
                source.Add(DevLogLevel.Warning, "Tag", "message " + index, null, Now);
            }

            DevLog restored = new DevLog();
            restored.Configure(DevLogLevel.Info, 2, false);
            restored.Load(source.Serialize());

            List<DevLogEntry> entries = restored.GetEntries();

            Assert.Equal(2, entries.Count);
            Assert.Equal("message 5", entries[0].Message);
            Assert.Equal("message 4", entries[1].Message);
        }

        /// <summary>
        /// The report starts with the given lines and lists the entries from the oldest to the newest.
        /// </summary>
        [Fact]
        public void FormatReport_ContainsHeaderAndEntriesInOrder()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now);
            log.Add(DevLogLevel.Warning, "Tiles", "timeout", null, Now.AddMinutes(3));
            log.Add(DevLogLevel.Error, "Map", "first line\nsecond line", CreateException("boom"), Now.AddMinutes(4));

            string report = log.FormatReport(new List<string> { "Wersja: 0.4.0", "Telefon: Test" }, true);

            Assert.StartsWith("Wersja: 0.4.0\nTelefon: Test\n", report);
            Assert.Contains("Wpisów: 2", report);
            Assert.Contains("[2026-11-02 10:03:00Z] WARN Tiles ×2 (od 2026-11-02 10:00:00Z)", report);
            Assert.Contains("[2026-11-02 10:04:00Z] ERROR Map", report);
            Assert.Contains("  first line\n  second line", report);
            Assert.Contains("  System.InvalidOperationException: boom", report);
            Assert.True(report.IndexOf("WARN Tiles", StringComparison.Ordinal) < report.IndexOf("ERROR Map", StringComparison.Ordinal));
        }

        /// <summary>
        /// The report without details omits the stack traces.
        /// </summary>
        [Fact]
        public void FormatReport_WithoutDetails_OmitsStackTraces()
        {
            DevLog log = CreateLog();
            log.Add(DevLogLevel.Error, "Map", "failure", CreateException("boom"), Now);

            string report = log.FormatReport(new List<string>(), false);

            Assert.Contains("failure", report);
            Assert.DoesNotContain("InvalidOperationException", report);
        }

        /// <summary>
        /// The names of the levels are the ones used in the report.
        /// </summary>
        [Fact]
        public void GetLevelName_ReturnsReportNames()
        {
            Assert.Equal("INFO", DevLog.GetLevelName(DevLogLevel.Info));
            Assert.Equal("WARN", DevLog.GetLevelName(DevLogLevel.Warning));
            Assert.Equal("ERROR", DevLog.GetLevelName(DevLogLevel.Error));
        }
    }
}

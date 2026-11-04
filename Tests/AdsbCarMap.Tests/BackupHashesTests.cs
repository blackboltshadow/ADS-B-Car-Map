using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the fingerprints that tell which data the system backup already contains.
    /// </summary>
    public class BackupHashesTests
    {
        /// <summary>
        /// The fingerprint has 16 hexadecimal digits, is the same for the same text and differs for another text.
        /// </summary>
        [Fact]
        public void Hash_IsStableAndSensitive()
        {
            string first = BackupHashes.Hash("zażółć");

            Assert.Equal(16, first.Length);
            Assert.Matches("^[0-9a-f]{16}$", first);
            Assert.Equal(first, BackupHashes.Hash("zażółć"));
            Assert.NotEqual(first, BackupHashes.Hash("zażółć "));
            Assert.NotEqual(BackupHashes.Hash(string.Empty), first);
        }

        /// <summary>
        /// The fingerprints of sections are given by identifier and depend only on the content.
        /// </summary>
        [Fact]
        public void OfSections_ByIdentifier()
        {
            Dictionary<string, string> hashes = BackupHashes.OfSections(new List<BackupSection>
            {
                new BackupSection("settings", "a"),
                new BackupSection("alerts", "b")
            });

            Assert.Equal(2, hashes.Count);
            Assert.Equal(BackupHashes.Hash("a"), hashes["settings"]);
            Assert.Equal(BackupHashes.Hash("b"), hashes["alerts"]);
        }

        /// <summary>
        /// Fingerprints are written in the order of the catalog, read back, and unknown categories and damaged pairs are dropped.
        /// </summary>
        [Fact]
        public void FormatAndParse_RoundTripAndDamage()
        {
            Dictionary<string, string> hashes = new Dictionary<string, string>
            {
                ["alerts"] = "bbbb",
                ["unknown"] = "cccc",
                ["settings"] = "aaaa"
            };

            string text = BackupHashes.Format(hashes);

            Assert.Equal("settings=aaaa,alerts=bbbb", text);

            Dictionary<string, string> loaded = BackupHashes.Parse(text);

            Assert.Equal(2, loaded.Count);
            Assert.Equal("aaaa", loaded["settings"]);
            Assert.Empty(BackupHashes.Parse(null));
            Assert.Empty(BackupHashes.Parse(string.Empty));
            Assert.Equal(new List<string> { "settings" }, BackupHashes.Parse("=x,settings=1,alerts=,bogus=2,noequals").Keys.ToList());
        }

        /// <summary>
        /// When the system has taken nothing yet, every chosen category waits.
        /// </summary>
        [Fact]
        public void FindPending_NothingDelivered_AllWait()
        {
            Dictionary<string, string> current = new Dictionary<string, string> { ["alerts"] = "1", ["settings"] = "2" };

            Assert.Equal(new List<string> { "settings", "alerts" }, BackupHashes.FindPending(current, null));
            Assert.Equal(new List<string> { "settings", "alerts" }, BackupHashes.FindPending(current, new Dictionary<string, string>()));
        }

        /// <summary>
        /// Only categories whose data changed, or that are new, wait; the order is the order of the catalog.
        /// </summary>
        [Fact]
        public void FindPending_ChangedAndNewWait()
        {
            Dictionary<string, string> delivered = new Dictionary<string, string> { ["settings"] = "1", ["alerts"] = "2", ["favorites"] = "3" };
            Dictionary<string, string> current = new Dictionary<string, string> { ["settings"] = "1", ["alerts"] = "changed", ["favorites"] = "3", ["warnings"] = "new" };

            Assert.Equal(new List<string> { "alerts", "warnings" }, BackupHashes.FindPending(current, delivered));
            Assert.Empty(BackupHashes.FindPending(delivered, delivered));
        }

        /// <summary>
        /// A category that the system holds but that is not chosen any more waits too, because the backup still has its data.
        /// </summary>
        [Fact]
        public void FindPending_RemovedCategoryWaits()
        {
            Dictionary<string, string> delivered = new Dictionary<string, string> { ["settings"] = "1", ["aircraftHistory"] = "2" };
            Dictionary<string, string> current = new Dictionary<string, string> { ["settings"] = "1" };

            Assert.Equal(new List<string> { "aircraftHistory" }, BackupHashes.FindPending(current, delivered));
        }

        /// <summary>
        /// The state is up to date only when a backup is made and nothing waits.
        /// </summary>
        [Fact]
        public void SyncState_IsUpToDate()
        {
            Assert.True(new BackupSyncState(true, true, new List<string>()).IsUpToDate);
            Assert.False(new BackupSyncState(true, true, new List<string> { "alerts" }).IsUpToDate);
            Assert.False(new BackupSyncState(false, true, new List<string>()).IsUpToDate);
        }
    }
}

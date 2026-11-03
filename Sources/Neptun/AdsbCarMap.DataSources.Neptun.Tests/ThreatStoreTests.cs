using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of the list of the threats built from the stream.
    /// </summary>
    public class ThreatStoreTests
    {
        /// <summary>
        /// Creates a threat.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="status">State of the track.</param>
        /// <param name="latitude">Latitude.</param>
        /// <returns>The threat.</returns>
        public static Threat Create(string id, ThreatStatus status = ThreatStatus.Active, double latitude = 50.0)
        {
            return new Threat(id, ThreatKind.Uav, "t", string.Empty, string.Empty, string.Empty, latitude, 30.0, null, null, ThreatConfidence.Medium, 1, 0, null, null, status, 5.0, false, false, false, false, false, string.Empty, new List<ThreatTrailPoint>());
        }

        /// <summary>
        /// A snapshot replaces everything and leaves out resolved threats.
        /// </summary>
        [Fact]
        public void ApplySnapshot_ReplacesAllAndSkipsResolved()
        {
            ThreatStore store = new ThreatStore();

            store.ApplySnapshot(new List<Threat> { Create("a"), Create("b") });
            store.ApplySnapshot(new List<Threat> { Create("c"), Create("d", ThreatStatus.Resolved) });

            Assert.Single(store.Threats);
            Assert.Equal("c", store.Threats[0].Id);
        }

        /// <summary>
        /// An upsert adds a new threat and replaces the old one of the same identifier.
        /// </summary>
        [Fact]
        public void Upsert_AddsAndReplaces()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(Create("a", ThreatStatus.Active, 50.0));
            store.Upsert(Create("b"));
            store.Upsert(Create("a", ThreatStatus.Stale, 51.0));

            Assert.Equal(2, store.Threats.Count);
            Assert.Equal(51.0, store.Threats.First(threat => threat.Id == "a").Latitude);
            Assert.Equal(ThreatStatus.Stale, store.Threats.First(threat => threat.Id == "a").Status);
        }

        /// <summary>
        /// An upsert of a resolved threat removes it.
        /// </summary>
        [Fact]
        public void Upsert_Resolved_Removes()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(Create("a"));
            store.Upsert(Create("a", ThreatStatus.Resolved));

            Assert.Empty(store.Threats);
        }

        /// <summary>
        /// Removing and clearing work, and removing an unknown identifier changes nothing.
        /// </summary>
        [Fact]
        public void RemoveAndClear_Work()
        {
            ThreatStore store = new ThreatStore();
            int changes = 0;

            store.Changed += () => changes++;
            store.ApplySnapshot(new List<Threat> { Create("a"), Create("b") });
            store.Remove("zzz");
            store.Remove("a");
            store.Clear();
            store.Clear();

            Assert.Empty(store.Threats);
            Assert.Equal(3, changes);
        }

        /// <summary>
        /// A list that was handed out does not change afterwards.
        /// </summary>
        [Fact]
        public void Threats_ListIsNotModifiedLater()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(Create("a"));

            IReadOnlyList<Threat> before = store.Threats;

            store.Upsert(Create("b"));

            Assert.Single(before);
            Assert.Equal(2, store.Threats.Count);
        }
    }
}

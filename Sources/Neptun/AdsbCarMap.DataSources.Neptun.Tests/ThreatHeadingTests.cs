using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of working out the heading of a threat from its movement, and of the calculations it uses.
    /// </summary>
    public class ThreatHeadingTests
    {
        /// <summary>
        /// Creates a threat without a heading at the given position.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <returns>The threat.</returns>
        private static Threat At(double latitude, double longitude = 30.0)
        {
            return ThreatStoreTests.Create("a", ThreatStatus.Active, latitude) with { Longitude = longitude };
        }

        /// <summary>
        /// A threat seen for the first time has no heading.
        /// </summary>
        [Fact]
        public void FirstSighting_HasNoHeading()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0));

            Assert.Null(store.Threats[0].HeadingDegrees);
            Assert.False(store.Threats[0].IsHeadingDerived);
        }

        /// <summary>
        /// A threat that moved north gets a heading of about 0, one that moved east about 90.
        /// </summary>
        [Fact]
        public void Movement_GivesHeading()
        {
            ThreatStore north = new ThreatStore();

            north.Upsert(At(50.0));
            north.Upsert(At(50.05));

            Assert.True(north.Threats[0].IsHeadingDerived);
            Assert.True(north.Threats[0].HeadingDegrees < 1.0 || north.Threats[0].HeadingDegrees > 359.0);

            ThreatStore east = new ThreatStore();

            east.Upsert(At(50.0, 30.0));
            east.Upsert(At(50.0, 30.1));

            Assert.InRange(east.Threats[0].HeadingDegrees!.Value, 89.0, 91.0);
        }

        /// <summary>
        /// A move shorter than the minimum gives no heading.
        /// </summary>
        [Fact]
        public void ShortMove_GivesNoHeading()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0));
            store.Upsert(At(50.005));

            Assert.Null(store.Threats[0].HeadingDegrees);
        }

        /// <summary>
        /// A slow threat is measured from the position where the heading was last worked out, so small steps add up.
        /// </summary>
        [Fact]
        public void SmallSteps_AddUp()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0));
            store.Upsert(At(50.007));

            Assert.Null(store.Threats[0].HeadingDegrees);

            store.Upsert(At(50.014));

            Assert.True(store.Threats[0].IsHeadingDerived);
        }

        /// <summary>
        /// A derived heading is kept while the threat stays where it is.
        /// </summary>
        [Fact]
        public void DerivedHeading_IsKeptWhileThreatStays()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0));
            store.Upsert(At(50.05));

            double? derived = store.Threats[0].HeadingDegrees;

            store.Upsert(At(50.05));
            store.Upsert(At(50.0501));

            Assert.Equal(derived, store.Threats[0].HeadingDegrees);
            Assert.True(store.Threats[0].IsHeadingDerived);
        }

        /// <summary>
        /// A heading given by the source is never replaced.
        /// </summary>
        [Fact]
        public void SourceHeading_IsKept()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0) with { HeadingDegrees = 123.0 });
            store.Upsert(At(50.05) with { HeadingDegrees = 123.0 });

            Assert.Equal(123.0, store.Threats[0].HeadingDegrees);
            Assert.False(store.Threats[0].IsHeadingDerived);
        }

        /// <summary>
        /// The trail of a new threat gives the heading at once.
        /// </summary>
        [Fact]
        public void Trail_GivesHeadingAtFirstSight()
        {
            ThreatStore store = new ThreatStore();
            List<ThreatTrailPoint> trail = new List<ThreatTrailPoint> { new ThreatTrailPoint(50.0, 30.0, null), new ThreatTrailPoint(50.0, 30.1, null) };

            store.Upsert(At(50.0, 30.1) with { Trail = trail });

            Assert.True(store.Threats[0].IsHeadingDerived);
            Assert.InRange(store.Threats[0].HeadingDegrees!.Value, 89.0, 91.0);
        }

        /// <summary>
        /// A threat of which only the region is known never gets a heading.
        /// </summary>
        [Fact]
        public void AreaOnly_NeverGetsHeading()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0) with { AreaOnly = true });
            store.Upsert(At(51.0) with { AreaOnly = true });

            Assert.Null(store.Threats[0].HeadingDegrees);
        }

        /// <summary>
        /// A snapshot keeps the memory of the earlier positions, so a heading appears after the next snapshot too.
        /// </summary>
        [Fact]
        public void Snapshots_KeepMemory()
        {
            ThreatStore store = new ThreatStore();

            store.ApplySnapshot(new List<Threat> { At(50.0) });
            store.ApplySnapshot(new List<Threat> { At(50.05) });

            Assert.True(store.Threats[0].IsHeadingDerived);
        }

        /// <summary>
        /// A removed threat is forgotten, so a threat that comes back is new.
        /// </summary>
        [Fact]
        public void Removed_IsForgotten()
        {
            ThreatStore store = new ThreatStore();

            store.Upsert(At(50.0));
            store.Remove("a");
            store.Upsert(At(52.0));

            Assert.Null(store.Threats[0].HeadingDegrees);

            store.ApplySnapshot(new List<Threat>());
            store.Upsert(At(54.0));

            Assert.Null(store.Threats[0].HeadingDegrees);
        }

        /// <summary>
        /// The distance and the bearing are calculated on a sphere.
        /// </summary>
        [Fact]
        public void ThreatGeo_CalculatesDistanceAndBearing()
        {
            Assert.InRange(ThreatGeo.DistanceKilometers(50.0, 30.0, 51.0, 30.0), 111.0, 111.4);
            Assert.Equal(0.0, ThreatGeo.DistanceKilometers(50.0, 30.0, 50.0, 30.0), 6);
            Assert.InRange(ThreatGeo.BearingDegrees(0.0, 0.0, 0.0, 1.0), 89.99, 90.01);
            Assert.InRange(ThreatGeo.BearingDegrees(0.0, 0.0, -1.0, 0.0), 179.99, 180.01);
            Assert.InRange(ThreatGeo.BearingDegrees(0.0, 0.0, 0.0, -1.0), 269.99, 270.01);
            Assert.InRange(ThreatGeo.BearingDegrees(0.0, 0.0, 1.0, -1.0), 314.0, 316.0);
        }
    }
}

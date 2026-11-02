using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the aircraft trail history.
    /// </summary>
    public class TrailTrackerTests
    {
        /// <summary>
        /// Reference time of the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Trail duration used in the tests.
        /// </summary>
        private static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Creates an aircraft at the given position.
        /// </summary>
        private static Aircraft Create(string hex, double latitude, double longitude, double secondsSincePosition = 0.0)
        {
            return new Aircraft(hex, null, latitude, longitude, 10000, false, 300.0, 90.0, secondsSincePosition, null, null);
        }

        /// <summary>
        /// Creates a trail point at the given position and age.
        /// </summary>
        private static TrailPoint Point(double latitude, double longitude, int ageMinutes)
        {
            return new TrailPoint(latitude, longitude, 10000, false, Now - TimeSpan.FromMinutes(ageMinutes));
        }

        /// <summary>
        /// A new aircraft gets a one-point trail stamped with the age of its position.
        /// </summary>
        [Fact]
        public void Update_NewAircraft_StartsTrail()
        {
            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(new Dictionary<string, TrailPoint[]>(), new List<Aircraft> { Create("a", 50.0, 20.0, 5.0) }, Now, Duration, 100);

            Assert.Single(result["a"]);
            Assert.Equal(Now - TimeSpan.FromSeconds(5.0), result["a"][0].TimestampUtc);
        }

        /// <summary>
        /// A moved aircraft gets a new point appended.
        /// </summary>
        [Fact]
        public void Update_MovedAircraft_AppendsPoint()
        {
            Dictionary<string, TrailPoint[]> previous = new Dictionary<string, TrailPoint[]> { ["a"] = new TrailPoint[] { Point(50.0, 20.0, 1) } };

            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(previous, new List<Aircraft> { Create("a", 50.1, 20.0) }, Now, Duration, 100);

            Assert.Equal(2, result["a"].Length);
            Assert.Equal(50.1, result["a"][1].Latitude);
        }

        /// <summary>
        /// An aircraft that has not moved adds no point.
        /// </summary>
        [Fact]
        public void Update_UnmovedAircraft_DoesNotAppendPoint()
        {
            Dictionary<string, TrailPoint[]> previous = new Dictionary<string, TrailPoint[]> { ["a"] = new TrailPoint[] { Point(50.0, 20.0, 1) } };

            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(previous, new List<Aircraft> { Create("a", 50.0, 20.0) }, Now, Duration, 100);

            Assert.Single(result["a"]);
        }

        /// <summary>
        /// Points older than the trail duration are dropped.
        /// </summary>
        [Fact]
        public void Update_OldPoints_AreDropped()
        {
            Dictionary<string, TrailPoint[]> previous = new Dictionary<string, TrailPoint[]> { ["a"] = new TrailPoint[] { Point(49.0, 20.0, 30), Point(50.0, 20.0, 1) } };

            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(previous, new List<Aircraft> { Create("a", 50.0, 20.0) }, Now, Duration, 100);

            Assert.Single(result["a"]);
            Assert.Equal(50.0, result["a"][0].Latitude);
        }

        /// <summary>
        /// The point limit removes the oldest points.
        /// </summary>
        [Fact]
        public void Update_PointLimit_RemovesOldest()
        {
            Dictionary<string, TrailPoint[]> previous = new Dictionary<string, TrailPoint[]> { ["a"] = new TrailPoint[] { Point(50.0, 20.0, 3), Point(50.1, 20.0, 2), Point(50.2, 20.0, 1) } };

            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(previous, new List<Aircraft> { Create("a", 50.3, 20.0) }, Now, Duration, 2);

            Assert.Equal(2, result["a"].Length);
            Assert.Equal(50.2, result["a"][0].Latitude);
            Assert.Equal(50.3, result["a"][1].Latitude);
        }

        /// <summary>
        /// A missing aircraft keeps only its recent points, and loses the trail when none remain.
        /// </summary>
        [Fact]
        public void Update_MissingAircraft_KeepsRecentPointsOnly()
        {
            Dictionary<string, TrailPoint[]> previous = new Dictionary<string, TrailPoint[]>
            {
                ["recent"] = new TrailPoint[] { Point(50.0, 20.0, 30), Point(50.1, 20.0, 2) },
                ["stale"] = new TrailPoint[] { Point(40.0, 10.0, 30) }
            };

            IReadOnlyDictionary<string, TrailPoint[]> result = TrailTracker.Update(previous, new List<Aircraft>(), Now, Duration, 100);

            Assert.Single(result["recent"]);
            Assert.False(result.ContainsKey("stale"));
        }
    }
}

using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the memory of flight information that expires and is kept in a file.
    /// </summary>
    public class TimedCacheTests
    {
        /// <summary>
        /// Time returned to the cache.
        /// </summary>
        private DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// How long a found item stays valid.
        /// </summary>
        private TimeSpan maxAge = TimeSpan.FromHours(24);

        /// <summary>
        /// Creates a cache over the controlled clock.
        /// </summary>
        /// <param name="maxEntries">Number of items above which the oldest ones are removed.</param>
        /// <returns>The cache.</returns>
        private TimedCache<FlightRoute> Create(int maxEntries = 100)
        {
            return new TimedCache<FlightRoute>(() => maxAge, TimeSpan.FromHours(6), () => now, maxEntries);
        }

        /// <summary>
        /// Creates a route for the tests.
        /// </summary>
        /// <returns>The route.</returns>
        private static FlightRoute CreateRoute()
        {
            return new FlightRoute("WAW", "Warsaw", "JFK", "New York", "LOT Polish Airlines", 52.16, 20.97, 40.64, -73.78);
        }

        /// <summary>
        /// A stored item is returned until it expires and the key does not depend on the letter case.
        /// </summary>
        [Fact]
        public void TryGet_ReturnsItemUntilItExpires()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("LOT281", CreateRoute());

            Assert.True(cache.TryGet("lot281", out FlightRoute? route));
            Assert.Equal("WAW", route!.OriginCode);

            now += TimeSpan.FromHours(23);

            Assert.True(cache.TryGet("LOT281", out _));

            now += TimeSpan.FromHours(2);

            Assert.False(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// The retention is read at every use, so a changed setting applies to items already stored.
        /// </summary>
        [Fact]
        public void TryGet_UsesCurrentRetention()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("LOT281", CreateRoute());
            now += TimeSpan.FromHours(5);

            maxAge = TimeSpan.FromHours(4);

            Assert.False(cache.TryGet("LOT281", out _));

            maxAge = TimeSpan.FromHours(48);

            Assert.True(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// A stored null is a valid answer "unknown", but it expires sooner than a found item.
        /// </summary>
        [Fact]
        public void TryGet_NullExpiresSooner()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("XYZ1", null);

            Assert.True(cache.TryGet("XYZ1", out FlightRoute? route));
            Assert.Null(route);

            now += TimeSpan.FromHours(7);

            Assert.False(cache.TryGet("XYZ1", out _));
        }

        /// <summary>
        /// A short retention also shortens the time for unknown items.
        /// </summary>
        [Fact]
        public void TryGet_NullNeverOutlivesRetention()
        {
            maxAge = TimeSpan.FromHours(1);
            TimedCache<FlightRoute> cache = Create();
            cache.Set("XYZ1", null);
            now += TimeSpan.FromMinutes(61);

            Assert.False(cache.TryGet("XYZ1", out _));
        }

        /// <summary>
        /// Remove, Clear and Prune delete items.
        /// </summary>
        [Fact]
        public void RemoveClearAndPrune_DeleteItems()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("A", CreateRoute());
            cache.Set("B", CreateRoute());
            now += TimeSpan.FromHours(10);
            cache.Set("C", CreateRoute());

            Assert.True(cache.Remove("A"));
            Assert.False(cache.Remove("A"));
            Assert.Equal(2, cache.Count);

            now += TimeSpan.FromHours(20);

            Assert.Equal(1, cache.Prune());
            Assert.Equal(1, cache.Count);

            cache.Clear();

            Assert.Equal(0, cache.Count);
        }

        /// <summary>
        /// Above the limit the oldest items are removed.
        /// </summary>
        [Fact]
        public void Set_AboveLimit_RemovesOldest()
        {
            TimedCache<FlightRoute> cache = Create(10);

            for (int i = 0; i < 11; i++)
            {
                now += TimeSpan.FromSeconds(1);
                cache.Set("K" + i, CreateRoute());
            }

            Assert.True(cache.Count <= 10);
            Assert.False(cache.TryGet("K0", out _));
            Assert.True(cache.TryGet("K10", out _));
        }

        /// <summary>
        /// Items written to the file are loaded by another cache, including the stored nulls, and the expired ones are skipped.
        /// </summary>
        [Fact]
        public void Flush_ThenAttach_RestoresValidItems()
        {
            string path = Path.Combine(Path.GetTempPath(), "timedcache-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                TimedCache<FlightRoute> first = Create();
                first.AttachFile(path, TimeSpan.FromMilliseconds(1));
                first.Set("OLD", CreateRoute());
                now += TimeSpan.FromHours(30);
                first.Set("LOT281", CreateRoute());
                first.Set("XYZ1", null);
                first.Flush();

                TimedCache<FlightRoute> second = Create();
                second.AttachFile(path, TimeSpan.FromMilliseconds(1));

                Assert.True(second.TryGet("LOT281", out FlightRoute? route));
                Assert.Equal("JFK", route!.DestinationCode);
                Assert.Equal(52.16, route.OriginLatitude);
                Assert.True(second.TryGet("XYZ1", out FlightRoute? unknown));
                Assert.Null(unknown);
                Assert.False(second.TryGet("OLD", out _));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A damaged file is ignored and the cache starts empty.
        /// </summary>
        [Fact]
        public void AttachFile_DamagedFile_StartsEmpty()
        {
            string path = Path.Combine(Path.GetTempPath(), "timedcache-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{ this is not json");

            try
            {
                TimedCache<FlightRoute> cache = Create();
                cache.AttachFile(path, TimeSpan.FromMilliseconds(1));

                Assert.Equal(0, cache.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A change is written to the file by itself after the delay.
        /// </summary>
        [Fact]
        public async Task Set_WithAttachedFile_WritesFileAfterDelay()
        {
            string path = Path.Combine(Path.GetTempPath(), "timedcache-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                TimedCache<FlightRoute> cache = Create();
                cache.AttachFile(path, TimeSpan.FromMilliseconds(20));
                cache.Set("LOT281", CreateRoute());

                for (int i = 0; i < 100 && !File.Exists(path); i++)
                {
                    await Task.Delay(20);
                }

                Assert.True(File.Exists(path));
                Assert.Contains("LOT281", File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Touching an item counts the time from the last sighting, so a visible flight outlives the retention.
        /// </summary>
        [Fact]
        public void Touch_ExtendsLifeFromLastSighting()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("LOT281", CreateRoute());

            now += TimeSpan.FromHours(20);

            Assert.True(cache.Touch("lot281"));

            now += TimeSpan.FromHours(20);

            Assert.True(cache.TryGet("LOT281", out _));

            now += TimeSpan.FromHours(5);

            Assert.False(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// Touching does nothing for a missing or an expired item.
        /// </summary>
        [Fact]
        public void Touch_MissingOrExpired_ReturnsFalse()
        {
            TimedCache<FlightRoute> cache = Create();

            Assert.False(cache.Touch("LOT281"));

            cache.Set("LOT281", CreateRoute());
            now += TimeSpan.FromHours(25);

            Assert.False(cache.Touch("LOT281"));
            Assert.False(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// A closed item expires after the grace period even though the retention is much longer.
        /// </summary>
        [Fact]
        public void Close_ExpiresAfterGraceNotRetention()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("LOT281", CreateRoute());

            Assert.True(cache.Close("LOT281", TimeSpan.FromMinutes(2)));

            now += TimeSpan.FromMinutes(1);

            Assert.True(cache.TryGet("LOT281", out _));

            now += TimeSpan.FromMinutes(2);

            Assert.False(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// Closing again does not postpone the end, and touching cancels the closing.
        /// </summary>
        [Fact]
        public void Close_KeepsEarlierEnd_AndTouchCancelsIt()
        {
            TimedCache<FlightRoute> cache = Create();
            cache.Set("LOT281", CreateRoute());
            cache.Close("LOT281", TimeSpan.FromMinutes(2));
            now += TimeSpan.FromMinutes(1);
            cache.Close("LOT281", TimeSpan.FromMinutes(30));
            now += TimeSpan.FromSeconds(90);

            Assert.False(cache.TryGet("LOT281", out _));

            cache.Set("LOT281", CreateRoute());
            cache.Close("LOT281", TimeSpan.FromMinutes(2));
            cache.Touch("LOT281");
            now += TimeSpan.FromMinutes(10);

            Assert.True(cache.TryGet("LOT281", out _));
        }

        /// <summary>
        /// Closing a missing item does nothing.
        /// </summary>
        [Fact]
        public void Close_Missing_ReturnsFalse()
        {
            Assert.False(Create().Close("LOT281", TimeSpan.FromMinutes(2)));
        }

        /// <summary>
        /// The closing survives a write to the file and a load.
        /// </summary>
        [Fact]
        public void Close_IsKeptInTheFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "timedcache-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                TimedCache<FlightRoute> first = Create();
                first.AttachFile(path, TimeSpan.FromMilliseconds(1));
                first.Set("LOT281", CreateRoute());
                first.Close("LOT281", TimeSpan.FromMinutes(2));
                first.Flush();

                now += TimeSpan.FromMinutes(5);
                TimedCache<FlightRoute> second = Create();
                second.AttachFile(path, TimeSpan.FromMilliseconds(1));

                Assert.False(second.TryGet("LOT281", out _));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

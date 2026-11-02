using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Selection;
using Xunit;

namespace AdsbCarMap.DataSources.Tests
{
    /// <summary>
    /// Tests of choosing an available data source.
    /// </summary>
    public class DataSourceRegistryTests
    {
        /// <summary>
        /// Source with a block time set by the test.
        /// </summary>
        private sealed class FakeSource : IDataSource
        {
            /// <summary>
            /// Creates a source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            /// <param name="block">Remaining block, zero when the source is available.</param>
            public FakeSource(string name, TimeSpan block)
            {
                Name = name;
                Block = block;
            }

            /// <inheritdoc />
            public string Name { get; }

            /// <summary>
            /// Remaining block reported by the source.
            /// </summary>
            public TimeSpan Block { get; set; }

            /// <inheritdoc />
            public RateLimitStatus GetRateLimitStatus()
            {
                return new RateLimitStatus(DateTime.MinValue, Block, Block > TimeSpan.Zero ? 1 : 0);
            }
        }

        /// <summary>
        /// An empty list is rejected.
        /// </summary>
        [Fact]
        public void Constructor_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DataSourceRegistry<FakeSource>(new List<FakeSource>()));
        }

        /// <summary>
        /// The most preferred available source is returned.
        /// </summary>
        [Fact]
        public void GetFirstAvailable_SkipsBlocked()
        {
            FakeSource first = new FakeSource("a", TimeSpan.FromSeconds(10));
            FakeSource second = new FakeSource("b", TimeSpan.Zero);
            FakeSource third = new FakeSource("c", TimeSpan.Zero);
            DataSourceRegistry<FakeSource> registry = new DataSourceRegistry<FakeSource>(new List<FakeSource> { first, second, third });

            Assert.Same(second, registry.GetFirstAvailable());
            Assert.Same(third, registry.GetFirstAvailable(second));
            Assert.Equal(2, registry.GetAvailable().Count);
        }

        /// <summary>
        /// The alternative is the next available source other than the current one.
        /// </summary>
        [Fact]
        public void HasAlternative_ReflectsOtherSources()
        {
            FakeSource first = new FakeSource("a", TimeSpan.Zero);
            FakeSource second = new FakeSource("b", TimeSpan.FromSeconds(30));
            DataSourceRegistry<FakeSource> registry = new DataSourceRegistry<FakeSource>(new List<FakeSource> { first, second });

            Assert.False(registry.HasAlternative(first));
            Assert.True(registry.HasAlternative(second));

            second.Block = TimeSpan.Zero;

            Assert.True(registry.HasAlternative(first));
        }

        /// <summary>
        /// When every source is blocked nothing is returned and the shortest wait is reported.
        /// </summary>
        [Fact]
        public void AllBlocked_ReturnsNullAndShortestWait()
        {
            FakeSource first = new FakeSource("a", TimeSpan.FromSeconds(40));
            FakeSource second = new FakeSource("b", TimeSpan.FromSeconds(12));
            DataSourceRegistry<FakeSource> registry = new DataSourceRegistry<FakeSource>(new List<FakeSource> { first, second });

            Assert.Null(registry.GetFirstAvailable());
            Assert.Empty(registry.GetAvailable());
            Assert.Equal(TimeSpan.FromSeconds(12), registry.GetTimeToAnyAvailable());
        }

        /// <summary>
        /// The wait is zero when any source is available.
        /// </summary>
        [Fact]
        public void GetTimeToAnyAvailable_SourceAvailable_IsZero()
        {
            DataSourceRegistry<FakeSource> registry = new DataSourceRegistry<FakeSource>(new List<FakeSource> { new FakeSource("a", TimeSpan.FromSeconds(5)), new FakeSource("b", TimeSpan.Zero) });

            Assert.Equal(TimeSpan.Zero, registry.GetTimeToAnyAvailable());
        }
    }
}

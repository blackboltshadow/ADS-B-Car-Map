using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the map tile cache stored on the disk.
    /// </summary>
    public sealed class TileDiskCacheTests : IDisposable
    {
        /// <summary>
        /// Temporary directory of the cache under test.
        /// </summary>
        private readonly string directory = Path.Combine(Path.GetTempPath(), "adsbcarmap-tiles-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Deletes the temporary directory.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// A missing tile is reported as null.
        /// </summary>
        [Fact]
        public void TryRead_MissingTile_ReturnsNull()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);

            byte[]? data = cache.TryRead(new TileKey(5, 1, 2), out DateTime writtenUtc);

            Assert.Null(data);
            Assert.Equal(DateTime.MinValue, writtenUtc);
        }

        /// <summary>
        /// A saved tile is read back with the time of saving.
        /// </summary>
        [Fact]
        public void WriteThenRead_ReturnsDataAndTime()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);
            byte[] data = new byte[] { 1, 2, 3 };

            cache.Write(new TileKey(5, 1, 2), data);
            byte[]? read = cache.TryRead(new TileKey(5, 1, 2), out DateTime writtenUtc);

            Assert.Equal(data, read);
            Assert.True(DateTime.UtcNow - writtenUtc < TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Overwriting a tile does not count it twice.
        /// </summary>
        [Fact]
        public void Write_SameTileTwice_CountsOnce()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);

            cache.Write(new TileKey(1, 0, 0), new byte[10]);
            cache.Write(new TileKey(1, 0, 0), new byte[4]);

            Assert.Equal((4L, 1), cache.GetUsage());
        }

        /// <summary>
        /// A new instance counts the tiles left by the previous one.
        /// </summary>
        [Fact]
        public void GetUsage_ExistingFiles_ScansDirectory()
        {
            TileDiskCache first = new TileDiskCache(directory, () => 1000);
            first.Write(new TileKey(1, 0, 0), new byte[10]);
            first.Write(new TileKey(1, 1, 0), new byte[20]);

            TileDiskCache second = new TileDiskCache(directory, () => 1000);

            Assert.Equal((30L, 2), second.GetUsage());
        }

        /// <summary>
        /// Leftovers of interrupted writes are removed and not counted.
        /// </summary>
        [Fact]
        public void GetUsage_TemporaryFile_IsDeletedAndNotCounted()
        {
            Directory.CreateDirectory(Path.Combine(directory, "3", "1"));
            string leftover = Path.Combine(directory, "3", "1", "2.png.tmp");
            File.WriteAllBytes(leftover, new byte[50]);
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);

            Assert.Equal((0L, 0), cache.GetUsage());
            Assert.False(File.Exists(leftover));
        }

        /// <summary>
        /// Exceeding the limit deletes the oldest tiles and keeps the newest.
        /// </summary>
        [Fact]
        public void Write_OverLimit_DeletesOldestTiles()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 100);

            cache.Write(new TileKey(1, 0, 0), new byte[40]);
            cache.Write(new TileKey(1, 1, 0), new byte[40]);
            File.SetLastWriteTimeUtc(Path.Combine(directory, "1", "0", "0.png"), DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(Path.Combine(directory, "1", "1", "0.png"), DateTime.UtcNow.AddDays(-1));

            cache.Write(new TileKey(1, 0, 1), new byte[40]);

            Assert.Null(cache.TryRead(new TileKey(1, 0, 0), out DateTime _));
            Assert.NotNull(cache.TryRead(new TileKey(1, 1, 0), out DateTime _));
            Assert.NotNull(cache.TryRead(new TileKey(1, 0, 1), out DateTime _));
            Assert.True(cache.GetUsage().Bytes <= 100);
        }

        /// <summary>
        /// Lowering the limit and calling Trim shrinks the cache.
        /// </summary>
        [Fact]
        public void Trim_LimitLowered_ShrinksCache()
        {
            long limit = 1000;
            TileDiskCache cache = new TileDiskCache(directory, () => limit);

            for (int index = 0; index < 5; index++)
            {
                cache.Write(new TileKey(2, index, 0), new byte[100]);
            }

            limit = 200;
            cache.Trim();

            (long bytes, int count) = cache.GetUsage();

            Assert.True(bytes <= 200);
            Assert.True(count < 5);
        }

        /// <summary>
        /// Clear removes everything and the cache stays usable.
        /// </summary>
        [Fact]
        public void Clear_RemovesAllTilesAndCacheStaysUsable()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);
            cache.Write(new TileKey(1, 0, 0), new byte[10]);

            cache.Clear();

            Assert.Equal((0L, 0), cache.GetUsage());
            Assert.Null(cache.TryRead(new TileKey(1, 0, 0), out DateTime _));

            cache.Write(new TileKey(1, 0, 0), new byte[7]);

            Assert.Equal((7L, 1), cache.GetUsage());
        }

        /// <summary>
        /// Clear on a directory that does not exist does not fail.
        /// </summary>
        [Fact]
        public void Clear_MissingDirectory_DoesNotThrow()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);

            cache.Clear();

            Assert.Equal((0L, 0), cache.GetUsage());
        }

        /// <summary>
        /// A directory path that is actually a file makes the cache behave as empty instead of throwing.
        /// </summary>
        [Fact]
        public void Write_DirectoryIsFile_DoesNotThrow()
        {
            File.WriteAllBytes(directory, new byte[1]);
            TileDiskCache cache = new TileDiskCache(directory, () => 1000);

            cache.Write(new TileKey(1, 0, 0), new byte[10]);

            Assert.Null(cache.TryRead(new TileKey(1, 0, 0), out DateTime _));

            File.Delete(directory);
        }

        /// <summary>
        /// Parallel writes keep the counters consistent with the files.
        /// </summary>
        [Fact]
        public void Write_Parallel_KeepsCountersConsistent()
        {
            TileDiskCache cache = new TileDiskCache(directory, () => 100000);

            Parallel.For(0, 50, index => cache.Write(new TileKey(3, index % 8, index / 8), new byte[10]));

            (long bytes, int count) = cache.GetUsage();
            (long scannedBytes, int scannedCount) = new TileDiskCache(directory, () => 100000).GetUsage();

            Assert.Equal(scannedBytes, bytes);
            Assert.Equal(scannedCount, count);
            Assert.Equal(50, count);
        }
    }
}

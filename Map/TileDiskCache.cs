namespace AdsbCarMap.Map
{
    /// <summary>
    /// Stores the encoded map tiles as files in a directory, so that they survive a restart of the application. The total size is kept below a limit by deleting the oldest files. A file never causes an exception to leave this class: any file system failure behaves like a missing tile.
    /// </summary>
    public sealed class TileDiskCache
    {
        /// <summary>
        /// Extension of the tile files.
        /// </summary>
        private const string TileExtension = ".png";

        /// <summary>
        /// Extension of the files that are being written. They are renamed to the final name when complete.
        /// </summary>
        private const string TemporaryExtension = ".tmp";

        /// <summary>
        /// Fraction of the size limit to which the cache is trimmed, so that the next write does not trim again.
        /// </summary>
        private const double TrimTarget = 0.9;

        /// <summary>
        /// Directory with the tile files.
        /// </summary>
        private readonly string directory;

        /// <summary>
        /// Returns the size limit in bytes; read at every write, so a change of the setting takes effect at once.
        /// </summary>
        private readonly Func<long> maxBytesProvider;

        /// <summary>
        /// Lock protecting the file operations and the counters.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// Total size of the tile files in bytes, or -1 until the directory has been scanned.
        /// </summary>
        private long totalBytes = -1;

        /// <summary>
        /// Number of the tile files; valid only when <see cref="totalBytes"/> is not -1.
        /// </summary>
        private int fileCount;

        /// <summary>
        /// Creates a disk cache.
        /// </summary>
        /// <param name="directory">Directory with the tile files; created when missing.</param>
        /// <param name="maxBytesProvider">Returns the size limit in bytes.</param>
        public TileDiskCache(string directory, Func<long> maxBytesProvider)
        {
            this.directory = directory;
            this.maxBytesProvider = maxBytesProvider;
        }

        /// <summary>
        /// Reads a tile from the disk.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <param name="writtenUtc">Time the tile was saved, or the minimum date when the tile is missing.</param>
        /// <returns>Encoded tile image, or null when the tile is not on the disk or cannot be read.</returns>
        public byte[]? TryRead(TileKey key, out DateTime writtenUtc)
        {
            writtenUtc = DateTime.MinValue;

            try
            {
                string path = GetPath(key);

                lock (syncLock)
                {
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    byte[] data = File.ReadAllBytes(path);
                    writtenUtc = File.GetLastWriteTimeUtc(path);

                    return data;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Saves a tile and trims the cache when it exceeds the size limit.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <param name="data">Encoded tile image.</param>
        public void Write(TileKey key, byte[] data)
        {
            try
            {
                string path = GetPath(key);

                lock (syncLock)
                {
                    EnsureScanned();

                    long previousSize = File.Exists(path) ? new FileInfo(path).Length : -1;

                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                    string temporaryPath = path + TemporaryExtension;
                    File.WriteAllBytes(temporaryPath, data);
                    File.Move(temporaryPath, path, true);

                    if (previousSize >= 0)
                    {
                        totalBytes -= previousSize;
                    }
                    else
                    {
                        fileCount++;
                    }

                    totalBytes += data.Length;

                    if (totalBytes > maxBytesProvider())
                    {
                        TrimLocked();
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Deletes the oldest tiles until the cache fits in the current size limit.
        /// </summary>
        public void Trim()
        {
            try
            {
                lock (syncLock)
                {
                    EnsureScanned();

                    if (totalBytes > maxBytesProvider())
                    {
                        TrimLocked();
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Counts the tile files and their total size.
        /// </summary>
        /// <returns>Total size in bytes and number of tiles.</returns>
        public (long Bytes, int Count) GetUsage()
        {
            try
            {
                lock (syncLock)
                {
                    EnsureScanned();

                    return (totalBytes, fileCount);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Deletes all tiles from the disk.
        /// </summary>
        public void Clear()
        {
            try
            {
                lock (syncLock)
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }

                    totalBytes = 0;
                    fileCount = 0;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                lock (syncLock)
                {
                    totalBytes = -1;
                }
            }
        }

        /// <summary>
        /// Scans the directory once to learn the total size, and removes the leftovers of interrupted writes.
        /// </summary>
        private void EnsureScanned()
        {
            if (totalBytes >= 0)
            {
                return;
            }

            long bytes = 0;
            int count = 0;

            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (path.EndsWith(TemporaryExtension, StringComparison.Ordinal))
                    {
                        File.Delete(path);
                    }
                    else if (path.EndsWith(TileExtension, StringComparison.Ordinal))
                    {
                        bytes += new FileInfo(path).Length;
                        count++;
                    }
                }
            }

            totalBytes = bytes;
            fileCount = count;
        }

        /// <summary>
        /// Deletes the oldest tiles until the size falls to the trim target. The caller holds the lock.
        /// </summary>
        private void TrimLocked()
        {
            long target = (long)(maxBytesProvider() * TrimTarget);
            List<FileInfo> files = new List<FileInfo>();

            foreach (string path in Directory.EnumerateFiles(directory, "*" + TileExtension, SearchOption.AllDirectories))
            {
                files.Add(new FileInfo(path));
            }

            files.Sort((first, second) => first.LastWriteTimeUtc.CompareTo(second.LastWriteTimeUtc));

            long bytes = 0;

            foreach (FileInfo file in files)
            {
                bytes += file.Length;
            }

            int count = files.Count;

            foreach (FileInfo file in files)
            {
                if (bytes <= target)
                {
                    break;
                }

                try
                {
                    long length = file.Length;
                    file.Delete();
                    bytes -= length;
                    count--;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                }
            }

            totalBytes = bytes;
            fileCount = count;
        }

        /// <summary>
        /// Returns the path of the file of a tile.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
        /// <returns>Full path of the file.</returns>
        private string GetPath(TileKey key)
        {
            return Path.Combine(directory, key.Zoom.ToString(System.Globalization.CultureInfo.InvariantCulture), key.X.ToString(System.Globalization.CultureInfo.InvariantCulture), key.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + TileExtension);
        }
    }
}

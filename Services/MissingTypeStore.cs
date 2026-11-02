using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Keeps the log of aircraft types missing from the type table in a file in the private storage of the application, so that it survives restarts. The file is written at most once per <see cref="SaveInterval"/> while new data arrive.
    /// </summary>
    public sealed class MissingTypeStore
    {
        /// <summary>
        /// Minimum time between two writes of the file.
        /// </summary>
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Name of the file with the log.
        /// </summary>
        private const string FileName = "missing-aircraft-types.tsv";

        /// <summary>
        /// Tag used in the Android log.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Lock protecting the saving state.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Path of the file with the log.
        /// </summary>
        private readonly string path;

        /// <summary>
        /// Whether the log has changes that are not saved yet.
        /// </summary>
        private bool dirty;

        /// <summary>
        /// Moment of the last write, in UTC.
        /// </summary>
        private DateTime lastSaveUtc = DateTime.MinValue;

        /// <summary>
        /// Creates the store and reads the saved log.
        /// </summary>
        public MissingTypeStore()
        {
            path = System.IO.Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, FileName);

            try
            {
                if (File.Exists(path))
                {
                    Missing.Load(File.ReadAllText(path));
                }
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Wczytanie listy brakujących typów nie powiodło się", exception);
            }
        }

        /// <summary>
        /// The log of the missing types.
        /// </summary>
        public MissingTypeLog Missing { get; } = new MissingTypeLog();

        /// <summary>
        /// Adds the aircraft of types missing from the current type table to the log and saves it when it is time. Does nothing while the table is not loaded yet, because every type would look missing.
        /// </summary>
        /// <param name="aircraft">Aircraft from the last download.</param>
        public void Record(IReadOnlyList<Aircraft> aircraft)
        {
            AircraftTypeTable table = AircraftTypeTable.Current;

            if (table.TypeCount == 0)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;

            if (Missing.Record(aircraft, table, now))
            {
                lock (syncRoot)
                {
                    dirty = true;
                }
            }

            SaveIfDue(now);
        }

        /// <summary>
        /// Removes every entry of the log and deletes the file.
        /// </summary>
        public void Clear()
        {
            Missing.Clear();

            lock (syncRoot)
            {
                dirty = false;

                try
                {
                    File.Delete(path);
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Usunięcie listy brakujących typów nie powiodło się", exception);
                }
            }
        }

        /// <summary>
        /// Writes the file when there are unsaved changes and enough time has passed since the last write.
        /// </summary>
        /// <param name="now">Current time, in UTC.</param>
        private void SaveIfDue(DateTime now)
        {
            lock (syncRoot)
            {
                if (!dirty || now - lastSaveUtc < SaveInterval)
                {
                    return;
                }

                dirty = false;
                lastSaveUtc = now;

                try
                {
                    File.WriteAllText(path, Missing.Serialize());
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Zapis listy brakujących typów nie powiodł się", exception);
                }
            }
        }
    }
}

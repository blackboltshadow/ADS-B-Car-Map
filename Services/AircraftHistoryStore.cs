using AdsbCarMap.Data;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Keeps the history of the selected and watched aircraft in a file in the application folder, so that it survives a restart. Every change is saved at once, because the selections are rare.
    /// </summary>
    public static class AircraftHistoryStore
    {
        /// <summary>
        /// Name of the file with the history.
        /// </summary>
        private const string FileName = "aircraft-history.json";

        /// <summary>
        /// Lock protecting the history and the file.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The history, read from the file the first time it is needed.
        /// </summary>
        private static AircraftHistory? history;

        /// <summary>
        /// Notes an aircraft in the history and saves it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <param name="registration">Registration or null.</param>
        /// <param name="typeCode">ICAO type code or null.</param>
        /// <param name="watched">Whether the notice comes from a watch alert.</param>
        public static void Record(string hex, string? callsign, string? registration, string? typeCode, bool watched)
        {
            lock (SyncRoot)
            {
                GetHistory().Record(hex, callsign, registration, typeCode, DateTime.UtcNow, watched);
                Save();
            }
        }

        /// <summary>
        /// Marks an aircraft as watched, when it is in the history, and saves it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public static void MarkWatched(string hex)
        {
            lock (SyncRoot)
            {
                GetHistory().MarkWatched(hex);
                Save();
            }
        }

        /// <summary>
        /// Pins or unpins an entry and saves the history.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="pinned">New state of the pin.</param>
        public static void SetPinned(string hex, bool pinned)
        {
            lock (SyncRoot)
            {
                GetHistory().SetPinned(hex, pinned);
                Save();
            }
        }

        /// <summary>
        /// Removes one entry and saves the history.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public static void Remove(string hex)
        {
            lock (SyncRoot)
            {
                GetHistory().Remove(hex);
                Save();
            }
        }

        /// <summary>
        /// Removes the entries that are not pinned and saves the history.
        /// </summary>
        public static void ClearUnpinned()
        {
            lock (SyncRoot)
            {
                GetHistory().ClearUnpinned();
                Save();
            }
        }

        /// <summary>
        /// Gives the entries for display.
        /// </summary>
        /// <returns>The pinned entries first, then the most recently seen ones.</returns>
        public static List<AircraftHistoryEntry> GetForDisplay()
        {
            lock (SyncRoot)
            {
                return GetHistory().GetForDisplay();
            }
        }

        /// <summary>
        /// Gives the path of the file with the history.
        /// </summary>
        /// <returns>Full path.</returns>
        private static string GetPath()
        {
            return System.IO.Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, FileName);
        }

        /// <summary>
        /// Gives the history, reading the file the first time. Call it only with <see cref="SyncRoot"/> held.
        /// </summary>
        /// <returns>The history.</returns>
        private static AircraftHistory GetHistory()
        {
            if (history == null)
            {
                string? text = null;

                try
                {
                    string path = GetPath();
                    text = File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (Exception exception)
                {
                    DevLogger.Warn("AircraftHistoryStore", "Nie udało się wczytać historii samolotów: " + exception.Message);
                }

                history = AircraftHistory.Parse(text);
            }

            return history;
        }

        /// <summary>
        /// Writes the history to the file. Call it only with <see cref="SyncRoot"/> held.
        /// </summary>
        private static void Save()
        {
            try
            {
                File.WriteAllText(GetPath(), GetHistory().Serialize());
            }
            catch (Exception exception)
            {
                DevLogger.Warn("AircraftHistoryStore", "Nie udało się zapisać historii samolotów: " + exception.Message);
            }
        }
    }
}

using AdsbCarMap.Data;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Keeps the history of the alerts in a file in the application folder, so that it survives a restart. The alerts are rare, so every change is saved at once.
    /// </summary>
    public static class AlertHistoryStore
    {
        /// <summary>
        /// Name of the file with the history.
        /// </summary>
        private const string FileName = "alert-history.json";

        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "AlertHistoryStore";

        /// <summary>
        /// Lock protecting the history and the file.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The history, read from the file the first time it is needed.
        /// </summary>
        private static AlertHistory? history;

        /// <summary>
        /// Adds an entry and saves the history.
        /// </summary>
        /// <param name="entry">Entry to add.</param>
        public static void Add(AlertHistoryEntry entry)
        {
            lock (SyncRoot)
            {
                GetHistory().Add(entry);
                Save();
            }
        }

        /// <summary>
        /// Gives the entries for display.
        /// </summary>
        /// <returns>The entries, the newest first.</returns>
        public static List<AlertHistoryEntry> GetNewestFirst()
        {
            lock (SyncRoot)
            {
                return GetHistory().GetNewestFirst();
            }
        }

        /// <summary>
        /// Removes all entries and saves the empty history.
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                GetHistory().Clear();
                Save();
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
        private static AlertHistory GetHistory()
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
                    Android.Util.Log.Warn(LogTag, "Nie udało się wczytać historii alertów: " + exception.Message);
                }

                history = AlertHistory.Parse(text);
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
                Android.Util.Log.Warn(LogTag, "Nie udało się zapisać historii alertów: " + exception.Message);
            }
        }
    }
}

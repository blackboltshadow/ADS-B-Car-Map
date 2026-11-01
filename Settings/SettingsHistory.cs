namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Keeps the previous values of the settings changed on the settings screen, so that every change can be undone step by step. The history lives only in memory: it survives closing and reopening the settings screen, but it is lost when the application process ends.
    /// </summary>
    public static class SettingsHistory
    {
        /// <summary>
        /// Maximum number of remembered changes of one setting.
        /// </summary>
        private const int MaxEntries = 200;

        /// <summary>
        /// Lock protecting the history.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Previous values by setting key; the most recent change is on top.
        /// </summary>
        private static readonly Dictionary<string, List<double>> Entries = new Dictionary<string, List<double>>();

        /// <summary>
        /// Remembers the value a setting had before it was changed.
        /// </summary>
        /// <param name="key">Key of the setting.</param>
        /// <param name="previousValue">Value before the change.</param>
        public static void Push(string key, double previousValue)
        {
            lock (SyncRoot)
            {
                if (!Entries.TryGetValue(key, out List<double>? list))
                {
                    list = new List<double>();
                    Entries[key] = list;
                }

                list.Add(previousValue);

                if (list.Count > MaxEntries)
                {
                    list.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Takes the most recently remembered previous value of a setting.
        /// </summary>
        /// <param name="key">Key of the setting.</param>
        /// <param name="previousValue">The value the setting had before its last change.</param>
        /// <returns>True when there was a value to take.</returns>
        public static bool TryPop(string key, out double previousValue)
        {
            lock (SyncRoot)
            {
                if (Entries.TryGetValue(key, out List<double>? list) && list.Count > 0)
                {
                    previousValue = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);

                    return true;
                }
            }

            previousValue = 0.0;

            return false;
        }

        /// <summary>
        /// Returns the number of changes of a setting that can still be undone.
        /// </summary>
        /// <param name="key">Key of the setting.</param>
        /// <returns>Number of remembered changes.</returns>
        public static int Count(string key)
        {
            lock (SyncRoot)
            {
                return Entries.TryGetValue(key, out List<double>? list) ? list.Count : 0;
            }
        }
    }
}

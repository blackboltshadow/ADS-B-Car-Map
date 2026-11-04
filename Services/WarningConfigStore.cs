using AdsbCarMap.Data;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The choice of the user which official notices the screen shows and whether he is notified about new ones. Stored in its own SharedPreferences file, separate from the settings: the categories are not a fixed list, so they do not fit the settings definitions. The configuration is replaced as a whole on every change, so it can be read without a lock.
    /// </summary>
    public sealed class WarningConfigStore
    {
        /// <summary>
        /// Name of the SharedPreferences file.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_warnings";

        /// <summary>
        /// Key of the stored text of the configuration.
        /// </summary>
        private const string ConfigKey = "config";

        /// <summary>
        /// Key of the stored text of the options of the notifications.
        /// </summary>
        private const string AlertOptionsKey = "alert_options";

        /// <summary>
        /// Key of the list of the notices already reported, one per line as the key, a tab, the ticks of the expiry, a tab and 1 for an alarm.
        /// </summary>
        private const string ReportedKey = "alert_reported";

        /// <summary>
        /// Key of the flag saying that the first check after switching the notifications on, or after changing the choice, has been made.
        /// </summary>
        private const string SeededKey = "alert_seeded";

        /// <summary>
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The current configuration.
        /// </summary>
        private WarningFilterConfig config;

        /// <summary>
        /// The current options of the notifications.
        /// </summary>
        private WarningAlertOptions alertOptions;

        /// <summary>
        /// Creates the store and reads it from the device.
        /// </summary>
        public WarningConfigStore()
        {
            ISharedPreferences preferences = OpenPreferences();

            config = WarningFilterConfig.Parse(preferences.GetString(ConfigKey, null));
            alertOptions = WarningAlertOptions.Parse(preferences.GetString(AlertOptionsKey, null));
        }

        /// <summary>
        /// Event raised after the configuration or the options changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// The current configuration.
        /// </summary>
        public WarningFilterConfig Config => Volatile.Read(ref config);

        /// <summary>
        /// The current options of the notifications.
        /// </summary>
        public WarningAlertOptions AlertOptions => Volatile.Read(ref alertOptions);

        /// <summary>
        /// Tells whether the first check after switching the notifications on, or after changing the choice, has been made.
        /// </summary>
        public bool AlertsSeeded => OpenPreferences().GetBoolean(SeededKey, false);

        /// <summary>
        /// Replaces the configuration and stores it. A change of the categories or of the voivodeships starts a new first check, which does not report the notices that are already there.
        /// </summary>
        /// <param name="value">The new configuration.</param>
        public void Save(WarningFilterConfig value)
        {
            lock (syncRoot)
            {
                WarningFilterConfig previous = config;
                bool choiceChanged = !previous.ShownCategories.SequenceEqual(value.ShownCategories, StringComparer.OrdinalIgnoreCase)
                    || !previous.Provinces.SequenceEqual(value.Provinces, StringComparer.OrdinalIgnoreCase);

                Volatile.Write(ref config, value);
                ISharedPreferencesEditor? editor = OpenPreferences().Edit()?.PutString(ConfigKey, value.Serialize());

                if (choiceChanged)
                {
                    editor?.PutBoolean(SeededKey, false);
                }

                editor?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Replaces the options of the notifications and stores them. Switching the notifications on starts a new first check, which does not report the notices that are already there.
        /// </summary>
        /// <param name="value">The new options.</param>
        public void SaveAlertOptions(WarningAlertOptions value)
        {
            lock (syncRoot)
            {
                bool switchedOn = value.Enabled && !alertOptions.Enabled;

                Volatile.Write(ref alertOptions, value);
                ISharedPreferencesEditor? editor = OpenPreferences().Edit()?.PutString(AlertOptionsKey, value.Serialize());

                if (switchedOn)
                {
                    editor?.PutBoolean(SeededKey, false);
                }

                editor?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Remembers that the first check has been made.
        /// </summary>
        public void MarkAlertsSeeded()
        {
            OpenPreferences().Edit()?.PutBoolean(SeededKey, true)?.Apply();
        }

        /// <summary>
        /// Reads the notices already reported.
        /// </summary>
        /// <returns>The key of each notice with what is remembered about it.</returns>
        public List<KeyValuePair<string, WarningReport>> LoadReported()
        {
            List<KeyValuePair<string, WarningReport>> result = new List<KeyValuePair<string, WarningReport>>();
            string? text = OpenPreferences().GetString(ReportedKey, null);

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');

                if (parts.Length == 3
                    && long.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long ticks)
                    && ticks > 0
                    && ticks <= DateTime.MaxValue.Ticks)
                {
                    result.Add(new KeyValuePair<string, WarningReport>(parts[0], new WarningReport(new DateTime(ticks, DateTimeKind.Utc), parts[2] == "1")));
                }
            }

            return result;
        }

        /// <summary>
        /// Stores the notices already reported.
        /// </summary>
        /// <param name="reported">The key of each notice with what is remembered about it.</param>
        public void SaveReported(IReadOnlyDictionary<string, WarningReport> reported)
        {
            string text = string.Join("\n", reported.Select((KeyValuePair<string, WarningReport> entry) => entry.Key + "\t" + WarningAlertEngine.FormatTicks(entry.Value.ExpiresUtc) + "\t" + (entry.Value.Alarm ? "1" : "0")));

            OpenPreferences().Edit()?.PutString(ReportedKey, text)?.Apply();
        }

        /// <summary>
        /// Opens the SharedPreferences file.
        /// </summary>
        /// <returns>SharedPreferences object.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

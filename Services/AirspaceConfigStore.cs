using AdsbCarMap.Data;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The choice of the user which airspace areas the layer shows. Stored in its own SharedPreferences file, separate from the settings: the kinds of areas are not a fixed list, so they do not fit the settings definitions. The configuration is replaced as a whole on every change, so the map can read it without a lock.
    /// </summary>
    public sealed class AirspaceConfigStore
    {
        /// <summary>
        /// Name of the SharedPreferences file.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_airspace";

        /// <summary>
        /// Key of the stored text of the configuration.
        /// </summary>
        private const string ConfigKey = "config";

        /// <summary>
        /// Key of the stored text of the options of the notifications.
        /// </summary>
        private const string AlertOptionsKey = "alert_options";

        /// <summary>
        /// Key of the list of the reservations already reported, one per line as the key, a tab and the ticks of the end.
        /// </summary>
        private const string ReportedKey = "alert_reported";

        /// <summary>
        /// Key of the flag saying that the first check after switching the notifications on has been made.
        /// </summary>
        private const string SeededKey = "alert_seeded";

        /// <summary>
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The current configuration.
        /// </summary>
        private AirspaceLayerConfig config;

        /// <summary>
        /// The current options of the notifications.
        /// </summary>
        private AirspaceAlertOptions alertOptions;

        /// <summary>
        /// Creates the store and reads it from the device.
        /// </summary>
        public AirspaceConfigStore()
        {
            ISharedPreferences preferences = OpenPreferences();

            config = AirspaceLayerConfig.Parse(preferences.GetString(ConfigKey, null));
            alertOptions = AirspaceAlertOptions.Parse(preferences.GetString(AlertOptionsKey, null));
        }

        /// <summary>
        /// Event raised after the configuration changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// The current configuration.
        /// </summary>
        public AirspaceLayerConfig Config => Volatile.Read(ref config);

        /// <summary>
        /// Replaces the configuration and stores it.
        /// </summary>
        /// <param name="value">The new configuration.</param>
        public void Save(AirspaceLayerConfig value)
        {
            lock (syncRoot)
            {
                Volatile.Write(ref config, value);
                OpenPreferences().Edit()?.PutString(ConfigKey, value.Serialize())?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// The current options of the notifications about areas that become active.
        /// </summary>
        public AirspaceAlertOptions AlertOptions => Volatile.Read(ref alertOptions);

        /// <summary>
        /// Replaces the options of the notifications and stores them. Switching the notifications on starts a new first check, which does not report the areas that are already active.
        /// </summary>
        /// <param name="value">The new options.</param>
        public void SaveAlertOptions(AirspaceAlertOptions value)
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
        /// Tells whether the first check after switching the notifications on has been made.
        /// </summary>
        public bool AlertsSeeded => OpenPreferences().GetBoolean(SeededKey, false);

        /// <summary>
        /// Remembers that the first check has been made.
        /// </summary>
        public void MarkAlertsSeeded()
        {
            OpenPreferences().Edit()?.PutBoolean(SeededKey, true)?.Apply();
        }

        /// <summary>
        /// Reads the reservations already reported.
        /// </summary>
        /// <returns>The key of each reservation with the time it ends.</returns>
        public List<KeyValuePair<string, DateTime>> LoadReported()
        {
            List<KeyValuePair<string, DateTime>> result = new List<KeyValuePair<string, DateTime>>();
            string? text = OpenPreferences().GetString(ReportedKey, null);

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');

                if (parts.Length == 2 && long.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long ticks) && ticks > 0 && ticks <= DateTime.MaxValue.Ticks)
                {
                    result.Add(new KeyValuePair<string, DateTime>(parts[0], new DateTime(ticks, DateTimeKind.Utc)));
                }
            }

            return result;
        }

        /// <summary>
        /// Stores the reservations already reported.
        /// </summary>
        /// <param name="reported">The key of each reservation with the time it ends.</param>
        public void SaveReported(IReadOnlyDictionary<string, DateTime> reported)
        {
            string text = string.Join("\n", reported.Select((KeyValuePair<string, DateTime> entry) => entry.Key + "\t" + entry.Value.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)));

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

using AdsbCarMap.Data;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The choice of the user which official notices the screen shows. Stored in its own SharedPreferences file, separate from the settings: the categories are not a fixed list, so they do not fit the settings definitions. The configuration is replaced as a whole on every change, so it can be read without a lock.
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
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The current configuration.
        /// </summary>
        private WarningFilterConfig config;

        /// <summary>
        /// Creates the store and reads it from the device.
        /// </summary>
        public WarningConfigStore()
        {
            config = WarningFilterConfig.Parse(OpenPreferences().GetString(ConfigKey, null));
        }

        /// <summary>
        /// Event raised after the configuration changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// The current configuration.
        /// </summary>
        public WarningFilterConfig Config => Volatile.Read(ref config);

        /// <summary>
        /// Replaces the configuration and stores it.
        /// </summary>
        /// <param name="value">The new configuration.</param>
        public void Save(WarningFilterConfig value)
        {
            lock (syncRoot)
            {
                Volatile.Write(ref config, value);
                OpenPreferences().Edit()?.PutString(ConfigKey, value.Serialize())?.Apply();
            }

            Changed?.Invoke();
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

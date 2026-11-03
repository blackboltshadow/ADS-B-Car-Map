using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Stores the map view (center, zoom, follow mode) in SharedPreferences so the next run starts where the previous one ended.
    /// </summary>
    public static class MapViewStore
    {
        /// <summary>
        /// Name of the SharedPreferences file.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_view";

        /// <summary>
        /// Key of the stored view text.
        /// </summary>
        private const string ViewKey = "view";

        /// <summary>
        /// Loads the remembered view.
        /// </summary>
        /// <returns>The remembered view, or null when there is none or it is invalid.</returns>
        public static MapViewState? Load()
        {
            try
            {
                string? text = Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)?.GetString(ViewKey, null);

                return MapViewState.TryParse(text, AppSettings.MinZoom, AppSettings.MaxZoom);
            }
            catch (Exception exception)
            {
                DevLogger.Warn("MapViewStore", "Nie udało się wczytać zapamiętanego widoku mapy.", exception);

                return null;
            }
        }

        /// <summary>
        /// Saves the view.
        /// </summary>
        /// <param name="state">View to remember.</param>
        public static void Save(MapViewState state)
        {
            try
            {
                ISharedPreferencesEditor? editor = Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)?.Edit();
                editor?.PutString(ViewKey, state.Format());
                editor?.Apply();
            }
            catch (Exception exception)
            {
                DevLogger.Warn("MapViewStore", "Nie udało się zapisać widoku mapy.", exception);
            }
        }
    }
}

using System.Collections.Concurrent;
using Android.Content;

namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Application settings stored in SharedPreferences and shared by the phone and Android Auto.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// Name of the SharedPreferences file with the settings.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_settings";

        /// <summary>
        /// Interval between aircraft requests in seconds.
        /// </summary>
        public static readonly SettingDefinition FetchIntervalSetting = new SettingDefinition("fetch_interval_s", "Odświeżanie samolotów [s]", 3.0, 1.0, 60.0, false);

        /// <summary>
        /// Interval between drawing frames in milliseconds.
        /// </summary>
        public static readonly SettingDefinition RenderIntervalSetting = new SettingDefinition("render_interval_ms", "Odstęp klatek mapy [ms]", 33.0, 16.0, 1000.0, true);

        /// <summary>
        /// Minimum zoom level.
        /// </summary>
        public static readonly SettingDefinition MinZoomSetting = new SettingDefinition("min_zoom", "Minimalny zoom", 4.0, 1.0, 18.0, true);

        /// <summary>
        /// Maximum zoom level.
        /// </summary>
        public static readonly SettingDefinition MaxZoomSetting = new SettingDefinition("max_zoom", "Maksymalny zoom", 13.0, 1.0, 18.0, true);

        /// <summary>
        /// Maximum number of decoded tiles kept in memory.
        /// </summary>
        public static readonly SettingDefinition MaxCachedTilesSetting = new SettingDefinition("max_cached_tiles", "Kafelki w pamięci", 128.0, 16.0, 1024.0, true);

        /// <summary>
        /// Time in seconds before a failed tile download is retried.
        /// </summary>
        public static readonly SettingDefinition TileRetrySetting = new SettingDefinition("tile_retry_s", "Ponowienie kafelka [s]", 15.0, 1.0, 600.0, true);

        /// <summary>
        /// Time the trail is remembered, in minutes.
        /// </summary>
        public static readonly SettingDefinition TrailDurationSetting = new SettingDefinition("trail_duration_min", "Długość śladu [min]", 30.0, 0.0, 120.0, false);

        /// <summary>
        /// Maximum number of trail points of one aircraft.
        /// </summary>
        public static readonly SettingDefinition MaxTrailPointsSetting = new SettingDefinition("trail_max_points", "Maks. punktów śladu", 700.0, 10.0, 5000.0, true);

        /// <summary>
        /// Width of the trail line in dp.
        /// </summary>
        public static readonly SettingDefinition TrailWidthSetting = new SettingDefinition("trail_width_dp", "Grubość śladu [dp]", 3.0, 0.5, 20.0, false);

        /// <summary>
        /// Opacity of the oldest trail segment.
        /// </summary>
        public static readonly SettingDefinition TrailMinAlphaSetting = new SettingDefinition("trail_min_alpha", "Krycie najstarszego śladu (0-255)", 140.0, 0.0, 255.0, true);

        /// <summary>
        /// Opacity of the newest trail segment.
        /// </summary>
        public static readonly SettingDefinition TrailMaxAlphaSetting = new SettingDefinition("trail_max_alpha", "Krycie najnowszego śladu (0-255)", 255.0, 0.0, 255.0, true);

        /// <summary>
        /// Width of the dark trail outline on each side in dp.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineWidthSetting = new SettingDefinition("trail_outline_dp", "Obwódka śladu [dp]", 1.0, 0.0, 10.0, false);

        /// <summary>
        /// Opacity of the dark trail outline.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineAlphaSetting = new SettingDefinition("trail_outline_alpha", "Krycie obwódki (0-255)", 140.0, 0.0, 255.0, true);

        /// <summary>
        /// Opacity of the black layer that dims the map tiles.
        /// </summary>
        public static readonly SettingDefinition MapDimSetting = new SettingDefinition("map_dim_alpha", "Przyciemnienie mapy (0-220)", 90.0, 0.0, 220.0, true);

        /// <summary>
        /// Width of the dark outline of the aircraft icons on each side in dp.
        /// </summary>
        public static readonly SettingDefinition AircraftOutlineWidthSetting = new SettingDefinition("aircraft_outline_dp", "Obwódka samolotów [dp]", 1.2, 0.0, 6.0, false);

        /// <summary>
        /// Switch for the visibility of the aircraft labels.
        /// </summary>
        public static readonly SettingDefinition ShowLabelsSetting = SettingDefinition.CreateSwitch("show_labels", "Etykiety samolotów", false, "Pokazuje przy każdym samolocie na mapie etykietę: znak wywoławczy lotu i wysokość w wybranej jednostce, a pod spodem typ samolotu i trasę, jeśli są znane. Etykiety włącza się też przyciskiem na mapie (telefon i Android Auto). Zaznaczony samolot zawsze ma etykietę, nawet gdy ta opcja jest wyłączona. Wielkość liter ustawia suwak niżej.");

        /// <summary>
        /// Size of the aircraft label text in dp.
        /// </summary>
        public static readonly SettingDefinition LabelTextSizeSetting = SettingDefinition.CreateSlider("label_text_dp", "Wielkość liter etykiet", 13.0, 6.0, 40.0, 0.5);

        /// <summary>
        /// Altitude of the orange color in meters.
        /// </summary>
        public static readonly SettingDefinition AltitudeOrangeSetting = new SettingDefinition("alt_orange_m", "Pomarańczowy do [m]", 300.0, 50.0, 20000.0, true, SettingKind.Scale);

        /// <summary>
        /// Altitude of the yellow color in meters.
        /// </summary>
        public static readonly SettingDefinition AltitudeYellowSetting = new SettingDefinition("alt_yellow_m", "Żółty [m]", 1200.0, 0.0, 20000.0, true, SettingKind.Scale);

        /// <summary>
        /// Altitude of the green color in meters.
        /// </summary>
        public static readonly SettingDefinition AltitudeGreenSetting = new SettingDefinition("alt_green_m", "Zielony [m]", 3000.0, 0.0, 20000.0, true, SettingKind.Scale);

        /// <summary>
        /// Altitude of the blue color in meters.
        /// </summary>
        public static readonly SettingDefinition AltitudeBlueSetting = new SettingDefinition("alt_blue_m", "Niebieski [m]", 6000.0, 0.0, 20000.0, true, SettingKind.Scale);

        /// <summary>
        /// Altitude of the purple color in meters.
        /// </summary>
        public static readonly SettingDefinition AltitudePurpleSetting = new SettingDefinition("alt_purple_m", "Fioletowy od [m]", 12000.0, 0.0, 20000.0, true, SettingKind.Scale);

        /// <summary>
        /// Switch for the altitude color legend bar at the bottom of the map.
        /// </summary>
        public static readonly SettingDefinition ShowAltitudeLegendSetting = SettingDefinition.CreateSwitch("show_altitude_legend", "Legenda wysokości", true, "Pasek z gradientem kolorów u dołu mapy. Pokazuje, jakiej wysokości odpowiada każdy kolor ikon samolotów i ich śladów: od zera (ziemia), przez pomarańczowy, żółty, zielony i niebieski, do fioletowego. Opisy progów są w wybranej jednostce wysokości, a same progi ustawia się w skali kolorów niżej.");

        /// <summary>
        /// Altitude unit used across the application (phone and Android Auto): meters, feet or flight level.
        /// </summary>
        public static readonly SettingDefinition AltitudeUnitSetting = SettingDefinition.CreateChoice("altitude_unit", "Jednostka wysokości", 0, "Jednostka, w której pokazywana jest wysokość samolotów w całej aplikacji, na telefonie i w Android Auto: na etykietach, w wynikach wyszukiwania, na karcie i w popupie samolotu, w legendzie oraz w skali kolorów. Metry to m i km, stopy to ft, a poziom lotu (FL) to wysokość w setkach stóp, np. FL350 to 35 000 ft. Karta samolotu pokazuje dodatkowo wartość w drugiej jednostce.", "Metry (m, km)", "Stopy (ft)", "Poziom lotu (FL)");

        /// <summary>
        /// All settings in display order.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
        {
            ShowLabelsSetting,
            ShowAltitudeLegendSetting,
            AltitudeUnitSetting,
            AltitudeOrangeSetting,
            AltitudeYellowSetting,
            AltitudeGreenSetting,
            AltitudeBlueSetting,
            AltitudePurpleSetting,
            FetchIntervalSetting,
            RenderIntervalSetting,
            MinZoomSetting,
            MaxZoomSetting,
            MaxCachedTilesSetting,
            TileRetrySetting,
            TrailDurationSetting,
            MaxTrailPointsSetting,
            TrailWidthSetting,
            TrailMinAlphaSetting,
            TrailMaxAlphaSetting,
            TrailOutlineWidthSetting,
            TrailOutlineAlphaSetting,
            MapDimSetting,
            AircraftOutlineWidthSetting,
            LabelTextSizeSetting,
        };

        /// <summary>
        /// Current setting values by key, loaded lazily from SharedPreferences.
        /// </summary>
        private static readonly ConcurrentDictionary<string, double> Values = new ConcurrentDictionary<string, double>();

        /// <summary>
        /// Lock protecting the one-time loading of the settings.
        /// </summary>
        private static readonly object LoadLock = new object();

        /// <summary>
        /// Whether the settings have already been loaded.
        /// </summary>
        private static bool loaded;

        /// <summary>
        /// Interval between aircraft requests.
        /// </summary>
        public static TimeSpan FetchInterval => TimeSpan.FromSeconds(Get(FetchIntervalSetting));

        /// <summary>
        /// Interval between drawing frames in milliseconds.
        /// </summary>
        public static int RenderIntervalMilliseconds => (int)Get(RenderIntervalSetting);

        /// <summary>
        /// Minimum zoom level, not greater than the maximum.
        /// </summary>
        public static int MinZoom => Math.Min((int)Get(MinZoomSetting), (int)Get(MaxZoomSetting));

        /// <summary>
        /// Maximum zoom level, not smaller than the minimum.
        /// </summary>
        public static int MaxZoom => Math.Max((int)Get(MinZoomSetting), (int)Get(MaxZoomSetting));

        /// <summary>
        /// Maximum number of decoded tiles kept in memory.
        /// </summary>
        public static int MaxCachedTiles => (int)Get(MaxCachedTilesSetting);

        /// <summary>
        /// Time before a failed tile download is retried.
        /// </summary>
        public static TimeSpan TileRetry => TimeSpan.FromSeconds(Get(TileRetrySetting));

        /// <summary>
        /// Time the trail is remembered.
        /// </summary>
        public static TimeSpan TrailDuration => TimeSpan.FromMinutes(Get(TrailDurationSetting));

        /// <summary>
        /// Maximum number of trail points of one aircraft.
        /// </summary>
        public static int MaxTrailPoints => (int)Get(MaxTrailPointsSetting);

        /// <summary>
        /// Width of the trail line in dp.
        /// </summary>
        public static float TrailWidth => (float)Get(TrailWidthSetting);

        /// <summary>
        /// Opacity of the oldest trail segment, not greater than that of the newest.
        /// </summary>
        public static int TrailMinAlpha => Math.Min((int)Get(TrailMinAlphaSetting), (int)Get(TrailMaxAlphaSetting));

        /// <summary>
        /// Opacity of the newest trail segment.
        /// </summary>
        public static int TrailMaxAlpha => (int)Get(TrailMaxAlphaSetting);

        /// <summary>
        /// Width of the dark trail outline on each side in dp.
        /// </summary>
        public static float TrailOutlineWidth => (float)Get(TrailOutlineWidthSetting);

        /// <summary>
        /// Opacity of the dark trail outline.
        /// </summary>
        public static int TrailOutlineAlpha => (int)Get(TrailOutlineAlphaSetting);

        /// <summary>
        /// Opacity of the black layer that dims the map tiles.
        /// </summary>
        public static int MapDim => (int)Get(MapDimSetting);

        /// <summary>
        /// Width of the dark outline of the aircraft icons on each side in dp.
        /// </summary>
        public static float AircraftOutlineWidth => (float)Get(AircraftOutlineWidthSetting);

        /// <summary>
        /// Whether the aircraft labels are visible on the map.
        /// </summary>
        public static bool ShowLabels => Get(ShowLabelsSetting) >= 0.5;

        /// <summary>
        /// Whether the altitude color legend bar is visible on the map.
        /// </summary>
        public static bool ShowAltitudeLegend => Get(ShowAltitudeLegendSetting) >= 0.5;

        /// <summary>
        /// Unit in which the aircraft altitude is shown.
        /// </summary>
        public static AltitudeUnit AltitudeUnit => (AltitudeUnit)(int)Get(AltitudeUnitSetting);

        /// <summary>
        /// Size of the aircraft label text in dp.
        /// </summary>
        public static float LabelTextSize => (float)Get(LabelTextSizeSetting);

        /// <summary>
        /// Altitude thresholds of the colors in meters, from orange to purple, sorted ascending.
        /// </summary>
        public static double[] AltitudeStopsMeters
        {
            get
            {
                double[] stops = new double[]
                {
                    Get(AltitudeOrangeSetting),
                    Get(AltitudeYellowSetting),
                    Get(AltitudeGreenSetting),
                    Get(AltitudeBlueSetting),
                    Get(AltitudePurpleSetting),
                };

                Array.Sort(stops);

                return stops;
            }
        }

        /// <summary>
        /// Returns the current value of a setting.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <returns>Stored or default value.</returns>
        public static double Get(SettingDefinition definition)
        {
            EnsureLoaded();

            return Values.TryGetValue(definition.Key, out double value) ? value : definition.DefaultValue;
        }

        /// <summary>
        /// Saves the setting values after limiting them to the allowed ranges.
        /// </summary>
        /// <param name="newValues">New values by setting.</param>
        public static void Save(IReadOnlyDictionary<SettingDefinition, double> newValues)
        {
            EnsureLoaded();

            ISharedPreferencesEditor? editor = OpenPreferences().Edit();

            foreach (KeyValuePair<SettingDefinition, double> entry in newValues)
            {
                double normalized = entry.Key.Normalize(entry.Value);
                Values[entry.Key.Key] = normalized;
                editor?.PutFloat(entry.Key.Key, (float)normalized);
            }

            editor?.Apply();
        }

        /// <summary>
        /// Restores the default values of all settings.
        /// </summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();

            ISharedPreferencesEditor? editor = OpenPreferences().Edit();
            editor?.Clear();
            editor?.Apply();

            foreach (SettingDefinition definition in All)
            {
                Values[definition.Key] = definition.DefaultValue;
            }
        }

        /// <summary>
        /// Loads the settings from SharedPreferences on first use.
        /// </summary>
        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            lock (LoadLock)
            {
                if (loaded)
                {
                    return;
                }

                ISharedPreferences preferences = OpenPreferences();

                foreach (SettingDefinition definition in All)
                {
                    double value = preferences.Contains(definition.Key)
                        ? preferences.GetFloat(definition.Key, (float)definition.DefaultValue)
                        : definition.DefaultValue;

                    Values[definition.Key] = definition.Normalize(value);
                }

                loaded = true;
            }
        }

        /// <summary>
        /// Opens the SharedPreferences file with the settings.
        /// </summary>
        /// <returns>SharedPreferences object.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

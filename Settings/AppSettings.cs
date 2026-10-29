using System.Collections.Concurrent;
using Android.Content;

namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Ustawienia aplikacji zapisywane w SharedPreferences i współdzielone przez telefon oraz Android Auto.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// Nazwa pliku SharedPreferences z ustawieniami.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_settings";

        /// <summary>
        /// Odstęp między zapytaniami o samoloty w sekundach.
        /// </summary>
        public static readonly SettingDefinition FetchIntervalSetting = new SettingDefinition("fetch_interval_s", "Odświeżanie samolotów [s]", 3.0, 1.0, 60.0, false);

        /// <summary>
        /// Odstęp między klatkami rysowania w milisekundach.
        /// </summary>
        public static readonly SettingDefinition RenderIntervalSetting = new SettingDefinition("render_interval_ms", "Odstęp klatek mapy [ms]", 33.0, 16.0, 1000.0, true);

        /// <summary>
        /// Minimalny poziom przybliżenia.
        /// </summary>
        public static readonly SettingDefinition MinZoomSetting = new SettingDefinition("min_zoom", "Minimalny zoom", 4.0, 1.0, 18.0, true);

        /// <summary>
        /// Maksymalny poziom przybliżenia.
        /// </summary>
        public static readonly SettingDefinition MaxZoomSetting = new SettingDefinition("max_zoom", "Maksymalny zoom", 13.0, 1.0, 18.0, true);

        /// <summary>
        /// Maksymalna liczba zdekodowanych kafelków w pamięci.
        /// </summary>
        public static readonly SettingDefinition MaxCachedTilesSetting = new SettingDefinition("max_cached_tiles", "Kafelki w pamięci", 128.0, 16.0, 1024.0, true);

        /// <summary>
        /// Czas w sekundach do ponowienia nieudanego pobrania kafelka.
        /// </summary>
        public static readonly SettingDefinition TileRetrySetting = new SettingDefinition("tile_retry_s", "Ponowienie kafelka [s]", 15.0, 1.0, 600.0, true);

        /// <summary>
        /// Czas pamiętania śladu w minutach.
        /// </summary>
        public static readonly SettingDefinition TrailDurationSetting = new SettingDefinition("trail_duration_min", "Długość śladu [min]", 30.0, 0.0, 120.0, false);

        /// <summary>
        /// Maksymalna liczba punktów śladu jednego samolotu.
        /// </summary>
        public static readonly SettingDefinition MaxTrailPointsSetting = new SettingDefinition("trail_max_points", "Maks. punktów śladu", 700.0, 10.0, 5000.0, true);

        /// <summary>
        /// Grubość linii śladu w dp.
        /// </summary>
        public static readonly SettingDefinition TrailWidthSetting = new SettingDefinition("trail_width_dp", "Grubość śladu [dp]", 3.0, 0.5, 20.0, false);

        /// <summary>
        /// Krycie najstarszego odcinka śladu.
        /// </summary>
        public static readonly SettingDefinition TrailMinAlphaSetting = new SettingDefinition("trail_min_alpha", "Krycie najstarszego śladu (0-255)", 140.0, 0.0, 255.0, true);

        /// <summary>
        /// Krycie najnowszego odcinka śladu.
        /// </summary>
        public static readonly SettingDefinition TrailMaxAlphaSetting = new SettingDefinition("trail_max_alpha", "Krycie najnowszego śladu (0-255)", 255.0, 0.0, 255.0, true);

        /// <summary>
        /// Grubość ciemnej obwódki śladu z każdej strony w dp.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineWidthSetting = new SettingDefinition("trail_outline_dp", "Obwódka śladu [dp]", 1.0, 0.0, 10.0, false);

        /// <summary>
        /// Krycie ciemnej obwódki śladu.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineAlphaSetting = new SettingDefinition("trail_outline_alpha", "Krycie obwódki (0-255)", 140.0, 0.0, 255.0, true);

        /// <summary>
        /// Krycie czarnej warstwy przyciemniającej kafelki mapy.
        /// </summary>
        public static readonly SettingDefinition MapDimSetting = new SettingDefinition("map_dim_alpha", "Przyciemnienie mapy (0-220)", 90.0, 0.0, 220.0, true);

        /// <summary>
        /// Grubość ciemnej obwódki ikon samolotów z każdej strony w dp.
        /// </summary>
        public static readonly SettingDefinition AircraftOutlineWidthSetting = new SettingDefinition("aircraft_outline_dp", "Obwódka samolotów [dp]", 1.2, 0.0, 6.0, false);

        /// <summary>
        /// Przełącznik widoczności etykiet z opisem samolotów: 0 ukrywa, 1 pokazuje.
        /// </summary>
        public static readonly SettingDefinition ShowLabelsSetting = new SettingDefinition("show_labels", "Etykiety samolotów (0 = ukryte, 1 = widoczne)", 0.0, 0.0, 1.0, true);

        /// <summary>
        /// Wielkość tekstu etykiet samolotów w dp.
        /// </summary>
        public static readonly SettingDefinition LabelTextSizeSetting = new SettingDefinition("label_text_dp", "Wielkość etykiet [dp]", 13.0, 6.0, 40.0, false);

        /// <summary>
        /// Wysokość koloru pomarańczowego w metrach.
        /// </summary>
        public static readonly SettingDefinition AltitudeOrangeSetting = new SettingDefinition("alt_orange_m", "Pomarańczowy do [m]", 300.0, 0.0, 20000.0, true);

        /// <summary>
        /// Wysokość koloru żółtego w metrach.
        /// </summary>
        public static readonly SettingDefinition AltitudeYellowSetting = new SettingDefinition("alt_yellow_m", "Żółty [m]", 1200.0, 0.0, 20000.0, true);

        /// <summary>
        /// Wysokość koloru zielonego w metrach.
        /// </summary>
        public static readonly SettingDefinition AltitudeGreenSetting = new SettingDefinition("alt_green_m", "Zielony [m]", 3000.0, 0.0, 20000.0, true);

        /// <summary>
        /// Wysokość koloru niebieskiego w metrach.
        /// </summary>
        public static readonly SettingDefinition AltitudeBlueSetting = new SettingDefinition("alt_blue_m", "Niebieski [m]", 6000.0, 0.0, 20000.0, true);

        /// <summary>
        /// Wysokość koloru fioletowego w metrach.
        /// </summary>
        public static readonly SettingDefinition AltitudePurpleSetting = new SettingDefinition("alt_purple_m", "Fioletowy od [m]", 12000.0, 0.0, 20000.0, true);

        /// <summary>
        /// Wszystkie ustawienia w kolejności wyświetlania.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
        {
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
            ShowLabelsSetting,
            LabelTextSizeSetting,
            AltitudeOrangeSetting,
            AltitudeYellowSetting,
            AltitudeGreenSetting,
            AltitudeBlueSetting,
            AltitudePurpleSetting,
        };

        /// <summary>
        /// Bieżące wartości ustawień według klucza, wczytywane leniwie z SharedPreferences.
        /// </summary>
        private static readonly ConcurrentDictionary<string, double> Values = new ConcurrentDictionary<string, double>();

        /// <summary>
        /// Blokada chroniąca jednorazowe wczytanie ustawień.
        /// </summary>
        private static readonly object LoadLock = new object();

        /// <summary>
        /// Informacja, czy ustawienia zostały już wczytane.
        /// </summary>
        private static bool loaded;

        /// <summary>
        /// Odstęp między zapytaniami o samoloty.
        /// </summary>
        public static TimeSpan FetchInterval => TimeSpan.FromSeconds(Get(FetchIntervalSetting));

        /// <summary>
        /// Odstęp między klatkami rysowania w milisekundach.
        /// </summary>
        public static int RenderIntervalMilliseconds => (int)Get(RenderIntervalSetting);

        /// <summary>
        /// Minimalny poziom przybliżenia, nie większy niż maksymalny.
        /// </summary>
        public static int MinZoom => Math.Min((int)Get(MinZoomSetting), (int)Get(MaxZoomSetting));

        /// <summary>
        /// Maksymalny poziom przybliżenia, nie mniejszy niż minimalny.
        /// </summary>
        public static int MaxZoom => Math.Max((int)Get(MinZoomSetting), (int)Get(MaxZoomSetting));

        /// <summary>
        /// Maksymalna liczba zdekodowanych kafelków w pamięci.
        /// </summary>
        public static int MaxCachedTiles => (int)Get(MaxCachedTilesSetting);

        /// <summary>
        /// Czas do ponowienia nieudanego pobrania kafelka.
        /// </summary>
        public static TimeSpan TileRetry => TimeSpan.FromSeconds(Get(TileRetrySetting));

        /// <summary>
        /// Czas pamiętania śladu.
        /// </summary>
        public static TimeSpan TrailDuration => TimeSpan.FromMinutes(Get(TrailDurationSetting));

        /// <summary>
        /// Maksymalna liczba punktów śladu jednego samolotu.
        /// </summary>
        public static int MaxTrailPoints => (int)Get(MaxTrailPointsSetting);

        /// <summary>
        /// Grubość linii śladu w dp.
        /// </summary>
        public static float TrailWidth => (float)Get(TrailWidthSetting);

        /// <summary>
        /// Krycie najstarszego odcinka śladu, nie większe niż najnowszego.
        /// </summary>
        public static int TrailMinAlpha => Math.Min((int)Get(TrailMinAlphaSetting), (int)Get(TrailMaxAlphaSetting));

        /// <summary>
        /// Krycie najnowszego odcinka śladu.
        /// </summary>
        public static int TrailMaxAlpha => (int)Get(TrailMaxAlphaSetting);

        /// <summary>
        /// Grubość ciemnej obwódki śladu z każdej strony w dp.
        /// </summary>
        public static float TrailOutlineWidth => (float)Get(TrailOutlineWidthSetting);

        /// <summary>
        /// Krycie ciemnej obwódki śladu.
        /// </summary>
        public static int TrailOutlineAlpha => (int)Get(TrailOutlineAlphaSetting);

        /// <summary>
        /// Krycie czarnej warstwy przyciemniającej kafelki mapy.
        /// </summary>
        public static int MapDim => (int)Get(MapDimSetting);

        /// <summary>
        /// Grubość ciemnej obwódki ikon samolotów z każdej strony w dp.
        /// </summary>
        public static float AircraftOutlineWidth => (float)Get(AircraftOutlineWidthSetting);

        /// <summary>
        /// Informacja, czy etykiety z opisem samolotów są widoczne na mapie.
        /// </summary>
        public static bool ShowLabels => Get(ShowLabelsSetting) >= 0.5;

        /// <summary>
        /// Wielkość tekstu etykiet samolotów w dp.
        /// </summary>
        public static float LabelTextSize => (float)Get(LabelTextSizeSetting);

        /// <summary>
        /// Wysokości progów kolorów w metrach, od pomarańczowego do fioletowego, posortowane rosnąco.
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
        /// Zwraca bieżącą wartość ustawienia.
        /// </summary>
        /// <param name="definition">Ustawienie.</param>
        /// <returns>Wartość zapisana lub domyślna.</returns>
        public static double Get(SettingDefinition definition)
        {
            EnsureLoaded();

            return Values.TryGetValue(definition.Key, out double value) ? value : definition.DefaultValue;
        }

        /// <summary>
        /// Zapisuje wartości ustawień po ograniczeniu ich do dozwolonych zakresów.
        /// </summary>
        /// <param name="newValues">Nowe wartości według ustawienia.</param>
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
        /// Przywraca wartości domyślne wszystkich ustawień.
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
        /// Wczytuje ustawienia z SharedPreferences przy pierwszym użyciu.
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
        /// Otwiera plik SharedPreferences z ustawieniami.
        /// </summary>
        /// <returns>Obiekt SharedPreferences.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

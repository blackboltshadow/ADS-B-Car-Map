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
        public static readonly SettingDefinition FetchIntervalSetting = new SettingDefinition("fetch_interval_s", "Odświeżanie samolotów [s]", 3.0, 1.0, 60.0, false, Help: "Co ile sekund aplikacja pobiera z serwera ADS-B (adsb.lol) aktualną listę samolotów w widocznym obszarze mapy. Mniejsza wartość daje świeższe pozycje, ale więcej zapytań. Gdy serwer odmówi (kod 429, limit zapytań), aplikacja sama poczeka dłużej, a ikona stanu w lewym dolnym rogu pokaże odliczanie.");

        /// <summary>
        /// Interval between drawing frames in milliseconds.
        /// </summary>
        public static readonly SettingDefinition RenderIntervalSetting = new SettingDefinition("render_interval_ms", "Odstęp klatek mapy [ms]", 33.0, 16.0, 1000.0, true, Help: "Czas między kolejnymi klatkami rysowania mapy w milisekundach. 33 ms to około 30 klatek na sekundę, 16 ms to około 60. Mniejsza wartość daje płynniejszy ruch, ale zużywa więcej baterii i procesora, co w samochodzie ma znaczenie dla płynności interfejsu.");

        /// <summary>
        /// Minimum zoom level.
        /// </summary>
        public static readonly SettingDefinition MinZoomSetting = new SettingDefinition("min_zoom", "Minimalny zoom", 4.0, 1.0, 18.0, true, Help: "Najmniejsze dozwolone przybliżenie mapy. Niski poziom pokazuje duży obszar (4 to mniej więcej cały kraj lub kontynent), wysoki to pojedyncze miasta. Obszar, z którego pobierane są samoloty, zależy od widocznego fragmentu mapy, więc po oddaleniu aplikacja prosi serwer o większy zasięg, aż do jego limitu.");

        /// <summary>
        /// Maximum zoom level.
        /// </summary>
        public static readonly SettingDefinition MaxZoomSetting = new SettingDefinition("max_zoom", "Maksymalny zoom", 13.0, 1.0, 18.0, true, Help: "Największe dozwolone przybliżenie mapy. Poziom 13 pokazuje pojedyncze dzielnice i lotniska z dużą szczegółowością, wyższe poziomy wymagają pobierania znacznie więcej kafelków mapy. Jeśli minimalny zoom jest większy od maksymalnego, aplikacja używa mniejszej z tych dwóch wartości jako minimum.");

        /// <summary>
        /// Maximum number of decoded tiles kept in memory.
        /// </summary>
        public static readonly SettingDefinition MaxCachedTilesSetting = new SettingDefinition("max_cached_tiles", "Kafelki w pamięci", 128.0, 16.0, 1024.0, true, Help: "Ile gotowych kafelków mapy (obrazków 256 × 256 px) aplikacja trzyma w pamięci operacyjnej. Więcej kafelków oznacza, że przesuwanie i powrót w to samo miejsce nie wymagają ponownego pobierania, ale zajmuje więcej pamięci. Kafelki nie są jeszcze zapisywane na dysku, więc po ponownym uruchomieniu pobierane są od nowa.");

        /// <summary>
        /// Time in seconds before a failed tile download is retried.
        /// </summary>
        public static readonly SettingDefinition TileRetrySetting = new SettingDefinition("tile_retry_s", "Ponowienie kafelka [s]", 15.0, 1.0, 600.0, true, Help: "Po ilu sekundach aplikacja ponowi pobieranie kafelka mapy, którego nie udało się pobrać (np. przy braku zasięgu). Krótszy czas szybciej uzupełni mapę po powrocie sieci, ale częściej obciąża serwer kafelków OpenStreetMap, który ma politykę uczciwego użytkowania.");

        /// <summary>
        /// Time the trail is remembered, in minutes.
        /// </summary>
        public static readonly SettingDefinition TrailDurationSetting = SettingDefinition.CreateSlider("trail_duration_min", "Długość śladu", 30.0, 0.0, 120.0, 1.0, " min", "Jak długo, w minutach, aplikacja pamięta pozycje samolotu i rysuje je jako ślad za ikoną. Ślad starszy niż ta wartość jest usuwany. Wartość 0 wyłącza ślady całkowicie. Ślady zbiera aplikacja od momentu, gdy samolot pojawi się w widocznym obszarze, więc po uruchomieniu zaczynają się puste.");

        /// <summary>
        /// Maximum number of trail points of one aircraft.
        /// </summary>
        public static readonly SettingDefinition MaxTrailPointsSetting = SettingDefinition.CreateSlider("trail_max_points", "Maks. punktów śladu", 700.0, 10.0, 5000.0, 10.0, " pkt", "Górny limit liczby punktów, z których składa się ślad jednego samolotu. Gdy limit zostanie przekroczony, najstarsze punkty są usuwane, nawet jeśli czas śladu jeszcze nie minął. Ogranicza zużycie pamięci i czas rysowania przy dużej liczbie samolotów.");

        /// <summary>
        /// Width of the trail line in dp.
        /// </summary>
        public static readonly SettingDefinition TrailWidthSetting = SettingDefinition.CreateSlider("trail_width_dp", "Grubość śladu", 3.0, 0.5, 20.0, 0.5, " dp", "Grubość linii śladu za samolotem w dp (jednostka niezależna od gęstości ekranu, więc ślad wygląda podobnie na różnych telefonach i w samochodzie). Kolor śladu zależy od wysokości, tak jak kolor ikony.");

        /// <summary>
        /// Opacity of the oldest trail segment.
        /// </summary>
        public static readonly SettingDefinition TrailMinAlphaSetting = SettingDefinition.CreateSlider("trail_min_alpha", "Krycie najstarszego śladu (0-255)", 140.0, 0.0, 255.0, 5.0, "", "Przezroczystość najstarszego końca śladu: 0 to niewidoczny, 255 to w pełni kryjący. Ślad stopniowo przechodzi od tej wartości (daleko za samolotem) do wartości dla najnowszego odcinka (przy samolocie), dzięki czemu starsza część wyraźnie znika. Wartość nie może być większa niż krycie najnowszego odcinka.");

        /// <summary>
        /// Opacity of the newest trail segment.
        /// </summary>
        public static readonly SettingDefinition TrailMaxAlphaSetting = SettingDefinition.CreateSlider("trail_max_alpha", "Krycie najnowszego śladu (0-255)", 255.0, 0.0, 255.0, 5.0, "", "Przezroczystość najnowszego odcinka śladu, tuż za ikoną samolotu: 0 to niewidoczny, 255 to w pełni kryjący. Razem z kryciem najstarszego śladu tworzy gradient przezroczystości wzdłuż całego śladu.");

        /// <summary>
        /// Width of the dark trail outline on each side in dp.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineWidthSetting = SettingDefinition.CreateSlider("trail_outline_dp", "Obwódka śladu", 1.0, 0.0, 10.0, 0.5, " dp", "Grubość ciemnej obwódki, którą aplikacja rysuje pod śladem z każdej strony, w dp. Obwódka poprawia czytelność kolorowego śladu na jasnych fragmentach mapy. Wartość 0 wyłącza obwódkę.");

        /// <summary>
        /// Opacity of the dark trail outline.
        /// </summary>
        public static readonly SettingDefinition TrailOutlineAlphaSetting = SettingDefinition.CreateSlider("trail_outline_alpha", "Krycie obwódki (0-255)", 140.0, 0.0, 255.0, 5.0, "", "Przezroczystość ciemnej obwódki śladu: 0 to niewidoczna, 255 to w pełni kryjąca. Dotyczy tylko obwódki śladu. Obwódkę ikon samolotów ustawia się osobno w sekcji „Ikony samolotów”.");

        /// <summary>
        /// Opacity of the black layer that dims the map tiles.
        /// </summary>
        public static readonly SettingDefinition MapDimSetting = new SettingDefinition("map_dim_alpha", "Przyciemnienie mapy (0-220)", 90.0, 0.0, 220.0, true, Help: "Siła czarnej warstwy nakładanej na kafelki mapy: 0 to mapa bez przyciemnienia, 220 to bardzo ciemna. Przyciemniona mapa sprawia, że kolorowe ikony i ślady samolotów są lepiej widoczne, szczególnie w samochodzie lub w nocy.");

        /// <summary>
        /// Switch for the airports drawn on the map.
        /// </summary>
        public static readonly SettingDefinition ShowAirportsSetting = SettingDefinition.CreateSwitch("show_airports", "Lotniska na mapie", true, "Pokazuje na mapie lotniska z bazy OurAirports razem z pasami startowymi przy dużym przybliżeniu. Dotknięcie lotniska pokazuje jego dane: nazwę, kody IATA i ICAO, miasto, kraj, wysokość nad poziomem morza i odległość od Ciebie. Lotniska można też wyszukać w oknie wyszukiwania, na telefonie i w samochodzie. Lotniska wojskowe mają inny kolor.");

        /// <summary>
        /// Switch that limits the airports on the map to the favorite ones.
        /// </summary>
        public static readonly SettingDefinition ShowOnlyFavoriteAirportsSetting = SettingDefinition.CreateSwitch("airport_only_favorites", "Tylko ulubione lotniska", false, "Pokazuje na mapie wyłącznie ulubione lotniska (ekran „Ulubione lotniska” w menu), bez żadnych innych. Ulubione są wtedy widoczne przy każdym poziomie przybliżenia, a rodzaje lotnisk, filtr małych lotnisk i zakres zoomu są nieaktywne, bo nie mają znaczenia. Kody lotnisk i ich wielkość działają dalej.");

        /// <summary>
        /// Which categories of airports are shown, as a bit mask of AirportCategory.
        /// </summary>
        public static readonly SettingDefinition AirportTypesSetting = SettingDefinition.CreateMultiChoice("airport_types", "Rodzaje lotnisk na mapie", (int)(AirportCategory.Large | AirportCategory.Medium | AirportCategory.Military), "Zaznacz, jakie lotniska mają być widoczne. Duże, średnie i małe to lotniska cywilne, wojskowe (czerwone) to lotniska wojskowe każdej wielkości, a helipady to lądowiska dla śmigłowców, których są dziesiątki tysięcy. Zakres zoomu, w którym lotniska są widoczne, jest wspólny dla wszystkich rodzajów i ustawia się go osobną kontrolką niżej. Gdy małych lotnisk albo helipadów w widoku jest dużo, pokazuje się najbliższe środka ekranu, a etykiety mniej ważnych są pomijane, żeby się nie nakładały. Helipady są też szukane w wyszukiwarce tylko wtedy, gdy są zaznaczone.", "Duże", "Średnie", "Małe", "Wojskowe", "Helipady");

        /// <summary>
        /// Switch that limits the small airports on the map to those with a paved runway of at least 800 m. Available only while the small airports are switched on.
        /// </summary>
        public static readonly SettingDefinition SmallAirportsPavedSetting = SettingDefinition.CreateSwitch("airport_small_paved", "Małe: tylko z utwardzonym pasem od 800 m", false, "Ogranicza małe lotniska na mapie do tych, które mają utwardzony pas (asfalt, beton) długości co najmniej 800 m. W Polsce z ponad 300 małych lotnisk zostaje wtedy kilkanaście, na świecie około 4800 z ponad 42 tys. Opcja jest dostępna tylko wtedy, gdy zaznaczone są małe lotniska. Lotnisko bez danych o pasach jest wtedy ukryte, a ulubione lotniska są zawsze widoczne, bez względu na ten filtr.");

        /// <summary>
        /// Switch for the codes drawn next to the airports.
        /// </summary>
        public static readonly SettingDefinition ShowAirportLabelsSetting = SettingDefinition.CreateSwitch("show_airport_labels", "Kody lotnisk", true, "Pokazuje obok znacznika lotniska jego kod: IATA, a gdy lotnisko go nie ma, ICAO. Etykiety ważniejszych lotnisk mają pierwszeństwo, a te, które by się nakładały, są pomijane.");

        /// <summary>
        /// Size of the airport code text in dp.
        /// </summary>
        public static readonly SettingDefinition AirportLabelSizeSetting = SettingDefinition.CreateSlider("airport_label_dp", "Wielkość kodów lotnisk", 11.0, 6.0, 30.0, 0.5, " dp", "Wielkość liter kodów IATA lub ICAO obok znaczników lotnisk, w dp (jednostka niezależna od gęstości ekranu). Większe litery są czytelniejsze w samochodzie, ale zajmują więcej miejsca, więc więcej etykiet zostanie pominiętych, żeby się nie nakładały. Rozmiar znaczników lotnisk zmienia ustawienie „Rozmiar ikon samolotów”.");

        /// <summary>
        /// Zoom level from which the airports appear, the lower end of the visibility range of all airport categories.
        /// </summary>
        public static readonly SettingDefinition AirportFromZoomSetting = new SettingDefinition("airport_from_zoom", "Lotniska od zoomu", 5.0, 1.0, 18.0, true, SettingKind.ZoomRange, null, 0.0, "Zakres poziomów przybliżenia mapy, w którym widać lotniska. Lewy uchwyt to zoom, od którego lotniska się pojawiają, prawy to zoom, powyżej którego znikają (prawy koniec skali oznacza brak górnego limitu). Zakres dotyczy wszystkich zaznaczonych rodzajów lotnisk jednakowo: o tym, które rodzaje są widoczne, decydują pola „Rodzaje lotnisk na mapie”. Domyślnie od zoomu 5, bez górnego limitu. Przesuń lewy uchwyt w prawo, jeśli lotniska zalewają mapę, a prawy w lewo, jeśli na dużym przybliżeniu mają zniknąć. Przy dużej liczbie małych lotnisk i helipadów w widoku pokazuje się 60 małych i 40 helipadów najbliższych środka ekranu. Ulubione lotniska są widoczne od zoomu 5 niezależnie od tego zakresu.");

        /// <summary>
        /// Zoom level above which the airports disappear; 18 means no upper limit.
        /// </summary>
        public static readonly SettingDefinition AirportToZoomSetting = new SettingDefinition("airport_to_zoom", "Lotniska do zoomu", 18.0, 1.0, 18.0, true, SettingKind.ZoomRange);

        /// <summary>
        /// Size of the aircraft icons as a multiple of the default size, on the phone and in Android Auto.
        /// </summary>
        public static readonly SettingDefinition IconScaleSetting = SettingDefinition.CreateSlider("icon_scale", "Rozmiar ikon samolotów", 1.0, 0.6, 2.0, 0.1);

        /// <summary>
        /// Width of the dark outline of the aircraft icons on each side in dp.
        /// </summary>
        public static readonly SettingDefinition AircraftOutlineWidthSetting = SettingDefinition.CreateSlider("aircraft_outline_dp", "Obwódka samolotów", 1.2, 0.0, 6.0, 0.1);

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
            LabelTextSizeSetting,
            AltitudeOrangeSetting,
            AltitudeYellowSetting,
            AltitudeGreenSetting,
            AltitudeBlueSetting,
            AltitudePurpleSetting,
            ShowAirportsSetting,
            ShowOnlyFavoriteAirportsSetting,
            AirportLabelSizeSetting,
            ShowAirportLabelsSetting,
            AirportTypesSetting,
            SmallAirportsPavedSetting,
            AirportFromZoomSetting,
            AirportToZoomSetting,
            IconScaleSetting,
            AircraftOutlineWidthSetting,
            TrailDurationSetting,
            MaxTrailPointsSetting,
            TrailWidthSetting,
            TrailMinAlphaSetting,
            TrailMaxAlphaSetting,
            TrailOutlineWidthSetting,
            TrailOutlineAlphaSetting,
            FetchIntervalSetting,
            RenderIntervalSetting,
            MinZoomSetting,
            MaxZoomSetting,
            MaxCachedTilesSetting,
            TileRetrySetting,
            MapDimSetting,
        };

        /// <summary>
        /// Settings that describe the airports. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> AirportSettings = new List<SettingDefinition>
        {
            ShowAirportsSetting,
            ShowOnlyFavoriteAirportsSetting,
            AirportLabelSizeSetting,
            ShowAirportLabelsSetting,
            AirportTypesSetting,
            SmallAirportsPavedSetting,
            AirportFromZoomSetting,
            AirportToZoomSetting,
        };

        /// <summary>
        /// Settings that describe the aircraft trails. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> TrailSettings = new List<SettingDefinition>
        {
            TrailDurationSetting,
            MaxTrailPointsSetting,
            TrailWidthSetting,
            TrailMinAlphaSetting,
            TrailMaxAlphaSetting,
            TrailOutlineWidthSetting,
            TrailOutlineAlphaSetting,
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
        /// Whether the airports are drawn on the map.
        /// </summary>
        public static bool ShowAirports => Get(ShowAirportsSetting) >= 0.5;

        /// <summary>
        /// Whether only the favorite airports are drawn.
        /// </summary>
        public static bool ShowOnlyFavoriteAirports => Get(ShowOnlyFavoriteAirportsSetting) >= 0.5;

        /// <summary>
        /// Whether the small airports are limited to those with a paved runway of at least 800 m.
        /// </summary>
        public static bool SmallAirportsPavedOnly => Get(SmallAirportsPavedSetting) >= 0.5;

        /// <summary>
        /// Bit mask of the airport categories shown on the map.
        /// </summary>
        public static int AirportTypes => (int)Get(AirportTypesSetting);

        /// <summary>
        /// Tells whether a category of airports is switched on.
        /// </summary>
        /// <param name="category">Category.</param>
        /// <returns>True when the category is shown on the map.</returns>
        public static bool IsAirportCategoryEnabled(AirportCategory category)
        {
            return (AirportTypes & (int)category) != 0;
        }

        /// <summary>
        /// Tells whether the setting of the categories allows an airport to be shown. A military airport is judged by the military category only, the others by their size class.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>True when the airport belongs to a category that is switched on.</returns>
        public static bool IsAirportCategoryEnabled(Data.Airport airport)
        {
            return IsAirportCategoryEnabled(GetCategory(airport));
        }

        /// <summary>
        /// Returns the category of an airport: military, or its size class.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Category of the airport.</returns>
        public static AirportCategory GetCategory(Data.Airport airport)
        {
            if (airport.IsMilitary)
            {
                return AirportCategory.Military;
            }

            switch (airport.Type)
            {
                case Data.AirportType.Large:
                    return AirportCategory.Large;
                case Data.AirportType.Medium:
                    return AirportCategory.Medium;
                case Data.AirportType.Heliport:
                    return AirportCategory.Heliport;
                default:
                    return AirportCategory.Small;
            }
        }

        /// <summary>
        /// Whether the codes of the airports are drawn on the map.
        /// </summary>
        public static bool ShowAirportLabels => Get(ShowAirportLabelsSetting) >= 0.5;

        /// <summary>
        /// Size of the airport code text in dp.
        /// </summary>
        public static float AirportLabelSize => (float)Get(AirportLabelSizeSetting);

        /// <summary>
        /// Zoom level from which the airports are drawn, the lower end of the visibility range.
        /// </summary>
        public static double AirportFromZoom => Get(AirportFromZoomSetting);

        /// <summary>
        /// Zoom level above which the airports are not drawn, or 18 when there is no upper limit.
        /// </summary>
        public static double AirportToZoom => Get(AirportToZoomSetting);

        /// <summary>
        /// Size of the aircraft icons as a multiple of the default size.
        /// </summary>
        public static float IconScale => (float)Get(IconScaleSetting);

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

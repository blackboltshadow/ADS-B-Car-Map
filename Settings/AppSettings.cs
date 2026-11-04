using AdsbCarMap.Localization;
using System.Collections.Concurrent;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Adsbdb;
using AdsbCarMap.DataSources.AdsbLol;
using AdsbCarMap.DataSources.Hexdb;
using AdsbCarMap.DataSources.AdsbFi;
using AdsbCarMap.DataSources.AirplanesLive;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.Selection;
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
        /// Whether the mode that shows only military aircraft downloads all the military aircraft with one request.
        /// </summary>
        public static readonly SettingDefinition MilitaryGlobalSetting = SettingDefinition.CreateSwitch("military_global", "Wojskowe z całego świata", true, "Gdy włączony jest tryb „tylko samoloty wojskowe” (przycisk z myśliwcem), aplikacja pobiera wszystkie samoloty wojskowe jednym zapytaniem (adsb.lol, /v2/mil) zamiast tylko tych z widocznego obszaru. Dzięki temu widać je także po oddaleniu mapy poza zasięg zwykłego zapytania (250 mil morskich) i po przesunięciu mapy bez czekania na nowe pobranie. Zapytanie nie zawiera Twojego położenia. Odpowiedź jest duża (kilkaset samolotów z całego świata), więc jest pobierana rzadziej (suwak niżej). Po wyłączeniu trybu wojskowych aplikacja wraca do zwykłego pobierania obszaru. Gdy źródło nie ma takiego zapytania albo odmawia, aplikacja pobiera obszar jak zwykle. Alerty nie są sprawdzane na liście wojskowych z całego świata.");

        /// <summary>
        /// Shortest time between two downloads of all the military aircraft.
        /// </summary>
        public static readonly SettingDefinition MilitaryRefreshSecondsSetting = SettingDefinition.CreateSlider("military_refresh_s", "Odświeżanie wojskowych z całego świata", 30.0, 15.0, 120.0, 5.0, " s", "Najkrótsza przerwa między pobraniami wszystkich samolotów wojskowych w trybie „tylko samoloty wojskowe”. Odpowiedź ma ok. 150 kB, a serwis ma limit zapytań (zdarzają się odpowiedzi 429), więc ta przerwa jest zwykle dłuższa niż odświeżanie zwykłych samolotów. Gdy odświeżanie zwykłych samolotów jest dłuższe niż ten suwak, obowiązuje to dłuższe.");

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
        public static readonly SettingDefinition MaxCachedTilesSetting = new SettingDefinition("max_cached_tiles", "Kafelki w pamięci", 128.0, 16.0, 1024.0, true, Help: "Ile gotowych kafelków mapy (obrazków 256 × 256 px) aplikacja trzyma w pamięci operacyjnej. Więcej kafelków oznacza, że przesuwanie i powrót w to samo miejsce nie wymagają ponownego pobierania, ale zajmuje więcej pamięci. To osobna pamięć od pamięci podręcznej na dysku (sekcja „Pamięć podręczna map”).");

        /// <summary>
        /// Time in seconds before a failed tile download is retried.
        /// </summary>
        public static readonly SettingDefinition TileRetrySetting = new SettingDefinition("tile_retry_s", "Ponowienie kafelka [s]", 15.0, 1.0, 600.0, true, Help: "Po ilu sekundach aplikacja ponowi pobieranie kafelka mapy, którego nie udało się pobrać (np. przy braku zasięgu). Krótszy czas szybciej uzupełni mapę po powrocie sieci, ale częściej obciąża serwer kafelków OpenStreetMap, który ma politykę uczciwego użytkowania.");

        /// <summary>
        /// Names of the aircraft sources in the order in which the application registers them.
        /// </summary>
        private static readonly IReadOnlyList<string> DataSourceNames = new List<string> { AdsbLolClient.SourceName, AirplanesLiveClient.SourceName, AdsbFiClient.SourceName };

        /// <summary>
        /// All orders of the aircraft sources that the user can choose from; the stored value is the index in this list. The settings screen shows them as a list that is dragged, not as a drop-down.
        /// </summary>
        public static readonly IReadOnlyList<IReadOnlyList<string>> DataSourceOrders = SourceOrder.GetOrders(DataSourceNames);

        /// <summary>
        /// Order in which the aircraft sources are asked: the first one is the main source, the next ones are asked when the previous one gives no data.
        /// </summary>
        public static readonly SettingDefinition DataSourceSetting = SettingDefinition.CreateChoice("data_source", "Kolejność źródeł samolotów", 0, "Kolejność, w jakiej aplikacja pyta serwisy o pozycje samolotów. Pierwszy serwis jest głównym źródłem, a kolejne są pytane tylko wtedy, gdy poprzedni nie zwraca danych (przy włączonym zapasowym źródle). Serwisy adsb.lol, airplanes.live i adsb.fi mają ten sam format danych i służą do użytku osobistego, niekomercyjnego (adsb.fi dopuszcza zapytania co najwyżej raz na sekundę i wymaga atrybucji). Uwaga: airplanes.live odrzuca na razie zapytania tej aplikacji (kod 403) i prosi o kontakt e-mailowy, więc do czasu uzgodnienia głównym źródłem powinno być adsb.lol. Zmiana działa od następnego odświeżenia mapy.", DataSourceOrders.Select(SourceOrder.Describe).ToArray());

        /// <summary>
        /// Whether the next sources are asked when the previous one gives no data.
        /// </summary>
        public static readonly SettingDefinition DataSourceFallbackSetting = SettingDefinition.CreateSwitch("data_source_fallback", "Zapasowe źródło przy braku danych", true, "Gdy główne źródło nie zwraca danych (limit zapytań 429, brak połączenia z serwisem albo błąd serwera), aplikacja pyta kolejne źródło z listy zamiast pokazywać pustą mapę. To samo dotyczy tras lotów: gdy źródło nie zna lotu albo nie odpowiada, aplikacja pyta kolejne. Pusta lista samolotów z działającego serwisu nie jest brakiem danych, bo w rzadko uczęszczanych rejonach to normalne. Wyłącz, jeśli chcesz używać wyłącznie głównego źródła.");

        /// <summary>
        /// Whether the type, the registration and the category of the selected aircraft are completed from the other sources when the main source lacks them.
        /// </summary>
        public static readonly SettingDefinition DataSourceDetailsSetting = SettingDefinition.CreateSwitch("data_source_details", "Uzupełniaj dane zaznaczonego samolotu", true, "Gdy główne źródło nie podaje typu, rejestracji lub kategorii zaznaczonego samolotu, aplikacja dopyta o ten jeden samolot pozostałe źródła (po kodzie ICAO, jedno krótkie zapytanie na samolot, nie częściej niż raz na 10 minut). Dzięki temu karta samolotu, ikona i filtry mają komplet danych. Dotyczy tylko samolotu, który zaznaczysz, a nie całej mapy.");

        /// <summary>
        /// Names of the flight route sources in the order in which the application registers them.
        /// </summary>
        private static readonly IReadOnlyList<string> RouteSourceNames = new List<string> { AdsbdbClient.SourceName, AdsbLolClient.SourceName, HexdbClient.SourceName };

        /// <summary>
        /// All orders of the flight route sources that the user can choose from; the stored value is the index in this list. The settings screen shows them as a list that is dragged, not as a drop-down.
        /// </summary>
        public static readonly IReadOnlyList<IReadOnlyList<string>> RouteSourceOrders = SourceOrder.GetOrders(RouteSourceNames);

        /// <summary>
        /// Order in which the flight route sources are asked: the first one is the main source, the next ones are asked when the previous one does not give the route.
        /// </summary>
        public static readonly SettingDefinition RouteSourceSetting = SettingDefinition.CreateChoice("route_source", "Kolejność źródeł tras", 0, "Kolejność, w jakiej aplikacja pyta serwisy o trasę lotu po znaku wywoławczym. Pierwszy serwis jest głównym źródłem, kolejne są pytane, gdy poprzedni nie zna lotu albo nie odpowiada (przy włączonym zapasowym źródle). adsbdb.com podaje też nazwę linii lotniczej, adsb.lol podaje trasę z nazwami i współrzędnymi lotnisk, a hexdb.io tylko parę lotnisk (resztę aplikacja uzupełnia z własnej bazy lotnisk), przy czym jego dane bywają starsze. Trasy są przypisane do znaku wywoławczego, więc nie zawsze odpowiadają bieżącemu przelotowi. Zmiana działa od następnego pobrania trasy.", RouteSourceOrders.Select(SourceOrder.Describe).ToArray());

        /// <summary>
        /// Size limit of the tile cache on the disk in megabytes.
        /// </summary>
        public static readonly SettingDefinition TileCacheSizeSetting = SettingDefinition.CreateSlider("tile_cache_mb", "Limit pamięci podręcznej", 100.0, 10.0, 1000.0, 10.0, " MB", "Maksymalny rozmiar kafelków mapy zapisanych na dysku telefonu. Po przekroczeniu limitu aplikacja usuwa najstarsze kafelki. Kafelki z dysku pozwalają pokazać mapę zaraz po uruchomieniu i bez zasięgu, a także zmniejszają ruch do serwera OpenStreetMap. Jeden kafelek zajmuje zwykle kilkanaście kilobajtów. Zmniejszenie limitu od razu usuwa nadmiar.");

        /// <summary>
        /// Number of days a tile on the disk is considered fresh.
        /// </summary>
        public static readonly SettingDefinition TileCacheDaysSetting = SettingDefinition.CreateSlider("tile_cache_days", "Czas trzymania kafelków", 7.0, 1.0, 90.0, 1.0, " dni", "Przez ile dni kafelek zapisany na dysku jest uznawany za aktualny i używany bez pytania serwera. Po tym czasie aplikacja pobiera go ponownie, aby mapa nadążała za zmianami w OpenStreetMap. Gdy pobranie się nie uda (brak zasięgu, limit zapytań), użyty zostaje starszy kafelek z dysku, dopóki nie zostanie usunięty z powodu limitu rozmiaru.");

        /// <summary>
        /// How many minutes after the last sighting the information about a single flight or aircraft (route, type, registration) is kept before it is asked about again.
        /// </summary>
        public static readonly SettingDefinition FlightInfoCacheMinutesSetting = SettingDefinition.CreateSlider("flight_info_cache_minutes", "Czas pamiętania lotu po ostatnim zobaczeniu", 10.0, 1.0, 30.0, 1.0, " min", "Jak długo aplikacja pamięta informacje o pojedynczym locie lub samolocie (trasę po znaku wywoławczym oraz typ i rejestrację uzupełnione z innych źródeł) od chwili, gdy ostatnio widziała ten samolot na mapie. Dopóki samolot jest widoczny, jego dane są odświeżane i aplikacja nie pyta o nie ponownie, nawet gdy leci wiele godzin. Gdy samolot zniknie (wyląduje, wyłączy transponder albo wyleci poza widoczny obszar), po tym czasie jest zapominany. Samolot, który właśnie wylądował, jest zapominany szybciej, po około 2 minutach. Dłuższy czas oznacza mniej zapytań, gdy wracasz do tych samych lotów, krótszy świeższe dane. Pozycje samolotów na mapie nie są tu zapamiętywane, bo zmieniają się co chwilę. Odpowiedź „serwer nie zna tego lotu” jest pamiętana krócej.");

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
        /// Map theme: automatic (follows the system or the car night mode), light or dark.
        /// </summary>
        public static readonly SettingDefinition MapThemeSetting = SettingDefinition.CreateChoice("map_theme", "Motyw mapy", 0, "Automatyczny (domyślnie): mapa robi się ciemna, gdy system telefonu lub samochód (Android Auto) przełączy się na tryb nocny. Jasny: mapa zawsze w oryginalnych kolorach. Ciemny: mapa zawsze ciemna, z zachowaniem barw (woda niebieska, zieleń zielona). Ciemny motyw odwraca jasność kafelków, więc w nocy mapa nie razi w oczy; możesz go łączyć z przyciemnieniem mapy.", "Automatyczny", "Jasny", "Ciemny");

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
        public static readonly SettingDefinition AirportTypesSetting = SettingDefinition.CreateMultiChoice("airport_types", "Rodzaje lotnisk na mapie", (int)(AirportCategory.Large | AirportCategory.Medium), "Zaznacz, jakie lotniska mają być widoczne. Duże, średnie i małe to lotniska cywilne, wojskowe (czerwone) to lotniska wojskowe każdej wielkości, a helipady to lądowiska dla śmigłowców, których są dziesiątki tysięcy. Zakres zoomu, w którym lotniska są widoczne, jest wspólny dla wszystkich rodzajów i ustawia się go osobną kontrolką niżej. Gdy małych lotnisk albo helipadów w widoku jest dużo, pokazuje się najbliższe środka ekranu, a etykiety mniej ważnych są pomijane, żeby się nie nakładały. Helipady są też szukane w wyszukiwarce tylko wtedy, gdy są zaznaczone.", "Duże", "Średnie", "Małe", "Wojskowe", "Helipady");

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
        /// Strength of the soft shadow cast by the aircraft icons, in percent.
        /// </summary>
        public static readonly SettingDefinition AircraftShadowSetting = SettingDefinition.CreateSlider("aircraft_shadow_percent", "Cień samolotów", 40.0, 0.0, 100.0, 5.0, " %", "Siła miękkiego cienia, który ikona samolotu rzuca na mapę w prawo i w dół. Cień oddziela ikonę od tła i nadaje jej głębi. Wartość 0 wyłącza cień. Obowiązuje na telefonie i w Android Auto.");

        /// <summary>
        /// Strength of the dark cockpit accent drawn on the aircraft icons, in percent.
        /// </summary>
        public static readonly SettingDefinition AircraftCockpitSetting = SettingDefinition.CreateSlider("aircraft_cockpit_percent", "Akcent kabiny", 60.0, 0.0, 100.0, 5.0, " %", "Siła ciemnego akcentu w miejscu kabiny pilotów (szyba kokpitu) na ikonie samolotu, śmigłowca i drona. Akcent pokazuje, w którą stronę leci samolot, nawet gdy ikona jest mała. Wartość 0 wyłącza akcent. Ikony bez kabiny (balon, obiekt naziemny, strzałka) nie mają akcentu.");

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
        /// Language of the user interface (phone and Android Auto); the stored value is the index of the language in <see cref="Loc.LanguageNames"/>.
        /// </summary>
        public static readonly SettingDefinition LanguageSetting = SettingDefinition.CreateChoice("language", "Język aplikacji", 0, "Język interfejsu aplikacji, na telefonie i w Android Auto. Zmiana działa od razu. Teksty, które przychodzą z zewnątrz (nazwy lotnisk, linii i typów samolotów), zostają w oryginalnym języku.", Loc.LanguageNames.ToArray());

        /// <summary>
        /// Settings of the language section. They share one card on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> LanguageSettings = new List<SettingDefinition>
        {
            LanguageSetting,
        };

        /// <summary>
        /// Altitude unit used across the application (phone and Android Auto): meters, feet or flight level.
        /// </summary>
        public static readonly SettingDefinition AltitudeUnitSetting = SettingDefinition.CreateChoice("altitude_unit", "Jednostka wysokości", 0, "Jednostka wysokości w całej aplikacji, na telefonie i w Android Auto. Pokazuje się: na etykietach samolotów na mapie; w wynikach wyszukiwania (wysokość samolotu i lotniska); na karcie i w popupie samolotu (Wysokość); na karcie i w popupie lotniska (wysokość lotniska oraz długość pasów, które w stopach i poziomach lotu są podawane w stopach); w legendzie i w edytorze skali kolorów; w powiadomieniach alertów; w filtrach (pole Wysokość w edytorze i opis filtra). Metry to m i km (w legendzie), stopy to ft, a poziom lotu (FL) to wysokość w setkach stóp, np. FL350 to 35 000 ft.", "Metry (m, km)", "Stopy (ft)", "Poziom lotu (FL)");

        /// <summary>
        /// Distance unit used across the application (phone and Android Auto): kilometers, nautical miles or statute miles.
        /// </summary>
        public static readonly SettingDefinition DistanceUnitSetting = SettingDefinition.CreateChoice("distance_unit", "Jednostka odległości", 0, "Jednostka odległości w całej aplikacji, na telefonie i w Android Auto. Pokazuje się: na karcie i w popupie samolotu (do celu oraz Od Ciebie); na karcie i w popupie lotniska (Odległość od Ciebie); w alertach (promień obszaru w edytorze i na liście oraz odległość samolotu w powiadomieniu). Nie dotyczy ustawień prywatności w sekcji Dev, które zawsze są w km, bo opisują technicznie zapytanie do serwera. Mila morska (NM) to 1,852 km, mila lądowa (mi) to 1,609 km.", "Kilometry (km)", "Mile morskie (NM)", "Mile lądowe (mi)");

        /// <summary>
        /// Speed unit used across the application (phone and Android Auto): kilometers per hour, knots or miles per hour.
        /// </summary>
        public static readonly SettingDefinition SpeedUnitSetting = SettingDefinition.CreateChoice("speed_unit", "Jednostka prędkości", 0, "Jednostka prędkości względem ziemi w całej aplikacji, na telefonie i w Android Auto. Pokazuje się w jednym miejscu: w linii Prędkość na karcie i w popupie zaznaczonego samolotu. Węzeł (kt) to jedna mila morska na godzinę, czyli 1,852 km/h; w tej jednostce samoloty podają prędkość w danych ADS-B. Szacowany czas do celu na karcie nie zależy od tego ustawienia.", "Kilometry na godzinę (km/h)", "Węzły (kt)", "Mile na godzinę (mph)");

        /// <summary>
        /// Vertical rate unit used across the application (phone and Android Auto): feet per minute or meters per second.
        /// </summary>
        public static readonly SettingDefinition VerticalSpeedUnitSetting = SettingDefinition.CreateChoice("vertical_speed_unit", "Jednostka prędkości pionowej", 0, "Jednostka tempa wznoszenia i opadania w całej aplikacji, na telefonie i w Android Auto. Pokazuje się w jednym miejscu: w linii Wznoszenie lub Opadanie na karcie i w popupie zaznaczonego samolotu. Stopy na minutę (ft/min) to jednostka używana w lotnictwie, metry na sekundę (m/s) to jednostka układu SI, np. 1 500 ft/min to 7,6 m/s. Lot z tempem poniżej 100 ft/min jest pokazywany jako poziomy.", "Stopy na minutę (ft/min)", "Metry na sekundę (m/s)");

        /// <summary>
        /// Switch that shows the "Dev log" item in the menu.
        /// </summary>
        public static readonly SettingDefinition ShowDevLogSetting = SettingDefinition.CreateSwitch("show_dev_log", "Pokaż Dev log w menu", false, "Dodaje do menu głównego pozycję „Dev log” pod „O aplikacji”. Zbieranie zdarzeń działa niezależnie od tego przełącznika, on tylko pokazuje lub ukrywa ekran z logiem. Log służy do przekazania twórcy informacji, co w aplikacji nie działa jak powinno.");

        /// <summary>
        /// Switch that shows the developer console button and the console on the map.
        /// </summary>
        public static readonly SettingDefinition ShowDevConsoleSetting = SettingDefinition.CreateSwitch("show_dev_console", "Włącz konsolę dev", false, "Pokazuje na ekranie głównym w lewym górnym rogu przycisk z ikoną błędu, który otwiera na mapie konsolę z danymi roboczymi aplikacji: poziomem zoomu, środkiem mapy, pozycją GPS urządzenia, położeniem wysłanym do serwera po zaokrągleniu prywatności, liczbą samolotów i stanem limitów. Konsola służy twórcy do sprawdzania, co aplikacja robi. Długie dotknięcie konsoli kopiuje jej treść.");

        /// <summary>
        /// Least severe kind of event that is written to the developer log.
        /// </summary>
        public static readonly SettingDefinition DevLogLevelSetting = SettingDefinition.CreateChoice("dev_log_level", "Poziom logowania", 1, "Które zdarzenia trafiają do Dev logu. „Tylko błędy” to nieobsłużone wyjątki i awarie. „Błędy i ostrzeżenia” dodają zdarzenia, po których aplikacja radzi sobie dalej, np. nieudane pobranie kafelka, limit zapytań (429) lub odmowę serwera. „Wszystko” dodaje także informacje, np. przełączenie na zapasowe źródło danych. Zmiana działa od następnego zdarzenia, zapisanych wpisów nie usuwa.", "Tylko błędy", "Błędy i ostrzeżenia", "Wszystko");

        /// <summary>
        /// Maximum number of entries kept in the developer log; the stored value is the index in <see cref="DevLog.MaxEntriesOptions"/>.
        /// </summary>
        public static readonly SettingDefinition DevLogMaxEntriesSetting = SettingDefinition.CreateChoice("dev_log_max_entries", "Długość logu", 2, "Ile wpisów Dev log przechowuje. Po przekroczeniu limitu najstarsze wpisy są usuwane. Zmniejszenie limitu od razu usuwa nadmiar.", DevLog.MaxEntriesOptions.Select((int count) => count + " wpisów").ToArray());

        /// <summary>
        /// Switch that merges identical events of the developer log into one entry with a counter.
        /// </summary>
        public static readonly SettingDefinition DevLogDeduplicateSetting = SettingDefinition.CreateSwitch("dev_log_deduplicate", "Łącz powtarzające się wpisy", true, "Identyczne zdarzenia (ten sam poziom, miejsce, opis i wyjątek) są zapisywane jako jeden wpis z licznikiem i czasem pierwszego oraz ostatniego wystąpienia, dzięki czemu powtarzający się błąd nie zapełnia logu. Po wyłączeniu każde zdarzenie jest osobnym wpisem, co pozwala zobaczyć dokładną kolejność zdarzeń, ale log zapełnia się szybciej.");

        /// <summary>
        /// Grid sizes in kilometers of the options of <see cref="PrivacyPrecisionSetting"/>; zero sends the exact position.
        /// </summary>
        public static readonly IReadOnlyList<double> PrivacyGridKilometers = new List<double> { 0.0, 1.0, 5.0, 10.0, 25.0 };

        /// <summary>
        /// Radius steps in kilometers of the options of <see cref="PrivacyRadiusStepSetting"/>; zero leaves the radius as it is.
        /// </summary>
        public static readonly IReadOnlyList<double> PrivacyRadiusStepsKilometers = new List<double> { 0.0, 10.0, 20.0, 50.0 };

        /// <summary>
        /// How precisely the position of the map is sent to the aircraft servers; the stored value is the index in <see cref="PrivacyGridKilometers"/>.
        /// </summary>
        public static readonly SettingDefinition PrivacyPrecisionSetting = SettingDefinition.CreateChoice("privacy_precision", "Dokładność wysyłanego położenia", 1, "Serwer samolotów dostaje od aplikacji środek widocznego obszaru i promień. Przy śledzeniu GPS środek to Twoje dokładne położenie. Ta opcja przyciąga środek do siatki o wybranym rozmiarze, więc serwer widzi tylko okolicę, a nie dokładne miejsce. Wynik zależy tylko od położenia, więc wielu zapytań nie da się uśrednić do dokładnego punktu. Aplikacja powiększa promień o możliwe przesunięcie, dzięki czemu widoczny obszar jest nadal w całości pokryty, a mapa wygląda tak samo. Im większa siatka, tym większa odpowiedź serwera. Nie ukrywa to adresu IP ani obszaru, z którego pobierane są kafelki mapy, ani tego, o który lot pytasz.", "Dokładne", "Około 1 km", "Około 5 km", "Około 10 km", "Około 25 km");

        /// <summary>
        /// Switch of the layer with the airborne threats from NEPTUN. Phone only.
        /// </summary>
        public static readonly SettingDefinition ShowThreatsSetting = SettingDefinition.CreateSwitch("show_threats", "Warstwa zagrożeń (NEPTUN)", false, "Pokazuje na mapie telefonu drony, rakiety i inne zagrożenia powietrzne zgłaszane przez serwis NEPTUN (neptun.in.ua), z kierunkiem lotu, śladem i kołem niepewności położenia. Warstwa jest domyślnie wyłączona i tylko na telefonie, nie ma jej w Android Auto. Gdy jest wyłączona, aplikacja nie łączy się z serwisem. Dane pochodzą z otwartych i zewnętrznych źródeł (m.in. kanałów Telegram), mogą być nieścisłe, niepełne lub spóźnione i nie są oficjalnym systemem ostrzegania: zawsze kieruj się oficjalnymi sygnałami alarmu przeciwlotniczego. Pozycje „tylko obwód” i obserwacje bez zagrożenia dla ludności są oznaczone inaczej niż alarmy. Przycisk z ikoną ostrzeżenia na mapie włącza i wyłącza tę samą warstwę.");

        /// <summary>
        /// Size of the threat icons as a multiple of the default size.
        /// </summary>
        public static readonly SettingDefinition ThreatIconScaleSetting = SettingDefinition.CreateSlider("threat_icon_scale", "Rozmiar ikon zagrożeń", 1.2, 0.6, 2.5, 0.1, string.Empty, "Rozmiar ikon dronów, rakiet i innych zagrożeń na mapie telefonu, niezależny od rozmiaru ikon samolotów. Podgląd u góry karty pokazuje wszystkie rodzaje zagrożeń z bieżącymi ustawieniami.");

        /// <summary>
        /// Width of the dark outline of the threat icons in dp.
        /// </summary>
        public static readonly SettingDefinition ThreatOutlineWidthSetting = SettingDefinition.CreateSlider("threat_outline_dp", "Obwódka zagrożeń", 1.2, 0.0, 6.0, 0.1, " dp", "Szerokość ciemnej obwódki wokół ikony zagrożenia, w dp z każdej strony. Wartość 0 wyłącza obwódkę. Cień i akcent ikon są wspólne z ikonami samolotów (sekcja Samoloty).");

        /// <summary>
        /// How the application obtains the threats; the stored value is the index of the transport.
        /// </summary>
        public static readonly SettingDefinition ThreatTransportSetting = SettingDefinition.CreateChoice("threat_transport", "Sposób pobierania zagrożeń", 0, "WebSocket z zapasowym pobieraniem (domyślnie): aplikacja trzyma jedno stałe połączenie ze strumieniem NEPTUN, a gdy ono nie działa, pobiera listę co 5 s i co jakiś czas próbuje połączyć się ponownie. Tylko WebSocket: po błędzie czeka (1, 2, 4… do 60 s) i łączy się ponownie, bez pobierania listy. Tylko pobieranie (REST): lista jest pobierana co 5 s, bez stałego połączenia. Zmiana działa od następnego kroku, bez wyłączania warstwy.", "WebSocket z zapasowym pobieraniem", "Tylko WebSocket", "Tylko pobieranie (REST)");

        /// <summary>
        /// Whether the layer with the restricted and reserved areas of the airspace is shown on the phone map.
        /// </summary>
        public static readonly SettingDefinition ShowAirspaceSetting = SettingDefinition.CreateSwitch("show_airspace", "Warstwa stref przestrzeni powietrznej (PAŻP)", false, "Pokazuje na mapie telefonu strefy przestrzeni powietrznej z mapy PAŻP (airspace.pansa.pl): strefy zakazane, ograniczone, niebezpieczne, czasowo wydzielone i inne rezerwacje, z wysokościami i czasem obowiązywania po dotknięciu. Warstwa jest domyślnie wyłączona i tylko na telefonie, nie ma jej w Android Auto. Gdy jest wyłączona, aplikacja nie łączy się z serwisem. Dane są nieoficjalne i mogą być spóźnione lub niepełne, więc nie służą do planowania ani wykonywania lotów. Rodzaje stref i ich stany (aktywne, planowane, nieaktywne) wybierasz na osobnym ekranie, który otwiera przycisk poniżej, a przycisk na mapie włącza i wyłącza tę samą warstwę.");

        /// <summary>
        /// Time between two downloads of the airspace areas while the layer is on.
        /// </summary>
        public static readonly SettingDefinition AirspaceRefreshMinutesSetting = SettingDefinition.CreateSlider("airspace_refresh_minutes", "Odświeżanie stref", 5.0, 1.0, 60.0, 1.0, " min", "Co ile minut aplikacja pobiera listę stref z serwisu PAŻP, gdy warstwa jest włączona. Strefy zmieniają się wolno (rezerwacje ogłasza się z wyprzedzeniem), więc rzadsze pobieranie wystarcza i oszczędza serwis. Po błędzie aplikacja ponawia próbę coraz później, a po odpowiedzi 429 czeka tyle, ile poprosił serwer.");

        /// <summary>
        /// Time between two downloads of the official notices while the screen of the notices is open.
        /// </summary>
        public static readonly SettingDefinition WarningsRefreshMinutesSetting = SettingDefinition.CreateSlider("warnings_refresh_minutes", "Odświeżanie ostrzeżeń", 10.0, 5.0, 60.0, 1.0, " min", "Co ile minut aplikacja pobiera komunikaty Regionalnego Systemu Ostrzegania (publiczny kanał udostępniany przez TVP), gdy ekran „Ostrzeżenia” jest otwarty. Komunikaty zmieniają się powoli, więc rzadsze pobieranie wystarcza i oszczędza serwis. Po błędzie aplikacja ponawia próbę coraz później, a po odpowiedzi 429 czeka tyle, ile poprosił serwer. Gdy ekran jest zamknięty, aplikacja nie łączy się z serwisem.");

        /// <summary>
        /// Switch that makes the privacy settings of the Dev section replace the choice of the user.
        /// </summary>
        public static readonly SettingDefinition PrivacyDevOverrideSetting = SettingDefinition.CreateSwitch("privacy_dev_override", "Ustawienia Dev nadpisują ustawienia użytkownika", false, "Gdy włączone, o zaokrąglaniu położenia decydują wyłącznie ustawienia z tej karty (rozmiar siatki, zaokrąglanie promienia, powiększanie promienia i logowanie), a wybór z sekcji Prywatność w Zaawansowanych jest ignorowany. Gdy wyłączone, ustawienia z tej karty nie mają żadnego wpływu i obowiązuje tylko wybór użytkownika.");

        /// <summary>
        /// Own size of the grid in kilometers, used while <see cref="PrivacyDevOverrideSetting"/> is on.
        /// </summary>
        public static readonly SettingDefinition PrivacyCustomGridSetting = SettingDefinition.CreateSlider("privacy_custom_grid_km", "Własny rozmiar siatki", 5.0, 0.0, 50.0, 1.0, " km", "Rozmiar siatki, do której przyciągany jest środek zapytania, gdy włączone jest nadpisywanie ustawień użytkownika. Zero oznacza dokładne położenie, bez zaokrąglania.");

        /// <summary>
        /// Step to which the query radius is rounded up; the stored value is the index in <see cref="PrivacyRadiusStepsKilometers"/>.
        /// </summary>
        public static readonly SettingDefinition PrivacyRadiusStepSetting = SettingDefinition.CreateChoice("privacy_radius_step", "Zaokrąglanie promienia w górę", 0, "Zaokrągla promień zapytania w górę do wielokrotności wybranej wartości. Dzięki temu serwer nie widzi dokładnego poziomu przybliżenia mapy. Większy promień oznacza większą odpowiedź.", "Bez zaokrąglania", "10 km", "20 km", "50 km");

        /// <summary>
        /// Switch that enlarges the query radius by the largest possible shift of the center.
        /// </summary>
        public static readonly SettingDefinition PrivacyExpandRadiusSetting = SettingDefinition.CreateSwitch("privacy_expand_radius", "Powiększaj promień o błąd zaokrąglenia", true, "Po przyciągnięciu środka do siatki promień rośnie o największą możliwą odległość między prawdziwym a wysłanym środkiem, żeby widoczny obszar był w całości pokryty. Wyłączenie pokazuje, co dzieje się bez tego: przy brzegu ekranu mogą brakować samolotów.");

        /// <summary>
        /// Switch that writes every aircraft query with the real and the sent position to the developer log.
        /// </summary>
        public static readonly SettingDefinition PrivacyLogSetting = SettingDefinition.CreateSwitch("privacy_log_requests", "Loguj wysyłane współrzędne w Dev logu", false, "Każde zapytanie o samoloty trafia do Dev logu jako informacja z prawdziwym położeniem i położeniem wysłanym do serwera. Pozwala sprawdzić działanie zaokrąglania. Uwaga: log zawiera wtedy Twoje prawdziwe położenie, więc nie udostępniaj go innym, dopóki ten przełącznik jest włączony. Wpisy widać przy poziomie logowania „Wszystko”.");

        /// <summary>
        /// Time between two checks of the alerts made without the map.
        /// </summary>
        public static readonly SettingDefinition AlertIntervalSetting = SettingDefinition.CreateSlider("alert_interval_s", "Sprawdzanie alertów w tle co", 60.0, 30.0, 600.0, 30.0, " s", "Co ile sekund tryb w tle alertów pobiera samoloty wokół obszarów alertów i je sprawdza. Krótszy odstęp daje szybsze powiadomienia, ale więcej zapytań do serwera (przy zbyt częstych serwer może odpowiedzieć kodem 429) i większe zużycie baterii i danych. Dotyczy tylko trybu w tle, który włącza się na ekranie „Alerty”. Zmiana działa od następnego sprawdzenia.");

        /// <summary>
        /// Number of alerts checked by the background mode.
        /// </summary>
        public static readonly SettingDefinition AlertMaxBackgroundSetting = SettingDefinition.CreateSlider("alert_max_background", "Maksymalna liczba alertów w tle", 5.0, 1.0, 20.0, 1.0, "", "Ile aktywnych alertów (licząc od góry listy) sprawdza tryb w tle. Każdy dodatkowy alert w innym miejscu to kolejne zapytanie do serwera w każdym sprawdzeniu; alerty leżące blisko siebie dzielą jedno zapytanie. Aktywne alerty ponad limit nie są sprawdzane, dopóki tryb w tle jest włączony.");

        /// <summary>
        /// Switch that silences the "appearance" notifications for the aircraft present in the area at the first check of an alert.
        /// </summary>
        public static readonly SettingDefinition AlertSkipInitialAppearanceSetting = SettingDefinition.CreateSwitch("alert_skip_initial", "Pomijaj samoloty już obecne przy starcie alertu", false, "Gdy włączone, samoloty, które w chwili pierwszego sprawdzenia alertu (po starcie aplikacji, trybu w tle albo po włączeniu alertu) już znajdują się w obszarze, nie wywołują powiadomienia „pojawienie się”. Powiadomienia dostają dopiero samoloty, które pojawią się lub wlecą później. Przydatne po restarcie telefonu lub aplikacji, żeby nie dostać lawiny powiadomień o samolotach, które i tak już tam były. Nie dotyczy zdarzenia „wlot”.");

        /// <summary>
        /// Switch that also shows the alert notifications on the Android Auto screen.
        /// </summary>
        public static readonly SettingDefinition AlertCarNotificationsSetting = SettingDefinition.CreateSwitch("alert_car_notifications", "Powiadomienia alertów w Android Auto", true, "Gdy włączone, powiadomienie o alercie pokazuje się także na ekranie samochodu, jeśli telefon jest połączony z Android Auto. Powiadomienie w samochodzie ma tytuł i treść jak na telefonie, ale dotknięcie go nie otwiera mapy. Wyłączenie zostawia powiadomienia tylko na telefonie. Android Auto może ukrywać powiadomienia podczas jazdy zgodnie z własnymi ustawieniami (na telefonie: Ustawienia Android Auto → Powiadomienia).");

        /// <summary>
        /// Number of aircraft of one alert in one check from which they are reported with a single notification.
        /// </summary>
        public static readonly SettingDefinition AlertGroupThresholdSetting = SettingDefinition.CreateSlider("alert_group_threshold", "Grupuj powiadomienia od liczby samolotów", 3.0, 2.0, 10.0, 1.0, "", "Gdy jeden alert zadziała dla tylu samolotów naraz (w jednym sprawdzeniu) albo więcej, aplikacja pokazuje jedno zbiorcze powiadomienie z listą znaków wywoławczych zamiast osobnego powiadomienia dla każdego samolotu. Mniejsza liczba oznacza częstsze grupowanie, a wartość 10 grupuje dopiero duże ilości. Grupowanie przydaje się po restarcie telefonu albo po dłuższej przerwie, gdy do obszaru trafia od razu wiele samolotów. Wszystkie samoloty zostają zapisane w historii alertów.");

        /// <summary>
        /// Switch that starts the background checking of the alerts after the device boots or the application is updated.
        /// </summary>
        public static readonly SettingDefinition AlertStartOnBootSetting = SettingDefinition.CreateSwitch("alert_start_on_boot", "Uruchamiaj tryb w tle po restarcie telefonu", true, "Gdy tryb w tle alertów jest włączony, usługa rusza sama po ponownym uruchomieniu telefonu i po aktualizacji aplikacji, bez otwierania aplikacji. Wyłączenie przywraca dawne zachowanie: tryb w tle rusza dopiero po otwarciu aplikacji. Producenci telefonów mogą blokować taki start, jeśli aplikacja nie ma zgody na autostart albo ma włączoną optymalizację baterii. Alerty wokół mojej pozycji GPS czekają po restarcie na pierwszą pozycję, a system może ograniczyć dostęp do lokalizacji aplikacji uruchomionej w tle.");

        /// <summary>
        /// All settings in display order.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
        {
            LanguageSetting,
            AltitudeUnitSetting,
            DistanceUnitSetting,
            SpeedUnitSetting,
            VerticalSpeedUnitSetting,
            ShowLabelsSetting,
            ShowAltitudeLegendSetting,
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
            AircraftShadowSetting,
            AircraftCockpitSetting,
            TrailDurationSetting,
            MaxTrailPointsSetting,
            TrailWidthSetting,
            TrailMinAlphaSetting,
            TrailMaxAlphaSetting,
            TrailOutlineWidthSetting,
            TrailOutlineAlphaSetting,
            TileCacheSizeSetting,
            TileCacheDaysSetting,
            FlightInfoCacheMinutesSetting,
            FetchIntervalSetting,
            MilitaryGlobalSetting,
            MilitaryRefreshSecondsSetting,
            RenderIntervalSetting,
            MinZoomSetting,
            MaxZoomSetting,
            MaxCachedTilesSetting,
            TileRetrySetting,
            DataSourceSetting,
            DataSourceFallbackSetting,
            DataSourceDetailsSetting,
            RouteSourceSetting,
            MapThemeSetting,
            MapDimSetting,
            ShowDevLogSetting,
            DevLogLevelSetting,
            DevLogMaxEntriesSetting,
            DevLogDeduplicateSetting,
            ShowDevConsoleSetting,
            ShowThreatsSetting,
            ThreatIconScaleSetting,
            ThreatOutlineWidthSetting,
            ThreatTransportSetting,
            ShowAirspaceSetting,
            AirspaceRefreshMinutesSetting,
            WarningsRefreshMinutesSetting,
            PrivacyPrecisionSetting,
            PrivacyDevOverrideSetting,
            PrivacyCustomGridSetting,
            PrivacyRadiusStepSetting,
            PrivacyExpandRadiusSetting,
            PrivacyLogSetting,
            AlertIntervalSetting,
            AlertMaxBackgroundSetting,
            AlertStartOnBootSetting,
            AlertSkipInitialAppearanceSetting,
            AlertCarNotificationsSetting,
            AlertGroupThresholdSetting,
        };

        /// <summary>
        /// Settings of the units section. They share one card on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> UnitSettings = new List<SettingDefinition>
        {
            AltitudeUnitSetting,
            DistanceUnitSetting,
            SpeedUnitSetting,
            VerticalSpeedUnitSetting,
        };

        /// <summary>
        /// Settings of the privacy section available to every user. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> PrivacySettings = new List<SettingDefinition>
        {
            PrivacyPrecisionSetting,
        };

        /// <summary>
        /// Advanced privacy settings of the Dev section. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> PrivacyDevSettings = new List<SettingDefinition>
        {
            PrivacyDevOverrideSetting,
            PrivacyCustomGridSetting,
            PrivacyRadiusStepSetting,
            PrivacyExpandRadiusSetting,
            PrivacyLogSetting,
        };

        /// <summary>
        /// Settings of the threat layer that the user sees. They share one card on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> ThreatSettings = new List<SettingDefinition>
        {
            ShowThreatsSetting,
        };

        /// <summary>
        /// Settings of the airspace layer that the user sees. They share one card on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> AirspaceSettings = new List<SettingDefinition>
        {
            ShowAirspaceSetting,
            AirspaceRefreshMinutesSetting,
        };

        /// <summary>
        /// Settings of the official notices. They share one card on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> WarningSettings = new List<SettingDefinition>
        {
            WarningsRefreshMinutesSetting,
        };

        /// <summary>
        /// Settings of the look of the threat icons. They share one card on the settings screen, which starts with a preview of the icons.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> ThreatIconSettings = new List<SettingDefinition>
        {
            ThreatIconScaleSetting,
            ThreatOutlineWidthSetting,
        };

        /// <summary>
        /// Settings of the way of obtaining the threats. They share one card in the advanced section of the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> ThreatTransportSettings = new List<SettingDefinition>
        {
            ThreatTransportSetting,
        };

        /// <summary>
        /// Settings of the developer log. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> DevLogSettings = new List<SettingDefinition>
        {
            ShowDevLogSetting,
            DevLogLevelSetting,
            DevLogMaxEntriesSetting,
            DevLogDeduplicateSetting,
        };

        /// <summary>
        /// Settings of the alerts checked without the map. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> AlertSettings = new List<SettingDefinition>
        {
            AlertIntervalSetting,
            AlertMaxBackgroundSetting,
            AlertStartOnBootSetting,
            AlertSkipInitialAppearanceSetting,
            AlertCarNotificationsSetting,
            AlertGroupThresholdSetting,
        };

        /// <summary>
        /// Settings of the tile cache on the disk. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> TileCacheSettings = new List<SettingDefinition>
        {
            TileCacheSizeSetting,
            TileCacheDaysSetting,
        };

        /// <summary>
        /// Settings of the cache of the flight information. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> FlightCacheSettings = new List<SettingDefinition>
        {
            FlightInfoCacheMinutesSetting,
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
        /// Raised after the settings were saved or restored to the defaults, so that the parts of the application that copy a setting can read it again.
        /// </summary>
        public static event Action? Saved;

        /// <summary>
        /// Interval between aircraft requests.
        /// </summary>
        public static TimeSpan FetchInterval => TimeSpan.FromSeconds(Get(FetchIntervalSetting));

        /// <summary>
        /// Whether the mode that shows only military aircraft downloads all the military aircraft with one request.
        /// </summary>
        public static bool MilitaryGlobal => Get(MilitaryGlobalSetting) >= 0.5;

        /// <summary>
        /// Shortest time between two downloads of all the military aircraft.
        /// </summary>
        public static TimeSpan MilitaryRefreshInterval => TimeSpan.FromSeconds(Get(MilitaryRefreshSecondsSetting));

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
        /// Size limit of the tile cache on the disk in bytes.
        /// </summary>
        public static long TileCacheMaxBytes => (long)Get(TileCacheSizeSetting) * 1024L * 1024L;

        /// <summary>
        /// Time a tile on the disk is considered fresh.
        /// </summary>
        public static TimeSpan TileCacheMaxAge => TimeSpan.FromDays(Get(TileCacheDaysSetting));

        /// <summary>
        /// How long after the last sighting the information about a single flight or aircraft is kept before it is asked about again.
        /// </summary>
        public static TimeSpan FlightInfoMaxAge => TimeSpan.FromMinutes(Get(FlightInfoCacheMinutesSetting));

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
        /// Index of the chosen map theme (see <see cref="AdsbCarMap.Map.MapTheme"/>).
        /// </summary>
        public static int MapThemeIndex => (int)Get(MapThemeSetting);

        /// <summary>
        /// Whether the layer with the threats is shown on the phone map.
        /// </summary>
        public static bool ShowThreats => Get(ShowThreatsSetting) >= 0.5;

        /// <summary>
        /// Whether the airspace layer is shown.
        /// </summary>
        public static bool ShowAirspace => Get(ShowAirspaceSetting) >= 0.5;

        /// <summary>
        /// Time between two downloads of the airspace areas, in minutes.
        /// </summary>
        public static int AirspaceRefreshMinutes => (int)Get(AirspaceRefreshMinutesSetting);

        /// <summary>
        /// Time between two downloads of the official notices, in minutes.
        /// </summary>
        public static int WarningsRefreshMinutes => (int)Get(WarningsRefreshMinutesSetting);

        /// <summary>
        /// Size of the threat icons as a multiple of the default size.
        /// </summary>
        public static float ThreatIconScale => (float)Get(ThreatIconScaleSetting);

        /// <summary>
        /// Width of the dark outline of the threat icons in dp.
        /// </summary>
        public static float ThreatOutlineWidth => (float)Get(ThreatOutlineWidthSetting);

        /// <summary>
        /// Index of the way of obtaining the threats: 0 stream with the download as a fallback, 1 stream only, 2 download only.
        /// </summary>
        public static int ThreatTransportIndex => (int)Get(ThreatTransportSetting);

        /// <summary>
        /// Whether the menu has the "Dev log" item.
        /// </summary>
        public static bool ShowDevLog => Get(ShowDevLogSetting) >= 0.5;

        /// <summary>
        /// Whether the main screen has the developer console button.
        /// </summary>
        public static bool ShowDevConsole => Get(ShowDevConsoleSetting) >= 0.5;

        /// <summary>
        /// Least severe kind of event that is written to the developer log.
        /// </summary>
        public static DevLogLevel DevLogMinimumLevel => DevLog.GetMinimumLevel((int)Get(DevLogLevelSetting));

        /// <summary>
        /// Maximum number of entries kept in the developer log.
        /// </summary>
        public static int DevLogMaxEntries => DevLog.GetMaxEntries((int)Get(DevLogMaxEntriesSetting));

        /// <summary>
        /// Time between two checks of the alerts made without the map.
        /// </summary>
        public static TimeSpan AlertInterval => TimeSpan.FromSeconds(Get(AlertIntervalSetting));

        /// <summary>
        /// Number of alerts checked by the background mode.
        /// </summary>
        public static int AlertMaxBackground => (int)Math.Round(Get(AlertMaxBackgroundSetting));

        /// <summary>
        /// Whether the background checking of the alerts starts after the device boots or the application is updated.
        /// </summary>
        public static bool AlertStartOnBoot => Get(AlertStartOnBootSetting) >= 0.5;

        /// <summary>
        /// Whether the alert notifications are also shown on the Android Auto screen.
        /// </summary>
        public static bool AlertCarNotifications => Get(AlertCarNotificationsSetting) >= 0.5;

        /// <summary>
        /// Number of aircraft of one alert in one check from which they are reported with a single notification.
        /// </summary>
        public static int AlertGroupThreshold => (int)Math.Round(Get(AlertGroupThresholdSetting));

        /// <summary>
        /// Whether the aircraft already in the area at the first check of an alert are not reported as appearing.
        /// </summary>
        public static bool AlertSkipInitialAppearance => Get(AlertSkipInitialAppearanceSetting) >= 0.5;

        /// <summary>
        /// Whether identical events of the developer log are merged into one entry.
        /// </summary>
        public static bool DevLogDeduplicate => Get(DevLogDeduplicateSetting) >= 0.5;

        /// <summary>
        /// How precisely the position of the map is sent to the aircraft servers: the settings of the Dev section while they override the user, otherwise only the size chosen by the user.
        /// </summary>
        public static PrivacyOptions PrivacyOptions
        {
            get
            {
                if (Get(PrivacyDevOverrideSetting) >= 0.5)
                {
                    double radiusStepKilometers = PrivacyRadiusStepsKilometers[Math.Clamp((int)Get(PrivacyRadiusStepSetting), 0, PrivacyRadiusStepsKilometers.Count - 1)];

                    return new PrivacyOptions(Get(PrivacyCustomGridSetting), radiusStepKilometers / CoordinateRounder.KilometersPerNauticalMile, Get(PrivacyExpandRadiusSetting) >= 0.5, Get(PrivacyLogSetting) >= 0.5);
                }

                return new PrivacyOptions(PrivacyGridKilometers[Math.Clamp((int)Get(PrivacyPrecisionSetting), 0, PrivacyGridKilometers.Count - 1)]);
            }
        }

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
        /// Strength of the aircraft icon shadow from 0 to 1.
        /// </summary>
        public static float AircraftShadowStrength => (float)(Get(AircraftShadowSetting) / 100.0);

        /// <summary>
        /// Strength of the aircraft icon cockpit accent from 0 to 1.
        /// </summary>
        public static float AircraftCockpitStrength => (float)(Get(AircraftCockpitSetting) / 100.0);

        /// <summary>
        /// Whether the aircraft labels are visible on the map.
        /// </summary>
        public static bool ShowLabels => Get(ShowLabelsSetting) >= 0.5;

        /// <summary>
        /// Whether the altitude color legend bar is visible on the map.
        /// </summary>
        public static bool ShowAltitudeLegend => Get(ShowAltitudeLegendSetting) >= 0.5;

        /// <summary>
        /// Settings that describe the sources of the aircraft positions. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> DataSourceSettings = new List<SettingDefinition>
        {
            DataSourceSetting,
            DataSourceFallbackSetting,
            DataSourceDetailsSetting,
            MilitaryGlobalSetting,
            MilitaryRefreshSecondsSetting,
        };

        /// <summary>
        /// Names of the aircraft sources in the order in which they are asked.
        /// </summary>
        public static IReadOnlyList<string> DataSourceOrder => DataSourceOrders[Math.Clamp((int)Get(DataSourceSetting), 0, DataSourceOrders.Count - 1)];

        /// <summary>
        /// Returns the stored value of the order of the aircraft sources for the given list of names.
        /// </summary>
        /// <param name="order">Names in the order of preference.</param>
        /// <returns>Index of the order, or 0 (the default order) when the list is not a known order.</returns>
        public static int GetDataSourceOrderIndex(IReadOnlyList<string> order)
        {
            return Math.Max(0, SourceOrder.IndexOf(DataSourceOrders, order));
        }

        /// <summary>
        /// Settings that describe the sources of the flight routes. They share one section on the settings screen.
        /// </summary>
        public static readonly IReadOnlyList<SettingDefinition> RouteSourceSettings = new List<SettingDefinition>
        {
            RouteSourceSetting,
        };

        /// <summary>
        /// Names of the flight route sources in the order in which they are asked.
        /// </summary>
        public static IReadOnlyList<string> RouteSourceOrder => RouteSourceOrders[Math.Clamp((int)Get(RouteSourceSetting), 0, RouteSourceOrders.Count - 1)];

        /// <summary>
        /// Returns the stored value of the order of the flight route sources for the given list of names.
        /// </summary>
        /// <param name="order">Names in the order of preference.</param>
        /// <returns>Index of the order, or 0 (the default order) when the list is not a known order.</returns>
        public static int GetRouteSourceOrderIndex(IReadOnlyList<string> order)
        {
            return Math.Max(0, SourceOrder.IndexOf(RouteSourceOrders, order));
        }

        /// <summary>
        /// Whether the next sources are asked when the previous one gives no data.
        /// </summary>
        public static bool DataSourceFallback => Get(DataSourceFallbackSetting) >= 0.5;

        /// <summary>
        /// Whether the data of the selected aircraft is completed from the other sources.
        /// </summary>
        public static bool DataSourceDetails => Get(DataSourceDetailsSetting) >= 0.5;

        /// <summary>
        /// Language of the user interface.
        /// </summary>
        public static AppLanguage Language => Loc.FromSettingValue((int)Get(LanguageSetting));

        /// <summary>
        /// Unit in which the aircraft altitude is shown.
        /// </summary>
        public static AltitudeUnit AltitudeUnit => (AltitudeUnit)(int)Get(AltitudeUnitSetting);

        /// <summary>
        /// Unit in which distances are shown.
        /// </summary>
        public static DistanceUnit DistanceUnit => (DistanceUnit)(int)Get(DistanceUnitSetting);

        /// <summary>
        /// Unit in which the ground speed is shown.
        /// </summary>
        public static SpeedUnit SpeedUnit => (SpeedUnit)(int)Get(SpeedUnitSetting);

        /// <summary>
        /// Unit in which the vertical rate is shown.
        /// </summary>
        public static VerticalSpeedUnit VerticalSpeedUnit => (VerticalSpeedUnit)(int)Get(VerticalSpeedUnitSetting);

        /// <summary>
        /// All the units chosen by the user.
        /// </summary>
        public static DisplayUnits Units => new DisplayUnits(AltitudeUnit, DistanceUnit, SpeedUnit, VerticalSpeedUnit);

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
            Loc.Language = Language;
            Saved?.Invoke();
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

            Loc.Language = Language;
            Saved?.Invoke();
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
                Loc.Language = Language;
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

# ADS-B Car Map

Aplikacja na Androida, która pokazuje na mapie OpenStreetMap samoloty w powietrzu na żywo (dane ADS-B). Działa na telefonie, a także jako aplikacja nawigacyjna w Android Auto, rysowana własnym kodem na `Canvas`.

> **Status: proof of concept.** Mapa na telefonie działa. Wyświetlanie w Android Auto jest wciąż w fazie uruchamiania i nie zostało potwierdzone end-to-end (patrz [Znane ograniczenia](#znane-ograniczenia)). Projekt jest przeznaczony do użytku własnego i instalacji z pliku (sideload), nie do Sklepu Play.

## Spis treści

- [Funkcje](#funkcje)
- [Jak to wygląda w użyciu](#jak-to-wygląda-w-użyciu)
- [Przykładowe zastosowania](#przykładowe-zastosowania)
- [Konfiguracja aplikacji](#konfiguracja-aplikacji)
- [Android Auto](#android-auto)
- [Budowanie ze źródeł](#budowanie-ze-źródeł)
- [Źródła danych i atrybucja](#źródła-danych-i-atrybucja)
- [Prywatność](#prywatność)
- [Znane ograniczenia](#znane-ograniczenia)
- [Struktura projektu](#struktura-projektu)
- [Plany](#plany)
- [Ważna uwaga](#ważna-uwaga)
- [Licencja](#licencja)

## Funkcje

**Mapa**
- Kafelki OpenStreetMap rysowane na własnej powierzchni. Płynne, ciągłe przybliżanie (gest dwoma palcami i przyciski `+` / `−`), przesuwanie z bezwładnością.
- Podczas ładowania brakujące kafelki są zastępowane powiększonym fragmentem kafelka o niższym poziomie, a kafelki sąsiednie są wczytywane z wyprzedzeniem, więc przy przesuwaniu nie widać czarnych dziur.
- Przycisk „center” wraca do Twojej pozycji GPS (niebieska kropka).

**Lotniska**
- Dane z projektu OurAirports (domena publiczna), dołączone do aplikacji: około 71 tys. lotnisk i helipadów (duże, średnie, małe i helipady, bez wodnosamolotowych i zamkniętych) oraz pasy startowe. Dane odświeża skrypt `Scripts\Update-AirportData.ps1`, uruchamiany automatycznie przed każdym wydaniem.
- Lotniska rysują się pod samolotami: w zakresie zoomu wybranym jedną kontrolką od–do (domyślnie od 5, bez górnego limitu), wspólnym dla wszystkich rodzajów; pola „Rodzaje lotnisk na mapie” wybierają, które rodzaje są widoczne. Znacznik jest większy dla większego lotniska, lotniska wojskowe są czerwone. Obok znacznika jest kod IATA (a gdy go brak, ICAO), a etykiety, które by się nakładały, są pomijane od tych mniej ważnych. Od zoomu 11 widać pasy startowe.
- Dotknięcie lotniska (gdy pod palcem nie ma samolotu) zaznacza je: na telefonie pojawia się karta, a w Android Auto popup, z nazwą, kodami IATA i ICAO, miastem i krajem, wysokością w wybranej jednostce, odległością i kierunkiem od Ciebie oraz najdłuższymi pasami. Karta ma przycisk „Wyśrodkuj”. Zaznaczenie lotniska jest wspólne dla telefonu i samochodu.
- Wyszukiwarka (telefon i Android Auto) szuka też lotnisk po kodzie, nazwie i mieście (bez względu na polskie znaki). Wyniki są uporządkowane według dopasowania, a przy takim samym dopasowaniu lotniska są przed samolotami. W samochodzie dwa wiersze z sześciu są zawsze zarezerwowane dla samolotów. Przy pustym polu na górze są trzy najbliższe lotniska. Wybór lotniska zaznacza je, centruje na nim mapę i przybliża do zoomu 11.
- **Ulubione lotniska:** osobny ekran w menu („Ulubione lotniska”). Ulubione są zawsze widoczne na mapie od zoomu 5, bez względu na wybrane rodzaje i filtr małych lotnisk, ze złotym pierścieniem i etykietą o najwyższym priorytecie. Dodajesz je gwiazdką na karcie lotniska, przyciskiem „＋” w wynikach wyszukiwania na tym ekranie albo w oknie szczegółów. Lista pozwala zmieniać kolejność (▲ ▼), usuwać (✕) i po dotknięciu wiersza zobaczyć szczegóły lotniska z przyciskiem „Pokaż na mapie”. W wyszukiwarce ulubione są przed innymi lotniskami o takim samym dopasowaniu, a przy pustym polu stoją na początku. Lista jest wspólna dla telefonu i samochodu (w samochodzie popup oznacza ulubione, zarządzanie jest na telefonie) i jest zapisana osobno od ustawień: „Przywróć wszystkie domyślne” jej nie kasuje.
- Ekran „O aplikacji” (menu) zawiera wersję, licencję, źródła danych z atrybucją (OpenStreetMap, adsb.lol, adsbdb, OurAirports) i informację o prywatności. Atrybucja „© OpenStreetMap” zostaje też na mapie.

**Samoloty**
- Pozycje z ADS-B odświeżane domyślnie co 3 sekundy, a między odpowiedziami przesuwane na podstawie kursu i prędkości, żeby ruch był płynny.
- **Ikona zależna od rodzaju:** samolot pasażerski, szerokokadłubowy, mały odrzutowiec, turbośmigłowiec, awionetka, śmigłowiec, myśliwiec, wojskowy transportowiec, szybowiec, balon, dron, obiekt naziemny. Rodzaj wynika z kategorii nadajnika ADS-B, znacznika wojskowego i kodu typu ICAO.
- **Kolor zależny od wysokości:** szary na ziemi, a w powietrzu przejście od pomarańczowego przez żółty, zielony i niebieski do fioletowego.
- **Ślad lotu:** linia za samolotem w kolorze wysokości, coraz bardziej przezroczysta ku przeszłości. Przy zmianie wysokości kolor przechodzi płynnie (gradient wzdłuż każdego odcinka śladu).
- **Legenda wysokości:** pasek z gradientem kolorów u dołu mapy (telefon i Android Auto), z progami z ustawień: od 0 m, przez pomarańczowy, żółty, zielony i niebieski, do fioletowego (domyślnie od 12 km). Progi są rozmieszczone równomiernie, bo kolory przechodzą liniowo między nimi.
- **Etykieta:** znak wywoławczy i poziom lotu (np. `LOT281 FL350`), a pod spodem typ i trasa (np. `B738 WAW→LHR`). Etykiety są domyślnie **ukryte** (mapa jest wtedy czytelniejsza, a aplikacja nie pobiera tras dla każdego samolotu na ekranie). Włączysz je przyciskiem z ikoną etykiety pod „M” albo w ustawieniach; wybór jest zapamiętywany i obowiązuje też w Android Auto. Szczegóły samolotu zawsze widać na jego karcie po dotknięciu.
- Trasa i linia lotnicza są pobierane w tle z adsbdb (po jednym zapytaniu na sekundę).

**Karta samolotu**
- Po dotknięciu samolotu u dołu mapy pojawia się karta (odświeżana co sekundę): lot, rejestracja, linia lotnicza, typ i rodzaj, trasa z lotniskami wylotu i przylotu oraz **pasek postępu lotu**.
- Pod paskiem: ile procent trasy przeleciano, ile kilometrów zostało do celu i szacowany czas przylotu (z prędkości względem ziemi, więc to przybliżenie).
- Parametry lotu: wysokość (ft i m), prędkość (kt i km/h), kurs z kierunkiem świata, prędkość wznoszenia lub opadania, squawk oraz wiek danych.
- **Odległość i kierunek od Ciebie** (np. „34,2 km, NE (47°)”), liczone z pozycji GPS.
- Przycisk „Śledź” wyśrodkowuje mapę na samolocie, a „✕” zamyka kartę i czyści zaznaczenie. Przy kilku zaznaczonych samolotach karta pokazuje ostatnio dotknięty, a przy jego nazwie jest liczba pozostałych.
- Dane trasy i postęp są dostępne tylko dla lotów, które zna `adsbdb`. Jego pokrycie jest niepełne: w próbie testowej część znaków wywoławczych (np. `WUK6351`) nie miała trasy. Wtedy karta pokazuje pozostałe dane, bez paska postępu.
- Karta mówi wprost, dlaczego trasy nie ma: „pobieranie…” (czeka w kolejce lub jest pobierana), „baza adsbdb nie zna tego lotu” albo „nie udało się pobrać (błąd sieci lub limit zapytań)”. W każdym z tych przypadków jest przycisk („Pobierz teraz”, „Sprawdź ponownie”, „Spróbuj ponownie”), który wymusza pobranie trasy w pierwszej kolejności. Trasa samolotu pokazanego na karcie jest też pobierana poza kolejką, bez czekania na pozostałe.

**Wyszukiwanie** (menu → „Szukaj samolotu”)
- Pole wyszukiwania zawsze na górze, pod nim przewijana lista widocznych samolotów: lot, rejestracja, linia lotnicza, typ, trasa, wysokość. Wiersze na przemian w jaśniejszym tle.
- Wpisz lot, rejestrację, kod typu, lotnisko lub linię, aby zawęzić listę. Dotknięcie wiersza centruje mapę na samolocie i śledzi go.

**Zaznaczanie i izolacja**
- Dotknięcie samolotu na mapie zaznacza go (żółty pierścień).
- **M** włącza tryb zaznaczania wielu samolotów: dotknięcia dodają lub odejmują samoloty. W tym trybie karta samolotu się nie pokazuje, żeby nie zasłaniała mapy przy zaznaczaniu. Wróci po wyłączeniu „M”, jeśli coś zostało zaznaczone. Obok litery „M” widać **licznik zaznaczonych samolotów** w nawiasie, nieco niżej i mniejszą czcionką (np. „M(5)”). Pojawia się od dwóch zaznaczonych samolotów, także po wyłączeniu trybu wielokrotnego.
- **I** pokazuje tylko zaznaczone samoloty (reszta i ich ślady znika). Ponowne dotknięcie przywraca wszystkie.

**Filtr samolotów wojskowych**
- Przycisk z sylwetką myśliwca (pod przyciskiem etykiet) pokazuje na mapie wyłącznie samoloty, które baza oznacza jako wojskowe (flaga `dbFlags` z API ADS-B). Ponowne dotknięcie przywraca wszystkie. Po włączeniu aplikacja podaje, ile takich samolotów jest w zasięgu.
- Filtr działa do zamknięcia aplikacji i nie jest zapamiętywany. Można go łączyć z zaznaczaniem i izolacją.
- Flaga pochodzi z zewnętrznej bazy i bywa niedokładna: część wojskowych samolotów nie jest oznaczona, a część cywilnych (np. samoloty rządowe) może być oznaczona jako wojskowe.

**Android Auto**
- Mapa z samolotami na ekranie samochodu, przyciski przybliżania i oddalania, centrowanie na pozycji, przesuwanie, wyszukiwarka samolotów.
- Górny pasek akcji (od lewej): **filtr samolotów wojskowych** (ikona myśliwca), **izolacja** zaznaczonego samolotu, **przycisk etykiet** (pokazuje lub ukrywa etykiety wszystkich samolotów, domyślnie ukryte; to samo ustawienie co na telefonie) i **wyszukiwarka**. Włączony tryb jest pokazany osobną ikoną: zieloną i z obwódką (host Android Auto ignoruje kolor nadany ikonie i pozwala na kolorowe tło tylko jednego przycisku w pasku, dlatego obwódka zostaje widoczna nawet wtedy, gdy host sam wybarwi ikonę): filtr wojskowych, centrowanie (gaśnie po ręcznym przesunięciu mapy), etykiety i izolacja. Krótki komunikat na ekranie mówi, co się włączyło.
- Pasek mapy po prawej: przesuwanie, przybliżanie, oddalanie i **centrowanie na pozycji GPS** (przy przyciskach zoomu; świeci, gdy mapa podąża za pozycją lub samolotem).
- Ikona stanu aplikacji w lewym dolnym rogu, zawsze widoczna, przed numerem wersji (telefon i Android Auto): zielony znak ✓ – wszystko działa; niebieski radar z napisem „Wyszukiwanie samolotów…” – pobieranie listy samolotów; niebieska lupka z napisem „Pobieranie danych samolotu…” – pobieranie trasy zaznaczonego samolotu; niebieskie kółko z napisem „Wczytywanie mapy…” – pobieranie kafelków; czerwony wykrzyknik z napisem „Brak połączenia z siecią – samoloty niedostępne” – ostatnie pobranie samolotów się nie udało (np. brak zasięgu); pomarańczowa pauza z licznikiem – limit zapytań (429).
- Dotknięcie samolotu na ekranie dotykowym samochodu zaznacza go (pierścień), a wybór z wyszukiwarki też go zaznacza. **Zaznaczony samolot zawsze ma etykietę** (lot, poziom, typ i trasa), nawet gdy etykiety pozostałych są ukryte, bo w samochodzie nie ma karty samolotu. Zaznaczasz jeden samolot na raz, bo nie ma trybu „M”.
- **Okienko z danymi zaznaczonego samolotu** (lewy dolny róg mapy): lot i rejestracja, linia, typ i rodzaj, trasa z paskiem postępu, wysokość, prędkość, kurs, wznoszenie, odległość od Ciebie i squawk. Samolotów przysłoniętych przez okienko nie da się zaznaczyć, a dotknięcie okienka nic nie zmienia. Dotknięcie pustego miejsca na mapie zamyka okienko.
- Wybór samolotu z wyszukiwarki centruje na nim mapę, **automatycznie włącza izolację** i pokazuje okienko.

**Telefon i samochód działają tak samo i są zsynchronizowane**
- Zaznaczenie samolotu, izolacja i filtr samolotów wojskowych są wspólne: zmiana na telefonie od razu widać w samochodzie i odwrotnie. Wspólne są też ustawienia (np. etykiety). Wspólne jest też centrowanie: „Śledź” samolot i przycisk centrowania na pozycji działają na obu mapach naraz (ręczne przesunięcie mapy przerywa śledzenie tylko na tej mapie). Niezależne są pozycja po ręcznym przesunięciu i przybliżenie, a tryb „M” (wielokrotne zaznaczanie) działa tylko na telefonie.
- Wybór samolotu w wyszukiwarce (na telefonie albo w samochodzie) pokazuje go na obu mapach: centruje, zaznacza i izoluje.
- Zaznaczenie innego samolotu na jednym ekranie (dotknięciem, z wyszukiwarki) centruje na nim mapę także na drugim ekranie.
- Gdy samolot zaznaczony na jednym ekranie jest poza obszarem danych drugiego, drugi ekran przenosi się do niego i od razu pobiera samoloty w tej okolicy (zamiast pokazywać „poza zasięgiem danych”); dane zaznaczonego samolotu są przekazywane między ekranami, więc działa to także, gdy serwer akurat odmawia zapytań. Limit zapytań (429) jest wspólny: gdy serwer każe czekać, wstrzymują się oba ekrany.
- Na telefonie samolotów przysłoniętych przez kartę samolotu nie da się zaznaczyć, tak jak w samochodzie pod okienkiem.

## Jak to wygląda w użyciu

| Element | Gdzie |
|---|---|
| Menu (trzy kreski) | prawy górny róg: „Wyszukaj”, „Ulubione lotniska”, „Ustawienia” i „O aplikacji” |
| `I`, `M`, etykiety i filtr wojskowych | prawy górny róg, pod menu |
| `+`, `−`, „center” | prawy dolny róg |
| Wersja aplikacji | lewy dolny róg mapy |
| Atrybucja OSM | prawy dolny róg mapy |

> Zrzuty ekranu: miejsce na `docs/screenshots/`.

## Przykładowe zastosowania

1. **Obserwacja lotniska.** Ustaw mapę nad lotniskiem, przybliż do poziomu 11–13. Widzisz podchodzące i startujące samoloty z linią, typem i trasą, a ślady pokazują, którędy lecą.
2. **Co to leci nad głową?** Dotknij samolotu, który słyszysz. Zaznaczenie pokazuje jego lot, typ i trasę. Włącz izolację („I”), żeby odciąć resztę ruchu.
3. **Śledzenie konkretnego lotu.** Wyszukaj numer lotu lub rejestrację w oknie wyszukiwania, wybierz wiersz. Mapa wyśrodkuje się na samolocie i będzie za nim podążać.
4. **Porównanie kilku samolotów.** Włącz „M”, zaznacz kilka samolotów (np. kilka lotów jednej linii albo samoloty w jednym korytarzu powietrznym), włącz „I” i obserwuj tylko je.
5. **Pasażer w samochodzie.** Po podłączeniu do Android Auto na ekranie auta jest ta sama mapa. Wyszukiwarka i przyciski pozwalają szybko znaleźć samolot (patrz uwagi o bezpieczeństwie).

## Konfiguracja aplikacji

Menu → „Ustawienia”. Wartości są zapisywane na urządzeniu i obowiązują na telefonie oraz w Android Auto. Przy ustawieniach liczbowych, przełącznikach i liście jednostki jest okrągły przycisk „i”, który otwiera okienko z opisem, co dokładnie robi to ustawienie, wraz z przykładem (etykieta w bieżącej wielkości, pasek legendy z bieżącymi progami, ta sama wysokość w trzech jednostkach). Liczby wpisuje się w polach tekstowych, ustawienia włącz/wyłącz zmienia się przełącznikami, a wybór jednostki listą rozwijaną. Wartości spoza dozwolonego zakresu są ucinane, puste pola zostają bez zmiany.

| Ustawienie | Domyślnie | Zakres | Znaczenie |
|---|---|---|---|
| Odświeżanie samolotów [s] | 3 | 1–60 | odstęp między zapytaniami o samoloty |
| Odstęp klatek mapy [ms] | 33 | 16–1000 | tempo rysowania (33 ms to ok. 30 klatek/s) |
| Minimalny zoom | 4 | 1–18 | największe oddalenie |
| Maksymalny zoom | 13 | 1–18 | największe przybliżenie |
| Kafelki w pamięci | 128 | 16–1024 | liczba zdekodowanych kafelków w pamięci (zawsze co najmniej tyle, ile widać na ekranie plus zapas) |
| Ponowienie kafelka [s] | 15 | 1–600 | po jakim czasie ponawiane jest nieudane pobranie kafelka |
| Długość śladu [min] | 30 | 0–120 | jak długo pamiętany jest ślad lotu (0 wyłącza ślady), suwak co 1 min |
| Maks. punktów śladu | 700 | 10–5000 | limit punktów jednego śladu, suwak co 10 |
| Grubość śladu [dp] | 3 | 0,5–20 | suwak co 0,5 dp |
| Krycie najstarszego śladu | 140 | 0–255 | suwak co 5 |
| Krycie najnowszego śladu | 255 | 0–255 | suwak co 5 |
| Obwódka śladu [dp] | 1 | 0–10 | ciemna obwódka pod śladem, suwak co 0,5 dp |
| Krycie obwódki | 140 | 0–255 | |
| Przyciemnienie mapy | 90 | 0–220 | krycie czarnej warstwy nad kafelkami (0 wyłącza), im więcej, tym ciemniejsza mapa |
| Rozmiar ikon samolotów | ×1,0 | ×0,6–×2,0 | suwak co 0,1, mnoży rozmiar wszystkich ikon, kółek zaznaczenia i śledzenia oraz odsunięcie etykiety, na telefonie i w Android Auto. Grubość obwódki nie zmienia się z rozmiarem ikony. |
| Obwódka samolotów [dp] | 1,2 | 0–6 | suwak co 0,1 dp (0 wyłącza obwódkę). Oba suwaki są w jednej ramce „Ikony samolotów” po skali kolorów wysokości, nad suwakami jest podgląd wszystkich ikon samolotów w bieżącym rozmiarze i grubości obwódki. |
| Etykiety samolotów | wyłączone | przełącznik | pokazuje lub ukrywa etykiety (to samo robi przycisk na mapie) |
| Lotniska na mapie | włączone | przełącznik | pokazuje lotniska i pasy startowe |
| Tylko ulubione lotniska | wyłączone | przełącznik | pod głównym przełącznikiem: pokazuje na mapie wyłącznie ulubione lotniska, przy każdym zoomie. Rodzaje lotnisk, filtr małych lotnisk i zakres zoomu są wtedy wyszarzone, a podgląd pokazuje wszystkie rodzaje jako ukryte. |
| Rodzaje lotnisk na mapie | duże, średnie, wojskowe | pola wyboru | zaznaczasz, które rodzaje widzisz: duże, średnie, małe, wojskowe (czerwone, każdej wielkości) i helipady (jasnoniebieskie, domyślnie wyłączone). Zakres zoomu nie zależy od rodzaju. Małych lotnisk jest najwyżej 60, a helipadów 40 na klatkę (najbliższe środka ekranu). Pola zastąpiły dawny wybór zakresu z listy. |
| Małe: tylko z utwardzonym pasem od 800 m | wyłączone | przełącznik | ogranicza małe lotniska do tych z asfaltowym lub betonowym pasem od 800 m (w Polsce kilkanaście z ponad 300, na świecie około 4800 z ponad 42 tys.). Przełącznik jest wyszarzony, dopóki małe lotniska są odznaczone, a jego przycisk „i” zostaje aktywny i to wyjaśnia. Ulubione lotniska omijają ten filtr. |
| Kody lotnisk | włączone | przełącznik | kod IATA albo ICAO obok znacznika |
| Zakres zoomu, w którym widać lotniska | od 5, bez górnego limitu | dwa uchwyty | jedna kontrolka ze skalą całego zakresu zoomu mapy (z ustawień zoomu minimalnego i maksymalnego): lewy uchwyt to zoom, od którego widać lotniska, a prawy to zoom, powyżej którego znikają (prawy koniec skali oznacza brak górnego limitu). Zakres jest taki sam dla wszystkich rodzajów, o rodzajach decydują pola wyboru. Pod skalą jest zapis zakresu, a pod markerami w podglądzie to samo. Ulubione są widoczne od zoomu 5 niezależnie od tego zakresu. |
| Wielkość kodów lotnisk [dp] | 11 | 6–30 | suwak co 0,5 dp |
| Legenda wysokości | włączona | przełącznik | pokazuje lub ukrywa pasek legendy kolorów wysokości |
| Jednostka wysokości | metry | lista wyboru | metry (m, km), stopy (ft) albo poziom lotu (FL). Dotyczy etykiet na mapie, wyników wyszukiwania, karty i popupu samolotu oraz legendy, na telefonie i w Android Auto. Karta pokazuje dodatkowo równoważną wartość w nawiasie. Jednostka dotyczy też edytora skali kolorów. |
| Wielkość liter etykiet [dp] | 13 | 6–40 | suwak co 0,5 dp z podglądem przykładowej etykiety w wybranej wielkości |
| Skala kolorów wysokości | 300 / 1200 / 3000 / 6000 / 12000 m | 50–20000 m | jeden edytor zamiast pięciu pól: pasek z gradientem i pięcioma znacznikami (pomarańczowy, żółty, zielony, niebieski, fioletowy). Znacznik przeciąga się palcem (nie przejdzie przez sąsiada, wartości przyciągają się do 50 m albo 100 ft), a dotknięcie otwiera okienko do wpisania dokładnej wartości w wybranej jednostce. Szary punkt po lewej to ziemia (0), a pierwszy próg nie może być niższy niż 50 m, żeby strefa pomarańczowa nie zniknęła. Skala paska jest pierwiastkowa, żeby niskie progi się nie zlewały. Pod paskiem jest podgląd śladu wznoszącego się samolotu w tych samych kolorach co na mapie. Kolory są stałe. Sekcja jest wydzielona w ramce, a przycisk „Przywróć domyślne kolory” ustawia domyślne progi i zapisuje je od razu. Wartości są zapisywane w metrach. |

Każda zmiana zapisuje się od razu, na telefonie i w Android Auto, bez przycisku „Zapisz”. Pola liczbowe zapisują się po opuszczeniu pola (Gotowe albo dotknięcie innego miejsca), suwaki i lista na bieżąco. Przy suwakach, liście jednostki i polach liczbowych jest przycisk „↺” (cofnij), widoczny, gdy jest co cofać: każde dotknięcie cofa kolejną zmianę o jeden krok (przeciągnięcie suwaka i wpisanie wartości to po jednej zmianie). Historia zmian jest w pamięci aplikacji: zostaje po wyjściu z ustawień, ale znika po zamknięciu aplikacji. Przełączniki i skala kolorów nie mają cofania. Ramki „Ikony samolotów” i „Ślady samolotów” mają własne przyciski przywracające domyślne wartości, a na dole jest „Przywróć wszystkie domyślne”. Przywrócenie domyślnych też można cofnąć „↺”.

Wszystkie suwaki: dotknięcie niebieskiej wartości pod nazwą (✎) otwiera okienko do wpisania dokładnej liczby z zakresu, także pomiędzy pozycjami suwaka.

Ustawienia lotnisk są w jednej ramce po skali kolorów wysokości. Na samej górze jest przełącznik „Lotniska na mapie”, a pod nim „Tylko ulubione lotniska”; po wyłączeniu głównego przełącznika cała reszta ramki (podgląd, suwaki, pola, przełączniki, przyciski) jest wyszarzona i nieaktywna, tylko przyciski „i” działają. Pod przełącznikiem jest podgląd pięciu rodzajów lotnisk z nazwami rodzajów (duże, średnie, małe, wojskowe, helipady) pod markerami w wielkości kodów; rodzaje wyłączone są wyszarzone. Zaraz pod podglądem jest suwak „Wielkość kodów lotnisk”, a dalej kolejno: „Kody lotnisk”, „Rodzaje lotnisk na mapie”, „Małe: tylko z utwardzonym pasem od 800 m” i zakres zoomu, w którym widać lotniska. Ten sam podgląd jest w okienkach „i” tych ustawień, a ramka ma przycisk „Przywróć domyślne lotniska”.

Ustawienia śladu (od „Długość śladu” do „Krycie obwódki”) są w jednej ramce „Ślady samolotów”, po ramce „Ikony samolotów”. Na górze ramki jest podgląd śladów trzech samolotów (dron, śmigłowiec, samolot pasażerski) rysowanych tym samym kodem co mapa, z bieżącymi wartościami suwaków i z progami kolorów wysokości. Podgląd zmienia się na bieżąco podczas przesuwania suwaków.

**Wskazówki**
- Wolniejszy wyświetlacz w samochodzie? Zwiększ „Odstęp klatek mapy” (np. do 50–66 ms).
- Obciążenie sieci i bateria spadają po zwiększeniu „Odświeżania samolotów”. Zbyt częste zapytania kończą się odpowiedzią 429 z API i aplikacja zwalnia sama (patrz niżej).

**Uprawnienia**
- **Lokalizacja:** potrzebna do niebieskiej kropki i przycisku „center”. Bez niej mapa działa, ale nie śledzi Twojej pozycji.
- **Usługa pierwszoplanowa (lokalizacja) i powiadomienia:** gdy aplikacja liczy pozycję, Android wymaga stałego powiadomienia „Śledzenie pozycji”. Bez niego system zatrzymałby pobieranie pozycji, gdy ekran telefonu jest wygaszony, a mapa działa w samochodzie.
- **Internet:** kafelki, samoloty, trasy.

## Android Auto

Aplikacja rejestruje się w Android Auto jako aplikacja **nawigacyjna** (`NavigationTemplate` z własną powierzchnią do rysowania). Ponieważ nie jest dystrybuowana przez Sklep Play, trzeba ją uruchamiać z nieznanych źródeł:

1. W aplikacji **Android Auto** na telefonie włącz tryb dewelopera (wielokrotnie dotknij pozycji „Wersja” w ustawieniach).
2. W menu dewelopera włącz **„Nieznane źródła”**.
3. Podłącz telefon do auta lub uruchom **Desktop Head Unit (DHU)** z Android SDK.
4. Uruchom aplikację z listy aplikacji w Android Auto.

> W obecnej wersji aplikacja nie zawsze pojawia się na liście w Android Auto. To jest główne otwarte zadanie.

## Budowanie ze źródeł

**Wymagania**
- .NET SDK 10 z workloadem `android` (`dotnet workload install android`),
- Android SDK (API 36), najlepiej z Visual Studio 2026 lub Android Studio,
- `keytool` (z JDK) do utworzenia klucza podpisu,
- `adb`, aby zainstalować aplikację na telefonie.

**Klucz podpisu.** Każdy build (Debug i Release) jest podpisywany tym samym kluczem w formacie `aab`. Klucza i haseł **nie ma w repozytorium**. Utwórz własny:

```powershell
keytool -genkeypair -v -keystore "$env:USERPROFILE\adsbcarmap-release.jks" -alias adsbcarmap -keyalg RSA -keysize 2048 -validity 10000
```

Następnie utwórz plik `%USERPROFILE%\adsbcarmap-signing.props` z hasłami (plik leży poza projektem, a `.gitignore` go ignoruje):

```xml
<Project>
  <PropertyGroup>
    <AndroidSigningStorePass>hasło-magazynu</AndroidSigningStorePass>
    <AndroidSigningKeyPass>hasło-klucza</AndroidSigningKeyPass>
  </PropertyGroup>
</Project>
```

Zamiast pliku możesz ustawić zmienne środowiskowe `ADSBCARMAP_STOREPASS` i `ADSBCARMAP_KEYPASS` (ich zmiana wymaga restartu programu, który buduje, np. Visual Studio).

**Kontakt w User-Agent (opcjonalnie).** Serwery danych i kafelków OpenStreetMap proszą, aby aplikacje się przedstawiały. Utwórz `%USERPROFILE%\adsbcarmap-contact.props`:

```xml
<Project>
  <PropertyGroup>
    <AdsbCarMapContact>twoj@adres.pl</AdsbCarMapContact>
  </PropertyGroup>
</Project>
```

Bez tego pliku aplikacja wysyła tylko nazwę i wersję. Jeśli publikujesz własną wersję aplikacji, wpisz własny kontakt.

**Build i instalacja**

```powershell
dotnet build -c Debug
dotnet build -c Debug -t:Install "-p:AdbTarget=-s <numer-seryjny-urządzenia>"
```

Numer seryjny urządzenia pokaże `adb devices`. Każdy build zwiększa numer kompilacji w `BuildNumber.txt`, który jest też `versionCode` aplikacji. Wersja aplikacji (`versionName`) jest w pliku `Version.txt` w postaci `0.<druga>.<trzecia>` (np. `0.3.0`) i zmienia się tylko przy przygotowaniu wydania skryptem `Scripts\Bump-Version.ps1`. W lewym dolnym rogu mapy i na ekranie ustawień widać wersję z numerem kompilacji, np. `v0.3.0 (164)`. `versionCode` nigdy nie jest zerowany, bo Google Play wymaga, żeby rósł.

**Skrypty pomocnicze (`Scripts/`)**

| Skrypt | Działanie |
|---|---|
| `Setup-Environment.ps1` | Przygotowuje nowy komputer: kopiuje pliki podpisu i kontaktu z wybranego katalogu (np. pendrive'a) do `%USERPROFILE%`, opcjonalnie ustawia `JAVA_HOME` i zmienne z hasłami oraz dopisuje katalog `platform-tools` (ADB) do PATH użytkownika. |
| `Release.ps1` | Buduje podpisany plik `aab` w konfiguracji Release. Gdy istnieje `adsbcarmap-signing.props` lub zmienna `ADSBCARMAP_STOREPASS`, nie pyta o hasło, a w przeciwnym razie prosi o nie i ustawia zmienne tylko na czas działania. Po udanym buildzie tworzy adnotowany tag `v<wersja z Version.txt>` (np. `v0.3.0`), a jeśli taki tag już istnieje, przerywa z podpowiedzią, żeby najpierw podbić wersję na bieżącym commicie, z wersją, `versionCode`, commitem, nazwą, rozmiarem i SHA-256 paczki, oraz dopisuje te wartości do `release-history.csv`. Wersja musi być zatwierdzona w `Version.txt` przed buildem. Przed buildem wymaga czystego drzewa (poza `BuildNumber.txt` i `release-history.csv`). Przełączniki: `-NoTag` (sam build), `-Push` (wysyła tag na GitHuba), `-Force` (pomija sprawdzenie drzewa), `-SkipAirportData` (nie pobiera danych lotnisk). Przed buildem, gdy istnieje `Assets\Airports\airports.meta`, skrypt sam uruchamia `Update-AirportData.ps1`; jeśli dane się zmieniły, drzewo jest brudne i trzeba zatwierdzić nowe pliki przed ponownym uruchomieniem. |
| `Bump-Version.ps1` | Zmienia wersję w `Version.txt` przed wydaniem. Wymaga dokładnie jednego przełącznika, bo to Ty decydujesz: `-Minor` (nowe wydanie, druga cyfra rośnie, trzecia wraca do 0, np. 0.3.0 → 0.4.0) albo `-Patch` (poprawka, tylko trzecia cyfra, np. 0.3.0 → 0.3.1). Po skrypcie zatwierdź `Version.txt` i uruchom `Release.ps1`. |
| `Update-AirportData.ps1` | Pobiera najnowsze dane lotnisk i pasów startowych z OurAirports (domena publiczna) i zapisuje zwięzłe pliki `Assets\Airports\airports.tsv`, `runways.tsv` i `airports.meta` (liczby wierszy i SHA-256). Zostają duże, średnie i małe lotniska (bez śmigłowcowych, wodnosamolotowych i zamkniętych) oraz pasy ze współrzędnymi obu końców. Lotnisko wojskowe jest zgadywane po nazwie. Pliki są nadpisywane tylko wtedy, gdy dane się zmieniły. Parametry: `-OutputDirectory`, `-Url`. |
| `Release-Notes.ps1` | Tworzy notatki wydania: podsumowanie commitów od poprzedniego tagu wydania, pogrupowane na Add, Fix, Change, Remove i Other. Wypisuje je i zapisuje w `release-notes\<wersja>.md` (katalog ignorowany przez git). Parametry: `-From`, `-To`, `-Version`, `-NoFile`. |
| `DHU_Start.ps1` | Przekierowuje port `5277` i uruchamia Desktop Head Unit (emulator samochodu). |
| `DHU_Logs.ps1` | Czyści logcat i zapisuje go do `logs/android-auto.log` (katalog ignorowany przez git). |

**Praca na drugim komputerze.** Kod pobierz z GitHuba (`git clone`). Poza repozytorium trzeba przenieść trzy pliki z `%USERPROFILE%`: `adsbcarmap-release.jks`, `adsbcarmap-signing.props` i `adsbcarmap-contact.props`. Bez tego samego klucza podpisu nie zaktualizujesz aplikacji zainstalowanej na telefonie, trzeba by ją odinstalować. Przenoś je na zaszyfrowanym nośniku i usuń po pracy. Potem uruchom:

```powershell
.\Scripts\Setup-Environment.ps1 -SourceDirectory E:\ -JavaHome "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot" -PlatformTools "C:\Android\platform-tools"
```

Wszystkie parametry są opcjonalne (`-SetSigningVariables` zapisuje hasła w zmiennych środowiskowych zamiast w pliku `.props`). Po skrypcie zrestartuj Visual Studio i terminale. Emulator Desktop Head Unit zainstalujesz przez SDK Manager (Android Auto Desktop Head Unit Emulator).

## Źródła danych i atrybucja

| Dane | Źródło | Uwagi |
|---|---|---|
| Mapa | © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, kafelki z `tile.openstreetmap.org` | obowiązuje [Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/); atrybucja jest rysowana na mapie |
| Samoloty (ADS-B) | [adsb.lol](https://adsb.lol), `api.adsb.lol` (API zgodne z airplanes.live) | pozycje, kurs, wysokość, typ, rejestracja, kategoria |
| Trasa i linia lotnicza | [adsbdb](https://www.adsbdb.com), `api.adsbdb.com` | pobierane po znaku wywoławczym |

Zapoznaj się z warunkami użycia tych serwisów, zanim udostępnisz własną kompilację innym osobom. Aplikacja nie ma własnego serwera: wszystkie zapytania idą bezpośrednio z telefonu.

## Prywatność

- Aplikacja **nie ma telemetrii ani kont** i niczego nie wysyła do autora.
- Pozycja GPS jest używana lokalnie. **Do serwera samolotów (adsb.lol) trafiają współrzędne środka widocznej mapy** (oraz promień), bo tym zapytaniem pobierane są samoloty w okolicy. Gdy mapa jest wyśrodkowana na Twojej pozycji, jest to przybliżenie Twojej lokalizacji. Serwery kafelków dostają numery kafelków, a serwis tras (adsbdb) znaki wywoławcze samolotów, których trasę aplikacja pokazuje.
- Wszystkie serwery widzą adres IP telefonu i nagłówek User-Agent (nazwa aplikacji, wersja, opcjonalny kontakt).

## Znane ograniczenia

- **Android Auto:** aplikacja nie zawsze pojawia się lub działa w Android Auto. Walidator hosta (`AllowAllHostsValidator`) jest ustawiony tak, że przyjmuje każdego hosta. To uproszczenie na potrzeby PoC, a nie ustawienie do dystrybucji.
- **Limity API:** zbyt częste zapytania kończą się odpowiedzią 429. Aplikacja wtedy wstrzymuje zapytania z wydłużanym odstępem (5, 10, 20, 40, 60 s, albo według `Retry-After`).
- **Typ samolotu:** pokazywany jest kod ICAO (np. `A21N`), bez pełnej nazwy. Ikona jest dobierana heurystycznie, więc rzadkie typy mogą dostać ogólną ikonę. Znacznik „wojskowy” w bazie bywa niedokładny.
- **Dane ADS-B:** nie obejmują wszystkich samolotów (część wojskowych, część nieużywających transpondera lub zakłócona). Opóźnienia i błędy pozycji są możliwe.
- **Cache kafelków:** tylko w pamięci, po restarcie kafelki są pobierane od nowa.
- **Minimalna wersja Androida:** 7.0 (API 24). Aplikacja była sprawdzana tylko na jednym telefonie z nowym Androidem.

## Struktura projektu

```
Activities/   ekrany telefonu: mapa (MainActivity), wyszukiwarka, ustawienia
Auto/         część Android Auto: usługa Car App, ekran mapy, ekran wyszukiwania
Data/         modele i klienci: samoloty, trasy, klasyfikacja rodzajów, wyszukiwanie
Map/          renderowanie mapy: kontroler, kafelki, projekcja, kształty ikon, widok telefonu
Services/     współdzielone usługi: klienci HTTP, pozycja GPS, usługa pierwszoplanowa, ruch na żywo
Settings/     definicje i zapis ustawień
Resources/    ikony i zasoby graficzne
Scripts/      skrypty PowerShell: konfiguracja komputera, build Release, Android Auto DHU
RELEASE_NOTES.md  historia wersji: co zostało wydane w kolejnych wydaniach
release-history.csv  wersja, commit i SHA-256 każdej zbudowanej paczki
```

Mapa na telefonie i w Android Auto używa tego samego `MapController`, więc zmiany w rysowaniu dotyczą obu.

## Plany

- Pełne nazwy typów samolotów.
- Cache kafelków na dysku.
- Przycisk izolacji w Android Auto.

## Ważna uwaga

To narzędzie do zabawy i obserwacji, **nie do nawigacji, ruchu lotniczego ani zapewnienia bezpieczeństwa**. Dane mogą być niepełne, opóźnione lub błędne. W samochodzie nie obsługuj aplikacji w czasie jazdy. Kierowca ma patrzeć na drogę, a z aplikacji korzystać powinien pasażer albo kierowca na postoju.

## Licencja

Kod jest udostępniony na licencji [MIT](LICENSE). Dane mapy i samolotów podlegają licencjom i warunkom ich dostawców (patrz wyżej).

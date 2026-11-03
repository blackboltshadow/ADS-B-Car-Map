# Plan rozwoju

Lista funkcji, które zostały do zrobienia, z priorytetami (stan na 2026-11-03; adsb.fi dodane jako trzecie źródło pozycji). Ukończone pozycje są usuwane (historia jest w commitach i README). Wydaniami zajmuje się autor projektu, ten plik ich nie planuje.

Zasada kolejności: najpierw rzeczy, które pomagają ocenić to, co już dodano (diagnostyka, odporność), potem dostęp do danych i prywatność, a dopiero potem nowe funkcje.

## P1 – diagnostyka i odporność (najpierw)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 2 | Sprawdzenie na telefonie i w DHU po rozbiciu `MapController` (pozycja 10): kafelki, zoom i przesuwanie, popup samolotu i lotniska, legenda wysokości oraz ikona stanu (zasilana teraz z `AircraftDataLoop`: ładowanie, brak sieci, uzupełnianie danych zaznaczonego samolotu) wyglądają i działają jak przed zmianą; do tego karta „Stan źródeł” i ekran „Log zapytań” (filtr błędów i hosta); ekran startowy (ciemne tło z ikoną zamiast białego błysku przy zimnym starcie, napis „Wczytywanie…” do pierwszej odpowiedzi samolotów). **Sprawdzone na telefonie (2026-11-03); w DHU i samochodzie jeszcze nie.** | Testy ręczne, bez kodu, ale od nich zależy, co poprawiać | mały |

## P2 – dostęp do danych i przed jakąkolwiek dystrybucją

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 3 | Dostęp do airplanes.live: napisać na contact@airplanes.live (opis projektu, link do repozytorium, kontakt w User-Agencie) i poprosić o dopuszczenie ruchu aplikacji. Zadanie po stronie autora aplikacji | Dziś serwis odpowiada 403, więc jako główne źródło nie działa, a jako zapasowe tylko pozornie | mały |
| 6 | Sprawdzenie warunków użycia adsb.lol, hexdb.io i adsb.fi (oraz airplanes.live po uzgodnieniu) i notatka licencyjna w `DATA_SOURCES.md` przed udostępnieniem aplikacji komukolwiek poza autorem; ewentualnie FlyItalyADSB (klucz poza repozytorium) lub umowa z dostawcą | Częściowo zrobione 2026-11-03: sekcja „Warunki użycia” w `DATA_SOURCES.md` (adsb.fi: tylko niekomercyjnie z atrybucją; adsb.lol: brak warunków danych i zapowiedź klucza dla karmiących; hexdb.io: brak jakichkolwiek warunków). Zostaje: airplanes.live po uzgodnieniu (pozycja 3), ręczne przejrzenie stron niedostępnych automatycznie, mail do hexdb.io i decyzja przed dystrybucją | mały |

## P3 – funkcje

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 7 | Alerty „samolot w pobliżu” (lokalne powiadomienia, bez Firebase: brak backendu i nie wysyłamy pozycji na serwer). Reguła = nazwa + obszar + warunek (zapisany filtr samolotów) + zdarzenia (wlot, pojawienie się) + czas ciszy liczony osobno dla każdego alertu. **Zrobione i sprawdzone na telefonie (2026-11-03):** etap 1, czysta logika (`AlertEngine`, model reguł, `AlertQueryPlanner`, zapis JSON, teksty, testy); etap 2, ekrany „Alerty” i „Alert” (obszary: wokół GPS, wokół lotniska, okrąg ze współrzędnymi wpisanymi, z danych lub z GPS; test z licznikiem; powiadomienie otwiera mapę z zaznaczonym samolotem); etap 3, tryb w tle (`AlertBackgroundRunner`, usługa lokalizacji, bliskie obszary dzielą zapytanie), a jego częstotliwość i limit alertów to suwaki w Ustawienia → Alerty; autostart trybu w tle po restarcie telefonu i aktualizacji aplikacji (`BootReceiver`, `AlertBackgroundPolicy`, przełącznik w Ustawienia → Alerty, prośby o zgodę na powiadomienia i lokalizację „Zawsze zezwalaj” po włączeniu trybu w tle; zrobione w kodzie, niesprawdzone na telefonie). **Zrobione w kodzie (2026-11-03), niesprawdzone na telefonie ani w samochodzie:** (4) wybór środka okręgu dotknięciem na mapie oraz wielokąty i prostokąty rysowane na mapie (`AreaPickerActivity`, `AreaPickerView`, `AlertArea.Polygon`; tylko telefon), (5) powiadomienia w Android Auto (`CarAppExtender`, przełącznik w ustawieniach), harmonogram (dni tygodnia i godziny ciszy, `AlertSchedule`), opcja pominięcia „pojawienia się” przy pierwszym sprawdzeniu (`AlertEngine.SkipInitialAppearance`). Do tego zrobione w kodzie (2026-11-03, niesprawdzone na telefonie): grupowanie powiadomień (`AlertGrouper`, próg w ustawieniach), historia alertów (`AlertHistory`, `AlertHistoryStore`, ekran „Historia alertów”), rodzaj powiadomienia per alert (`AlertImportance`, trzy kanały) oraz przyciski „Pokaż na mapie” i „Wycisz na 1 h” (`AlertActionReceiver`, `AlertMuteList`). **Zostaje:** sprawdzenie tych funkcji na telefonie i w DHU, przesuwanie wierzchołków wielokąta na mapie, ewentualnie korytarz/linia, sprawdzenie na telefonie, czy po restarcie tryb w tle dostaje pozycję GPS z przyznanym `ACCESS_BACKGROUND_LOCATION` | Wyróżnik aplikacji. Obszary zostają tylko na telefonie | średni |
| 8 | Filtry samolotów, część czwarta: wysokość w wybranej jednostce, tabela linii lotniczych (kody IATA i nazwy linii w warunku linii; źródło OpenFlights na licencji ODbL wymaga atrybucji i sprawdzenia licencji przed użyciem), kod ICAO lotniska obok IATA w warunkach lotniska, podpowiedzi przy wpisywaniu, pobieranie tras od samolotów najbliższych środka mapy, parser zapisu tekstowego | Zrobione: tabela typów z kodami IATA i rodzinami, pole „Cała rodzina” w warunku typu, dziennik brakujących typów z ekranem „Dane developerskie” w ustawieniach (sprawdzone na telefonie), model wyrażeń z logiką trójwartościową, tabela producentów, ekran listy filtrów z przełącznikami, kreator z drzewem warunków (grupy ORAZ/LUB bez praktycznego limitu zagnieżdżeń, NIE na każdym węźle, przesuwanie, przenoszenie, kopiowanie, zwijanie), opis i zdanie „Czytaj”, zapis JSON, trzy filtry wbudowane tylko do odczytu, przycisk z licznikiem na mapie. | średni |
| 11 | Warstwa zagrożeń powietrznych NEPTUN (osobna, domyślnie wyłączona warstwa tylko na telefonie; opis i warunki użycia w `DATA_SOURCES.md`). **Zrobione w kodzie (2026-11-03), niesprawdzone na telefonie:** projekt `Sources\Neptun` (parser, `ThreatStore`, klient REST, WebSocket, `NeptunThreatFeed` z ponawianiem i zapasem), `Threat` i `IThreatFeed` w Core, `ThreatLayer` (znacznik, strzałka, koło niepewności, ślad, etykieta), przycisk na mapie i ustawienie, szczegóły po dotknięciu, klikalna atrybucja z ostrzeżeniem, wybór transportu w Ustawienia → Dev. **Zostaje:** sprawdzenie na telefonie (czy strumień działa w sieci komórkowej, zużycie baterii, czytelność przy wielu zagrożeniach), filtry według rodzaju i wiarygodności, ewentualne wypełnianie alarmów rejonów i obwodów (pliki GeoJSON granic), alerty „zagrożenie w pobliżu” przez silnik alertów (wymaga adaptera z `Threat`), przewidywanie położenia między odczytami (`velocity`, `confirmedAt`), wiadomości z Telegrama | Nowa wartość dla użytkowników w Ukrainie i okolicy; ostrożność w prezentacji (obserwacje, pozycje tylko z obwodem, ostrzeżenie), bo dotyczy bezpieczeństwa ludzi | średni |

## P4 – porządki techniczne (przy okazji)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 10 | Dalsze rozbicie `MapController` (po podziale na pliki partial ma około 2400 linii): gesty i zaznaczanie (największy jest `MapController.Selection.cs`, 710 linii) | Zrobione: podział na pliki partial, TileLayer, PopupRenderer, AltitudeLegendRenderer, StatusIconRenderer, MapMath, TrailTracker i AircraftDataLoop (pętla pobierania, bez zmian zachowania). Ryzykowne, więc osobne zadanie, bez zmian zachowania | średni |

- Tłumaczenie pozostałych komentarzy na angielski (przy okazji edycji plików).
- Zrzuty ekranu do README.
- GitHub Actions: testy już działają (`.github/workflows/tests.yml`); zostaje build aplikacji bez podpisu (klucz jest poza repozytorium).
- Uzupełnianie tabeli typów samolotów o typy z dziennika brakujących typów (pełne nazwy są już na karcie i w wyszukiwarce).

## P5 – pomysły


- Historia obserwowanych samolotów.
- **Pomysły do alertów (zapisane 2026-11-03, w kolejności polecanej):**
  - Squawk awaryjny: **zrobione w kodzie (2026-11-03), niesprawdzone na telefonie** jako wbudowany filtr „Squawk awaryjny” (`BuiltInFilters.Emergency`, nowe pole `FilterField.Squawk`), który wybiera się jako warunek alertu. Zostaje ewentualnie gotowa reguła alertu bez ręcznego wybierania obszaru i warunku.
  - Zdarzenie „wylot z obszaru”: **zrobione w kodzie (2026-11-03), niesprawdzone na telefonie** (`AlertTrigger.Exited`, pole `NotifyOnExit`, własny czas ciszy wylotu; samolot, który zniknął z danych, nie daje wylotu).
  - Lądowanie i start jako zdarzenie w obszarze lotniska (przejście „w powietrzu ↔ na ziemi”).
  - Alert na konkretny samolot: **zrobione w kodzie (2026-11-03), niesprawdzone na telefonie** jako przycisk „Obserwuj” na karcie samolotu (`AlertRule.WatchedHex`, `AlertBundle.CreateWatchRule`: okrąg 300 km wokół GPS). Zostaje: alert po rejestracji lub znaku wywoławczym bez wybranego samolotu, obserwowanie z poziomu wyszukiwarki i w samochodzie, samoczynne usuwanie alertu po lądowaniu.
  - Warunek na prędkość pionową: **zrobione w kodzie (2026-11-03), niesprawdzone na telefonie** jako pole `FilterField.VerticalRate` (zakres w ft/min, w kreatorze w jednostce z ustawień) i wbudowany filtr „Szybko opadające” (poniżej −3000 ft/min). Zostaje ewentualne wymaganie, by wartość utrzymała się przez dwa kolejne odczyty (szum transpondera).
  - Korytarz wzdłuż linii (odległość od odcinka; blisko wielokątów, które już są).
  - Kopiowanie alertu, eksport i import alertów jako plik: **zrobione w kodzie (2026-11-03), niesprawdzone na telefonie** (`AlertBundle`, ikona kopiowania na liście, przyciski „Eksportuj alerty” i „Importuj alerty”; eksport zawiera użyte własne filtry, import nie nadpisuje istniejących alertów).
  - Test „czy ten alert zadziałałby wczoraj” (wymaga historii pozycji, więc raczej daleka przyszłość).
  - Ostrzeżenie o kosztach zapytań przy dużych obszarach w trybie w tle (planer zapytań już liczy koszt).
  - Przesuwanie wierzchołków wielokąta na mapie jest zrobione; zostaje ewentualne zaznaczanie kilku wierzchołków naraz.
- Przyloty i odloty lotniska: najpierw lista „na żywo” z pozycji i tras, potem opcjonalnie rozkład z AeroDataBox z kluczem użytkownika (porównanie źródeł i limity w DATA_SOURCES.md, sekcja „Przyloty i odloty lotniska”).

## Proponowana kolejność

1 → 2 → 3 (mail, można wysłać od razu, równolegle) → 6 → 7 → 8. Pozycje z P4 dochodzą przy okazji edycji danego pliku; 10 jako osobne zadanie, gdy zmiany w mapie przestaną być częste.

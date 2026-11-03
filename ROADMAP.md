# Plan rozwoju

Lista funkcji, które zostały do zrobienia, z priorytetami (stan na 2026-11-03). Ukończone pozycje są usuwane (historia jest w commitach i README). Wydaniami zajmuje się autor projektu, ten plik ich nie planuje.

Zasada kolejności: najpierw rzeczy, które pomagają ocenić to, co już dodano (diagnostyka, odporność), potem dostęp do danych i prywatność, a dopiero potem nowe funkcje.

## P1 – diagnostyka i odporność (najpierw)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 2 | Sprawdzenie na telefonie i w DHU po rozbiciu `MapController` (pozycja 10): kafelki, zoom i przesuwanie, popup samolotu i lotniska, legenda wysokości oraz ikona stanu (zasilana teraz z `AircraftDataLoop`: ładowanie, brak sieci, uzupełnianie danych zaznaczonego samolotu) wyglądają i działają jak przed zmianą; do tego karta „Stan źródeł” i ekran „Log zapytań” (filtr błędów i hosta); ekran startowy (ciemne tło z ikoną zamiast białego błysku przy zimnym starcie, napis „Wczytywanie…” do pierwszej odpowiedzi samolotów). **Sprawdzone na telefonie (2026-11-03); w DHU i samochodzie jeszcze nie.** | Testy ręczne, bez kodu, ale od nich zależy, co poprawiać | mały |

## P2 – dostęp do danych i przed jakąkolwiek dystrybucją

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 3 | Dostęp do airplanes.live: napisać na contact@airplanes.live (opis projektu, link do repozytorium, kontakt w User-Agencie) i poprosić o dopuszczenie ruchu aplikacji. Zadanie po stronie autora aplikacji | Dziś serwis odpowiada 403, więc jako główne źródło nie działa, a jako zapasowe tylko pozornie | mały |
| 4 | adsb.fi jako drugie źródło pozycji (`opendata.adsb.fi/api`, 1 zapytanie/s, tylko użytek osobisty): nowy katalog `Sources\AdsbFi` z klientem i testami, wpis w `AppServices`, `DataSourceSetting` i `AdsbCarMap.csproj`. Ścieżkę zapytania punktu trzeba najpierw sprawdzić na żywo | Prawdziwy zapas, gdy airplanes.live jest niedostępne. Dzięki zasadzie niezależnych źródeł nie dotyka reszty kodu | średni |
| 6 | Sprawdzenie warunków użycia adsb.lol, hexdb.io i adsb.fi (oraz airplanes.live po uzgodnieniu) i notatka licencyjna w `DATA_SOURCES.md` przed udostępnieniem aplikacji komukolwiek poza autorem; ewentualnie FlyItalyADSB (klucz poza repozytorium) lub umowa z dostawcą | Częściowo zrobione 2026-11-03: sekcja „Warunki użycia” w `DATA_SOURCES.md` (adsb.fi: tylko niekomercyjnie z atrybucją; adsb.lol: brak warunków danych i zapowiedź klucza dla karmiących; hexdb.io: brak jakichkolwiek warunków). Zostaje: airplanes.live po uzgodnieniu (pozycja 3), ręczne przejrzenie stron niedostępnych automatycznie, mail do hexdb.io i decyzja przed dystrybucją | mały |

## P3 – funkcje

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 7 | Alerty „samolot w pobliżu” (wojskowy, typ, linia, promień) | Wyróżnik aplikacji. Wymaga decyzji, jak powiadamiać w Android Auto | średni/duży |
| 8 | Filtry samolotów, część czwarta: wysokość w wybranej jednostce, tabela linii lotniczych (kody IATA i nazwy linii w warunku linii; źródło OpenFlights na licencji ODbL wymaga atrybucji i sprawdzenia licencji przed użyciem), kod ICAO lotniska obok IATA w warunkach lotniska, podpowiedzi przy wpisywaniu, pobieranie tras od samolotów najbliższych środka mapy, parser zapisu tekstowego | Zrobione: tabela typów z kodami IATA i rodzinami, pole „Cała rodzina” w warunku typu, dziennik brakujących typów z ekranem „Dane developerskie” w ustawieniach (sprawdzone na telefonie), model wyrażeń z logiką trójwartościową, tabela producentów, ekran listy filtrów z przełącznikami, kreator z drzewem warunków (grupy ORAZ/LUB bez praktycznego limitu zagnieżdżeń, NIE na każdym węźle, przesuwanie, przenoszenie, kopiowanie, zwijanie), opis i zdanie „Czytaj”, zapis JSON, trzy filtry wbudowane tylko do odczytu, przycisk z licznikiem na mapie. | średni |
| 9 | Wbudowana tabela pełnych nazw typów (np. A21N jako Airbus A321neo) dla samolotów niezaznaczonych i dla wyszukiwarki | Dla zaznaczonego samolotu pełna nazwa typu, producent i właściciel pochodzą już z adsbdb i hexdb (karta samolotu). Tabela działa offline i nie dokłada zapytań. Tabela producentów (kod typu → Boeing, Airbus itd.) już jest, tabela typów z pełnymi nazwami (około 200 pozycji) też, brakuje pokazania nazw w wyszukiwarce i na niezaznaczonych samolotach oraz uzupełnienia tabeli typami z listy brakujących | mały |

## P4 – porządki techniczne (przy okazji)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 10 | Dalsze rozbicie `MapController` (po podziale na pliki partial ma około 2400 linii): gesty i zaznaczanie (największy jest `MapController.Selection.cs`, 710 linii) | Zrobione: podział na pliki partial, TileLayer, PopupRenderer, AltitudeLegendRenderer, StatusIconRenderer, MapMath, TrailTracker i AircraftDataLoop (pętla pobierania, bez zmian zachowania). Ryzykowne, więc osobne zadanie, bez zmian zachowania | średni |

- Tłumaczenie pozostałych komentarzy na angielski (przy okazji edycji plików).
- Testy: indeks lotnisk, wyszukiwanie, formatowanie tekstów kart.
- Zrzuty ekranu do README.

## P5 – pomysły

- Ustawienie jednostki odległości (km, mile morskie, mile) obok jednostki wysokości. Dziś odległości i ustawienia zaokrąglania położenia są w km, a promień w API w NM.

- Zapamiętywanie ostatniej pozycji i zoomu mapy między uruchomieniami.
- Historia obserwowanych samolotów.

## Proponowana kolejność

1 → 2 → 3 (mail, można wysłać od razu, równolegle) → 4 → 6 → 7 → 8 → 9. Pozycje z P4 dochodzą przy okazji edycji danego pliku; 10 jako osobne zadanie, gdy zmiany w mapie przestaną być częste.

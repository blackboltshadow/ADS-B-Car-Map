# Plan rozwoju

Lista funkcji, które zostały do zrobienia, z priorytetami (stan na 2026-11-02). Ukończone pozycje są usuwane (historia jest w commitach i README). Wydaniami zajmuje się autor projektu, ten plik ich nie planuje.

Zasada kolejności: najpierw rzeczy, które pomagają ocenić to, co już dodano (diagnostyka, odporność), potem dostęp do danych i prywatność, a dopiero potem nowe funkcje.

## P1 – diagnostyka i odporność (najpierw)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 1 | Ikona limitu 429 na mapie czerpie stan z `IDataSource` (`RateLimitStatus`) zamiast ze starego licznika `LiveTraffic.ReportRateLimited` (wołanego z `MapController.Data.cs`) | Karta „Stan źródeł” już pokazuje stan z `RateLimitStatus`, więc ikona na mapie i karta mogą się rozjechać (np. gdy limit dotyczy zapasowego źródła) | mały |
| 2 | Sprawdzenie na telefonie nowych funkcji: **zaokrąglanie współrzędnych** (karta Zaawansowane → Prywatność i Dev → Prywatność (zaawansowane); w Dev logu na poziomie „Wszystko” przy włączonym logowaniu współrzędnych widać prawdziwe i wysłane położenie; samoloty przy brzegu ekranu nie znikają przy 5, 10 i 25 km; zmiana opcji działa od następnego odświeżenia); **Dev log, który nie był jeszcze testowany ani uruchamiany** (przełącznik „Pokaż Dev log w menu” i pozycja w menu pod „O aplikacji”; ustawienia poziomu, długości i łączenia wpisów; „Udostępnij” przez `FileProvider`, „Kopiuj” i „Wyczyść”; wpisy przy braku sieci, 429 i błędach; zapis pliku po zamknięciu i awarii aplikacji; czy raport nie zawiera niczego, czego nie chcesz wysyłać); przeciąganie kolejności na obu listach (źródła samolotów i źródła tras) działa płynnie i zapisuje kolejność; na karcie samolotu jest pełna nazwa typu i właściciel; przy wyłączonym zapasie i zmienionym głównym źródle mapa zachowuje się zgodnie z oczekiwaniem; trasa z hexdb.io dostaje współrzędne (pasek postępu) także tuż po starcie; w razie błędu w logu jest pełny stos wyjątku. Po rozbiciu `MapController` (pozycja 10): kafelki, zoom i przesuwanie, popup samolotu i lotniska, legenda wysokości oraz ikona stanu wyglądają i działają jak przed zmianą, na telefonie i w DHU. Z wcześniejszych: kreator drzewa filtrów (wcięcia, menu ⋮, przenoszenie), pobieranie tras w tle dla „Do Rzeszowa”, cache lotów (po restarcie ta sama trasa od razu), lista hostów Android Auto (`allowed_hosts.xml`): czy aplikacja nadal pokazuje się w DHU i w samochodzie przy buildzie Debug i Release, a w logu jest „Host allowlist: … certificate matches the list”; przy innym podpisie hosta dopisać wpis z ostrzeżenia `CarApp.Val` | Testy ręczne, bez kodu, ale od nich zależy, co poprawiać | mały |

## P2 – dostęp do danych i przed jakąkolwiek dystrybucją

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 3 | Dostęp do airplanes.live: napisać na contact@airplanes.live (opis projektu, link do repozytorium, kontakt w User-Agencie) i poprosić o dopuszczenie ruchu aplikacji. Zadanie po stronie autora aplikacji | Dziś serwis odpowiada 403, więc jako główne źródło nie działa, a jako zapasowe tylko pozornie | mały |
| 4 | adsb.fi jako drugie źródło pozycji (`opendata.adsb.fi/api`, 1 zapytanie/s, tylko użytek osobisty): nowy katalog `Sources\AdsbFi` z klientem i testami, wpis w `AppServices`, `DataSourceSetting` i `AdsbCarMap.csproj`. Ścieżkę zapytania punktu trzeba najpierw sprawdzić na żywo | Prawdziwy zapas, gdy airplanes.live jest niedostępne. Dzięki zasadzie niezależnych źródeł nie dotyka reszty kodu | średni |
| 6 | Sprawdzenie warunków użycia adsb.lol, hexdb.io i adsb.fi (oraz airplanes.live po uzgodnieniu) i notatka licencyjna w `DATA_SOURCES.md` przed udostępnieniem aplikacji komukolwiek poza autorem; ewentualnie FlyItalyADSB (klucz poza repozytorium) lub umowa z dostawcą | Wszystkie darmowe serwisy zastrzegają użytek osobisty i niekomercyjny; hexdb.io nie sprawdzono w ogóle | mały |

## P3 – funkcje

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 7 | Alerty „samolot w pobliżu” (wojskowy, typ, linia, promień) | Wyróżnik aplikacji. Wymaga decyzji, jak powiadamiać w Android Auto | średni/duży |
| 8 | Filtry samolotów, część czwarta: wysokość w wybranej jednostce, tabela linii lotniczych (kody IATA i nazwy linii w warunku linii; źródło OpenFlights na licencji ODbL wymaga atrybucji i sprawdzenia licencji przed użyciem), kod ICAO lotniska obok IATA w warunkach lotniska, podpowiedzi przy wpisywaniu, pobieranie tras od samolotów najbliższych środka mapy, parser zapisu tekstowego | Zrobione: tabela typów z kodami IATA i rodzinami, pole „Cała rodzina” w warunku typu, dziennik brakujących typów z ekranem „Dane developerskie” w ustawieniach (sprawdzone na telefonie), model wyrażeń z logiką trójwartościową, tabela producentów, ekran listy filtrów z przełącznikami, kreator z drzewem warunków (grupy ORAZ/LUB bez praktycznego limitu zagnieżdżeń, NIE na każdym węźle, przesuwanie, przenoszenie, kopiowanie, zwijanie), opis i zdanie „Czytaj”, zapis JSON, trzy filtry wbudowane tylko do odczytu, przycisk z licznikiem na mapie. Do sprawdzenia na telefonie: cały kreator drzewa (wcięcia, menu ⋮, przenoszenie) oraz pobieranie tras w tle dla „Do Rzeszowa”. | średni |
| 9 | Wbudowana tabela pełnych nazw typów (np. A21N jako Airbus A321neo) dla samolotów niezaznaczonych i dla wyszukiwarki | Dla zaznaczonego samolotu pełna nazwa typu, producent i właściciel pochodzą już z adsbdb i hexdb (karta samolotu). Tabela działa offline i nie dokłada zapytań. Tabela producentów (kod typu → Boeing, Airbus itd.) już jest, tabela typów z pełnymi nazwami (około 200 pozycji) też, brakuje pokazania nazw w wyszukiwarce i na niezaznaczonych samolotach oraz uzupełnienia tabeli typami z listy brakujących | mały |

## P4 – porządki techniczne (przy okazji)

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 10 | Rozbicie `MapController` (ponad 2800 linii) na rysowanie, pobieranie danych i gesty | Największy dług techniczny, ale ryzykowny, więc osobne zadanie z testami, bez zmian zachowania | duży; w toku: klasa podzielona na pliki partial, z kontrolera wydzielone TileLayer, PopupRenderer, AltitudeLegendRenderer, StatusIconRenderer, MapMath i TrailTracker; zostają pobieranie danych (DataLoop), gesty i zaznaczanie |
| 11 | `.gitattributes` (`* text=auto`) i ujednolicenie końców linii | Przy każdej operacji gita pojawiają się ostrzeżenia LF/CRLF | mały |
| 12 | Plik runsettings do liczenia pokrycia testami w repozytorium i GitHub Actions z buildem i testami (bez podpisu, bo klucz jest poza repozytorium) | Pokrycie liczy się dziś ręcznie; testy powinny biegać przy każdym pushu | mały/średni |

- Tłumaczenie pozostałych komentarzy na angielski (przy okazji edycji plików).
- Testy: indeks lotnisk, wyszukiwanie, formatowanie tekstów kart.
- Zrzuty ekranu do README.

## P5 – pomysły

- Ustawienie jednostki odległości (km, mile morskie, mile) obok jednostki wysokości. Dziś odległości i ustawienia zaokrąglania położenia są w km, a promień w API w NM.

- Zapamiętywanie ostatniej pozycji i zoomu mapy między uruchomieniami.
- Historia obserwowanych samolotów.

## Proponowana kolejność

1 → 2 → 3 (mail, można wysłać od razu, równolegle) → 4 → 6 → 7 → 8 → 9. Pozycje z P4 dochodzą przy okazji edycji danego pliku; 10 jako osobne zadanie, gdy zmiany w mapie przestaną być częste.

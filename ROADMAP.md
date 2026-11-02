# Plan rozwoju

Lista funkcji, które zostały do zrobienia, z priorytetami (stan na 2026-11-02). Ukończone pozycje są usuwane (historia jest w commitach i README). Wydaniami zajmuje się autor projektu, ten plik ich nie planuje.

## P1 – następne w kolejności

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 1 | Filtry samolotów, część czwarta: wysokość w wybranej jednostce, tabela linii lotniczych (kody IATA i nazwy linii w warunku linii; źródło OpenFlights na licencji ODbL wymaga atrybucji i sprawdzenia licencji przed użyciem), kod ICAO lotniska obok IATA w warunkach lotniska, podpowiedzi przy wpisywaniu, pobieranie tras od samolotów najbliższych środka mapy, parser zapisu tekstowego | Zrobione: tabela typów z kodami IATA i rodzinami, pole „Cała rodzina” w warunku typu, dziennik brakujących typów z ekranem „Dane developerskie” w ustawieniach (sprawdzone na telefonie), model wyrażeń z logiką trójwartościową, tabela producentów, ekran listy filtrów z przełącznikami, kreator z drzewem warunków (grupy ORAZ/LUB bez praktycznego limitu zagnieżdżeń, NIE na każdym węźle, przesuwanie, przenoszenie, kopiowanie, zwijanie), opis i zdanie „Czytaj”, zapis JSON, trzy filtry wbudowane tylko do odczytu, przycisk z licznikiem na mapie. Do sprawdzenia na telefonie: cały kreator drzewa (wcięcia, menu ⋮, przenoszenie) oraz pobieranie tras w tle dla „Do Rzeszowa”. | średni |
| 2 | Zapasowe źródło danych przy 429 (airplanes.live, adsb.fi) wraz z wyborem źródła w ustawieniach. Podłączyć DataSourceRegistry i BackoffRateLimiter (już gotowe w Sources) w AppServices i MapController oraz zastąpić LiveTraffic.ReportRateLimited stanem z IDataSource. | Backoff istnieje, ale dane nadal znikają. | średni |
| 3 | Zaokrąglanie współrzędnych wysyłanych do API (razem z pozycją 2) | Prywatność, ten sam klient API. | mały |
| 4 | Alerty „samolot w pobliżu” (wojskowy, typ, linia, promień) | Wyróżnik aplikacji. Wymaga decyzji, jak powiadamiać w Android Auto. | średni/duży |

## P2 – przed jakąkolwiek dystrybucją

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 5 | Zastąpienie AllowAllHostsValidator listą znanych hostów Android Auto | Jedyna realna luka bezpieczeństwa; przy użyciu własnym mało pilna. | mały |
| 6 | Pełne nazwy typów samolotów (np. A21N jako Airbus A321neo) | Tabela wbudowana zamiast adsbdb: działa offline i nie dokłada zapytań. Tabela producentów (kod typu → Boeing, Airbus itd.) już jest, tabela typów z pełnymi nazwami (około 200 pozycji) też, brakuje pokazania nazw na karcie i w wyszukiwarce oraz uzupełnienia tabeli typami z listy brakujących. | mały |

## P3 – porządki i jakość (przy okazji)

- `.gitattributes` (`* text=auto`) i ujednolicenie końców linii.
- Plik runsettings do liczenia pokrycia testami w repozytorium.
- GitHub Actions: build i testy bez podpisu.
- Tłumaczenie pozostałych komentarzy na angielski (przy okazji edycji plików).
- Testy: indeks lotnisk, wyszukiwanie, formatowanie tekstów kart.
- Zrzuty ekranu do README.

## P4 – pomysły

- Własny odbiornik tar1090 jako źródło danych (razem z pozycją 2).
- Zapamiętywanie ostatniej pozycji i zoomu mapy między uruchomieniami.
- Historia obserwowanych samolotów.

## Proponowana kolejność

1 → 2 i 3 razem → 5 → 4 → 6. Pozycje z P3 dochodzą przy okazji edycji danego pliku.

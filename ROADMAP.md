# Plan rozwoju

Lista planowanych funkcji z priorytetami (stan na 2026-11-02). Wydaniami zajmuje się autor projektu, ten plik ich nie planuje.

## P1 – najbliższa praca

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 1 | Cache kafelków mapy na dysku (zrobione) | Mniej transferu i szybszy start, szczególnie w samochodzie przy słabym zasięgu. | średni |

## P2 – następne w kolejności

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 2 | Filtry samolotów: zakres wysokości, rodzaj, linia lotnicza, ukrycie samolotów na ziemi | Przy gęstym ruchu mapa jest nieczytelna. Część infrastruktury już istnieje (klasyfikator rodzajów, izolacja). | średni |
| 3 | Zapasowe źródło danych przy 429 (airplanes.live, adsb.fi) wraz z wyborem źródła w ustawieniach | Backoff istnieje, ale dane nadal znikają. | średni |
| 4 | Zaokrąglanie współrzędnych wysyłanych do API (razem z pozycją 3) | Prywatność, ten sam klient API. | mały |
| 5 | Alerty „samolot w pobliżu” (wojskowy, typ, linia, promień) | Wyróżnik aplikacji. Wymaga decyzji, jak powiadamiać w Android Auto. | średni/duży |

## P3 – przed jakąkolwiek dystrybucją

| # | Funkcja | Uzasadnienie | Koszt |
|---|---|---|---|
| 6 | Zastąpienie AllowAllHostsValidator listą znanych hostów Android Auto | Jedyna realna luka bezpieczeństwa; przy użyciu własnym mało pilna. | mały |
| 7 | Pełne nazwy typów samolotów (np. A21N jako Airbus A321neo) | Tabela wbudowana zamiast adsbdb: działa offline i nie dokłada zapytań. | średni |

## P4 – porządki i jakość (przy okazji)

- `.gitattributes` (`* text=auto`) i ujednolicenie końców linii.
- Plik runsettings do liczenia pokrycia testami w repozytorium.
- GitHub Actions: build i testy bez podpisu.
- Tłumaczenie pozostałych komentarzy na angielski (przy okazji edycji plików).
- Testy: indeks lotnisk, wyszukiwanie, formatowanie tekstów kart.
- Zrzuty ekranu do README.

## P5 – pomysły

- Własny odbiornik tar1090 jako źródło danych (razem z pozycją 3).
- Zapamiętywanie ostatniej pozycji i zoomu mapy między uruchomieniami.
- Historia obserwowanych samolotów.

## Proponowana kolejność

1 → 2 → 3 i 4 razem → 6 → 5 → 7. Pozycje z P4 dochodzą przy okazji edycji danego pliku.

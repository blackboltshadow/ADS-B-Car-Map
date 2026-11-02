# Źródła danych: przemyślenia i decyzje

Stan na 2026-11-02. Notatka zbiera, jakie źródła danych istnieją, które wybraliśmy i dlaczego, oraz jak jest zbudowana obsługa źródeł w aplikacji. Źródłem informacji o cudzych serwisach jest głównie [porównanie API ADS-B](https://flyitalyadsb.com/en/dati/confronto/) (strona jednego z porównywanych dostawców, aktualizacja 28.10.2026) i [archiwum API ADSB One](https://github.com/airplanes-live/api-archive). Limity i warunki trzeba sprawdzić u dostawcy przed każdą dystrybucją aplikacji.

## Założenia

- Aplikacja ma być szeroko dostępna. Jedyne urządzenia to telefon i samochód (Android Auto), więc **nie planujemy integracji z własnym sprzętem**, w tym z odbiornikiem tar1090. Wpis o tar1090 usunięto z planów.
- Użytek jest na razie własny. Wszystkie darmowe serwisy poniżej zastrzegają użytek osobisty i niekomercyjny, co przy szerszej dystrybucji trzeba ponownie ocenić.
- Prywatność: do serwera samolotów trafia środek widocznej mapy i promień, więc każde dodatkowe źródło pytane po kolei to kolejny serwis, który widzi to samo.

## Przegląd dostępnych źródeł pozycji samolotów

| Źródło | Klucz | Limit | Warunki | Format | Decyzja |
|---|---|---|---|---|---|
| adsb.lol | brak | nie sprawdzono, backoff po 429 | otwarte dane | ADS-B Exchange v2 | **jest** (domyślne) |
| airplanes.live (`api.airplanes.live/v2`) | nie podano | nie podano, promień do 250 NM | użytek osobisty niekomercyjny lub wewnętrzny firmowy | ADS-B Exchange v2 | **dodane** |
| adsb.fi (`opendata.adsb.fi/api`) | brak | 1 zapytanie/s, promień do 250 NM | tylko osobisty niekomercyjny | ADS-B Exchange v2 | następne w kolejce |
| ADSB One | brak | 1 zapytanie/s | nie podano | ADS-B Exchange v2 | pomijamy: repozytorium zarchiwizowano 29.04.2026, usługa przeszła pod airplanes.live |
| FlyItalyADSB | darmowy klucz | 30 zapytań/min, 300 tys./miesiąc, 500 km | użytek komercyjny dozwolony, atrybucja CC BY-SA 4.0 | ADS-B Exchange v2 | opcjonalnie, dopiero przy dystrybucji komercyjnej (klucz trzeba trzymać poza repozytorium) |
| OpenSky Network | konto OAuth2 | kredyty dzienne: 400 anonimowo, 4000 z kontem | pisemna licencja do zastosowań komercyjnych | własny | odrzucone: niski limit, inny format, nie potwierdzono typu i rejestracji w odpowiedzi |
| ADS-B Exchange (RapidAPI) | klucz, płatny | 10 tys. zapytań/miesiąc za 10 USD | niekomercyjny | v2 | odrzucone: płatne i mały limit |
| Własny odbiornik tar1090 | brak | brak | własny sprzęt | readsb JSON | odrzucone: aplikacja nie zakłada własnego sprzętu |

Trasy lotów: tylko adsbdb.com. Alternatyw nie badano.

## Architektura i decyzje

Warstwy i zależności (tylko w jedną stronę):

```
Core (AdsbCarMap.DataSources)             tylko kontrakt, bez logiki; nie zna nikogo
   ▲                 ▲
Źródła               Zarządca (Management)
(AdsbLol, AirplanesLive, Adsbdb)          źródła i zarządca znają tylko Core, nie znają siebie
   ▲                 ▲
   └──────── Aplikacja (AppServices składa całość) ────────┘
```

1. **Źródło jest w pełni niezależne.** Każde źródło ma własny model rekordu API, własny parser i własne mapowanie na modele Core. Nie wie nic o innych źródłach ani o zarządcy. Źródło tylko pobiera dane: przy 429 zgłasza `RateLimitedException` z wartością `Retry-After`, ale niczego nie wstrzymuje. Kosztem jest powielony parser w adsb.lol i airplanes.live (oba w formacie ADS-B Exchange v2). Wybraliśmy to świadomie, żeby zmiana jednego serwisu nie dotykała drugiego i żeby dodanie źródła nie wymagało wspólnej biblioteki.
2. **Core to wyłącznie kontrakt.** Interfejsy `IDataSource`, `IAircraftSource`, `IRouteSource` i modele `Aircraft`, `FlightRoute`, `RateLimitedException`. Źródła „dopasowują się do zarządcy”, implementując te interfejsy.
3. **Cała logika jest w zarządcy** (`AdsbCarMap.DataSources.Management`): backoff po 429 (`BackoffRateLimiter`, dekorator `RateLimitedAircraftSource`), stan limitów (`RateLimitStatus`), rejestr (`DataSourceRegistry`) i zapas (`FallbackAircraftSource`). Zarządca zna tylko Core.
4. **Kolejność źródeł wybiera użytkownik** (Ustawienia → Zaawansowane → Źródło danych): na ekranie to lista przeciągana za uchwyt ≡ (wiersz na górze to główne źródło, pod nim zapasowe w kolejności zapytań), zapisana jako indeks na liście wszystkich kolejności (dla dwóch źródeł dwie, dla trzech sześć), więc ustawienie przechodzi przez zwykły zapis, historię i przywracanie domyślnych. Na liście są tylko źródła pozycji samolotów. adsbdb (trasy) ma osobną kartę „Źródła tras” z jednym wpisem, bo nie dostarcza pozycji i nie może zastępować pozostałych; własną kolejność dostanie z drugim źródłem tras. Zmiana działa od następnego odświeżenia, bez restartu.
5. **Zapas.** Gdy źródło domyślne nie zwraca danych, aplikacja pyta kolejne. „Brak danych” znaczy tu awarię: odpowiedź 429, błąd sieci lub serwera albo błędny JSON. **Pustej listy samolotów nie traktujemy jako braku danych**, bo nad morzem lub w nocy jest normalna, a pytanie drugiego serwisu za każdym razem podwoiłoby ruch. Zapas można wyłączyć w ustawieniach (wtedy pytane jest tylko pierwsze źródło).
6. **Źródło wstrzymane po 429 jest pomijane** bez zapytania, więc przez czas wstrzymania aplikacja pyta od razu drugi serwis. Gdy wszystkie źródła są wstrzymane, błąd 429 zawiera najkrótszy czas oczekiwania, więc ikona limitu i odstęp ponowienia w `MapController` działają jak wcześniej.

## Struktura w kodzie

```
Sources\
  AdsbCarMap.DataSources\                 Core: Interfaces\, Models\
  AdsbCarMap.DataSources.Management\      RateLimiting\, Selection\
  AdsbLol\       AdsbCarMap.DataSources.AdsbLol        + .Tests
  AirplanesLive\ AdsbCarMap.DataSources.AirplanesLive  + .Tests
  Adsbdb\        AdsbCarMap.DataSources.Adsbdb         + .Tests   (trasy lotów)
Tests\
  AdsbCarMap.Tests\                       testy aplikacji
  AdsbCarMap.DataSources.Management.Tests\ testy limitera, dekoratora, rejestru i zapasu
```

Aplikacja zna tylko interfejsy; konkretne klienty składa `AppServices`. Nowe źródło: katalog `Sources\{Nazwa}` z projektem `AdsbCarMap.DataSources.{Nazwa}` (własny model, własny parser, referencja tylko do Core) i projektem `.Tests`, wpis w `AppServices`, w `AdsbCarMap.csproj` oraz nazwa w opcjach `DataSourceSetting` (musi być taka sama jak `Name` źródła).

## Czego jeszcze brakuje

- adsb.fi (ścieżka punktu do sprawdzenia na żywym zapytaniu, limit 1 zapytanie/s).
- Pokazanie użytkownikowi, które źródło aktualnie odpowiada (`LastUsedSourceName` jest gotowe, brakuje miejsca w UI).
- Ikona limitu 429 nadal opiera się na `LiveTraffic.ReportRateLimited`, a nie na stanie z `IDataSource`.
- Zapasowe źródło tras: adsbdb nie obsługuje dziś 429 (`AdsbdbRouteClient` zawsze zgłasza brak limitu).
- Zaokrąglanie współrzędnych wysyłanych do API (prywatność).
- Licencje: przed udostępnieniem aplikacji poza własny użytek ponownie sprawdzić warunki airplanes.live, adsb.lol i adsb.fi; ewentualnie przejść na FlyItalyADSB lub umowę z dostawcą.
- Nie potwierdzono limitów zapytań airplanes.live ani adsb.lol. Strona porównawcza podaje je jako „nie podano”.

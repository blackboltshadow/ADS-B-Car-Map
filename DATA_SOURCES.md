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
| airplanes.live (`api.airplanes.live/v2`) | brak klucza, ale **zapytania bez uzgodnienia są odrzucane (403)** | nie podano, promień do 250 NM | użytek osobisty niekomercyjny lub wewnętrzny firmowy; dostęp wymaga kontaktu e-mailowego | ADS-B Exchange v2 | **dodane w kodzie, ale nieużyteczne do czasu uzyskania zgody** (patrz niżej) |
| adsb.fi (`opendata.adsb.fi/api`) | brak | 1 zapytanie/s, promień do 250 NM | tylko osobisty niekomercyjny | ADS-B Exchange v2 | następne w kolejce |
| ADSB One | brak | 1 zapytanie/s | nie podano | ADS-B Exchange v2 | pomijamy: repozytorium zarchiwizowano 29.04.2026, usługa przeszła pod airplanes.live |
| FlyItalyADSB | darmowy klucz | 30 zapytań/min, 300 tys./miesiąc, 500 km | użytek komercyjny dozwolony, atrybucja CC BY-SA 4.0 | ADS-B Exchange v2 | opcjonalnie, dopiero przy dystrybucji komercyjnej (klucz trzeba trzymać poza repozytorium) |
| OpenSky Network | konto OAuth2 | kredyty dzienne: 400 anonimowo, 4000 z kontem | pisemna licencja do zastosowań komercyjnych | własny | odrzucone: niski limit, inny format, nie potwierdzono typu i rejestracji w odpowiedzi |
| ADS-B Exchange (RapidAPI) | klucz, płatny | 10 tys. zapytań/miesiąc za 10 USD | niekomercyjny | v2 | odrzucone: płatne i mały limit |
| Własny odbiornik tar1090 | brak | brak | własny sprzęt | readsb JSON | odrzucone: aplikacja nie zakłada własnego sprzętu |

### airplanes.live odpowiada 403 (stan na 2026-11-02)

Sprawdzone zapytaniem `GET https://api.airplanes.live/v2/point/52.2/21.0/50`, także z nagłówkiem User-Agent aplikacji: serwis (za Cloudflare) odpowiada **403 Forbidden** z treścią „Please contact us at contact@airplanes.live. Your email MUST include any links, a description of the project, and any information you deem appropriate.” Dla porównania adsb.lol odpowiada 200. Wcześniejsze założenie z tej notatki („klucz: nie podano”, czyli swobodny dostęp) było błędne. Skutki:

- Jako **główne źródło** airplanes.live nie działa, a przy wyłączonym zapasowym źródle mapa pozostaje pusta (w logu: „Pobieranie samolotów nie powiodło się: … 403 (Forbidden)”).
- Jako **zapasowe źródło** nic nie psuje (adsb.lol jest pytany pierwszy), ale też niczego nie ratuje, bo przy awarii adsb.lol drugie źródło zwróci 403.
- Do zrobienia po stronie autora aplikacji: napisać na contact@airplanes.live z opisem projektu i linkiem do repozytorium, i zapytać o dopuszczenie ruchu aplikacji (z kontaktem w User-Agencie). Do tego czasu zostawić adsb.lol jako główne źródło (jest domyślne).

## Warunki użycia (sprawdzone 2026-11-03)

Sprawdzono strony dostawców (nie umowy ani korespondencję). Wynik jest jednoznaczny tylko dla adsb.fi; adsb.lol i hexdb.io nie publikują warunków użycia danych.

| Serwis | Co jest opublikowane | Wniosek |
|---|---|---|
| adsb.lol | Repozytorium API ma licencję BSD-3-Clause, ale dotyczy **kodu**, nie danych. Strony `api.adsb.lol/docs` nie dało się odczytać (treść generowana w przeglądarce). README: „Rate limits are dynamic based on the environment load”, „If you get 4xx errors, you are doing something wrong” oraz zapowiedź: „In the future, you will require an API key which you can obtain by feeding adsb.lol”. Nie znaleziono licencji danych, zasad użytku komercyjnego ani wymogu atrybucji. | Brak podstawy do użytku komercyjnego i brak zakazu. **Ryzyko: wymóg klucza dla karmiących w przyszłości**, co dla aplikacji bez własnego odbiornika oznaczałoby utratę domyślnego źródła. Nie sprawdzono stron `/sponsor` (plany API) i `/about`. |
| hexdb.io | Brak strony warunków, polityki prywatności i licencji (`/about`, `/terms`, `/privacy` zwracają 404). Strona główna: „Free · No API key”, bez limitów. Dane pochodzą z PlaneBase/PlanePlotter, Jima Masona, Steve'a Hibberda, ip2location i Airport-Data, a licencji tych źródeł nie sprawdzono. Kontakt: adres e-mail na stronie głównej. | Warunki nie istnieją, więc nie wiadomo, czy użytek poza własnym jest dozwolony. Licencje danych źródłowych mogą być bardziej restrykcyjne niż sam serwis. Przed dystrybucją napisać do autora. |
| adsb.fi | README repozytorium `adsbfi/opendata`: „for personal, non-commercial use only. You may not license, sell, rent, or lease any part of the data or the service.” Wymaga atrybucji: „You must cite adsb.fi and include a link to our home page.” Limit: 1 zapytanie/s na publiczne endpointy; odpowiedzi 400, 401, 403, 404 i 429 wliczają się do limitu, a nadmiar błędnych zapytań daje tymczasową blokadę IP. Brak licencji danych (ODbL, CC). „Please contact us if you have commercial or higher request rate requirements.” | Użytek osobisty niekomercyjny, atrybucja obowiązkowa. Dla `AboutActivity` dodać nazwę i link do adsb.fi razem z dodaniem źródła. |
| airplanes.live | Patrz niżej: użytek osobisty niekomercyjny, dostęp po uzgodnieniu e-mailowym. | Czeka na odpowiedź autora aplikacji (e-mail do contact@airplanes.live jeszcze niewysłany). |

Wnioski:

- Do dystrybucji komercyjnej nie nadaje się żaden z darmowych serwisów bez osobnej zgody. FlyItalyADSB (darmowy klucz, użytek komercyjny, CC BY-SA 4.0) jest jedynym źródłem z jawnie dozwolonym użytkiem komercyjnym.
- Przy dystrybucji niekomercyjnej (np. publiczne repozytorium i sideload) obowiązuje atrybucja z adsb.fi; dla pozostałych nie ma jawnego wymogu, ale ekran „O aplikacji” już je wymienia.
- Zapytania aplikacji idą z telefonów użytkowników, a nie z jednego serwera, więc limity (adsb.fi: 1 zapytanie/s) dotyczą każdego użytkownika osobno; tym ważniejsze jest, że `BackoffRateLimiter` nie ponawia zapytań po 4xx.
- **Nie sprawdzono**: `adsb.lol/sponsor`, `adsb.lol/about`, strony `adsb.fi` (403 dla automatycznego pobrania), licencji źródeł danych hexdb.io, treści `api.adsb.lol/docs`. Do ręcznego przejrzenia w przeglądarce.
- Ścieżka punktu w adsb.fi to `/v3/lat/{lat}/lon/{lon}/dist/{nm}` (do 250 NM) w bazie `https://opendata.adsb.fi/api/`; wariant `/v2/lat/.../lon/.../dist/...` jest oznaczony jako przestarzały i zwraca inny format niż pozostałe punkty v2. Przy dodawaniu źródła użyć v3 i sprawdzić format na żywym zapytaniu.

## Źródła tras lotów i danych samolotu (sprawdzone 2026-11-02 żywymi zapytaniami)

| Źródło | Trasa po znaku wywoławczym | Dane samolotu po kodzie ICAO | Uwagi |
|---|---|---|---|
| **adsbdb.com** | tak: linia (nazwa i kody), lotniska z nazwą, miastem i współrzędnymi | tak: pełna nazwa typu, kod typu, producent, rejestracja, właściciel, zdjęcie | 404 dla nieznanych; domyślnie pierwsze |
| **adsb.lol** `/api/0/route/{znak}` | tak: lotniska z nazwą, kodami ICAO i IATA, miastem, współrzędnymi; z linii tylko kod (np. LOT) | (przez `/v2/hex` w źródle pozycji) | zapytanie jest przekierowywane (302) do statycznego pliku danych (VRS standing data, aktualizowany co godzinę wg opisu repozytorium adsb.lol); bez klucza; 404 dla nieznanych; tylko GET |
| **hexdb.io** | tylko para lotnisk ICAO, np. `EPWA-EGLL`; w odpowiedzi znacznik czasu z 2012 roku, więc dane mogą być stare | tak: nazwa typu, producent, rejestracja, właściciel, kod operatora; bez zdjęcia | 404 dla nieznanych; brak opublikowanych warunków użycia (patrz „Warunki użycia”); aplikacja uzupełnia trasę kodami IATA, miastami i współrzędnymi z własnej bazy lotnisk |
| OpenSky, Aviationstack, AirLabs, FlightAware | nie | nie | odrzucone: OpenSky pyta po przedziale czasu i samolocie, nie po znaku wywoławczym (konto OAuth2, 400 do 4000 kredytów dziennie); Aviationstack ma około 100 zapytań miesięcznie i tylko HTTP; AirLabs bez potwierdzonych limitów; FlightAware płatne |

Trasy są przypisane do znaku wywoławczego (dane standardowe), a nie do bieżącego przelotu, więc żadne z tych źródeł nie zastępuje danych na żywo.

## Architektura i decyzje

Warstwy i zależności (tylko w jedną stronę):

```
Core (AdsbCarMap.DataSources)             tylko kontrakt, bez logiki; nie zna nikogo
   ▲                 ▲
Źródła               Zarządca (Management)
(AdsbLol, AirplanesLive, Adsbdb, Hexdb)  źródła i zarządca znają tylko Core, nie znają siebie
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
  AdsbCarMap.DataSources.Management\      RateLimiting\, Selection\, Privacy\
  AdsbLol\       AdsbCarMap.DataSources.AdsbLol        + .Tests
  AirplanesLive\ AdsbCarMap.DataSources.AirplanesLive  + .Tests
  Adsbdb\        AdsbCarMap.DataSources.Adsbdb         + .Tests   (trasy lotów i dane samolotu)
  Hexdb\         AdsbCarMap.DataSources.Hexdb          + .Tests   (trasy lotów i dane samolotu)
Tests\
  AdsbCarMap.Tests\                       testy aplikacji
  AdsbCarMap.DataSources.Management.Tests\ testy limitera, dekoratora, rejestru i zapasu
```

Aplikacja zna tylko interfejsy; konkretne klienty składa `AppServices`. Nowe źródło: katalog `Sources\{Nazwa}` z projektem `AdsbCarMap.DataSources.{Nazwa}` (własny model, własny parser, referencja tylko do Core) i projektem `.Tests`, wpis w `AppServices`, w `AdsbCarMap.csproj` oraz nazwa w opcjach `DataSourceSetting` (musi być taka sama jak `Name` źródła).

## Prywatność położenia

Zapytanie o samoloty niesie środek widocznej mapy (przy śledzeniu GPS: położenie użytkownika) i promień. `PrivacyAircraftSource` (`Privacy\` w zarządcy) opakowuje `FallbackAircraftSource`, więc każde źródło, także dodane w przyszłości, dostaje już zaokrąglone zapytanie i nic o tym nie wie. Środek jest przyciągany do siatki o rozmiarze w km (deterministycznie, bez szumu, którego dałoby się pozbyć uśrednianiem), a promień rośnie o połowę przekątnej komórki z zapasem 10%. Przesunięcie jest stałe, nie wyliczane z faktycznej odległości, bo inaczej promień zdradzałby położenie w komórce. Opcje czyta się przy każdym zapytaniu (`Func<PrivacyOptions>`). Lokalnie (odległości, wyszukiwanie, `LiveTraffic`) używane jest prawdziwe położenie. Zapytania po kodzie ICAO i znaku wywoławczym nie mają współrzędnych.

## Pamięć informacji o lotach (cache)

Pozycje samolotów zmieniają się co kilka sekund, więc ich nie zapamiętujemy. Zapamiętujemy za to informacje o **pojedynczym locie lub samolocie**, żeby nie pytać serwisów dwa razy o to samo: trasę po znaku wywoławczym (adsbdb) i typ, rejestrację oraz kategorię po kodzie ICAO (uzupełnianie z innych źródeł). Służy do tego `TimedCache<T>` w zarządcy.

- **Czas pamiętania lotu po ostatnim zobaczeniu** wybiera użytkownik (Zaawansowane → Pamięć podręczna lotów, domyślnie 10 minut, 1–30). Czas jest liczony od chwili, gdy samolot ostatnio był w danych z mapy, a nie od zapisania wpisu. Dzięki temu lot, który jest widoczny, zostaje w pamięci tak długo, jak leci (trasa nie jest pobierana co N minut), a lot, który zniknął, jest zapominany po N minutach. Czas jest czytany przy każdym użyciu, więc zmiana działa od razu także na wpisy już zapisane.
- **Zamykanie lotów, które wylądowały.** `FlightCacheKeeper` po każdym pobraniu pozycji odświeża wpisy widocznych lotów, a lot, który był widziany w powietrzu i jest teraz na ziemi, zamyka na ok. 2 minuty (trasa i dane samolotu są wtedy zapominane szybciej niż po pełnym czasie). Samolot, który stoi na ziemi i nigdy nie był widziany w powietrzu (np. przed odlotem), nie jest uznawany za wylądowany. Brak samolotu w danych nie oznacza lądowania (mógł wyjść poza widoczny obszar), dlatego po powrocie po dłuższej przerwie aplikacja pyta o trasę ponownie.
- **Odpowiedź „nieznany”** (serwer nie zna lotu) też jest zapamiętywana, żeby nie pytać w kółko, ale krócej: trasy do 6 godzin, dane samolotu do 10 minut, i nigdy dłużej niż wybrany czas.
- **Zapis na dysk** w katalogu pamięci podręcznej aplikacji (`flights\routes.json`, `flights\aircraft-details.json`), z opóźnieniem 5 sekund, żeby seria zmian dawała jeden zapis. Po uruchomieniu wczytywane są tylko wpisy, które jeszcze nie wygasły. Uszkodzony plik jest ignorowany. Android może opróżnić katalog pamięci podręcznej, wtedy dane są pobierane od nowa.
- **Limit 5000 wpisów** na pamięć; po przekroczeniu usuwane są najstarsze.
- Przycisk „Wyczyść teraz” zapomina wszystko. Przycisk odświeżenia trasy na karcie samolotu nadal wymusza pobranie jednej trasy.
- Ograniczenie: gdy karta nadal pokazuje samolot, który wylądował i został zamknięty, aplikacja po wygaśnięciu wpisu zapyta o jego trasę jeszcze raz, i potem co N minut, dopóki karta jest otwarta.

## Przyloty i odloty lotniska (rozpoznanie z 2026-11-03, na przyszłość)

Obecne źródła (adsb.lol, airplanes.live, adsbdb, hexdb) nie dają listy lotów lotniska: pozycje są chwilowe, trasy odpowiadają tylko na pytanie „jaka trasa dla tego znaku wywoławczego”. Dane z wyszukiwania w sieci, warunki trzeba sprawdzić na oficjalnych stronach przed użyciem.

**Wariant A: lista „na żywo” z istniejących danych (zalecany na początek, bez kluczy).** Samoloty w promieniu wokół lotniska (`AppServices.Adsb`), trasa każdego z `AppServices.Routes` (`RouteCache`); przylot = lotnisko jest celem, odlot = jest początkiem. Ograniczenia: tylko loty w zasięgu odbiorników (bez planowanych i niewystartowanych), jedno zapytanie o trasę na znak wywoławczy (limity i 429), trasy bywają błędne lub puste (general aviation, wojskowe). Miejsce w UI: karta lotniska na telefonie, w samochodzie prosty popup z najbliższymi przylotami.

**Wariant B: prawdziwy rozkład z zewnętrznego API (opcjonalne źródło za interfejsem w `Sources/`).** Każde wymaga klucza API użytkownika. Repozytorium jest publiczne, więc klucz tylko jako ustawienie poza kodem (jak kontakt User-Agent). Aplikacja ma działać bez niego.

| Źródło | Koszt | Limity | Uwagi |
|---|---|---|---|
| OpenSky `/flights/arrival`, `/flights/departure` | darmowe | anonimowo 400 kredytów/dzień, konto 4000/dzień, licencjonowane 14 400 odnawianych co godzinę; osobna pula dla `/flights/*` | tylko loty już zakończone (bez rozkładu), OAuth2, użycie komercyjne wymaga pisemnej licencji, anonimowa historia może dawać 403. Odpada |
| AeroDataBox (RapidAPI / API.Market) | plan Basic 0 USD: 400 jednostek i 1600 zapytań miesięcznie (starsze źródła 600/2400); płatne od ok. 7,50 USD (inne źródła 5,35) | twarde limity miesięczne; endpointy kosztują 1, 2 lub 6 jednostek | rozkład lotniska (FIDS), najlepiej pasuje. Darmowy plan: atrybucja, bez użytku komercyjnego, zakaz przekazywania danych dalej. Nieustalone: kategoria kosztu FIDS |
| AviationStack | darmowy 100 zapytań/mies. (starsze źródła 500); 49,99 USD za 10 000; 499,99 USD za 250 000 | darmowy: 1 zapytanie/60 s, maks. 100 wyników; `/timetable` i `/flightsfuture` jak wyżej | loty przyszłe, historia i trasy tylko w płatnych. Darmowy za mały. Brak oficjalnego cennika w wynikach |
| FlightAware AeroAPI | Personal: kredyt ok. 5 USD/mies. (feederzy ADS-B 10–20 USD), od ok. 0,002 USD za zapytanie; Standard od ok. 100 USD/mies. | Personal: 10 zestawów wyników/min | nie potwierdzono, czy Personal obsługuje endpointy lotniskowe (zgłoszono 401 na części endpointów); komercyjnie tylko Standard |

**Do sprawdzenia przed decyzją:** kategoria kosztu FIDS w AeroDataBox ([aerodatabox.com/pricing](https://aerodatabox.com/pricing)); czy FlightAware Personal daje dostęp do endpointów lotniskowych (potrzebny testowy klucz); aktualny limit darmowego AviationStack.

**Decyzja robocza:** najpierw wariant A, potem ewentualnie AeroDataBox jako opcjonalne źródło rozkładu.

## Czego jeszcze brakuje

- adsb.fi (ścieżka punktu do sprawdzenia na żywym zapytaniu, limit 1 zapytanie/s).
- Pokazanie użytkownikowi, które źródło aktualnie odpowiada (`LastUsedSourceName` jest gotowe, brakuje miejsca w UI).
- Pokazanie użytkownikowi, które źródło tras odpowiedziało.
- Licencje: warunki adsb.lol, hexdb.io i adsb.fi sprawdzone 2026-11-03 (sekcja „Warunki użycia”); zostaje airplanes.live po uzgodnieniu, ręczne przejrzenie stron niedostępnych automatycznie oraz decyzja przed udostępnieniem poza własny użytek (napisać do hexdb.io, ewentualnie przejść na FlyItalyADSB lub umowę z dostawcą).
- Nie potwierdzono limitów zapytań airplanes.live ani adsb.lol. Strona porównawcza podaje je jako „nie podano”.

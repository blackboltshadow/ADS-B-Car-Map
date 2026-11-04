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
| adsb.fi (`opendata.adsb.fi/api/v3`) | brak | 1 zapytanie/s, promień do 250 NM | tylko osobisty niekomercyjny, wymaga atrybucji | ADS-B Exchange v2 | **jest** (trzecie w domyślnej kolejności; pole `desc` daje pełną nazwę typu) |
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
- Ścieżka punktu w adsb.fi to `/v3/lat/{lat}/lon/{lon}/dist/{nm}` (do 250 NM) w bazie `https://opendata.adsb.fi/api/`; wariant `/v2/lat/.../lon/.../dist/...` jest oznaczony jako przestarzały i zwraca inny format niż pozostałe punkty v2. Sprawdzone na żywo 2026-11-03: `v3/lat/…` zwraca listę w kluczu `ac` (jak adsb.lol), a `v3/hex/…` odpowiada 400, więc zapytanie po hex idzie przez `v2/hex/{hex}` (też z kluczem `ac`). `AdsbFiClient` używa tych dwóch ścieżek.

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
(AdsbLol, AirplanesLive, AdsbFi, Adsbdb, Hexdb)  źródła i zarządca znają tylko Core, nie znają siebie
   ▲                 ▲
   └──────── Aplikacja (AppServices składa całość) ────────┘
```

1. **Źródło jest w pełni niezależne.** Każde źródło ma własny model rekordu API, własny parser i własne mapowanie na modele Core. Nie wie nic o innych źródłach ani o zarządcy. Źródło tylko pobiera dane: przy 429 zgłasza `RateLimitedException` z wartością `Retry-After`, ale niczego nie wstrzymuje. Kosztem jest powielony parser w adsb.lol, airplanes.live i adsb.fi (wszystkie w formacie ADS-B Exchange v2). Wybraliśmy to świadomie, żeby zmiana jednego serwisu nie dotykała drugiego i żeby dodanie źródła nie wymagało wspólnej biblioteki.
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
  AdsbFi\        AdsbCarMap.DataSources.AdsbFi         + .Tests
  Adsbdb\        AdsbCarMap.DataSources.Adsbdb         + .Tests   (trasy lotów i dane samolotu)
  Hexdb\         AdsbCarMap.DataSources.Hexdb          + .Tests   (trasy lotów i dane samolotu)
  Neptun\        AdsbCarMap.DataSources.Neptun         + .Tests   (zagrożenia powietrzne: IThreatFeed, WebSocket i REST)
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

Obecne źródła (adsb.lol, airplanes.live, adsb.fi, adsbdb, hexdb) nie dają listy lotów lotniska: pozycje są chwilowe, trasy odpowiadają tylko na pytanie „jaka trasa dla tego znaku wywoławczego”. Dane z wyszukiwania w sieci, warunki trzeba sprawdzić na oficjalnych stronach przed użyciem.

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

## Zagrożenia powietrzne: NEPTUN (rozpoznanie z 2026-11-03)

Serwis [neptun.in.ua](https://neptun.in.ua/developers) udostępnia otwarte API bez klucza o zagrożeniach powietrznych nad Ukrainą (drony, rakiety, KAB). To nie jest źródło ADS-B: dane pochodzą z „otwartych i zewnętrznych źródeł”, m.in. kanałów Telegram, więc są zgłoszeniami, nie pomiarami.

| Co | Adres | Uwagi |
|---|---|---|
| Lista zagrożeń | `GET https://neptun.in.ua/api/v1/threats` | JSON `{epoch, seq, serverTime, threats[]}`; nagłówek `cache-control: public, max-age=5`, CORS `*`; prośba serwisu: nie częściej niż co 5 s |
| Alarmy rejonów i obwodów | `GET /api/v1/alerts` | klucze pasują do `raions.geojson` i `oblasts.geojson`; poziomy `yellow` i `red`; aplikacja tego **nie używa** |
| Wiadomości z Telegrama | `GET /api/v1/messages` | aplikacja tego nie używa |
| Strumień | `wss://neptun.in.ua/api/v1/stream` | ramki `{type, ts, data}`: `snapshot` (cała lista, ok. 9 KB przy 15 zagrożeniach), `upsert`, `remove`, `heartbeat` (co 15 s), `alerts` (zaraz po snapshocie) |

Sprawdzone żywym połączeniem (2026-11-03): `ClientWebSocket` łączy się bez nagłówka `Origin` i bez klucza; snapshot przychodzi natychmiast, potem `alerts`, sygnał życia co 15 s, zmiany w sekundach. Pola poza dokumentacją: typ `fpv`, flagi `sea`, `seaLoiter`, `destination`, `presumptiveCourse` (przy zakładanym kursie `positionQuality` to `approx`, `lifecycle` to `uncertain`), `lifecycle`, `displayConfidence`, `trail` (lista punktów `lat`, `lon`, `t`), `epoch` i `seq`. Dokumentacja podaje też flagi `advisory` (obserwacja, nie sygnał do schronienia) i `areaOnly` (znany tylko obwód; `lat` i `lon` to jego środek i nie wolno rysować go jako miejsca ani liczyć z niego ruchu); w próbce z żywego serwera ich nie było, ale parser je obsługuje.

**Warunki użycia** (`neptun.in.ua/api-terms`, 2026-11-03): użycie komercyjne i niekomercyjne dozwolone; **obowiązkowe widoczne łącze do `https://neptun.in.ua/`** obok mapy lub danych (przykład: „Dane: Karta powietrznych alarmów — NEPTUN”), zakaz ukrywania go i przedstawiania danych jako własnych; tylko odczyt (GET i subskrypcja); uczciwe obciążenie (REST nie częściej niż co 5 s, serwis może blokować nadmiarowe źródła); serwis jest agregatorem, a aplikacje dotykające bezpieczeństwa ludzi muszą jasno informować, że dane mogą być nieścisłe lub spóźnione, i odsyłać do oficjalnych sygnałów alarmowych; API „jak jest”, bez gwarancji dostępności ani dokładności, może się zmienić lub zniknąć w każdej chwili; nazwa i logo nie są objęte licencją (aplikacja używa tylko tekstu i linku).

Decyzje:

- **Osobny rodzaj źródła, nie `Aircraft`.** Zagrożenia nie mają kodu ICAO, rejestracji ani wysokości i nie mogą trafić do alertów, wyszukiwarki, filtrów ani śladów samolotów. Core dostał model `Threat` i interfejs `IThreatFeed`, bo źródło nie jest pytane o obszar: samo utrzymuje listę (inaczej niż `IAircraftSource`). Zarządca (limity, zapas, prywatność współrzędnych) go nie dotyczy: żadna pozycja nie jest wysyłana.
- **WebSocket jako domyślny, pobieranie jako zapas.** Jedno połączenie zamiast żądania co 5 s, zmiany bez opóźnienia. Przy zerwaniu `NeptunThreatFeed` czeka wykładniczo (1, 2, 4… do 60 s), a w trybie z zapasem w tym czasie pobiera listę. Brak jakiejkolwiek ramki przez 45 s (trzy okresy sygnału życia) uznaje połączenie za martwe. Sposób wybiera się w ustawieniach Zaawansowane → „Zagrożenia (NEPTUN): sposób pobierania” (WebSocket z zapasem, tylko WebSocket, tylko REST). Karta „Stan źródeł” pokazuje stan feedu zagrożeń, tryb, wiek ostatniej aktualizacji i ostatni błąd; feed zachowuje te dwie ostatnie informacje po zatrzymaniu, bo gdy ustawienia są otwarte, mapa jest w tle i połączenie jest zamknięte.
- **Tylko telefon, tylko gdy warstwa jest włączona i mapa na wierzchu.** Połączenie jest zamykane w `OnPause` i po wyłączeniu warstwy, a lista zapominana. Android Auto nigdy nie rysuje tej warstwy (`MapController.ThreatsAllowed` ustawia tylko `MainActivity`).
- **Kierunek dla zagrożeń bez kursu.** W próbce z żywego serwera `heading` było `null` dla dronów FPV i dla części zagrożeń z flagą `approx`. `ThreatStore` wylicza wtedy kierunek: z dwóch ostatnich punktów `trail` albo z przesunięcia o co najmniej 1,5 km względem miejsca, w którym kierunek był ostatnio wyliczony (próg ucina szum, bo pozycje są zaokrąglone do miejscowości), i zachowuje go, dopóki zagrożenie stoi. Kierunek z serwisu nigdy nie jest nadpisywany, a pozycje `areaOnly` nie dostają kierunku. Wyliczony kierunek ma znacznik `IsHeadingDerived` i opis „(z ruchu)” w karcie. Nie wiadomo jeszcze, jak często FPV zmienia pozycję między odczytami, więc bywa, że kierunku nadal nie ma; wtedy ikona FPV jest symetryczna.
- **Bezpieczeństwo prezentacji.** Obserwacja (`advisory`) jest neutralna kolorystycznie i opisana jako „nie sygnał do schronienia”; zagrożenie z flagą `areaOnly` nie jest rysowane jako punkt; ślad niepotwierdzany od dawna jest przyciemniony, a zakładany kurs rysowany wyblakłą ikoną. Stałe łącze „Dane: NEPTUN” i zdanie ostrzeżenia są widoczne przez cały czas, gdy warstwa jest włączona.
- **Czego nie robimy (na razie):** granic rejonów i obwodów (`raions.geojson`, `oblasts.geojson`) i wypełniania alarmów, wiadomości z Telegrama, alertów „zagrożenie w pobliżu” ani przewidywania położenia między odczytami.

## Strefy przestrzeni powietrznej: PAŻP (rozpoznanie z 2026-11-03)

Źródło: mapa przestrzeni powietrznej PAŻP, `https://airspace.pansa.pl/map-configuration/uup` (mapa na kolejny dzień według Updated Use Plan) i `.../aup` (Airspace Use Plan). Serwis **nie ma udokumentowanego API**: aplikacja czyta publiczny JSON, który ładuje strona mapy. Nie ma kluczy ani tokenów. **Warunki użycia nie są znane**, więc przed udostępnieniem aplikacji komukolwiek poza autorem trzeba je sprawdzić (ROADMAP, pozycja 6).

Odpowiedź (żywe zapytanie 2026-11-03): tablica 188 obiektów GeoJSON `Feature`, wszystkie z geometrią `Polygon` (jeden zewnętrzny kontur, współrzędne `[długość, szerokość]`), razem 3405 wierzchołków i 318 kB. W `properties`: `designator` (np. `EPTR815`), `airspaceElementType` (TRA 129, TSA 23, D 22, ATZ 9, MRT 3, R 1, ADHOC 1), `centroid`, `section`, `fuaStatus`, `activeH24` i lista `airspaceReservations`. Rezerwacja ma `startDate` i `endDate` (UTC, ISO 8601), `lowerAltitude` i `upperAltitude`, `altitudeUnit`, `unit` (np. `ZZZZ`, `EPWA`), opcjonalne `remarks` (np. numer NOTAM) i `reservationStatus` (`ACTIVATED` 115, `PLANNED` 96). Wysokości mają zapis `GND` (ziemia), `Annn` (wysokość nad poziomem morza w setkach stóp, np. `A009` to 900 ft) i `Fnnn` (poziom lotu, np. `F095` to FL095, 9500 ft); `altitudeUnit` ma w próbce wartość `FL` także dla zapisu `A…`, więc aplikacja opiera się na literze, nie na tym polu. 167 stref ma jedną rezerwację, 19 dwie, 2 trzy.

- **Co aplikacja z tego robi.** `PansaParser` zamienia to na modele Core (rodzaj zostaje tekstem, żeby nowy rodzaj nie wymagał zmiany kodu). Stan strefy liczy się z rezerwacji w danej chwili, nie z pola `fuaStatus`: aktywna, gdy rezerwacja ze statusem `ACTIVATED` obowiązuje teraz; planowana, gdy zapowiedziana na teraz lub później; nieaktywna, gdy wszystkie minęły.
- **Pobieranie.** Najpierw mapa UUP; gdy jest pusta lub zawiedzie, mapa AUP. Odświeżanie co 5 min (suwak), tylko gdy warstwa jest włączona i mapa na wierzchu. Po błędzie przerwa 30 s, 60 s… do zwykłego odstępu; 429 wstrzymuje pobieranie na czas z `Retry-After`. Limit czasu zapytania to 30 s, bo odpowiedź jest większa niż w pozostałych serwisach.
- **Bezpieczeństwo prezentacji.** Stałe „Dane: PAŻP” z linkiem przy włączonej warstwie i zdanie, że strefy są nieoficjalne, mogą być spóźnione lub niepełne i nie służą do planowania lotów (dialog przy włączaniu i karta strefy). Dane nie zastępują AIP ani NOTAM.
- **Warunki użycia PAŻP (sprawdzone 2026-11-03).** Nie znaleziono żadnych jawnych warunków użycia ani licencji dla `airspace.pansa.pl` i jego punktów JSON: strona mapy (`/`, `/map`, `/aup/current`, `/aup/next`) jest powłoką wczytywaną skryptem bez stopki z regulaminem, a odpowiedzi `uup` (318 kB) i `aup` (459 kB) to goła tablica `Feature` bez pola licencji (pierwsze ok. 39 tys. znaków każdej przeczytano dosłownie, resztę tylko w streszczeniu). Strona `www.pansa.pl` w stopce ma tylko ochronę danych osobowych, deklarację dostępności, politykę cookies i kontakt; strona „Dla deweloperów” (`/dla-deweloperow/`), do której odsyła stopka, zwraca 404. Nie znaleziono oficjalnego API ani strumienia danych o rezerwacjach AUP/UUP, ani zdania, że mapa nie służy do planowania lotów.
- **Jedyne jawne ograniczenie dotyczy innego serwisu:** `ais.pansa.pl/o-ais/zastrzezenia-prawne/` (zmiana z 24.11.2025) mówi „Wszelkie prawa zastrzeżone”, że zawartość „stanowi własność Polskiej Agencji Żeglugi Powietrznej i/lub innych wskazanych właścicieli”, „nie może być używana w celach komercyjnych”, że „wszelka modyfikacja, reprodukcja lub przetwarzanie zawartości strony ais.pansa.pl jest zabronione”, oraz że PAŻP nie gwarantuje „ścisłości, kompletności, bezbłędności i aktualności” informacji. Zastrzeżenie dotyczy AIS (AIP, NOTAM), nie mapy `airspace.pansa.pl`, ale pokazuje stanowisko PAŻP wobec danych lotniczych: **nie wolno zakładać swobodnej redystrybucji ani użytku komercyjnego**. PAŻP deklaruje też na `www.pansa.pl/dane/`, że dane „mogą mieć wartość handlową”.
- **Wniosek na dziś:** użytek własny (sideload, bez dystrybucji i bez zysku) jest niskiego ryzyka, ale **brak zgody nie jest zgodą**. Przed udostępnieniem aplikacji komukolwiek poza autorem trzeba o nią zapytać: ogólny kontakt `info@pansa.pl` (Dział Komunikacji i PR), planowanie rezerwacji przestrzeni (ASM1, Dział Planowania Strategicznego) `asm1@pansa.pl`, wnioski o informację publiczną `udip@pansa.pl`; ogólne warunki sprzedaży publikacji AIS są w `https://docs.pansa.pl/ais/form/others/Ogolne_warunki_sprzedazy_publikacji.pdf` (nieprzeczytane). Do rozstrzygnięcia: czy zbieranie tych samych danych jako strumienia dla aplikacji wymaga umowy, i czy stosować się do zastrzeżenia AIS jako wzorca. Do tego czasu strefy nie wchodzą do żadnej wersji udostępnianej publicznie.
- **Filtr wysokości i powiadomienia.** Warstwa i powiadomienia mają wspólny filtr przedziału wysokości (ekran stref): strefa przechodzi, gdy zarezerwowana przestrzeń zahacza o przedział (dolna granica nie wyżej niż góra przedziału, górna nie niżej niż jego dół); poziomy lotu są porównywane jako wysokości w stopach bez poprawki na ciśnienie. Powiadomienie o aktywacji dotyczy rezerwacji ze statusem `ACTIVATED`, które obowiązują albo zaczynają się w ciągu ustawionego czasu uprzedzenia, dla stref w ustawionej odległości od pozycji GPS (odległość do najbliższej krawędzi konturu, zero wewnątrz). Każda rezerwacja (oznaczenie strefy i początek) jest zgłaszana raz, pamięć zgłoszonych jest zapisana na urządzeniu i czyszczona doba po końcu rezerwacji; pierwsze sprawdzenie po włączeniu zapamiętuje strefy już aktywne bez powiadomienia. Sprawdzanie działa po każdym pobraniu przy otwartej mapie oraz w każdym cyklu trybu w tle alertów (wtedy `AirspaceFeed.RefreshIfDueAsync` pobiera strefy tylko wtedy, gdy minęła przerwa z poprzedniego pobrania).
- **Ograniczenia.** Tylko zewnętrzne kontury, bez otworów (w próbce ich nie ma). Dotknięcie rozpoznaje punkt w konturze przy założeniu płaskich stopni, co wystarcza dla stref rzędu kilkudziesięciu kilometrów. Brak pamięci na dysku. Brak stref w Android Auto.

## Samoloty wojskowe z całego świata: adsb.lol `/v2/mil` (rozpoznanie z 2026-11-04)

Zapytanie `https://api.adsb.lol/v2/mil` zwraca wszystkie samoloty, które baza adsb.lol oznacza jako wojskowe (`dbFlags & 1`), niezależnie od położenia użytkownika. Żywe zapytania: 349 rekordów i 147 kB (282 z pozycją), a po kilku godzinach 63 samoloty z pozycją, więc liczba waha się od kilkudziesięciu do kilkuset. Odpowiedź ma ten sam format co `/v2/point/...` (tablica `ac`), więc parser jest wspólny z pozycjami; czas odpowiedzi ok. 0,3 s. Zapytanie jest globalne: nie niesie współrzędnych użytkownika, ale odpowiedź jest duża, a serwis ma limit zapytań (w tym samym czasie adsb.lol odpowiadał 429 na zwykłe zapytania o obszar).

- **Dlaczego.** Zwykłe zapytanie `/v2/point` ma promień do 250 mil morskich, więc po oddaleniu mapy tryb „tylko wojskowe” pokazuje tylko to, co jest w tym kole. Zapytanie globalne daje przegląd całego regionu jednym zapytaniem.
- **Jak jest podłączone.** Core ma opcjonalny interfejs `IMilitaryAircraftSource` (zwraca `null`, gdy źródło nie ma takiego zapytania); implementuje go `AdsbLolClient`. Przechodzi przez `RateLimitedAircraftSource` (ten sam limiter co zapytania o obszar: odpowiedź 429 wstrzymuje całe źródło), `FallbackAircraftSource` (pierwsze źródło z kolejności, które ma to zapytanie i nie jest wstrzymane; brak źródeł z takim zapytaniem daje `null`) i `PrivacyAircraftSource` (przekazuje bez zaokrąglania, bo nie ma co zaokrąglać). `AircraftDataLoop` używa go, gdy tryb „tylko wojskowe” jest włączony i ustawienie „Wojskowe z całego świata” też (`MilitaryQueryPolicy`); gdy zapytanie zwróci `null`, pobiera obszar jak zwykle, a błąd i 429 traktuje jak przy zwykłym pobraniu.
- **Odstęp.** Po zapytaniu globalnym przerwa to dłuższa z dwóch wartości: zwykłe odświeżanie samolotów i „Odświeżanie wojskowych z całego świata” (15–120 s, domyślnie 30 s).
- **Alerty.** Na liście z zapytania globalnego alerty nie są sprawdzane (`AlertMonitor.Process` jest pomijany), bo silnik alertów porównuje kolejne pobrania tego samego obszaru i uznałby samoloty za wylatujące i wracające przy każdym przełączeniu trybu. Tryb w tle alertów ma własne pobieranie obszarów i nie jest dotknięty.
- **Inne źródła.** airplanes.live ma `/v2/mil` w tym samym formacie (dziś odpowiada 403 na całość), adsb.fi nie sprawdzano, FlyItaly (`api.flyitalyadsb.com/v2/mil`, klucz `X-Api-Key`) wymaga klucza. Na razie tylko adsb.lol; dodanie kolejnego to implementacja `IMilitaryAircraftSource` w jego kliencie.
- **Czego nie ma.** Sygnału „wzrost aktywności” (ponad dwukrotnie więcej wojskowych niż średnia z 7 dni; pomysł z analizy Strażnika), zapasowego źródła i własnego klasyfikatora wojskowych na tej liście (flaga serwisu wystarcza).

## Ostrzeżenia: Regionalny System Ostrzegania przez TVP (rozpoznanie z 2026-11-03)

Źródło: publiczny kanał, w którym TVP udostępnia komunikaty RSO: `https://komunikaty.tvp.pl/komunikatyxml/{województwo}/{kategoria}/{strona}?_format=json` (opis integracji: `https://komunikaty.tvp.pl/Info/Integration`; ta sama konstrukcja jest używana w integracji Home Assistant `alert-rcb-hass`). Nie ma klucza ani tokena. Oficjalne API CAP/OpenAPI RSO wymaga tokena od administratora systemu. **Warunki użycia i limity zapytań nie są znane**, więc przed udostępnieniem aplikacji trzeba o nie zapytać (ROADMAP, pozycja 6). **Alert RCB (SMS od operatora) nie jest częścią tego kanału** i aplikacja go nie odczyta.

Odpowiedź (żywe zapytania 2026-11-03): obiekt `{"pagination":{"totalitems","itemsperpage"},"newses":[...]}`; strona ma do 20 komunikatów, kolejne strony są starsze i nie nakładają się na pierwszą. Województwo w adresie to `wszystkie` albo jeden z 16 identyfikatorów (`dolnoslaskie`, `kujawsko-pomorskie`, `lubelskie`, `lubuskie`, `lodzkie`, `malopolskie`, `mazowieckie`, `opolskie`, `podkarpackie`, `podlaskie`, `pomorskie`, `slaskie`, `swietokrzyskie`, `warminsko-mazurskie`, `wielkopolskie`, `zachodniopomorskie`; nieznany daje 404), kategoria to `wszystkie`, `ogolne`, `meteorologiczne`, `hydrologiczne`, `informacje-drogowe` albo `stany-wod`. W dniu sprawdzenia: `ogolne` 20 komunikatów (od kwietnia do listopada, 2 alarmy), `hydrologiczne` 20 (7 alarmów, głównie susza), `informacje-drogowe` 3, `meteorologiczne` 0, `stany-wod` 20 odczytów wodowskazów (wszystkie z 2026-11-03 23:50, bez tytułu i treści). Komunikat nie ma pola kategorii: kategorię określa adres.

Pola komunikatu: `id`, `title`, `shortcut` (skrót), `content`, `rso_alarm` (tekst `"0"` lub `"1"`, dla odczytów `null`), `valid_from`, `valid_to`, `created_at`, `updated_at` (`yyyy-MM-dd HH:mm:ss` w czasie Polski), `longitude`, `latitude` (zwykle `null`), `river_name`, `location_name`, `water_level_value`, `water_level_warning_status_value`, `water_level_alarm_status_value`, `water_level_trend`, `provinces` (obiekt z kluczami liczbowymi; w wartościach `id`, `name`, `slug_name`). Obiekt ma też śmieciowe klucze `"0"`, `"1"`, `"2"`, `"3"` z formatem daty (parser je ignoruje). Znaczenia `water_level_trend` nie znamy, więc nie jest używane; poziomy wody czytamy jako centymetry, bo kanał nie podaje jednostki (do potwierdzenia).

- **Pobieranie.** Dla 1–4 wybranych województw osobne zapytanie o każde województwo i kategorię (komunikaty ruchliwego województwa nie wypychają wtedy z 20 najnowszych komunikatów cichego); dla pozostałych przypadków jedno zapytanie `wszystkie` na kategorię i filtrowanie po stronie aplikacji. Przy domyślnych trzech kategoriach i czterech województwach to 12 zapytań na odświeżenie, wykonywanych po kolei, co 10 min (suwak 5–60). Pobieranie działa tylko przy otwartym ekranie „Ostrzeżenia”. Po błędzie przerwa 30 s, 60 s… do zwykłego odstępu; 429 wstrzymuje pobieranie na czas z `Retry-After`. Limit czasu zapytania to 20 s.
- **Bezpieczeństwo prezentacji.** Zawsze widoczne zastrzeżenie, że to nie jest oficjalny system ostrzegania i że komunikat może być spóźniony lub niepełny; „ALARM” tylko dla flagi `rso_alarm = 1`; osobno wyróżniamy przekroczenie stanu ostrzegawczego i alarmowego wodowskazu. Komunikaty nie są rysowane na mapie. Nie ma powiadomień (zostawione na później, ROADMAP pozycja 12).
- **Nazwy w adresie** są sprawdzane (tylko małe litery i myślniki), żeby uszkodzone ustawienie nie zmieniło adresu zapytania.

## Czego jeszcze brakuje

- Pokazanie użytkownikowi, które źródło aktualnie odpowiada (`LastUsedSourceName` jest gotowe, brakuje miejsca w UI).
- Pokazanie użytkownikowi, które źródło tras odpowiedziało.
- Licencje: warunki adsb.lol, hexdb.io i adsb.fi sprawdzone 2026-11-03 (sekcja „Warunki użycia”); zostaje airplanes.live po uzgodnieniu, ręczne przejrzenie stron niedostępnych automatycznie oraz decyzja przed udostępnieniem poza własny użytek (napisać do hexdb.io, ewentualnie przejść na FlyItalyADSB lub umowę z dostawcą).
- Nie potwierdzono limitów zapytań airplanes.live ani adsb.lol. Strona porównawcza podaje je jako „nie podano”.

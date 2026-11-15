# Roadmapa V2: własny serwer danych

Stan na 2026-11-05. Plan przejścia z architektury „telefon pyta wiele serwisów” na „aplikacja pyta wyłącznie nasz serwer, a serwer pyta serwisy”. Ten plik jest osobny od `ROADMAP.md` i niczego w nim nie zmienia. Ceny i limity pochodzą z wyszukiwania w sieci (źródła na końcu); agregatory cen rozjeżdżają się ze sobą, a Hetzner, Contabo i Fly.io zmieniały ceny w 2026 roku, więc wszystkie kwoty to **szacunki do sprawdzenia w konfiguratorze dostawcy przed zakupem**. Przeliczniki: 1 EUR ≈ 4,25 zł, 1 USD ≈ 3,7 zł.

## 1. Decyzje i założenia

| Decyzja | Treść |
|---|---|
| D1 (2026-11-05) | Aplikacja Android rozmawia **wyłącznie z naszym serwerem**. Obecne źródła (adsb.lol, adsb.fi, airplanes.live, adsbdb, hexdb, AWC, NEPTUN, PAŻP, TVP/RSO) przechodzą do aplikacji serwera. |
| D2 (2026-11-05) | Skala użytkowników jest nieznana, więc plan liczymy dla trzech progów: 10, 100 i 500 aktywnych użytkowników miesięcznie. |
| D3 (2026-11-05) | Żadnego serwera domowego. Tylko hosting zewnętrzny. |
| Założenie | Serwer w C# / .NET 10 (ten sam język i te same biblioteki `Sources\*`, które są już `net10.0` bez zależności od Androida). Inny język tylko po Twoim potwierdzeniu. |
| Założenie | Aplikacja jest darmowa i publikowana w Google Play (ROADMAP, pozycja 18). Serwer jest więc **redystrybutorem danych**, a nie tylko pośrednikiem użytku osobistego. To zmienia ocenę licencji (rozdział 3). |

## 2. Najważniejsze wnioski (czytaj najpierw)

1. **Wąskim gardłem nie jest hosting, tylko licencja i limity źródeł.** Serwer obsługujący 500 użytkowników to szczyt rzędu 15 zapytań na sekundę i ok. 30 GB transferu miesięcznie. Zmieści się na najtańszym VPS-ie (ok. 5 EUR) z dużym zapasem. Natomiast wszystkie darmowe źródła pozycji zastrzegają użytek osobisty i niekomercyjny, a adsb.fi wprost zakazuje „license, sell, rent, or lease any part of the data or the service”. Serwer przekazujący dane wielu osobom takie warunki łamie, dopóki nie ma zgody.
2. **Jedyne źródło pozycji z jawnie dozwolonym użytkiem komercyjnym to FlyItalyADSB** (darmowy klucz, 30 zapytań/min, 300 tys. zapytań/miesiąc, promień do 500 km, atrybucja CC BY-SA 4.0). Miesięczny limit 300 tys. to w przybliżeniu **jedno zapytanie co 8,6 s**. Wystarcza na jedno koło o promieniu 500 km (cała Polska i sąsiedzi) odświeżane co 10 s (259 tys./miesiąc, 86% limitu), ale na nic więcej. Pokrycie Europy wymaga zgód od adsb.lol, adsb.fi lub airplanes.live albo płatnego planu.
3. **Koszt serwera praktycznie nie rośnie z liczbą użytkowników**, bo serwer pyta źródła według własnego harmonogramu (ruch do źródeł zależy od obszaru i czasu, nie od liczby osób), a użytkownicy dostają gotowy, skompresowany, cache'owany snapshot. Rośnie tylko transfer wychodzący i liczba połączeń.
4. **Rekomendacja hostingu:** jeden VPS w UE (Hetzner Cloud lub OVHcloud, Docker Compose) z darmowym Cloudflare przed nim jako cache i ochroną. Alternatywa bez administrowania systemem: Fly.io (region Warszawa). Szczegóły w rozdziale 5.
5. **Pojedynczy serwer to pojedynczy punkt awarii**, a D1 usuwa awaryjny tryb bezpośredni. Żeby to zrównoważyć, serwer zwraca ostatni snapshot z oznaczeniem wieku (aplikacja już pokazuje wiek danych na karcie „Stan źródeł”), a sam serwer ma monitoring i automatyczny restart. Rozważ, czy nie zostawić w aplikacji ukrytego trybu awaryjnego dla użytku własnego (decyzja do podjęcia w rozdziale 10).
6. **Bez uwierzytelnienia aplikacji serwer stanie się publicznym darmowym API do cudzych danych**, co samo w sobie narusza warunki źródeł i zje limity. Weryfikacja instalacji (Play Integrity) i limity na instalację są częścią MVP, nie dodatkiem (etap E7).

## 3. Architektura docelowa

```
 Źródła zewnętrzne                    Serwer (ASP.NET Core, .NET 10)                  Klienci
 ─────────────────                    ──────────────────────────────                  ───────
 FlyItaly / adsb.lol / adsb.fi  ──►   PositionPoller (harmonogram kafli,       ┌─►  Telefon
 airplanes.live                        limity, zapas, backoff)                 │    (MainActivity)
 adsbdb / hexdb / adsb.lol routes ─►  Enricher (SQLite, trwały cache)   ──► API ─┤
 aviationweather.gov (METAR, TAF) ─►  WeatherStore / ForecastStore      /v1    │
 neptun.in.ua (WebSocket)  ─────────►  ThreatRelay (1 połączenie, SSE)         ├─►  Android Auto
 airspace.pansa.pl  ────────────────►  AirspaceFeed (hash, ETag)               │    (MapScreen)
 komunikaty.tvp.pl  ────────────────►  WarningFeed                             │
                                       Cloudflare (cache, WAF, TLS) ◄─────────┘
```

Zasady:

- **Warstwy się nie mieszają.** `Sources\*` zostają bez zmian i bez wiedzy o serwerze. Serwer referencuje Core, źródła i `Management` (limiter, zapas, `TimedCache`, `AircraftEnricher`, `WeatherStore`, `ForecastStore`, `PollingFeed<T>`), czyli składa je tak samo jak dziś `DataComposition`, tylko po stronie serwera. To oszczędza większość pracy z etapów E2, E4–E6.
- **Aplikacja dostaje nowy projekt** `Sources\Server\AdsbCarMap.DataSources.Server` (klient HTTP implementujący `IAircraftSource`, `IAircraftByHexSource`, `IMilitaryAircraftSource`, `IRouteSource`, `IAircraftLookupSource`, `IWeatherSource`, `IThreatFeed`, `IAirspaceSource`, `IWarningSource`), więc `MapController` i reszta nadal znają tylko interfejsy.
- **Kontrakt wspólny** w osobnym projekcie `AdsbCarMap.Contracts` (DTO, kody błędów, wersja API), do którego odwołują się serwer i klient. Core zostaje czystym kontraktem źródeł.
- **Serwer jest bezstanowy poza cache'em** (SQLite w woluminie: szczegóły samolotów, trasy, ostatnie snapshoty). Utrata dysku to chłodny start, nie utrata danych użytkowników, bo **serwer nie ma kont ani danych osobowych**.

## 4. Plan dla każdego źródła danych

Kolumna „Zapytania/mies.” to ruch **z serwera do źródła** (nie zależy liniowo od liczby użytkowników). Założenia dotyczące obszaru: koło 500 km (ok. 270 NM) wokół 52,0°N 19,4°E (Polska i sąsiedzi), chyba że napisano inaczej.

| Źródło | Co serwer robi | Interwał | Zapytania/mies. (10 / 100 / 500 użytk.) | Limity i warunki | Status zgody | Ryzyko |
|---|---|---|---|---|---|---|
| **FlyItalyADSB** (pozycje) | Jedno koło 500 km, snapshot w pamięci, aktywne tylko gdy ktoś ma otwartą mapę (polling na żądanie) | 10 s | ok. 65 tys. / 150 tys. / 195 tys. (maks. 259 tys. przy pracy 24/7) | 30/min, 300 tys./mies., klucz `X-Api-Key`, CC BY-SA 4.0, użytek komercyjny dozwolony | **Jedyne jawnie dozwolone.** Wymaga darmowego klucza (rejestracja). | Limit miesięczny blisko pełnego wykorzystania; jedno źródło = jeden punkt awarii; atrybucja musi być w aplikacji |
| **adsb.lol** (pozycje, `/v2/mil`, `/v2/hex`) | Zapas dla FlyItaly, globalne `/v2/mil` (jedno zapytanie na wszystkich) | `mil` co 30–60 s tylko przy aktywnych klientach w trybie wojskowych | ok. 10 tys. – 40 tys. | Zmierzone w tej aplikacji: ok. 10–12 zapytań na kilkadziesiąt sekund z jednego IP, potem 429 z `Retry-After`; zapowiedź wymogu klucza karmiących; klucz anonimowy 24 h, 500/dobę | Brak opublikowanych warunków danych; dla produkcji prosi o kontakt | **Wysokie** jako główne źródło dla redystrybucji; serwer ma jedno IP, więc limit „na adres” uderza w całość ruchu |
| **adsb.fi** (pozycje) | Zapas | 1 zapytanie/s dozwolone, używamy ≤ 0,2/s | 0 w normalnej pracy, do ok. 130 tys. przy awarii głównego | 1 zapytanie/s, błędne zapytania wliczają się do limitu i dają blokadę IP; „personal, non-commercial only”, atrybucja | **Brak zgody** na redystrybucję (wprost zakaz) | Wysokie bez maila z prośbą o warunki komercyjne („contact us if you have commercial … requirements”) |
| **airplanes.live** | Zapas; w trybie z rozszerzonym pokryciem główne źródło kafli Europy | 1 zapytanie/s | wg trybu | 1 zapytanie/s, bez SLA, „Non-Commercial Use”; dziś 403 do czasu uzgodnienia | Czeka na mail (ROADMAP, pozycja 3) | Średnie: sprawa do załatwienia mailem z opisem serwera |
| **adsbdb** (trasy po znaku, dane samolotu po hex) | Trwały cache w SQLite: dane samolotu bez wygasania (typ, rejestracja, właściciel są stałe), trasy na 24 h (nieznane na 6 h) | na żądanie | trasy: ok. 3 / 20 / 50 tys.; dane samolotu: ok. 1 / 5 / 10 tys. (potem malejące, bo każdy hex pobierany raz) | Brak opublikowanych limitów; serwer sam ogranicza do 2 zapytań/s | Brak warunków | Średnie: masowe zapytania z jednego IP mogą wywołać blokadę; napisać z opisem |
| **hexdb.io** | Drugie źródło tras i danych samolotu | na żądanie, tylko gdy adsbdb nie zna lotu | kilkaset – kilka tysięcy | Brak warunków (404 na `/terms`), dane pochodzą od osób trzecich | Brak zgody; licencje danych źródłowych mogą być ostrzejsze | **Wysokie** prawnie (nieznane licencje danych); kandydat do wycofania przy braku odpowiedzi |
| **adsb.lol routes** | Trzecie źródło tras (statyczny plik VRS standing data, aktualizowany co godzinę) | na żądanie; do rozważenia: pobranie całego pliku i lokalny indeks | ok. 3 tys. – 10 tys. | Redirect do statycznego pliku | jak adsb.lol | Niskie technicznie; opcja „bulk” zeruje zapytania do tras, do sprawdzenia w osobnym spike |
| **aviationweather.gov (AWC)** METAR i TAF | Zbiorcze zapytanie o ok. 60–100 stacji regionu, wynik w `WeatherStore`/`ForecastStore`, najbliższa stacja liczona po stronie serwera | METAR co 5–10 min, TAF co 30 min, tylko gdy są aktywni klienci | ok. 4–9 tys. METAR + 1,5 tys. TAF | Dane publiczne NOAA, limit ok. 100 zapytań/min (do potwierdzenia); atrybucja „Dane: NOAA AWC” | Dane rządowe USA, redystrybucja zwykle dozwolona (do potwierdzenia na stronie AWC) | Niskie |
| **NEPTUN** (zagrożenia) | **Jedno** połączenie WebSocket z serwerem NEPTUN, relay do klientów przez SSE; zapas REST nie częściej niż co 5 s | ciągłe | 1 połączenie, 0 zapytań REST w normalnej pracy | Użytek komercyjny dozwolony, obowiązkowe widoczne łącze „Dane: NEPTUN”, REST ≥ 5 s, zastrzeżenie o nieoficjalności danych | **Warunki dopuszczają** (sprawdzone 2026-11-03) | Niskie prawnie; operacyjnie: serwer musi przekazać atrybucję i ostrzeżenie do klienta |
| **PAŻP** (strefy UUP/AUP) | Pobiera UUP, zapas AUP, liczy hash, publikuje klientom z ETag (klient pobiera tylko po zmianie) | co 5 min | ok. 17 tys. (ok. 6,7 GB pobrania do serwera miesięcznie) | Brak warunków; ais.pansa.pl zastrzega „prawa zastrzeżone” i brak użytku komercyjnego | **Brak zgody**, a redystrybucja jest poważniejsza niż użytek własny | **Wysokie.** Do czasu odpowiedzi PAŻP strefy zostają poza serwerem publicznym (flaga funkcji wyłączona) |
| **TVP / RSO** (komunikaty) | Pobiera „wszystkie” na kategorię co 5 min (3 zapytania) oraz województwa co 30 min (16 × 3); filtrowanie po stronie serwera | 5 / 30 min | ok. 95 tys. | Brak warunków i limitów; kanał nieoficjalny; oficjalne CAP API RSO wymaga tokena od administratora | **Brak zgody** | **Wysokie.** Alternatywa: oficjalny token CAP z RCB (zapytać). Do czasu zgody poza serwerem publicznym |
| **Kafelki OSM** | **Nie przechodzą przez nasz serwer.** Osobna decyzja (ROADMAP, pozycja 18): kafelki rastrowe od komercyjnego dostawcy lub własny serwer | n/d | n/d | Tile Usage Policy OSMF nie przewiduje dużych aplikacji publicznych; proxowanie przez nasz serwer czyni nas klientem | do ustalenia | Niezależne od V2, ale blokuje publikację w Play |

**Scenariusze pozycji samolotów:**

- **Scenariusz A, licencjonowany (start):** wyłącznie FlyItalyADSB, jedno koło 500 km, 10 s. Zero kosztów danych, zgodnie z warunkami, pokrycie Polska i sąsiedzi. Źródła zapasowe wyłączone do czasu zgód, więc awaria FlyItaly oznacza stare dane z oznaczeniem wieku.
- **Scenariusz B, po zgodach:** adsb.fi i airplanes.live przy 1 zapytaniu/s każde dają ok. 170 tys. zapytań na dobę. To wystarcza na ok. 20 kół po 250 NM (Europa) przy 15 s, z zapasem na awarię. Zgody są warunkiem, nie formalnością.
- **Płatna alternatywa:** adsb.lol sponsor (od 20 EUR/mies., 24 tys. zapytań/mies.) jest za mały dla serwera (jedno zapytanie na ok. 108 s). Inne płatne dane (FlightAware, OpenSky na licencji komercyjnej) nie były w tym rozpoznaniu wyceniane. Do ustalenia z dostawcami, jeśli A i B odpadną.

## 5. Model ruchu: 10 / 100 / 500 użytkowników

### Założenia (zmień je, gdy będą dane z Play Console)

| Parametr | Wartość | Uwaga |
|---|---|---|
| Aktywny czas na użytkownika | 45 min/dobę, 25 dni/mies. = 18,75 h = 67 500 s/mies. | Telefon z otwartą mapą lub Android Auto w trasie |
| Odświeżanie pozycji przez klienta | co 5 s (założenie ostrożne) | Sprawdzić domyślną wartość w ustawieniach; wolniejsze odświeżanie obniża wszystkie liczby proporcjonalnie |
| Samoloty widoczne w oknie klienta | ok. 100 | W praktyce 20–250 zależnie od zoomu |
| Rozmiar odpowiedzi | ok. 4 KB po gzip (kontrakt: tylko używane pola, ok. 120 B na samolot przed kompresją, zamiast ok. 420 B w odpowiedzi adsb.lol) | Z ETag/304 i delta mniej |
| Jednoczesność w szczycie | 15% użytkowników | Poranna/popołudniowa jazda |

### Wyniki (ruch klient ↔ nasz serwer)

| Wskaźnik | 10 użytk. | 100 użytk. | 500 użytk. |
|---|---|---|---|
| Zapytania o pozycje/mies. | 135 tys. | 1,35 mln | 6,75 mln |
| Jednoczesni użytkownicy w szczycie | 2 | 15 | 75 |
| Zapytania/s w szczycie (pozycje) | 0,4 | 3 | 15 |
| Transfer wychodzący/mies. (pozycje) | 0,5 GB | 5,4 GB | 27 GB |
| Transfer łącznie (z pogodą, trasami, strefami, zagrożeniami, +20%) | 0,7 GB | 6,5 GB | 32 GB |
| Połączenia SSE (zagrożenia) w szczycie | 2 | 15 | 75 |
| CPU serwera w szczycie (1 vCPU, cache w pamięci) | < 1% | < 5% | 10–20% |
| RAM serwera | ok. 150 MB | ok. 200 MB | ok. 300 MB |

Wnioski:

- Przy 500 użytkownikach serwer wykonuje ok. 15 zapytań/s, z których prawie wszystkie to odczyt gotowego bufora z pamięci. Jeden VPS z 1–2 rdzeniami wytrzyma wielokrotnie więcej; granica jednego serwera to dopiero kilka tysięcy aktywnych użytkowników (do zmierzenia testem obciążeniowym, etap E7).
- Transfer 32 GB/mies. mieści się w pakietach każdego rozważanego hostingu (Hetzner i Contabo mają kilkadziesiąt TB, Fly.io 100 GB w UE).
- **Ruch do źródeł nie zależy od liczby użytkowników**, tylko od obszaru i czasu aktywności (rozdział 4). To największa korzyść z serwera względem dziś: dziś 500 użytkowników = 500 adresów IP pytających adsb.lol, po zmianie = jeden adres z kontrolowanym harmonogramem.
- Przy Cloudflare przed serwerem (krótki `max-age` 3–5 s na snapshot kwadratów) ruch do originu spada jeszcze kilkukrotnie.

## 6. Hosting: zestawienie za i przeciw

Wymagania: .NET 10 (kontener Docker), stały adres i TLS, długotrwałe połączenia SSE/WebSocket, mały trwały wolumen (SQLite), region UE (najlepiej blisko Polski), przewidywalny koszt w EUR/USD.

| Hosting | Szacunkowy koszt miesięczny* | Za | Przeciw |
|---|---|---|---|
| **Hetzner Cloud** (CAX11 ARM lub CX23 x86; DE/FI) | ok. 4–5 EUR + VAT (po podwyżkach z kwietnia i czerwca 2026); duży pakiet transferu | Najlepszy stosunek ceny do mocy, stały IP, brak limitów połączeń, Docker, snapshoty i kopie za ok. 20% ceny, ARM działa z .NET | Zero zarządzania za Ciebie: aktualizacje systemu, firewall, kopie, restarty. Brak regionu w Polsce (opóźnienie ok. 25–40 ms, bez znaczenia dla tej aplikacji). Jedna maszyna, bez HA. Adresy centrów danych bywają blokowane przez źródła |
| **OVHcloud VPS** (UE, możliwa Warszawa/Frankfurt) | ok. 5–7 EUR; ceny w 2026 zmieniały się kilka razy, rozbieżne w źródłach | Polska lokalizacja i polski/UE dostawca, nielimitowany transfer, mocne maszyny w cenie | Reputacja wsparcia i stabilności zmienna, zmiany cen, konfigurator mniej przejrzysty, nie potwierdziłem dostępności regionu Warszawa dla VPS |
| **Contabo VPS 10** | ok. 4–5,5 EUR (4 vCPU, 8 GB RAM, 75 GB) | Najwięcej RAM i CPU za cenę, duży transfer | Silne przeładowanie hostów i zmienna wydajność, wolne wsparcie, podwyżki, słabszy przy jittrze CPU; do pętli odpytywania akceptowalne, do SSE średnie |
| **Fly.io** (region Warszawa) | ok. 7–9 USD (np. 1 GB RAM, wolumen, dedykowany IPv4); 256 MB to 2,19 USD, ale .NET potrzebuje więcej | Wdrożenie z Dockera jedną komendą, TLS i certyfikaty automatyczne, region w Polsce, łatwe skalowanie i druga maszyna, WebSocket/SSE działają | Brak darmowej puli (od 2026 nowe konta płacą), RAM zdrożał o 20% w październiku 2026, wolumen jest związany z jedną maszyną, rozliczanie w USD, historia incydentów platformy |
| **Railway** | ok. 10–15 USD (plan Hobby 5 USD z kredytem; ok. 20 USD/vCPU, 10 USD/GB RAM) | Najprostsze wdrożenie, podgląd logów, zmienne środowiskowe | Rozliczanie za zużycie jest drogie przy ciągłej pracy, region UE to Amsterdam, wolumeny i SQLite mniej wygodne |
| **Azure Container Apps** (Poland Central lub West Europe) | ok. 10–15 USD przy jednej stale uruchomionej replice (wliczone miesięczne grupy darmowe), plus przechowywanie | Region w Polsce, natywna integracja z Visual Studio, zarządzany TLS, kopie, skalowanie, Key Vault na klucze | Złożoność (grupy zasobów, rejestr, sieci), SQLite na Azure Files jest niewygodne (potrzebna inna baza lub cache w pamięci), koszty trudne do przewidzenia, wymaga karty |
| **Cloudflare Workers + Durable Objects** | od 5 USD (plan płatny) + zużycie | Najtańsze skalowanie, wychodzący transfer bez opłat, globalny cache | **Nie uruchamia .NET**: wymagałoby przepisania serwera na TypeScript i odrzucenia `Sources\*` (łamie założenie o C# i kasuje reużycie). Polling co 10 s wymaga Durable Objects (alarmy); wiadomości WebSocket liczone jako zapytania. Zablokowanie u dostawcy |
| **Oracle Cloud Always Free** (ARM) | 0 | Darmowa mocna maszyna ARM | Konta bywają zamykane bez ostrzeżenia, brak pojemności, niepewne SLA. Co najwyżej prototyp, nie produkcja |
| ~~Serwer domowy~~ | n/d | n/d | Wykluczony decyzją D3 |

\* Ceny bez VAT, stan 2026-11-05, rozbieżne w źródłach, do potwierdzenia u dostawcy.

### Rekomendacja

1. **Produkcja: Hetzner Cloud (CAX11 lub CX23) + Cloudflare (plan darmowy) przed serwerem + Caddy lub Traefik w Docker Compose.** Powód: najniższy koszt, brak limitów połączeń, pełna kontrola nad IP i harmonogramem pytań do źródeł. Jeśli zależy Ci na polskiej lokalizacji, OVHcloud VPS to zamiennik 1:1 (ten sam obraz Dockera).
2. **Jeśli nie chcesz administrować systemem:** Fly.io (region Warszawa), koszt ok. 8 USD. Dokładnie ta sama aplikacja w kontenerze.
3. **Prototyp (E1–E3):** najtańszy VPS na miesięcznej subskrypcji. Nie przywiązuj się do dostawcy: wszystko jest w jednym kontenerze, a stan jest tylko w SQLite i można go odbudować.
4. Cloudflare Workers i Azure odrzucone na V2.0: pierwszy z powodu języka, drugi z powodu złożoności i kosztu przy stałej pracy.

## 7. Protokół wymiany danych

### Zasady

- **REST + JSON** po HTTPS (gzip, w przyszłości brotli), prefiks `/v1`. **ETag i `If-None-Match`** na każdym odczycie, `Cache-Control` z krótkim `max-age`. **SSE** tylko dla zagrożeń (jednokierunkowo, bez dwustronnego WebSocketu, prostsze dla Cloudflare i proxy). Binarny format (MessagePack) dopiero gdy pomiary pokażą potrzebę.
- **Pozycje po komórkach siatki, nie po współrzędnych klienta.** Aplikacja prosi o prostokąt dociągnięty **na zewnątrz do granic stałej siatki** (np. komórka 0,5° szer. × 1° dł.). Dzięki temu: (a) odpowiedź jest taka sama dla wszystkich i cache'owalna przez Cloudflare, (b) serwer widzi tylko komórkę, a nie dokładną pozycję, co zastępuje `CoordinateRounder` jeszcze lepiej niż dziś (nie ma co uśredniać), (c) zapytania o ten sam obszar nie mnożą pracy serwera.
- **Kontrakt wersjonowany.** Wersja API w ścieżce i w nagłówku odpowiedzi. Stare wersje aplikacji zostają na telefonach latami, więc `/v1` żyje co najmniej tak długo, jak najstarsza wspierana wersja; `GET /v1/config` zwraca minimalną wspieraną wersję aplikacji i przełączniki funkcji (kill switch).
- **Wiek danych w każdej odpowiedzi** (`generatedAt`, `ageSeconds`, `source`), żeby aplikacja pokazała, że dane są stare, zamiast udawać świeże. Ma to zastąpić obecną kartę „Stan źródeł” zasilaną ze stanów serwera.
- **Atrybucje i ostrzeżenia z warunków źródeł** (NEPTUN, PAŻP, FlyItaly, NOAA, adsb.fi) dostarcza serwer w `config`, a aplikacja je wyświetla (wymóg warunków, który nie może zniknąć przy przeniesieniu).

### Punkty końcowe

| Metoda i adres | Zastępuje dziś | Odpowiedź | Cache |
|---|---|---|---|
| `GET /v1/aircraft?box=…` | `IAircraftSource.GetAircraftAsync` | lista samolotów w komórkach pokrywających prostokąt, `generatedAt` | `max-age` 3 s, ETag |
| `GET /v1/aircraft/byhex?h=…` | `IAircraftByHexSource` | samoloty po adresach hex (zaznaczone, spoza obszaru) | 3 s |
| `GET /v1/military` | `IMilitaryAircraftSource` | wszystkie wojskowe (globalnie) | 30 s |
| `GET /v1/aircraft/{hex}` | `IAircraftLookupSource`, `AircraftEnricher` | typ, nazwa typu, producent, właściciel, rejestracja, kategoria | 1 dzień, ETag |
| `GET /v1/routes/{callsign}` | `IRouteSource` | trasa lub „nieznana” | 6 h – 24 h |
| `GET /v1/weather/metar?icao=a,b,…` i `/near?lat=&lon=&km=` | `IWeatherSource` | METAR i najbliższa stacja (liczone na serwerze) | 5 min |
| `GET /v1/weather/taf?icao=…` | `GetTafsAsync` | TAF | 30 min |
| `GET /v1/threats` i `/v1/threats/stream` (SSE) | `IThreatFeed` | lista i strumień `snapshot/upsert/remove/heartbeat` | brak |
| `GET /v1/airspace` | `IAirspaceSource` | strefy i rezerwacje, ETag po hashu | 5 min |
| `GET /v1/warnings?provinces=&categories=` | `IWarningSource` | komunikaty RSO | 5 min |
| `GET /v1/status` | `GetSourceStates` | stan każdego źródła po stronie serwera (OK, 429, awaria, wiek) | 15 s |
| `GET /v1/config` | nowe | min. wersja, przełączniki funkcji, atrybucje | 5 min |
| `POST /v1/session` | nowe | token sesji po weryfikacji instalacji (Play Integrity) | n/d |

### Uwierzytelnianie i ochrona przed nadużyciem

1. Aplikacja wysyła losowy identyfikator instalacji (GUID, bez kont, rotowany) i token Play Integrity do `POST /v1/session`; serwer zwraca krótki token (JWT, 1–24 h) wysyłany w nagłówku.
2. Limity na instalację i na IP (np. 600 zapytań/10 min na pozycje) w serwerze, grubsze filtry w Cloudflare (WAF, rate limiting).
3. Bez tokena odpowiadają tylko `/v1/config` i `/health`.
4. Zapytania o nierealne prostokąty (zbyt duże, poza obszarem pokrycia) są odrzucane 400, żeby klient nie wymusił pobrania z zewnętrznego serwisu.

### Zachowanie przy awarii

- Serwer zawsze zwraca ostatni dobry snapshot z `ageSeconds`; po przekroczeniu progu (pozycje: 60 s, METAR: 90 min) zwraca `503` z `Retry-After`.
- Klient używa istniejącego mechanizmu backoffu (`BackoffRateLimiter`) wobec **naszego** serwera, a 429 z naszego serwera pokazuje tę samą ikonę limitu.
- Alerty w tle (`AlertBackgroundRunner`) przechodzą na ten sam punkt `/v1/aircraft`; push z serwera (FCM) wymagałby wysłania reguł i lokalizacji użytkownika na serwer, więc w V2.0 **zostają lokalne**.

## 8. Prywatność, bezpieczeństwo i zgodność

- **Serwer zobaczy adres IP i komórkę siatki użytkownika.** Polityka prywatności (ROADMAP, pozycja 18) musi to opisać; formularz bezpieczeństwa danych w Play Console też. Minimalizacja: bez kont, bez logowania pozycji, logi z adresem IP kasowane po 7 dniach, brak zapisu komórek powiązanych z identyfikatorem instalacji.
- **RODO:** administrator danych to Ty. Przy hostingu w UE (Hetzner, OVH, Fly.io Warszawa) brak transferu poza EOG, ale Cloudflare przetwarza ruch poza UE na podstawie umowy powierzenia, co trzeba wpisać do polityki.
- **Klucze** (FlyItaly, w przyszłości inne) tylko w zmiennych środowiskowych serwera lub w menedżerze sekretów, nigdy w repozytorium ani w aplikacji.
- **Aktualizacje i kopie:** automatyczne aktualizacje bezpieczeństwa systemu, snapshot dobowy (ok. 20% ceny VPS-a), skrypt odtwarzania z obrazu Dockera.
- **Ostrzeżenia dotyczące bezpieczeństwa ludzi** (NEPTUN, RSO) nadal muszą mówić, że to nie jest oficjalny system. Serwer nie może ukrywać atrybucji ani podmieniać komunikatów; błąd lub opóźnienie ma być widoczne (`ageSeconds`).

## 9. Etapy i szacunek kosztów

Szacunki pracy dotyczą autora z pomocą Claude Code; przedział odzwierciedla niepewność (zwłaszcza E6 i E7). Koszty infrastruktury poniżej to **stan po zakończeniu etapu**.

| Etap | Zakres | Praca (h) | Zależności |
|---|---|---|---|
| **E0** | Zgody i decyzje: rejestracja klucza FlyItaly, maile do adsb.lol, adsb.fi, airplanes.live, adsbdb, hexdb, PAŻP, TVP/RCB z opisem serwera, decyzja o kafelkach, wybór hostingu, domena | 6–10 (własne) + 2–6 tygodni oczekiwania | Blokuje publikację E7, nie blokuje E1–E3 |
| **E1** | Kontrakt API (`AdsbCarMap.Contracts`), szkielet serwera (ASP.NET Core minimal API, health, config), Docker, CI (GitHub Actions), VPS, TLS, Cloudflare, backup | 22–32 | E0 (wybór hostingu) |
| **E2** | Pozycje: kafle i harmonogram (`PositionPoller`), limity i zapas z `Management`, snapshot w pamięci, endpoint komórek, ETag, gzip, testy | 24–32 | E1 |
| **E3** | Klient w aplikacji dla pozycji (`Sources\Server`), przełącznik serwer/źródła w ustawieniach (na czas przejścia), karta „Stan źródeł” z `/v1/status` | 8–12 | E2. **Koniec prototypu: można latać z serwerem** |
| **E4** | Trasy, dane samolotu i wojskowe: `AircraftEnricher` i `RouteCache` po stronie serwera z SQLite, endpointy, klient | 22–34 | E2 |
| **E5** | Pogoda i prognoza: `WeatherStore`/`ForecastStore` w serwerze, najbliższa stacja po stronie serwera, klient i przełączenie `AirportWeatherLoader` | 10–16 | E1 |
| **E6** | Zagrożenia (relay SSE), strefy (hash, ETag), komunikaty RSO (filtrowanie, harmonogram), klient i atrybucje z `config`; funkcje stref i RSO włączane flagą po zgodach | 30–44 | E0 (zgody), E1 |
| **E7** | Bezpieczeństwo (Play Integrity, limity), monitoring i alarmy (uptime, logi, metryki), test obciążeniowy 500 użytkowników, polityka prywatności, wydanie V2 z usunięciem bezpośrednich źródeł z aplikacji (migracja ustawień, `DataSourceSetting`, kopii zapasowej) | 30–50 | E3–E6 |
| **Razem** | | **ok. 150–230 godzin** | |

Gdyby ta praca była zlecona zewnętrznie przy przykładowej stawce 150 zł/h, odpowiadałoby to 22–35 tys. zł; to punkt odniesienia, nie koszt, który poniesiesz.

### Koszty stałe po wdrożeniu (miesięcznie)

| Pozycja | 10 użytk. | 100 użytk. | 500 użytk. |
|---|---|---|---|
| VPS (Hetzner CAX11/CX23) | ok. 4,5 EUR | ok. 4,5 EUR | 4,5–9 EUR (większy wariant dla zapasu) |
| Kopie (snapshoty, ok. 20%) | 0,9 EUR | 0,9 EUR | 1–2 EUR |
| Domena (ok. 5 EUR rocznie) | 0,4 EUR | 0,4 EUR | 0,4 EUR |
| Cloudflare (plan darmowy), monitoring (darmowe plany), FlyItaly (darmowy klucz) | 0 | 0 | 0 |
| **Razem EUR / zł** | **ok. 6 EUR / 26 zł** | **ok. 6 EUR / 26 zł** | **ok. 6–12 EUR / 26–51 zł** |
| Odpowiednik na Fly.io | ok. 8 USD / 30 zł | ok. 8 USD / 30 zł | ok. 8–12 USD / 30–45 zł |

Dodatkowe jednorazowe lub warunkowe pozycje: opłata konta dewelopera Google Play (25 USD jednorazowo, jeśli jeszcze nie zapłacona), ewentualny płatny plan danych, gdy scenariusze A i B odpadną (rozdział 4), oraz dostawca kafelków map (do ustalenia osobno, może przekroczyć koszt serwera).

**Całkowity pierwszy rok** przy 100 użytkownikach: ok. 6 EUR × 12 + domena ≈ 75 EUR (ok. 320 zł) infrastruktury plus ok. 150–230 godzin pracy. Gdy źródła nie wyrażą zgody, koszt danych to pozycja nieznana i największe ryzyko całego planu.

## 10. Ryzyka i decyzje otwarte

| Ryzyko lub decyzja | Skutek | Proponowane działanie |
|---|---|---|
| Brak zgody źródeł na redystrybucję (adsb.fi, adsb.lol, airplanes.live, hexdb, PAŻP, TVP) | Serwer publiczny nie może używać danych tych źródeł | Start na scenariuszu A (FlyItaly), flagi funkcji do czasu zgód, równolegle maile (E0) |
| Limit 300 tys./mies. FlyItaly | Brak miejsca na wzrost obszaru lub częstsze odświeżanie | Polling na żądanie (tylko przy aktywnych klientach), monitor zużycia, alarm przy 80% |
| Jedno źródło pozycji i jeden serwer (brak awaryjnego trybu bezpośredniego po D1) | Awaria = brak danych | Snapshot z wiekiem, monitoring, restart; **do rozstrzygnięcia: czy zostawić w aplikacji tryb awaryjny dla własnego użytku?** |
| Jedno IP serwera pytające adsb.lol, adsbdb, hexdb | Limity „na adres” uderzają w całość; ryzyko blokady z zakresu centrum danych | Harmonogram i limity na serwerze, cache trwały, kontakt z operatorami, w razie potrzeby drugie IP |
| Zasięg tylko Polska i sąsiedzi (jedno koło 500 km) | Użytkownik za granicą nie ma samolotów | Informacja w aplikacji; rozszerzenie dopiero w scenariuszu B |
| Nadużycie serwera przez osoby spoza aplikacji | Wyczerpanie limitów i naruszenie warunków | Play Integrity, limity, WAF (E7) |
| Zmiana warunków lub cen hostingu | Wzrost kosztu o kilkadziesiąt procent | Obraz Dockera przenośny między dostawcami, brak zależności od usług własnych dostawcy |
| Zakres: czy przenosimy kafelki | Nie w V2 | Osobna decyzja w pozycji 18 `ROADMAP.md` |

Pytania do Ciebie przed E1: (1) czy zostawiamy awaryjny tryb bezpośredni dla wersji własnej (Debug), (2) jaki obszar pokrycia na start (proponuję koło 500 km wokół Polski), (3) kto pisze maile z E0 (szkice mogę przygotować), (4) czy serwer ma być w Polsce (OVH) czy wystarczy UE (Hetzner).

## 11. Co nie zostało zweryfikowane

- Aktualne ceny VPS-ów (Hetzner, OVHcloud, Contabo), wysokość pakietu transferu Hetzner w UE i dostępność OVH w Warszawie: źródła są rozbieżne lub pochodzą z agregatorów, nie ze stron dostawców.
- Dokładna aktualna cena Fly.io (strona oficjalna pokazywała 1,94 USD za 256 MB, a artykuły 2,19 USD po podwyżce z 1 października 2026) i opłata za dedykowany IPv4.
- Limity i warunki AWC (aviationweather.gov) dla redystrybucji, limit zapytań (ok. 100/min podany z pamięci).
- Aktualna oferta płatnych planów danych (FlightAware, OpenSky komercyjny): nie sprawdzano.
- Domyślny interwał odświeżania pozycji w aplikacji (model zakłada 5 s).
- Liczba samolotów i rozmiar odpowiedzi (założenia z rozpoznania: 349 rekordów = 147 kB w `/v2/mil`), docelowo do zmierzenia na prototypie.

## Źródła

- [Hetzner: cennik i podwyżki 2026 (bitdoze)](https://www.bitdoze.com/md/hetzner-cloud-cost-optimized-plans.md), [przegląd cen chmur 2026-08](https://kimmo.suominen.com/stuff/cpc-2026-08.txt), [podwyżka czerwiec 2026](https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do)
- [Fly.io: cennik zasobów](https://fly.io/docs/about/pricing/), [Fly.io Pricing 2026](https://www.buildmvpfast.com/blog/fly-io-pricing-2026-ram-increase-vs-hetzner)
- [Railway: plany i cennik](https://docs.railway.com/reference/pricing), [Azure Container Apps vs Railway](https://sealos.io/comparison/railway-vs-azure/)
- [Cloudflare Durable Objects: cennik](https://developers.cloudflare.com/workers/platform/pricing/index.md)
- [OVHcloud VPS-1 (vpsbenchmarks)](https://www.vpsbenchmarks.com/hosters/ovhcloud/plans/vps-1), [Contabo Cloud VPS 10](https://www.vpsbenchmarks.com/hosters/contabo/plans/cloud-vps-10), [Contabo podwyżki 2026](https://pricingsaas.com/companies/contabo.main/diffs/2026Q2)
- [Przewodnik API airplanes.live](https://airplanes.live/api-guide), [adsb.lol API](https://github.com/adsblol/api), [porównanie API ADS-B (FlyItalyADSB)](https://flyitalyadsb.com/en/dati/confronto/)
- W repozytorium: `DATA_SOURCES.md` (warunki i limity źródeł), `ROADMAP.md` (pozycje 3, 6, 17, 18).

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

**Samoloty**
- Pozycje z ADS-B odświeżane domyślnie co 3 sekundy, a między odpowiedziami przesuwane na podstawie kursu i prędkości, żeby ruch był płynny.
- **Ikona zależna od rodzaju:** samolot pasażerski, szerokokadłubowy, mały odrzutowiec, turbośmigłowiec, awionetka, śmigłowiec, myśliwiec, wojskowy transportowiec, szybowiec, balon, dron, obiekt naziemny. Rodzaj wynika z kategorii nadajnika ADS-B, znacznika wojskowego i kodu typu ICAO.
- **Kolor zależny od wysokości:** szary na ziemi, a w powietrzu przejście od pomarańczowego przez żółty, zielony i niebieski do fioletowego.
- **Ślad lotu:** linia za samolotem w kolorze wysokości, coraz bardziej przezroczysta ku przeszłości.
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
- Wskaźnik wczytywania w lewym dolnym rogu, obok numeru wersji: obracające się kółko z napisem „Wczytywanie samolotów…” albo „Wczytywanie mapy…”. Działa też na telefonie.
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
| Menu (trzy kreski) | prawy górny róg: „Szukaj samolotu” i „Ustawienia” |
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

Menu → „Ustawienia”. Wartości są zapisywane na urządzeniu i obowiązują na telefonie oraz w Android Auto. Wartości spoza dozwolonego zakresu są ucinane, puste pola zostają bez zmiany.

| Ustawienie | Domyślnie | Zakres | Znaczenie |
|---|---|---|---|
| Odświeżanie samolotów [s] | 3 | 1–60 | odstęp między zapytaniami o samoloty |
| Odstęp klatek mapy [ms] | 33 | 16–1000 | tempo rysowania (33 ms to ok. 30 klatek/s) |
| Minimalny zoom | 4 | 1–18 | największe oddalenie |
| Maksymalny zoom | 13 | 1–18 | największe przybliżenie |
| Kafelki w pamięci | 128 | 16–1024 | liczba zdekodowanych kafelków w pamięci (zawsze co najmniej tyle, ile widać na ekranie plus zapas) |
| Ponowienie kafelka [s] | 15 | 1–600 | po jakim czasie ponawiane jest nieudane pobranie kafelka |
| Długość śladu [min] | 30 | 0–120 | jak długo pamiętany jest ślad lotu (0 wyłącza ślady) |
| Maks. punktów śladu | 700 | 10–5000 | limit punktów jednego śladu |
| Grubość śladu [dp] | 3 | 0,5–20 | |
| Krycie najstarszego śladu | 140 | 0–255 | |
| Krycie najnowszego śladu | 255 | 0–255 | |
| Obwódka śladu [dp] | 1 | 0–10 | ciemna obwódka pod śladem |
| Krycie obwódki | 140 | 0–255 | |
| Przyciemnienie mapy | 90 | 0–220 | krycie czarnej warstwy nad kafelkami (0 wyłącza), im więcej, tym ciemniejsza mapa |
| Obwódka samolotów [dp] | 1,2 | 0–6 | ciemna obwódka wokół ikon (0 wyłącza) |
| Etykiety samolotów | 0 | 0–1 | 0 ukrywa etykiety, 1 pokazuje (to samo robi przycisk na mapie) |
| Wielkość etykiet [dp] | 13 | 6–40 | |
| Progi kolorów wysokości [m] | 300 / 1200 / 3000 / 6000 / 12000 | 0–20000 | granice pomarańczowego, żółtego, zielonego, niebieskiego i fioletowego |

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

Numer seryjny urządzenia pokaże `adb devices`. Każdy build zwiększa numer kompilacji w `BuildNumber.txt`, który jest też `versionCode` aplikacji. Wersja wyświetlana to `0.2.<numer kompilacji>` (widoczna w lewym dolnym rogu mapy).

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
```

Mapa na telefonie i w Android Auto używa tego samego `MapController`, więc zmiany w rysowaniu dotyczą obu.

## Plany

- Lotniska na mapie (źródło: OurAirports) z kodami, informacją po dotknięciu i wyszukiwaniem lotnisk.
- Pełne nazwy typów samolotów.
- Cache kafelków na dysku.
- Przycisk izolacji w Android Auto.

## Ważna uwaga

To narzędzie do zabawy i obserwacji, **nie do nawigacji, ruchu lotniczego ani zapewnienia bezpieczeństwa**. Dane mogą być niepełne, opóźnione lub błędne. W samochodzie nie obsługuj aplikacji w czasie jazdy. Kierowca ma patrzeć na drogę, a z aplikacji korzystać powinien pasażer albo kierowca na postoju.

## Licencja

Kod jest udostępniony na licencji [MIT](LICENSE). Dane mapy i samolotów podlegają licencjom i warunkom ich dostawców (patrz wyżej).

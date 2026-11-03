# Historia wersji

Opis tego, co zostało wydane w kolejnych wersjach, od najnowszej. Wersja ma postać `0.<druga>.<trzecia>` i jest w pliku `Build/Version.txt`: przy nowym wydaniu rośnie druga cyfra, a trzecia wraca do 0 (0.3.0), przy poprawce rośnie tylko trzecia (0.3.1); decyduje o tym osoba przygotowująca wydanie. Numer kompilacji (`versionCode`) nigdy nie jest zerowany. Do wersji 0.2.163 trzecia cyfra była numerem kompilacji. Od wersji 0.2.163 każdej wersji odpowiada tag w git (`v<wersja>`) i wiersz w `Build/release-history.csv` (commit, rozmiar i SHA-256 paczki). Podsumowanie commitów dla wersji można wygenerować skryptem `Scripts\Release-Notes.ps1`. Wcześniejsze wersje nie mają tagów.

## 0.5.0 (2026-11-03)

Zmiany od wersji 0.4.0.

**Jednostki**
- Nowy dział „Jednostki” na górze ustawień z czterema niezależnymi listami: wysokość (m, ft, FL), odległość (km, mile morskie, mile lądowe), prędkość (km/h, kt, mph) i prędkość pionowa (ft/min, m/s). Każde ustawienie ma opis, gdzie się pokazuje. Karta samolotu pokazuje tylko wybraną jednostkę. Jednostki obejmują kartę i popup samolotu i lotniska, wyszukiwarkę, alerty (promień i powiadomienia) oraz filtry (granice wysokości wpisuje się w wybranej jednostce). Długość pasów przy poziomie lotu jest w stopach.

**Alerty**
- Alerty „samolot w pobliżu”: reguły z obszarem (wokół mojej pozycji, lotniska albo wybranego środka), filtrem jako warunkiem, zdarzeniami wlotu i pojawienia się oraz czasem ciszy dla każdego alertu. Powiadomienia lokalne otwierają mapę z samolotem. Tryb w tle sprawdza obszary także przy wyłączonej mapie.

**Mapa**
- Aplikacja pamięta środek, zoom i tryb podążania między uruchomieniami.
- Ikony samolotów z nowymi sylwetkami, cieniem i akcentem kokpitu. Ślady rysowane tylko dla zaznaczonych i izolowanych samolotów. Ekran startowy z ikoną aplikacji.
- Nazwa typu z wbudowanej tabeli na karcie i w wyszukiwarce, gdy źródła jej nie podają.

**Prywatność i diagnostyka**
- Zaokrąglanie położenia wysyłanego do serwerów samolotów (ustawienie „Prywatność”).
- Dev log nieprzewidzianych zdarzeń, karta „Stan źródeł” i ekran „Log zapytań”.

**Pod maską**
- Rozbicie `MapController`, nowe testy (m.in. jednostki, alerty, `RouteCache`), skrypt łączący pokrycie testów.

Tekst „Co nowego” w Google Play dla tej wersji:

```
Jednostki: osobne ustawienia wysokości, odległości, prędkości i prędkości pionowej (m, ft, FL, km, NM, mi, km/h, kt, mph, m/s).
Alerty: powiadomienie, gdy wybrany samolot wleci w obszar, także w tle.
Mapa: pamiętany widok, nowe ikony samolotów, nazwy typów.
Prywatność: zaokrąglanie położenia wysyłanego do serwerów.
```

## 0.4.0 (2026-11-02)

Zmiany od wersji 0.3.1.

**Źródła danych**
- Dwa źródła pozycji samolotów: adsb.lol i airplanes.live. Kolejność wybiera się w ustawieniach zaawansowanych na liście przeciąganej za uchwyt (pierwszy serwis jest głównym źródłem), a gdy główne źródło nie zwraca danych (limit zapytań 429, błąd sieci lub serwera), aplikacja pyta następne. Uwaga: airplanes.live odrzuca na razie zapytania aplikacji (kod 403) i prosi o kontakt e-mailowy, więc do czasu uzgodnienia głównym źródłem powinno zostać adsb.lol.
- Trasy lotów z trzech serwisów: adsbdb.com, adsb.lol i hexdb.io, z własną kolejnością na liście przeciąganej. Gdy serwis nie zna lotu albo nie odpowiada, aplikacja pyta następny. Trasę z hexdb.io, która jest samą parą lotnisk, aplikacja uzupełnia kodami IATA, miastami i współrzędnymi z własnej bazy lotnisk, żeby działał pasek postępu.
- Karta zaznaczonego samolotu pokazuje pełną nazwę typu i producenta (np. „Beechcraft King Air B350”) oraz właściciela, gdy nie ma linii lotniczej. Dane są dopytywane o ten jeden samolot w adsbdb i hexdb.io, gdy główne źródło ich nie podaje (przełącznik „Uzupełniaj dane zaznaczonego samolotu”).
- Pamięć podręczna informacji o lotach: trasy i dane samolotów są pamiętane od ostatniego zobaczenia samolotu na mapie (suwak 1–30 min, domyślnie 10), zapisują się na dysk i działają po ponownym uruchomieniu, więc aplikacja nie pyta dwa razy o ten sam lot. Lot, który wylądował, jest zapominany po około 2 minutach. Przycisk „Wyczyść teraz” w karcie „Pamięć podręczna lotów”.
- Limity zapytań (429) obsługuje osobna warstwa zarządzająca źródłami: wykładniczy backoff 5, 10, 20, 40, 60 s albo według `Retry-After`, wspólny dla wszystkich źródeł.

**Android Auto**
- Z usługą mogą się połączyć tylko znane hosty z listy w `Resources\values\allowed_hosts.xml` (Android Auto z Google Play), zamiast każdego hosta. Osobne listy dla buildów Debug i Release.

**Mapa i filtry**
- Pamięć podręczna kafelków mapy na dysku z ustawieniami rozmiaru i czasu.
- Filtry samolotów z kreatorem drzewa warunków (grupy ORAZ/LUB, NIE, przesuwanie, kopiowanie), zapisem JSON, filtrami wbudowanymi i przyciskiem z licznikiem na mapie.
- Tabela typów samolotów z kodami IATA i rodzinami oraz dziennik brakujących typów w ustawieniach.
- Wyszukiwarka znajduje wyniki bez polskich znaków („lodz” znajduje „Łódź”).

**Ustawienia**
- Ustawienia pogrupowane w zwijane sekcje, z potwierdzeniem przywracania domyślnych. W zaawansowanych nowe karty: „Źródło danych”, „Źródła tras” i „Pamięć podręczna lotów”.

**Pod maską**
- Źródła danych wydzielone do osobnych projektów bez zależności od Androida (kontrakt, zarządca, po jednym projekcie na serwis), każdy z własnymi testami. Złożenie źródeł jest w jednej testowalnej klasie. Liczba testów jednostkowych wzrosła z 253 do 575.

Tekst „Co nowego” w Google Play dla tej wersji:

```
Źródła danych:
• Dwa źródła pozycji samolotów i trzy źródła tras, z kolejnością do wyboru i zapasem przy awarii
• Pełna nazwa typu, producent i właściciel samolotu na karcie
• Pamięć lotów: aplikacja nie pyta dwa razy o ten sam lot, także po restarcie
Android Auto: połączenie tylko ze znanymi hostami.
Mapa: filtry samolotów, pamięć kafelków, wyszukiwanie bez polskich znaków.
```

## 0.3.1 (2026-11-01)

Zmiany od wersji 0.2.163. Wersja 0.3.0 nie została wydana, linia 0.3 zaczyna się od 0.3.1.

**Lotniska i pasy startowe**
- Lotniska na mapie z bazy OurAirports (około 71 tys.: duże, średnie, małe, wojskowe i helipady) z kodami IATA lub ICAO, a od zoomu 11 także z pasami startowymi. Lotniska są pod samolotami, wojskowe są czerwone, etykiety się nie nakładają.
- Dotknięcie lotniska pokazuje kartę na telefonie (kody, miasto, wysokość, odległość i kierunek od Ciebie, pasy) i popup w Android Auto. Zaznaczenie jest wspólne dla telefonu i samochodu.
- Wyszukiwarka („Wyszukaj”) szuka lotnisk razem z samolotami: po kodzie, nazwie i mieście, bez względu na polskie znaki. Lotniska są przed samolotami o takim samym dopasowaniu, a w samochodzie dwa wiersze z sześciu zostają dla samolotów.
- Ulubione lotniska: osobny ekran w menu z dodawaniem, zmianą kolejności, szczegółami i przyciskiem „Pokaż na mapie”. Ulubione są widoczne zawsze od zoomu 5 i wspólne dla telefonu i samochodu.
- Ekran „O aplikacji” z wersją, licencją, źródłami danych (OpenStreetMap, adsb.lol, adsbdb, OurAirports) i informacją o prywatności.

**Ustawienia lotnisk**
- Jedna sekcja z głównym przełącznikiem, podglądem, suwakiem wielkości kodów, przełącznikiem kodów, rodzajami lotnisk (pola wyboru), filtrem małych lotnisk z utwardzonym pasem od 800 m, kontrolką zakresu zoomu od–do, przełącznikiem „Tylko ulubione lotniska” i przyciskiem przywracania domyślnych. Po wyłączeniu lotnisk reszta sekcji jest wyszarzona.

Tekst „Co nowego” w Google Play dla tej wersji:

```
Lotniska:
• Lotniska na mapie: duże, średnie, małe, wojskowe i helipady, z kodami i pasami startowymi
• Karta lotniska na telefonie, popup w Android Auto, lotniska w wyszukiwarce
• Ulubione lotniska: osobny ekran, wspólne dla telefonu i samochodu
• Ustawienia: rodzaje, zakres zoomu, rozmiar kodów, tylko ulubione

Nowe: ekran „O aplikacji”, ikony samolotów i ślady z podglądem w ustawieniach.
```

## 0.2.163 (2026-11-01)

Zmiany od wersji 0.2.112.

**Telefon i samochód**
- Ikona stanu aplikacji w lewym dolnym rogu mapy, zastępuje wskaźnik wczytywania: wszystko działa, wyszukiwanie samolotów, pobieranie danych samolotu, wczytywanie mapy, brak połączenia z siecią, limit zapytań serwera. Brak sieci jest teraz widoczny na mapie.
- Legenda kolorów wysokości u dołu mapy.
- Płynny gradient koloru wzdłuż śladu przy zmianie wysokości.
- Jednostka wysokości do wyboru: metry (domyślnie), stopy albo poziom lotu (FL). Dotyczy etykiet, wyników wyszukiwania, karty i popupu samolotu, legendy i skali kolorów.
- Rozmiar ikon samolotów (×0,6–×2,0) i grubość ich obwódki.

**Ustawienia**
- Nowy ekran ustawień: przełączniki, lista wyboru, suwaki z możliwością wpisania dokładnej wartości, okienka „i” z opisem i przykładem.
- Edytor skali kolorów wysokości: pasek z gradientem, przeciągane progi (pierwszy próg nie niżej niż 50 m), podgląd śladu.
- Ramka „Ślady samolotów” z podglądem śladów drona, śmigłowca i samolotu pasażerskiego oraz wszystkimi ustawieniami śladu.
- Suwak wielkości liter etykiet z podglądem.
- Każda zmiana zapisuje się od razu (bez przycisku „Zapisz”), przycisk „↺” cofa zmiany krok po kroku (historia do zamknięcia aplikacji), osobne przyciski przywracają domyślne ikony, ślady i kolory oraz wszystkie ustawienia.

**Android Auto**
- Wyszukiwarka pokazuje listę samolotów od razu po otwarciu.

**Poprawki**
- Pusta mapa po powrocie z ustawień (zablokowana powierzchnia rysowania jest tworzona od nowa).

Tekst „Co nowego” w Google Play dla tej wersji:

```
Telefon i samochód:
• Ikona stanu w rogu mapy: działa, pobieranie samolotów, brak sieci, limit zapytań
• Legenda kolorów wysokości, płynny gradient koloru śladu
• Wysokość w metrach, stopach lub jako poziom lotu (FL)
• Rozmiar ikon samolotów i etykiet do wyboru

Ustawienia: suwaki z podglądem, edytor skali kolorów, ślady, cofanie zmian, zapis na bieżąco.

Android Auto: lista samolotów w wyszukiwarce od razu po otwarciu.

Poprawka: pusta mapa po powrocie z ustawień.
```

## 0.2.112 (2026-10-31)

Wersja zbudowana z commitu `a78748e` i późniejszych zmian z commitu `6de5ed3`. Tekst „Co nowego” w Google Play:

```
Android Auto:
• Okienko z danymi zaznaczonego samolotu: lot, trasa, wysokość, prędkość, odległość
• Dotknięcie samolotu na ekranie samochodu zaznacza go
• Nowy układ przycisków, aktywne tryby (myśliwce, izolacja, etykiety, centrowanie) wyróżnione ikoną
• Wskaźnik wczytywania danych

Telefon i samochód:
• Wspólne zaznaczenie, izolacja i filtr samolotów wojskowych
• Wybór samolotu na jednym ekranie centruje na nim oba

Poprawki: stabilniejsze uruchamianie w Android Auto.
```

## Funkcje dostępne od pierwszych wersji

- Mapa OpenStreetMap z płynnym zoomem, bezwładnością przesuwania, kafelkami zastępczymi i wyprzedzającym wczytywaniem.
- Samoloty z adsb.lol z ikonami według rodzaju, kolorem według wysokości, śladami i etykietami (domyślnie ukrytymi).
- Zaznaczanie samolotów dotknięciem, tryb zaznaczania wielu samolotów, izolacja, filtr samolotów wojskowych.
- Karta samolotu z parametrami lotu, odległością od użytkownika i trasą z postępem lotu.
- Okno wyszukiwania samolotów po locie, rejestracji, typie, trasie i linii lotniczej.
- Układ poziomy telefonu i zachowanie stanu przy obrocie ekranu.
- Mapa w Android Auto z tymi samymi samolotami co na telefonie.

## Znane ograniczenia

- Kafelki mapy nie są zapisywane na dysku, po uruchomieniu pobierane są od nowa.
- Pełne nazwy typów samolotów (np. Airbus A321neo) nie są jeszcze dostępne, widać kod typu.
- Lotniska na mapie, dodatkowe filtry samolotów i wybór źródła danych są w planach.

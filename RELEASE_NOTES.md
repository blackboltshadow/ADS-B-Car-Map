# Historia wersji

Opis tego, co zostało wydane w kolejnych wersjach, od najnowszej. Wersja ma postać `0.<druga>.<trzecia>` i jest w pliku `Version.txt`: przy nowym wydaniu rośnie druga cyfra, a trzecia wraca do 0 (0.3.0), przy poprawce rośnie tylko trzecia (0.3.1); decyduje o tym osoba przygotowująca wydanie. Numer kompilacji (`versionCode`) nigdy nie jest zerowany. Do wersji 0.2.163 trzecia cyfra była numerem kompilacji. Od wersji 0.2.163 każdej wersji odpowiada tag w git (`v<wersja>`) i wiersz w `release-history.csv` (commit, rozmiar i SHA-256 paczki). Podsumowanie commitów dla wersji można wygenerować skryptem `Scripts\Release-Notes.ps1`. Wcześniejsze wersje nie mają tagów.

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

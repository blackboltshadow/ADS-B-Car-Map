# ADS-B Car Map

Aplikacja na Androida, która pokazuje na mapie OpenStreetMap samoloty w powietrzu na żywo (dane ADS-B). Działa na telefonie, a także jako aplikacja nawigacyjna w Android Auto, rysowana własnym kodem na `Canvas`.

> **Status: proof of concept.** Mapa działa na telefonie i w Android Auto (sprawdzone w samochodzie i w DHU na wydaniu 0.4.0, patrz [Znane ograniczenia](#znane-ograniczenia)). Projekt jest przeznaczony do użytku własnego i instalacji z pliku (sideload), nie do Sklepu Play.

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
- Kafelki są zapisywane także na dysku (pamięć podręczna w katalogu cache aplikacji), więc mapa pojawia się od razu po uruchomieniu i działa bez zasięgu w obszarach, które były już oglądane. Świeży kafelek jest brany z dysku bez pytania serwera, starszy niż ustawiony czas jest pobierany ponownie, a gdy pobranie się nie uda, używany jest starszy kafelek z dysku. Po przekroczeniu limitu rozmiaru usuwane są najstarsze kafelki. Limit, czas trzymania, aktualne zajęcie miejsca i przycisk „Wyczyść teraz” są w ustawieniach, w sekcji „Pamięć podręczna map”.
- Przycisk „center” wraca do Twojej pozycji GPS (niebieska kropka).

**Lotniska**
- Dane z projektu OurAirports (domena publiczna), dołączone do aplikacji: około 71 tys. lotnisk i helipadów (duże, średnie, małe i helipady, bez wodnosamolotowych i zamkniętych) oraz pasy startowe. Dane odświeża skrypt `Scripts\Update-AirportData.ps1`, uruchamiany automatycznie przed każdym wydaniem.
- **Zapamiętany widok mapy:** przy zamknięciu mapy aplikacja zapisuje środek, zoom i to, czy mapa podąża za pozycją GPS, a przy następnym uruchomieniu zaczyna w tym miejscu (zoom mieści się w aktualnych ustawieniach zoomu minimalnego i maksymalnego). Zapamiętuje widok pierwsza uruchomiona mapa, telefon albo samochód.
- Lotniska rysują się pod samolotami: w zakresie zoomu wybranym jedną kontrolką od–do (domyślnie od 5, bez górnego limitu), wspólnym dla wszystkich rodzajów; pola „Rodzaje lotnisk na mapie” wybierają, które rodzaje są widoczne. Znacznik jest większy dla większego lotniska, lotniska wojskowe są czerwone. Obok znacznika jest kod IATA (a gdy go brak, ICAO), a etykiety, które by się nakładały, są pomijane od tych mniej ważnych. Od zoomu 11 widać pasy startowe.
- Dotknięcie lotniska (gdy pod palcem nie ma samolotu) zaznacza je: na telefonie pojawia się karta, a w Android Auto popup, z nazwą, kodami IATA i ICAO, miastem i krajem, wysokością w wybranej jednostce, odległością i kierunkiem od Ciebie oraz najdłuższymi pasami. Karta ma przycisk „Wyśrodkuj”. Zaznaczenie lotniska jest wspólne dla telefonu i samochodu.
- Wyszukiwarka (telefon i Android Auto) szuka też lotnisk po kodzie, nazwie i mieście (bez względu na polskie znaki). Wyniki są uporządkowane według dopasowania, a przy takim samym dopasowaniu lotniska są przed samolotami. W samochodzie dwa wiersze z sześciu są zawsze zarezerwowane dla samolotów. Przy pustym polu na górze są trzy najbliższe lotniska. Wybór lotniska zaznacza je, centruje na nim mapę i przybliża do zoomu 11.
- **Ulubione lotniska:** osobny ekran w menu („Ulubione lotniska”). Ulubione są zawsze widoczne na mapie od zoomu 5, bez względu na wybrane rodzaje i filtr małych lotnisk, ze złotym pierścieniem i etykietą o najwyższym priorytecie. Dodajesz je gwiazdką na karcie lotniska, przyciskiem „＋” w wynikach wyszukiwania na tym ekranie albo w oknie szczegółów. Lista pozwala zmieniać kolejność (▲ ▼), usuwać (✕) i po dotknięciu wiersza zobaczyć szczegóły lotniska z przyciskiem „Pokaż na mapie”. W wyszukiwarce ulubione są przed innymi lotniskami o takim samym dopasowaniu, a przy pustym polu stoją na początku. Lista jest wspólna dla telefonu i samochodu (w samochodzie popup oznacza ulubione, zarządzanie jest na telefonie) i jest zapisana osobno od ustawień: „Przywróć wszystkie domyślne” jej nie kasuje.
- Ekran „O aplikacji” (menu) zawiera wersję, licencję, źródła danych z atrybucją (OpenStreetMap, adsb.lol, airplanes.live, adsbdb, hexdb.io, OurAirports) i informację o prywatności. Atrybucja „© OpenStreetMap” zostaje też na mapie.

**Samoloty**
- Pozycje z ADS-B odświeżane domyślnie co 3 sekundy, a między odpowiedziami przesuwane na podstawie kursu i prędkości, żeby ruch był płynny.
- **Ikona zależna od rodzaju:** samolot pasażerski, szerokokadłubowy, mały odrzutowiec, turbośmigłowiec, awionetka, śmigłowiec, myśliwiec, wojskowy transportowiec, szybowiec, balon, dron, obiekt naziemny. Rodzaj wynika z kategorii nadajnika ADS-B, znacznika wojskowego i kodu typu ICAO.
- **Kolor zależny od wysokości:** szary na ziemi, a w powietrzu przejście od pomarańczowego przez żółty, zielony i niebieski do fioletowego.
- **Ślad lotu:** rysowany tylko dla zaznaczonych i wyizolowanych samolotów (pozostałe nie mają śladu, ale historia jest zbierana dla wszystkich, więc ślad pojawia się od razu po zaznaczeniu). Linia za samolotem w kolorze wysokości, coraz bardziej przezroczysta ku przeszłości. Przy zmianie wysokości kolor przechodzi płynnie (gradient wzdłuż każdego odcinka śladu).
- **Legenda wysokości:** pasek z gradientem kolorów u dołu mapy (telefon i Android Auto), z progami z ustawień: od 0 m, przez pomarańczowy, żółty, zielony i niebieski, do fioletowego (domyślnie od 12 km). Progi są rozmieszczone równomiernie, bo kolory przechodzą liniowo między nimi.
- **Etykieta:** znak wywoławczy i poziom lotu (np. `LOT281 FL350`), a pod spodem typ i trasa (np. `B738 WAW→LHR`). Etykiety są domyślnie **ukryte** (mapa jest wtedy czytelniejsza, a aplikacja nie pobiera tras dla każdego samolotu na ekranie). Włączysz je przyciskiem z ikoną etykiety pod „M” albo w ustawieniach; wybór jest zapamiętywany i obowiązuje też w Android Auto. Szczegóły samolotu zawsze widać na jego karcie po dotknięciu.
- Trasa lotu jest pobierana w tle (po jednym zapytaniu na sekundę) z jednego z trzech serwisów: adsbdb.com (podaje też nazwę linii lotniczej), adsb.lol lub hexdb.io. Kolejność wybiera się w ustawieniach zaawansowanych (lista do przeciągania), a gdy serwis nie zna lotu albo nie odpowiada, aplikacja pyta następny. Trasę z hexdb.io, która jest samą parą lotnisk, aplikacja uzupełnia kodami IATA, miastami i współrzędnymi z własnej bazy lotnisk.

**Karta samolotu**
- Po dotknięciu samolotu u dołu mapy pojawia się karta (odświeżana co sekundę): lot, rejestracja, linia lotnicza, typ i rodzaj, trasa z lotniskami wylotu i przylotu oraz **pasek postępu lotu**.
- Pod paskiem: ile procent trasy przeleciano, ile kilometrów zostało do celu i szacowany czas przylotu (z prędkości względem ziemi, więc to przybliżenie).
- Parametry lotu: wysokość (ft i m), prędkość (kt i km/h), kurs z kierunkiem świata, prędkość wznoszenia lub opadania, squawk oraz wiek danych.
- **Odległość i kierunek od Ciebie** (np. „34,2 km, NE (47°)”), liczone z pozycji GPS.
- Przycisk „Śledź” wyśrodkowuje mapę na samolocie, a „✕” zamyka kartę i czyści zaznaczenie. Przy kilku zaznaczonych samolotach karta pokazuje ostatnio dotknięty, a przy jego nazwie jest liczba pozostałych.
- Dane trasy i postęp są dostępne tylko dla lotów, które zna któryś z trzech serwisów tras. Pokrycie każdego jest niepełne: w próbie testowej część znaków wywoławczych (np. `WUK6351`) nie miała trasy. Wtedy karta pokazuje pozostałe dane, bez paska postępu.
- Karta mówi wprost, dlaczego trasy nie ma: „pobieranie…” (czeka w kolejce lub jest pobierana), „żadne źródło nie zna tego lotu” albo „nie udało się pobrać (błąd sieci lub limit zapytań)”. W każdym z tych przypadków jest przycisk („Pobierz teraz”, „Sprawdź ponownie”, „Spróbuj ponownie”), który wymusza pobranie trasy w pierwszej kolejności. Trasa samolotu pokazanego na karcie jest też pobierana poza kolejką, bez czekania na pozostałe.

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

**Filtry samolotów (osobny ekran)**
- Pod przyciskiem wojskowych jest przycisk **z lejkiem**. Dotknięcie otwiera listę filtrów. Gdy jakiś filtr jest włączony, przycisk świeci na niebiesko, a pomarańczowa plakietka w rogu pokazuje, ile filtrów jest włączonych (bez filtrów plakietki nie ma). Przycisk jest tylko na telefonie; w Android Auto filtry działają, ale nie ma ich wskaźnika.
- Ekran **Filtry samolotów** otwiera się też z menu mapy (przycisk menu, pozycja pod „Ulubione lotniska”). To lista filtrów z przełącznikiem włącz/wyłącz przy każdym. Filtrów nie ma w ustawieniach, a „Przywróć wszystkie domyślne” ich nie usuwa. Są zapamiętywane między uruchomieniami, osobno od ustawień.
- Wbudowane filtry (tylko do włączania i kopiowania): **Wojskowe** (pokazuje tylko samoloty oznaczone jako wojskowe), **Ukryj na ziemi** oraz **Do Rzeszowa** (pokazuje tylko loty, których lotniskiem docelowym jest Rzeszów: RZE lub EPRZ). Dotknięcie wbudowanego filtru (albo ikony oka) otwiera go w kreatorze **tylko do odczytu**: widać nazwę, tryb i wszystkie warunki, ale nic nie da się zmienić, a przycisk „Kopiuj do własnych” tworzy własną kopię do edycji.
- **Kreator filtru** (przycisk „＋ Nowy filtr” albo ikona ołówka przy własnym filtrze; obok jest ikona kosza, która po potwierdzeniu usuwa filtr): nazwa, tryb (**pokaż** tylko pasujące samoloty albo **ukryj** pasujące) i **drzewo warunków** bez praktycznego limitu zagnieżdżeń (zapisać można do 100 poziomów). Warunek to jedno sprawdzenie, np. lotnisko docelowe, linia (początek znaku wywoławczego, np. LOT), producent, typ (kod ICAO lub IATA, np. `B738` albo `738`), rodzaj samolotu, wojskowy, na ziemi, wysokość od/do w metrach albo lotnisko wylotu (kod IATA/ICAO lub miasto, bez względu na wielkość liter i znaki diakrytyczne). **Grupa** łączy swoje elementy przez **ORAZ** (wszystkie) albo **LUB** (którykolwiek) i może zawierać warunki oraz inne grupy; przyciski „＋ warunek” i „＋ grupa” na końcu każdej grupy dodają elementy. Każdy warunek i każda grupa ma przełącznik **NIE**: po włączeniu cały element robi się czerwony, a jego wynik jest odwrócony (dla grupy odwrócona jest cała grupa). Pasek po lewej ma inny kolor na każdym poziomie, wcięcie rośnie do sześciu poziomów (głębiej przy grupie pojawia się numer poziomu), a strzałka ▾/▸ zwija grupę do jednej linii z jej treścią. Menu ⋮ elementu: przesuń wyżej, przesuń niżej, przenieś do grupy… (lista grup do wyboru), kopiuj i usuń (grupa z zawartością wymaga potwierdzenia). Na dole kreatora są dwie linie na żywo: **Opis** (z nawiasami, np. „Pokaż: (typ B744 ORAZ linia LOT ORAZ NIE na ziemi) LUB (typ B744 ORAZ linia CHG)”) i **Czytaj** (zdanie po polsku, np. „Pokazuje każdy samolot, który pochodzi od producenta Boeing oraz nie leci do lotniska WAW oraz nie leci do lotniska KRK.”; negacje grup są w nim rozpisane na warunki, a dla filtrów zagnieżdżonych głębiej niż cztery grupy zdania nie ma). Błędy (brak nazwy, pusty warunek, pusta grupa, wysokość „od” większa niż „do”) są pokazane komunikatem, a element, którego dotyczą, jest rozwijany, obramowany na żółto i przewijany na ekran; filtr się wtedy nie zapisuje. Nowy filtr jest zapisany jako wyłączony; włącza się go na liście. Wynik filtra zależy tylko od struktury drzewa, nie od kolejności elementów w grupie: warunki wymagające trasy (lotnisko) są liczone na końcu grupy, żeby nie pobierać tras dla samolotów już rozstrzygniętych prostszymi warunkami.
- **Typ samolotu w filtrze** można wpisać jako kod ICAO (`B738`) albo IATA (`738`, `73H`, `32N`); kod IATA jest zamieniany na kody ICAO według tabeli typów (`Assets/AircraftTypes/aircraft-types.tsv`: kod IATA, kod ICAO, rodzina, pełna nazwa). Pole **„Cała rodzina”** w warunku typu rozszerza dopasowanie na wszystkie typy rodziny z tabeli: `738` z tym polem obejmuje każde 737, także MAX, a `320` całą rodzinę A320 z wersjami neo. Nazwę rodziny (np. `B737`) też można wpisać. Bez tego pola pasuje tylko dokładny typ (z jego wariantami IATA). Typ spoza tabeli działa nadal jako dokładny kod ICAO, ale nie ma dla niego aliasu IATA ani rodziny. Starsze filtry bez tego pola są odczytywane jako „dokładnie”. Tabela ma około 200 typów (liniowce, regionalne, biznesowe, śmigłowce, wojskowe) i jest uzupełniana ręcznie; kody IATA, których nie jestem pewien, zostały puste.
- **Nazwy typów bez zapytań**: gdy źródła danych nie podały pełnej nazwy typu, karta samolotu, opis w wyszukiwarce i samo wyszukiwanie używają nazwy z wbudowanej tabeli typów (np. `A21N` jako „Airbus A321neo (A21N)”), więc samolot można znaleźć wpisując „airbus neo”. Działa offline, także dla niezaznaczonych samolotów.
- **Brakujące typy**: aplikacja zapamiętuje typy samolotów widziane w ruchu, których nie ma w tabeli typów (z przykładowym znakiem wywoławczym, rejestracją i kategorią oraz liczbą samolotów i czasem pierwszego i ostatniego zobaczenia), żeby wiadomo było, co dopisać. Lista jest w ustawieniach, w dziale **Dev**, w karcie „Dane developerskie” (przycisk „Brakujące typy samolotów (N)”). Ekran ma przyciski „Kopiuj listę” (tekst rozdzielany tabulatorami, gotowy do wklejenia do arkusza) i „Wyczyść listę”. Lista zajmuje najwyżej 2000 typów, jest zapisywana w prywatnym pliku aplikacji `missing-aircraft-types.tsv` nie częściej niż co 30 sekund i nigdzie nie jest wysyłana. Liczba oznacza samoloty zauważone od uruchomienia aplikacji, więc ten sam samolot widziany po restarcie liczy się ponownie. Samoloty bez kodu typu nie trafiają na listę. Typy, które po aktualizacji tabeli są już w niej opisane, znikają z listy same (przy otwarciu ekranu i przy kolejnych pobraniach samolotów).
- **Dev log**: dziennik nieprzewidzianych zdarzeń, który pozwala przekazać twórcy, co w aplikacji nie działa jak powinno. Trafiają do niego nieobsłużone wyjątki (także z Android Auto), nieudane pobrania (samoloty, trasy, kafelki, wyszukiwanie), odpowiedzi 429 i odmowy źródeł, błędy odczytu danych aplikacji oraz, na poziomie „Wszystko”, przełączenia na zapasowe źródło. Log zbiera się zawsze, a ekran pojawia się w menu głównym pod „O aplikacji” dopiero po włączeniu przełącznika „Pokaż Dev log w menu” (ustawienia, dział **Dev**, karta „Dev log”). Karta ma też ustawienia: **poziom logowania** (tylko błędy / błędy i ostrzeżenia, domyślnie / wszystko), **długość logu** (100, 250, 500, 1000 lub 2000 wpisów; po przekroczeniu najstarsze są usuwane) i **łączenie powtarzających się wpisów** (identyczne zdarzenie zwiększa licznik zamiast tworzyć nowy wpis; przy wyłączonym każde zdarzenie jest osobnym wpisem). Ekran „Dev log” pokazuje wpisy od najnowszych (kolor poziomu, licznik, czas pierwszego i ostatniego wystąpienia, po dotknięciu stos wyjątku) i ma przyciski „Udostępnij” (plik tekstowy przez systemowe okno udostępniania), „Kopiuj” (cały raport do schowka) i „Wyczyść”. Raport zaczyna się od opisu: wersja i kompilacja, urządzenie i wersja Androida, kolejność źródeł, ustawienia logu. Log jest zapisywany w prywatnym pliku `dev-log.tsv` kilka sekund po zmianie, a przy nieobsłużonym wyjątku od razu, więc przetrwa zamknięcie aplikacji. Nigdzie nie jest wysyłany automatycznie; do wysłania trafia tylko to, co sam udostępnisz.
- **Stan źródeł**: karta w Zaawansowane → „Stan źródeł” pokazuje dla każdego źródła samolotów (adsb.lol, airplanes.live) i tras (adsbdb.com, adsb.lol, hexdb.io), w kolejności z ustawień, czy da się je teraz odpytać albo na ile sekund jest wstrzymane po 429 (odliczanie co sekundę), które źródło dało ostatnią odpowiedź („ostatnio użyte”) oraz ostatni błąd z kodem HTTP (np. 403 lub 429) i czasem, jaki od niego upłynął. Błąd znika po pierwszej udanej odpowiedzi tego źródła. Karta nie wysyła żadnych zapytań, a przy wyłączonym zapasowym źródle kolejne źródła pokazują się jako dostępne, choć nie są odpytywane.
- **Log zapytań**: przycisk „Pokaż log zapytań” na karcie „Stan źródeł” otwiera ekran z zapytaniami, które aplikacja wysłała na zewnątrz (pozycje samolotów, trasy, dane samolotu, kafelki mapy): metoda, host, kod odpowiedzi albo opis błędu sieci, czas trwania, ścieżka z zapytaniem i godzina. Filtr „Tylko błędy” zostawia odpowiedzi 4xx/5xx, brak odpowiedzi i przekroczenia czasu (zapytania anulowane przez samą aplikację, np. po przesunięciu mapy, nie są błędami), a lista hostów ogranicza widok do jednego serwera. Log trzyma ostatnie 500 zapytań tylko w pamięci i znika po zamknięciu aplikacji. Ścieżka zawiera wysyłane współrzędne (po zaokrągleniu z ustawień prywatności). Kafelki mogą szybko wypełnić log, wtedy pomaga filtr hosta.
- Zasady łączenia włączonych filtrów: samolot ukrywa każdy włączony filtr „ukryj”. Jeśli włączony jest co najmniej jeden filtr „pokaż”, widoczne są tylko samoloty pasujące do któregokolwiek z nich (np. „Wojskowe” razem z „Do Rzeszowa” pokazuje obie grupy). Zaznaczony samolot jest zawsze widoczny, a izolacja ma pierwszeństwo przed filtrami.
- Filtry działają na telefonie i w Android Auto, ale w samochodzie nie ma elementów do ich włączania, więc ustawia się je na telefonie. Przycisk wojskowych na mapie to osobny, szybki tryb niezależny od filtru „Wojskowe”.
- Filtry z lotniskiem (np. „Do Rzeszowa”) wymagają trasy lotu, której nie ma w danych ADS-B. Aplikacja pobiera ją w tle z serwisów tras (adsbdb.com, adsb.lol, hexdb.io), jedną trasę na sekundę (kolejka do 200 lotów), więc po włączeniu filtra mapa może być pusta przez kilkadziesiąt sekund, a samoloty bez znanej trasy nie są pokazywane. Przy gęstym ruchu pełne zapełnienie trwa kilka minut.
- Brak danych (np. nieznana jeszcze trasa, brak kodu typu) daje wynik „nieznany”, który nigdy nie pokazuje ani nie ukrywa samolotu. Dlatego warunek „NIE lotnisko docelowe WAW” nie przepuści samolotu o nieznanej trasie.
- Tabela producentów (`AircraftManufacturers`) przyporządkowuje kody typów ICAO do producentów (Boeing, Airbus, Embraer, Cessna i inni). Kody spoza tabeli mają producenta „nieznany”.
- **Alerty** (menu → „Alerty”): reguły, które pokazują powiadomienie na telefonie, gdy samolot spełniający wybrany filtr wleci w obszar („wlot”) albo pojawi się w nim już w środku, np. po starcie („pojawienie się”). Obszar to okrąg: wokół mojej pozycji GPS, wokół lotniska (kod ICAO lub IATA, także z ulubionych) albo wokół wybranego środka (współrzędne wpisane ręcznie, ze środka pobieranych danych lub z mojej pozycji), o promieniu od 1 do odpowiednika 300 km (w jednostce odległości z ustawień, suwak liczy pełne jednostki). Warunek to jeden z zapisanych filtrów samolotów (w tym wbudowanych; z filtra bierze się wyrażenie, niezależnie od tego, czy filtr pokazuje, czy ukrywa) albo „Każdy samolot”, a przycisk „Nowy filtr” otwiera kreator filtrów. Każdy alert ma przełączniki zdarzeń i czas ciszy o tym samym samolocie (domyślnie 30 min, 0 wyłącza ciszę); cisza dotyczy tylko tego alertu, więc ten sam samolot, który spełni warunki innego alertu, wywoła jego powiadomienie. Przycisk „Sprawdź, ile samolotów pasuje teraz” liczy pasujące samoloty w ostatnio pobranych danych. Dotknięcie powiadomienia otwiera mapę z zaznaczonym samolotem. Przełącznik na górze ekranu wyłącza wszystkie alerty. Alert, którego filtr usunięto, jest nieaktywny. **Tryb w tle** (przełącznik na ekranie „Alerty”, domyślnie wyłączony): usługa ze stałym powiadomieniem sama pobiera samoloty wokół obszarów alertów, niezależnie od tego, co pokazuje mapa, i sprawdza je co kilkadziesiąt sekund. Częstotliwość (Ustawienia → Alerty, suwak 30–600 s, domyślnie 60 s) i maksymalna liczba alertów sprawdzanych w tle (suwak 1–20, domyślnie 5) są zwykłymi ustawieniami aplikacji. Obszary leżące blisko siebie dzielą jedno zapytanie. Zapytania idą przez te same źródła, zaokrąglanie współrzędnych i limit zapytań co mapa. Pod przełącznikiem widać stan trybu w tle. Reguły są zapisane w osobnym pliku ustawień i nie znikają po zresetowaniu ustawień, a obszary nie są nigdzie wysyłane.
**Android Auto**
- Mapa z samolotami na ekranie samochodu, przyciski przybliżania i oddalania, centrowanie na pozycji, przesuwanie, wyszukiwarka samolotów.
- Górny pasek akcji (od lewej): **filtr samolotów wojskowych** (ikona myśliwca), **izolacja** zaznaczonego samolotu, **przycisk etykiet** (pokazuje lub ukrywa etykiety wszystkich samolotów, domyślnie ukryte; to samo ustawienie co na telefonie) i **wyszukiwarka**. Włączony tryb jest pokazany osobną ikoną: zieloną i z obwódką (host Android Auto ignoruje kolor nadany ikonie i pozwala na kolorowe tło tylko jednego przycisku w pasku, dlatego obwódka zostaje widoczna nawet wtedy, gdy host sam wybarwi ikonę): filtr wojskowych, centrowanie (gaśnie po ręcznym przesunięciu mapy), etykiety i izolacja. Krótki komunikat na ekranie mówi, co się włączyło.
- Pasek mapy po prawej: przesuwanie, przybliżanie, oddalanie i **centrowanie na pozycji GPS** (przy przyciskach zoomu; świeci, gdy mapa podąża za pozycją lub samolotem).
- Ikona stanu aplikacji w lewym dolnym rogu, zawsze widoczna, przed numerem wersji (telefon i Android Auto): zielony znak ✓ – wszystko działa; niebieski radar z napisem „Wyszukiwanie samolotów…” – pobieranie listy samolotów; niebieska lupka z napisem „Pobieranie danych samolotu…” – pobieranie trasy zaznaczonego samolotu; niebieskie kółko z napisem „Wczytywanie mapy…” – pobieranie kafelków; czerwony wykrzyknik z napisem „Brak połączenia z siecią – samoloty niedostępne” – ostatnie pobranie samolotów się nie udało (np. brak zasięgu); pomarańczowa pauza z licznikiem – limit zapytań (429).
- Ekran startowy: przy zimnym starcie telefon pokazuje ciemne tło mapy z ikoną aplikacji zamiast białego błysku (na Androidzie 12 i nowszym to systemowy ekran startowy). Do czasu pierwszej odpowiedzi z serwera samolotów na środku mapy widać napis „Wczytywanie…” (telefon i Android Auto).
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
| Menu (trzy kreski) | prawy górny róg: „Wyszukaj”, „Ulubione lotniska”, „Ustawienia” i „O aplikacji” (oraz „Dev log”, gdy włączono go w ustawieniach) |
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

Menu → „Ustawienia”. Wartości są zapisywane na urządzeniu i obowiązują na telefonie oraz w Android Auto. Przy ustawieniach liczbowych, przełącznikach i liście jednostki jest okrągły przycisk „i”, który otwiera okienko z opisem, co dokładnie robi to ustawienie, wraz z przykładem (etykieta w bieżącej wielkości, pasek legendy z bieżącymi progami, ta sama wartość we wszystkich dostępnych jednostkach). Liczby wpisuje się w polach tekstowych, ustawienia włącz/wyłącz zmienia się przełącznikami, a wybór jednostki listą rozwijaną. Wartości spoza dozwolonego zakresu są ucinane, puste pola zostają bez zmiany. Ustawienia są pogrupowane w zwijane działy, domyślnie zwinięte: **Język / Language** (polski lub angielski, zmiana działa od razu na telefonie i w Android Auto), **Jednostki** (cztery niezależne listy: wysokość, odległość, prędkość i prędkość pionowa), **Wygląd** (Samoloty: ikony i etykiety, ślady, kolory wysokości ze skalą i legendą; Lotniska; Mapa: zoom i przyciemnienie), **Zaawansowane** (źródła danych, karta „Prywatność”, odświeżanie, pobieranie kafelków, pamięć podręczna map, „Przywróć wszystkie domyślne”) i **Dev** (wersja, karta „Dev log” z ustawieniami dziennika zdarzeń, karta „Prywatność (zaawansowane)” i brakujące typy samolotów). Wewnątrz działów karty też się zwijają; dział i kartę rozwija strzałka ▾/▴ albo dotknięcie nagłówka, a stan jest pamiętany do zamknięcia aplikacji, także po obrocie ekranu. Każdy przycisk „Przywróć domyślne…” najpierw pyta o potwierdzenie: okienko ma przyciski „Anuluj” i „Przywróć domyślne wartości”, a ten drugi jest nieaktywny przez pierwsze 10 sekund (z odliczaniem w napisie).

| Ustawienie | Domyślnie | Zakres | Znaczenie |
|---|---|---|---|
| Odświeżanie samolotów [s] | 3 | 1–60 | odstęp między zapytaniami o samoloty |
| Odstęp klatek mapy [ms] | 33 | 16–1000 | tempo rysowania (33 ms to ok. 30 klatek/s) |
| Minimalny zoom | 4 | 1–18 | największe oddalenie |
| Maksymalny zoom | 13 | 1–18 | największe przybliżenie |
| Kafelki w pamięci | 128 | 16–1024 | liczba zdekodowanych kafelków w pamięci (zawsze co najmniej tyle, ile widać na ekranie plus zapas) |
| Ponowienie kafelka [s] | 15 | 1–600 | po jakim czasie ponawiane jest nieudane pobranie kafelka |
| Kolejność źródeł samolotów | adsb.lol, airplanes.live | wszystkie kolejności źródeł | lista przeciągana za uchwyt ≡ (Zaawansowane → Źródło danych): wiersz na górze (GŁÓWNE) to główne źródło, wiersze pod nim (ZAPASOWE 1, 2…) są pytane po kolei, gdy poprzedni nie zwraca danych; zapasowe są przyciemnione, gdy zapas jest wyłączony; zmiana działa od następnego odświeżenia |
| Kolejność źródeł tras | adsbdb.com, adsb.lol, hexdb.io | wszystkie kolejności źródeł | lista przeciągana za uchwyt ≡ (Zaawansowane → Źródła tras): pierwszy serwis to główne źródło tras, kolejne są pytane, gdy poprzedni nie zna lotu albo nie odpowiada; zmiana działa od następnego pobrania trasy |
| Zapasowe źródło przy braku danych | włączone | wł./wył. | gdy główne źródło nie zwraca danych (429, błąd sieci lub serwera), aplikacja pyta kolejne według kolejności; dotyczy samolotów i tras (przy trasach także gdy źródło nie zna lotu) |
| Uzupełniaj dane zaznaczonego samolotu | włączone | wł./wył. | gdy główne źródło nie podaje typu, rejestracji lub pełnej nazwy typu zaznaczonego samolotu, aplikacja dopytuje o ten jeden samolot (po kodzie ICAO) adsbdb.com, hexdb.io, a potem pozostałe źródła, i uzupełnia typ, pełną nazwę typu, producenta, właściciela i rejestrację, niczego nie nadpisując (nie częściej niż raz na czas pamiętania lotu) |
| Dokładność wysyłanego położenia | około 1 km | lista wyboru | Zaawansowane → Prywatność: dokładne, około 1, 5, 10 albo 25 km. Środek zapytania o samoloty jest przyciągany do siatki o tym rozmiarze, więc serwer widzi okolicę, a nie dokładne miejsce (przy śledzeniu GPS to Twoje położenie). Wynik zależy tylko od położenia (bez losowego szumu), więc wielu zapytań nie da się uśrednić. Promień rośnie o największe możliwe przesunięcie środka, żeby widoczny obszar był w całości pokryty; im większa siatka, tym większa odpowiedź serwera. Zmiana działa od następnego odświeżenia |
| Ustawienia Dev nadpisują ustawienia użytkownika | wyłączone | przełącznik | Dev → Prywatność (zaawansowane): gdy włączone, o zaokrąglaniu decyduje wyłącznie ta karta (siatka, promień, powiększanie, logowanie), a wybór z Zaawansowane → Prywatność jest ignorowany; gdy wyłączone, pozostałe ustawienia karty są wyszarzone i nie mają wpływu |
| Własny rozmiar siatki [km] | 5 | 0–50 | Dev: suwak co 1 km, działa tylko przy włączonym nadpisywaniu; 0 oznacza dokładne położenie |
| Zaokrąglanie promienia w górę | bez zaokrąglania | lista wyboru | Dev: zaokrągla promień zapytania w górę do 10, 20 albo 50 km, żeby serwer nie widział dokładnego poziomu przybliżenia |
| Powiększaj promień o błąd zaokrąglenia | włączone | przełącznik | Dev: wyłączenie pokazuje działanie bez pokrycia widocznego obszaru (przy brzegu ekranu mogą brakować samolotów) |
| Loguj wysyłane współrzędne w Dev logu | wyłączone | przełącznik | Dev: każde zapytanie o samoloty trafia do Dev logu (poziom „Wszystko”) z prawdziwym położeniem i położeniem wysłanym do serwera; przy włączeniu nie udostępniaj logu |
| Limit pamięci podręcznej [MB] | 100 | 10–1000 | maksymalny rozmiar kafelków na dysku, suwak co 10 MB; zmniejszenie limitu od razu usuwa najstarsze kafelki |
| Czas pamiętania lotu po ostatnim zobaczeniu [min] | 10 | 1–30 | jak długo po ostatnim zobaczeniu samolotu na mapie aplikacja pamięta trasę lotu (po znaku wywoławczym) i dane samolotu uzupełnione z innych źródeł (typ, rejestracja), także po ponownym uruchomieniu (Zaawansowane → Pamięć podręczna lotów). Dopóki samolot jest widoczny, jego dane są odświeżane i nie są pobierane ponownie, nawet gdy leci wiele godzin; zniknięty samolot jest zapominany po tym czasie, a samolot, który właśnie wylądował, po ok. 2 minutach. Odpowiedź „serwer nie zna tego lotu” jest pamiętana krócej (trasy do 6 godzin, dane samolotu do 10 minut, nigdy dłużej niż wybrany czas). Przycisk „Wyczyść teraz” zapomina wszystko |
| Czas trzymania kafelków [dni] | 7 | 1–90 | przez ile dni kafelek z dysku jest świeży i używany bez pytania serwera; starszy służy jako zapas, gdy pobranie się nie uda |
| Długość śladu [min] | 30 | 0–120 | jak długo pamiętany jest ślad lotu (0 wyłącza ślady), suwak co 1 min |
| Maks. punktów śladu | 700 | 10–5000 | limit punktów jednego śladu, suwak co 10 |
| Grubość śladu [dp] | 3 | 0,5–20 | suwak co 0,5 dp |
| Krycie najstarszego śladu | 140 | 0–255 | suwak co 5 |
| Krycie najnowszego śladu | 255 | 0–255 | suwak co 5 |
| Obwódka śladu [dp] | 1 | 0–10 | ciemna obwódka pod śladem, suwak co 0,5 dp |
| Krycie obwódki | 140 | 0–255 | |
| Przyciemnienie mapy | 90 | 0–220 | krycie czarnej warstwy nad kafelkami (0 wyłącza), im więcej, tym ciemniejsza mapa |
| Rozmiar ikon samolotów | ×1,0 | ×0,6–×2,0 | suwak co 0,1, mnoży rozmiar wszystkich ikon, kółek zaznaczenia i śledzenia oraz odsunięcie etykiety, na telefonie i w Android Auto. Grubość obwódki nie zmienia się z rozmiarem ikony. |
| Obwódka samolotów [dp] | 1,2 | 0–6 | suwak co 0,1 dp (0 wyłącza obwódkę). Oba suwaki są w jednej ramce „Ikony samolotów” po skali kolorów wysokości, nad suwakami jest podgląd wszystkich ikon samolotów w bieżącym rozmiarze i grubości obwódki. |
| Cień samolotów [%] | 40 | 0–100 | suwak co 5 % (0 wyłącza cień), miękki cień ikony w prawo i w dół, odsuwany proporcjonalnie do rozmiaru ikony; nie dotyczy znacznika obiektu naziemnego. |
| Akcent kabiny [%] | 60 | 0–100 | suwak co 5 % (0 wyłącza akcent), ciemna szyba kokpitu na ikonie samolotu, śmigłowca i drona, pokazuje kierunek lotu przy małych ikonach. Balon, obiekt naziemny i strzałka nie mają kabiny. Oba suwaki są w ramce „Ikony samolotów”, podgląd ikon pokazuje je na bieżąco. |
| Etykiety samolotów | wyłączone | przełącznik | pokazuje lub ukrywa etykiety (to samo robi przycisk na mapie) |
| Lotniska na mapie | włączone | przełącznik | pokazuje lotniska i pasy startowe |
| Tylko ulubione lotniska | wyłączone | przełącznik | pod głównym przełącznikiem: pokazuje na mapie wyłącznie ulubione lotniska, przy każdym zoomie. Rodzaje lotnisk, filtr małych lotnisk i zakres zoomu są wtedy wyszarzone, a podgląd pokazuje wszystkie rodzaje jako ukryte. |
| Rodzaje lotnisk na mapie | duże, średnie, wojskowe | pola wyboru | zaznaczasz, które rodzaje widzisz: duże, średnie, małe, wojskowe (czerwone, każdej wielkości) i helipady (jasnoniebieskie, domyślnie wyłączone). Zakres zoomu nie zależy od rodzaju. Małych lotnisk jest najwyżej 60, a helipadów 40 na klatkę (najbliższe środka ekranu). Pola zastąpiły dawny wybór zakresu z listy. |
| Małe: tylko z utwardzonym pasem od 800 m | wyłączone | przełącznik | ogranicza małe lotniska do tych z asfaltowym lub betonowym pasem od 800 m (w Polsce kilkanaście z ponad 300, na świecie około 4800 z ponad 42 tys.). Przełącznik jest wyszarzony, dopóki małe lotniska są odznaczone, a jego przycisk „i” zostaje aktywny i to wyjaśnia. Ulubione lotniska omijają ten filtr. |
| Kody lotnisk | włączone | przełącznik | kod IATA albo ICAO obok znacznika |
| Zakres zoomu, w którym widać lotniska | od 5, bez górnego limitu | dwa uchwyty | jedna kontrolka ze skalą całego zakresu zoomu mapy (z ustawień zoomu minimalnego i maksymalnego): lewy uchwyt to zoom, od którego widać lotniska, a prawy to zoom, powyżej którego znikają (prawy koniec skali oznacza brak górnego limitu). Zakres jest taki sam dla wszystkich rodzajów, o rodzajach decydują pola wyboru. Pod skalą jest zapis zakresu, a pod markerami w podglądzie to samo. Ulubione są widoczne od zoomu 5 niezależnie od tego zakresu. |
| Wielkość kodów lotnisk [dp] | 11 | 6–30 | suwak co 0,5 dp |
| Legenda wysokości | włączona | przełącznik | pokazuje lub ukrywa pasek legendy kolorów wysokości |
| Jednostka wysokości | metry | lista wyboru | metry (m, km w legendzie), stopy (ft) albo poziom lotu (FL, setki stóp). Pokazuje się: na etykietach samolotów na mapie; w wynikach wyszukiwania (wysokość samolotu i lotniska); w linii „Wysokość” karty i popupu samolotu; w linii „Wysokość” karty i popupu lotniska oraz w długościach pasów (w stopach i poziomach lotu pasy są w stopach); w legendzie i w edytorze skali kolorów; w powiadomieniach alertów; w filtrach (nazwa pola „Wysokość [m/ft/FL]”, wpisywanie granic i opis filtra; filtry zapisują granice w metrach, więc zmiana jednostki niczego w nich nie psuje). Karta pokazuje tylko wybraną jednostkę. |
| Jednostka odległości | kilometry | lista wyboru | kilometry (km), mile morskie (NM, 1,852 km) albo mile lądowe (mi, 1,609 km). Pokazuje się: w linii postępu karty i popupu samolotu („do celu”) i w linii „Od Ciebie”; w linii „Odległość” karty i popupu lotniska; w alertach (promień obszaru w edytorze i na liście oraz odległość samolotu w powiadomieniu). Nie dotyczy ustawień prywatności w sekcji Dev, które zawsze są w km. |
| Jednostka prędkości | km/h | lista wyboru | kilometry na godzinę (km/h), węzły (kt) albo mile na godzinę (mph). Pokazuje się w linii „Prędkość” karty i popupu zaznaczonego samolotu. Szacowany czas do celu nie zależy od tego ustawienia. |
| Jednostka prędkości pionowej | ft/min | lista wyboru | stopy na minutę (ft/min) albo metry na sekundę (m/s). Pokazuje się w linii „Wznoszenie”/„Opadanie” karty i popupu zaznaczonego samolotu; próg lotu poziomego (poniżej 100 ft/min) jest taki sam w obu jednostkach. |
| Wielkość liter etykiet [dp] | 13 | 6–40 | suwak co 0,5 dp z podglądem przykładowej etykiety w wybranej wielkości |
| Skala kolorów wysokości | 300 / 1200 / 3000 / 6000 / 12000 m | 50–20000 m | jeden edytor zamiast pięciu pól: pasek z gradientem i pięcioma znacznikami (pomarańczowy, żółty, zielony, niebieski, fioletowy). Znacznik przeciąga się palcem (nie przejdzie przez sąsiada, wartości przyciągają się do 50 m albo 100 ft), a dotknięcie otwiera okienko do wpisania dokładnej wartości w wybranej jednostce. Szary punkt po lewej to ziemia (0), a pierwszy próg nie może być niższy niż 50 m, żeby strefa pomarańczowa nie zniknęła. Skala paska jest pierwiastkowa, żeby niskie progi się nie zlewały. Pod paskiem jest podgląd śladu wznoszącego się samolotu w tych samych kolorach co na mapie. Kolory są stałe. Sekcja jest wydzielona w ramce, a przycisk „Przywróć domyślne kolory” ustawia domyślne progi i zapisuje je od razu. Wartości są zapisywane w metrach. |

Każda zmiana zapisuje się od razu, na telefonie i w Android Auto, bez przycisku „Zapisz”. Pola liczbowe zapisują się po opuszczeniu pola (Gotowe albo dotknięcie innego miejsca), suwaki i lista na bieżąco. Przy suwakach, liście jednostki i polach liczbowych jest przycisk „↺” (cofnij), widoczny, gdy jest co cofać: każde dotknięcie cofa kolejną zmianę o jeden krok (przeciągnięcie suwaka i wpisanie wartości to po jednej zmianie). Historia zmian jest w pamięci aplikacji: zostaje po wyjściu z ustawień, ale znika po zamknięciu aplikacji. Przełączniki i skala kolorów nie mają cofania. Ramki „Ikony samolotów” i „Ślady samolotów” mają własne przyciski przywracające domyślne wartości, a na dole jest „Przywróć wszystkie domyślne”. Przywrócenie domyślnych też można cofnąć „↺”.

Wszystkie suwaki: dotknięcie niebieskiej wartości pod nazwą (✎) otwiera okienko do wpisania dokładnej liczby z zakresu, także pomiędzy pozycjami suwaka.

Ustawienia lotnisk są w jednej ramce po skali kolorów wysokości. Na samej górze jest przełącznik „Lotniska na mapie”, a pod nim „Tylko ulubione lotniska”; po wyłączeniu głównego przełącznika cała reszta ramki (podgląd, suwaki, pola, przełączniki, przyciski) jest wyszarzona i nieaktywna, tylko przyciski „i” działają. Pod przełącznikiem jest podgląd pięciu rodzajów lotnisk z nazwami rodzajów (duże, średnie, małe, wojskowe, helipady) pod markerami w wielkości kodów; rodzaje wyłączone są wyszarzone. Zaraz pod podglądem jest suwak „Wielkość kodów lotnisk”, a dalej kolejno: „Kody lotnisk”, „Rodzaje lotnisk na mapie”, „Małe: tylko z utwardzonym pasem od 800 m” i zakres zoomu, w którym widać lotniska. Ten sam podgląd jest w okienkach „i” tych ustawień, a ramka ma przycisk „Przywróć domyślne lotniska”.

Ustawienia śladu (od „Długość śladu” do „Krycie obwódki”) są w jednej ramce „Ślady samolotów”, po ramce „Ikony samolotów”. Na górze ramki jest podgląd śladów trzech samolotów (dron, śmigłowiec, samolot pasażerski) rysowanych tym samym kodem co mapa, z bieżącymi wartościami suwaków i z progami kolorów wysokości. Podgląd zmienia się na bieżąco podczas przesuwania suwaków.

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

Numer seryjny urządzenia pokaże `adb devices`. Każdy build zwiększa numer kompilacji w `BuildNumber.txt`, który jest też `versionCode` aplikacji. Wersja aplikacji (`versionName`) jest w pliku `Version.txt` w postaci `0.<druga>.<trzecia>` (np. `0.3.0`) i zmienia się tylko przy przygotowaniu wydania skryptem `Scripts\Bump-Version.ps1`. W lewym dolnym rogu mapy i na ekranie ustawień widać wersję z numerem kompilacji, np. `v0.3.0 (164)`. `versionCode` nigdy nie jest zerowany, bo Google Play wymaga, żeby rósł.

**Skrypty pomocnicze (`Scripts/`)**

| Skrypt | Działanie |
|---|---|
| `Setup-Environment.ps1` | Przygotowuje nowy komputer: kopiuje pliki podpisu i kontaktu z wybranego katalogu (np. pendrive'a) do `%USERPROFILE%`, opcjonalnie ustawia `JAVA_HOME` i zmienne z hasłami oraz dopisuje katalog `platform-tools` (ADB) do PATH użytkownika. |
| `Release.ps1` | Buduje podpisany plik `aab` w konfiguracji Release. Gdy istnieje `adsbcarmap-signing.props` lub zmienna `ADSBCARMAP_STOREPASS`, nie pyta o hasło, a w przeciwnym razie prosi o nie i ustawia zmienne tylko na czas działania. Po udanym buildzie tworzy adnotowany tag `v<wersja z Version.txt>` (np. `v0.3.0`), a jeśli taki tag już istnieje, przerywa z podpowiedzią, żeby najpierw podbić wersję na bieżącym commicie, z wersją, `versionCode`, commitem, nazwą, rozmiarem i SHA-256 paczki, oraz dopisuje te wartości do `release-history.csv`. Wersja musi być zatwierdzona w `Version.txt` przed buildem. Przed buildem wymaga czystego drzewa (poza `BuildNumber.txt` i `release-history.csv`). Przełączniki: `-NoTag` (sam build), `-Push` (wysyła tag na GitHuba), `-Force` (pomija sprawdzenie drzewa), `-SkipAirportData` (nie pobiera danych lotnisk). Przed buildem, gdy istnieje `Assets\Airports\airports.meta`, skrypt sam uruchamia `Update-AirportData.ps1`; jeśli dane się zmieniły, drzewo jest brudne i trzeba zatwierdzić nowe pliki przed ponownym uruchomieniem. |
| `Bump-Version.ps1` | Zmienia wersję w `Version.txt` przed wydaniem. Wymaga dokładnie jednego przełącznika, bo to Ty decydujesz: `-Minor` (nowe wydanie, druga cyfra rośnie, trzecia wraca do 0, np. 0.3.0 → 0.4.0) albo `-Patch` (poprawka, tylko trzecia cyfra, np. 0.3.0 → 0.3.1). Po skrypcie zatwierdź `Version.txt` i uruchom `Release.ps1`. |
| `Update-AirportData.ps1` | Pobiera najnowsze dane lotnisk i pasów startowych z OurAirports (domena publiczna) i zapisuje zwięzłe pliki `Assets\Airports\airports.tsv`, `runways.tsv` i `airports.meta` (liczby wierszy i SHA-256). Zostają duże, średnie i małe lotniska (bez śmigłowcowych, wodnosamolotowych i zamkniętych) oraz pasy ze współrzędnymi obu końców. Lotnisko wojskowe jest zgadywane po nazwie. Pliki są nadpisywane tylko wtedy, gdy dane się zmieniły. Parametry: `-OutputDirectory`, `-Url`. |
| `Release-Notes.ps1` | Tworzy notatki wydania: podsumowanie commitów od poprzedniego tagu wydania, pogrupowane na Add, Fix, Change, Remove i Other. Wypisuje je i zapisuje w `release-notes\<wersja>.md` (katalog ignorowany przez git). Parametry: `-From`, `-To`, `-Version`, `-NoFile`. |
| `DHU_Start.ps1` | Przekierowuje port `5277` i uruchamia Desktop Head Unit (emulator samochodu). |
| `DHU_Logs.ps1` | Czyści logcat i zapisuje go do `logs/android-auto.log` (katalog ignorowany przez git). |

**Praca na drugim komputerze.** Kod pobierz z GitHuba (`git clone`). Poza repozytorium trzeba przenieść trzy pliki z `%USERPROFILE%`: `adsbcarmap-release.jks`, `adsbcarmap-signing.props` i `adsbcarmap-contact.props`. Bez tego samego klucza podpisu nie zaktualizujesz aplikacji zainstalowanej na telefonie, trzeba by ją odinstalować. Przenoś je na zaszyfrowanym nośniku i usuń po pracy. Potem uruchom:

```powershell
.\Scripts\Setup-Environment.ps1 -SourceDirectory E:\ -JavaHome "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot" -PlatformTools "C:\Android\platform-tools"
```

Wszystkie parametry są opcjonalne (`-SetSigningVariables` zapisuje hasła w zmiennych środowiskowych zamiast w pliku `.props`). Po skrypcie zrestartuj Visual Studio i terminale. Emulator Desktop Head Unit zainstalujesz przez SDK Manager (Android Auto Desktop Head Unit Emulator).

## Źródła danych i atrybucja

| Dane | Źródło | Uwagi |
|---|---|---|
| Mapa | © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, kafelki z `tile.openstreetmap.org` | obowiązuje [Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/); atrybucja jest rysowana na mapie |
| Samoloty (ADS-B) | [adsb.lol](https://adsb.lol), `api.adsb.lol`, oraz [airplanes.live](https://airplanes.live), `api.airplanes.live` (oba w formacie ADS-B Exchange v2); źródło wybiera się w ustawieniach | pozycje, kurs, wysokość, typ, rejestracja, kategoria; oba serwisy są do użytku osobistego, niekomercyjnego (patrz `DATA_SOURCES.md`). **Uwaga:** airplanes.live odrzuca na razie zapytania aplikacji (403) i prosi o kontakt na contact@airplanes.live, więc do czasu uzgodnienia głównym źródłem powinno zostać adsb.lol |
| Trasa lotu i linia lotnicza | [adsbdb](https://www.adsbdb.com), `api.adsbdb.com`; `api.adsb.lol/api/0/route` (dane tras przekierowywane do statycznego zbioru adsb.lol); [hexdb.io](https://hexdb.io), `hexdb.io/api/v1/route` | pobierane po znaku wywoławczym; kolejność w ustawieniach. adsbdb podaje nazwę linii, adsb.lol lotniska z nazwami i współrzędnymi, hexdb.io samą parę lotnisk (może mieć starsze dane) |
| Dane samolotu (pełna nazwa typu, producent, właściciel) | adsbdb (`/v0/aircraft`), hexdb.io (`/api/v1/aircraft`), a także adsb.lol i airplanes.live (`/v2/hex`) | pobierane po kodzie ICAO tylko dla zaznaczonego samolotu, gdy główne źródło nie podaje typu, rejestracji lub pełnej nazwy typu |

Zapoznaj się z warunkami użycia tych serwisów, zanim udostępnisz własną kompilację innym osobom. Aplikacja nie ma własnego serwera: wszystkie zapytania idą bezpośrednio z telefonu.

## Prywatność

- Aplikacja **nie ma telemetrii ani kont** i niczego nie wysyła do autora.
- Pozycja GPS jest używana lokalnie. **Do serwera samolotów (adsb.lol lub airplanes.live, a przy awarii pierwszego z włączonym zapasowym źródłem do obu po kolei) trafiają współrzędne środka widocznej mapy** (oraz promień), bo tym zapytaniem pobierane są samoloty w okolicy. Gdy mapa jest wyśrodkowana na Twojej pozycji, jest to przybliżenie Twojej lokalizacji. Domyślnie środek jest przyciągany do siatki około 1 km (Zaawansowane → Prywatność; dokładne, 1, 5, 10 albo 25 km), a promień rośnie o możliwe przesunięcie, więc na mapie nic się nie zmienia, a serwer nie dostaje dokładnego punktu. Zaokrąglenie dotyczy także wyszukiwarki, telefonu i Android Auto. Serwery kafelków dostają numery kafelków, a serwisy tras i danych samolotów (adsbdb, adsb.lol, hexdb.io, w kolejności z ustawień, a przy braku danych kolejne) znaki wywoławcze samolotów, których trasę aplikacja pokazuje.
- Informacje o lotach (trasy i dane samolotów) są zapisywane lokalnie w katalogu pamięci podręcznej aplikacji (`flights\routes.json` i `flights\aircraft-details.json`), na czas wybrany w ustawieniach, i nigdzie nie są wysyłane. Można je wyczyścić w ustawieniach. i nagłówek User-Agent (nazwa aplikacji, wersja, opcjonalny kontakt).
- Dev log jest zapisywany lokalnie (`dev-log.tsv` w prywatnym katalogu aplikacji) i opuszcza telefon tylko wtedy, gdy sam go udostępnisz lub skopiujesz. Zawiera opisy błędów i stosy wyjątków, a nie współrzędne ani pozycję GPS (chyba że sam włączysz w Dev przełącznik „Loguj wysyłane współrzędne”: wtedy log zawiera prawdziwe położenie, więc go nie udostępniaj); adresy zapytań mogą jednak pojawić się w treści wyjątków sieciowych, więc przejrzyj raport przed wysłaniem.

## Znane ograniczenia

- Tłumaczenia (moduł `Localization`: klucz to polski tekst, tabela angielska w `EnglishTranslations.cs`): ekrany „Dev log” i „Dane developerskie”, komunikaty dziennika oraz dane z zewnątrz (nazwy lotnisk, linii, typów) zostają po polsku lub w oryginale. Brakujące tłumaczenie wraca jako tekst polski. Nowy tekst widoczny dla użytkownika zapisujemy jako `Loc.T("polski tekst")` (z parametrami `Loc.F`) i dopisujemy do tabeli.

- **Android Auto:** aplikacja nie zawsze pojawia się lub działa w Android Auto. Z usługą mogą się połączyć tylko znane hosty z listy w `Resources\values\allowed_hosts.xml`: build Debug używa listy `hosts_allowlist_dev`, a Release listy `hosts_allowlist_release`. Obecnie na obu jest Android Auto z Google Play (`com.google.android.projection.gearhead`, skrót certyfikatu sprawdzony na wersji 17.9). Jeśli Twój Android Auto ma inny podpis, aplikacja się w samochodzie nie pokaże; w logu pojawi się wtedy ostrzeżenie biblioteki (tag `CarApp.Val`) z dokładną linią do dopisania, a build Debug zapisuje też w logu (tag `AdsbCarMap`), czy zainstalowany host pasuje do listy.
- **Alerty:** bez trybu w tle sprawdzanie działa tylko, gdy aplikacja pobiera samoloty (mapa na telefonie lub w Android Auto) i obejmuje obszar aktualnie pobieranych danych, więc po przesunięciu mapy daleko od obszaru alertu nie zostanie on sprawdzony. Tryb w tle wymaga zgody na lokalizację i powiadomienia (Android 13+), zużywa baterię i dane, a po ponownym uruchomieniu telefonu albo zabiciu aplikacji przez system rusza dopiero po otwarciu aplikacji. Producenci telefonów (oszczędzanie baterii) mogą zatrzymywać usługę, wtedy trzeba wyłączyć optymalizację baterii dla aplikacji. Alerty związane z trasą (np. „do Rzeszowa”) działają dopiero po pobraniu trasy lotu, więc mogą się spóźnić. Gdy tryb w tle jest włączony, tylko on sprawdza alerty (mapa w tym nie uczestniczy), więc aktywne alerty ponad limit nie są wtedy sprawdzane wcale. Przy pierwszym pobraniu po uruchomieniu samoloty już będące w obszarze zgłaszają „pojawienie się”. Wielokąty i powiadomienia w Android Auto są planowane.
- **Zaokrąglanie współrzędnych:** ukrywa dokładny punkt tylko w zapytaniach o samoloty. Nie ukrywa adresu IP, obszaru widocznego z numerów kafelków pobieranych z serwerów mapy ani tego, o który lot lub samolot pytasz (znak wywoławczy i kod ICAO idą do serwisów tras i danych). Większa siatka oznacza większą odpowiedź serwera, a przy dużej siatce i dużym oddaleniu promień może dojść do limitu 250 mil morskich, wtedy przy brzegu ekranu mogą brakować samolotów. Przy szerokościach powyżej około 85° zapas promienia nie jest gwarantowany.- **Limity API:** zbyt częste zapytania kończą się odpowiedzią 429. Aplikacja wtedy wstrzymuje zapytania z wydłużanym odstępem (5, 10, 20, 40, 60 s, albo według `Retry-After`).
- **Typ samolotu:** na liście i w wyszukiwarce pokazywany jest kod ICAO (np. `A21N`); pełną nazwę, producenta i właściciela ma tylko karta zaznaczonego samolotu. Ikona jest dobierana heurystycznie, więc rzadkie typy mogą dostać ogólną ikonę. Znacznik „wojskowy” w bazie bywa niedokładny.
- **Dane ADS-B:** nie obejmują wszystkich samolotów (część wojskowych, część nieużywających transpondera lub zakłócona). Opóźnienia i błędy pozycji są możliwe.
- **Cache kafelków:** kafelki na dysku leżą w katalogu cache aplikacji, który Android może opróżnić przy braku miejsca. Starsze kafelki są usuwane według czasu zapisu, a nie ostatniego użycia. Aplikacja nie pobiera kafelków na zapas dla całych obszarów (zakazuje tego polityka OpenStreetMap).
- **Minimalna wersja Androida:** 7.0 (API 24). Aplikacja była sprawdzana tylko na jednym telefonie z nowym Androidem.

### Testy jednostkowe

Projekt `Tests\AdsbCarMap.Tests\AdsbCarMap.Tests.csproj` (xUnit, net10.0) linkuje te pliki aplikacji, które nie zależą od Androida (GeoMath, WebMercator, klasyfikator rodzajów, SelectionState, SettingDefinition, SettingsHistory, AirportDatabase, AircraftSearch, TextFolding, AltitudeFormatter, AircraftCardText, AirportCardText, AltitudeBlend, MapMath, TrailTracker, tabela producentów, tabela typów samolotów z dziennikiem brakujących typów, dziennik Dev log i model filtrów samolotów) i działa bez Androida i bez telefonu:

```powershell
dotnet test Tests\AdsbCarMap.Tests\AdsbCarMap.Tests.csproj
```

Pokrycie testami liczy się z plikiem `coverage.runsettings` (ustawia `IncludeTestAssembly=true`, bez czego coverlet pomija zlinkowany kod, i wyklucza pliki testów):

```powershell
dotnet test Tests\AdsbCarMap.Tests\AdsbCarMap.Tests.csproj --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

Kod w `Sources` testują osobne projekty, więc jeden raport pokazuje tylko część pokrycia. `Scripts\Coverage.ps1` uruchamia wszystkie projekty `*.Tests.csproj`, łączy raporty po plikach i liniach i wypisuje łączne pokrycie oraz najsłabiej pokryte pliki (wyniki trafiają do katalogu `coverage-results`, ignorowanego przez git).

Workflow GitHub Actions `.github/workflows/tests.yml` uruchamia wszystkie projekty `*.Tests.csproj` przy każdym pushu na `main` i w pull requestach, zapisuje raporty pokrycia jako artefakt i nie buduje aplikacji ani nie podpisuje paczki (klucz jest poza repozytorium).

Testy obejmują odległość i kierunek, ekstrapolację pozycji samolotu i promień pobierania (`MapMath`), historię śladów (`TrailTracker`), projekcję Web Mercator i dobór kafelków, klasyfikację rodzajów samolotów, wspólny stan zaznaczenia telefonu i samochodu oraz definicje ustawień z historią zmian. Kod zależny od Androida jest rozdzielony na pliki `*.Android.cs` i `*.App.cs` (klasy `partial`), a część czysta dostaje stan z zewnątrz: `AirportDatabase.LoadFrom` przyjmuje linie plików, a `AircraftSearch` odczyt tras, ulubione i formatowanie wysokości jako parametry. Jednostka wysokości jest parametrem metod formatujących (`AltitudeFormatter`, teksty kart), a obliczenia koloru po wysokości są w `AltitudeBlend`, bez `Android.Graphics.Color`. `RouteCache` nie zależy od Androida: błędy pobierania zgłasza przez callback (w aplikacji do `DevLogger`), a odstęp między zapytaniami jest parametrem, więc testy nie czekają. Nowa logika niezależna od Androida powinna dostać test, a jej plik trzeba dopisać jako `Compile Include` w pliku csproj testów.

### Projekty źródeł danych

Dane z zewnętrznych serwisów są obsługiwane w osobnych bibliotekach net10.0 bez zależności od Androida, w katalogu `Sources\`. Zależności idą tylko w jedną stronę: źródła i zarządca znają tylko Core, ale nie znają siebie nawzajem, a żadne źródło nie wie o istnieniu innego.

- **Core**, `Sources\AdsbCarMap.DataSources`: wyłącznie kontrakt, bez logiki. Interfejsy `IDataSource` (nazwa), `IAircraftSource` (samoloty w promieniu od punktu) i `IRouteSource` (trasa dla znaku wywoławczego) oraz modele `Aircraft`, `FlightRoute` i `RateLimitedException`, którą źródło zgłasza, gdy serwer odpowiada 429.
- **Źródła**, po jednym katalogu na serwis (`Sources\AdsbLol`, `Sources\AirplanesLive`, `Sources\Adsbdb`, `Sources\Hexdb`), każdy z projektem `AdsbCarMap.DataSources.{Nazwa}` i projektem `.Tests`. Źródło ma własny model rekordu API i własne parsowanie, a wynik mapuje na modele Core. Tylko pobiera dane: przy odpowiedzi 429 zgłasza `RateLimitedException` (z `Retry-After`), ale niczego nie wstrzymuje. Dwa serwisy w formacie ADS-B Exchange v2 (adsb.lol i airplanes.live) celowo mają osobne kopie parsera, żeby były całkowicie niezależne.
- **Zarządca**, `Sources\AdsbCarMap.DataSources.Management` (testy w `Tests\AdsbCarMap.DataSources.Management.Tests`): cała logika wokół źródeł. `BackoffRateLimiter` i `RateLimitedAircraftSource` wstrzymują zapytania po 429 (wykładniczo 5, 10, 20, 40, 60 s albo według `Retry-After`) i udostępniają `RateLimitStatus`. `DataSourceRegistry` mówi, czy jakieś lub inne niż bieżące źródło można teraz odpytać. `FallbackAircraftSource` składa źródła w jedno: pyta źródło domyślne, a gdy ono nie zwraca danych (429, błąd sieci lub serwera, błędna odpowiedź), pyta kolejne; wstrzymane pomija, a gdy wszystkie są wstrzymane, zgłasza najkrótszy czas oczekiwania. Pusta lista samolotów nie jest brakiem danych. `TimedCache` (`Caching`) jest wspólną pamięcią informacji o pojedynczych lotach i samolotach: wpisy wygasają po czasie wybranym w ustawieniach liczonym od ostatniego zobaczenia samolotu (odpowiedź „nieznany” wcześniej), `FlightCacheKeeper` odświeża wpisy widocznych lotów i zamyka (po ok. 2 minutach) loty, które właśnie wylądowały (wcześniej widziane w powietrzu, teraz na ziemi), są zapisywane do pliku z krótkim opóźnieniem i wczytywane po uruchomieniu, a przy przekroczeniu limitu 5000 wpisów najstarsze są usuwane. Korzystają z niej `RouteCache` (trasy) i `AircraftEnricher`. `AircraftEnricher` (`Enrichment`) uzupełnia typ, rejestrację i kategorię zaznaczonego samolotu: gdy główne źródło ich nie podaje, pyta pozostałe źródła o ten jeden samolot po kodzie ICAO (interfejs `IAircraftLookupSource`, endpoint `/v2/hex/{hex}`), nie częściej niż raz na 10 minut na samolot, i nie nadpisuje niczego, co główne źródło już podało.
- **Prywatność położenia**, `Privacy` w zarządcy: `CoordinateRounder` przyciąga środek zapytania do siatki (kroki w km zależne od szerokości geograficznej) i powiększa promień o stałe, niezależne od położenia przesunięcie, a `PrivacyAircraftSource` stosuje to do każdego zapytania o samoloty, z opcjami (`PrivacyOptions`) czytanymi przy każdym zapytaniu. Opakowuje `FallbackAircraftSource`, więc dotyczy wszystkich źródeł naraz; zapytania po kodzie ICAO nie mają położenia i idą bezpośrednio.
- **Aplikacja** składa całość w `AppServices`: źródła są opakowane w `RateLimitedAircraftSource` i połączone w `FallbackAircraftSource`, a ustawienia wybierają źródło domyślne.

Nowe źródło to nowy katalog `Sources\{Nazwa}` z projektem `AdsbCarMap.DataSources.{Nazwa}` (klasa implementująca `IAircraftSource` i ewentualnie `IRouteSource`, własny model i parser, referencja tylko do Core) oraz projektem `AdsbCarMap.DataSources.{Nazwa}.Tests`. Potem wystarczy dopisać je w `AppServices`, w `DataSourceSetting` (nazwa musi być taka sama jak `Name` źródła) i w `AdsbCarMap.csproj`. Testy źródeł uruchamia się osobno:

```powershell
dotnet test Sources\AdsbLol\AdsbCarMap.DataSources.AdsbLol.Tests\AdsbCarMap.DataSources.AdsbLol.Tests.csproj
dotnet test Sources\AirplanesLive\AdsbCarMap.DataSources.AirplanesLive.Tests\AdsbCarMap.DataSources.AirplanesLive.Tests.csproj
dotnet test Sources\Adsbdb\AdsbCarMap.DataSources.Adsbdb.Tests\AdsbCarMap.DataSources.Adsbdb.Tests.csproj
dotnet test Sources\Hexdb\AdsbCarMap.DataSources.Hexdb.Tests\AdsbCarMap.DataSources.Hexdb.Tests.csproj
dotnet test Tests\AdsbCarMap.DataSources.Management.Tests\AdsbCarMap.DataSources.Management.Tests.csproj
```

## Struktura projektu

```
Activities/   ekrany telefonu: mapa (MainActivity), wyszukiwarka, ustawienia
Auto/         część Android Auto: usługa Car App, ekran mapy, ekran wyszukiwania
Data/         dane aplikacji: klasyfikacja rodzajów, filtry, alerty (silnik, obszary, planowanie zapytań), wyszukiwanie, lotniska, cache tras
Map/          renderowanie mapy: kontroler (MapController.*.cs), kafelki (TileLayer), popup, legenda i ikona stanu (PopupRenderer, AltitudeLegendRenderer, StatusIconRenderer), pętla pobierania samolotów (AircraftDataLoop), zapamiętany widok (MapViewState), projekcja, kształty ikon, widok telefonu, MapMath
Services/     współdzielone usługi: klienci HTTP, pozycja GPS, usługa pierwszoplanowa, ruch na żywo, zapis widoku mapy (MapViewStore), alerty (reguły, powiadomienia, tryb w tle)
Settings/     definicje i zapis ustawień
Resources/    ikony i zasoby graficzne
Sources/      dane zewnętrzne: AdsbCarMap.DataSources (Core: interfejsy i modele), AdsbCarMap.DataSources.Management (limity, wybór źródła, zapas) i katalog na każde źródło (AdsbLol/, AirplanesLive/, Adsbdb/, Hexdb/) z jego testami
Tests/        projekty testowe (xUnit): AdsbCarMap.Tests (aplikacja) i AdsbCarMap.DataSources.Management.Tests (zarządca źródeł)
Scripts/      skrypty PowerShell: konfiguracja komputera, build Release, pokrycie testami (Coverage.ps1), Android Auto DHU
RELEASE_NOTES.md  historia wersji: co zostało wydane w kolejnych wydaniach
release-history.csv  wersja, commit i SHA-256 każdej zbudowanej paczki
```

Mapa na telefonie i w Android Auto używa tego samego `MapController`, więc zmiany w rysowaniu dotyczą obu. Klasa jest podzielona na pliki `MapController.*.cs` (gesty, zaznaczanie, połączenie z danymi, rysowanie, popup), a rysowanie kafelków, popupu, legendy i ikony stanu oraz cykliczne pobieranie samolotów wykonują osobne klasy (`TileLayer`, `PopupRenderer`, `AltitudeLegendRenderer`, `StatusIconRenderer`, `AircraftDataLoop`); czyste obliczenia (`MapMath`, `TrailTracker`) mają testy jednostkowe.

## Plany

- Filtry samolotów: wysokość w wybranej jednostce (dziś w metrach), kody IATA linii lotniczych i nazwy linii (potrzebna tabela linii), kod ICAO lotniska wylotu i przylotu obok IATA, podpowiedzi przy wpisywaniu typu i lotniska, kolejność pobierania tras od samolotów najbliższych środka mapy, zapis tekstowy filtru (np. `Boeing + LOT - WAW`).
- Pełne nazwy typów samolotów (np. A21N jako Airbus A321neo) są już w tabeli typów; zostaje pokazanie ich na karcie samolotu i w wyszukiwarce oraz uzupełnienie tabeli o typy z listy brakujących.
- Alerty: obszary rysowane na mapie (wielokąty, prostokąty), powiadomienia w Android Auto.
- Kolejne źródła danych (adsb.fi). Własny odbiornik tar1090 nie jest planowany (patrz `DATA_SOURCES.md`).
- GitHub Actions z buildem i testami, zrzuty ekranu w README.

## Ważna uwaga

To narzędzie do zabawy i obserwacji, **nie do nawigacji, ruchu lotniczego ani zapewnienia bezpieczeństwa**. Dane mogą być niepełne, opóźnione lub błędne. W samochodzie nie obsługuj aplikacji w czasie jazdy. Kierowca ma patrzeć na drogę, a z aplikacji korzystać powinien pasażer albo kierowca na postoju.

## Licencja

Kod jest udostępniony na licencji [MIT](LICENSE). Dane mapy i samolotów podlegają licencjom i warunkom ich dostawców (patrz wyżej).

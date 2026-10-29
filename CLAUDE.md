Zasady kodu:
- Język: C# (.NET 10, Android, Visual Studio 2026). Inny język tylko po potwierdzeniu.
- Nie używamy var, zawsze jawne typy.
- Zawsze pełne new List<string>(), bez target-typed new().
- Bez komentarzy opisujących, co kod robi. Dokumentacja tylko jako XML /// <summary> przy metodach, klasach, polach i zmiennych.

Projekt:
PoC aplikacji Android Auto (NavigationTemplate + SurfaceCallback, własne rysowanie na Canvas) z mapą OSM i nakładką samolotów ADS-B z adsb.lol (API zgodne z airplanes.live). Użytek własny, sideload.

Identyfikatory: ApplicationId i package w manifeście = com.blackboltshadow.adsbcarmap (wersja 0.2, numer kompilacji w BuildNumber.txt).
Podpis: Debug i Release podpisywane tym samym kluczem $(USERPROFILE)\adsbcarmap-release.jks, alias adsbcarmap (hasła w pliku $(USERPROFILE)\adsbcarmap-signing.props, poza projektem; rezerwowo zmienne środowiskowe ADSBCARMAP_STOREPASS i ADSBCARMAP_KEYPASS), format aab (celowo).

Stan: kod się kompiluje (BuildNumber 32, 2026-10-29), instalowany na Xiaomi 2602EPTC0G; aplikacja nie pojawia się/nie działa w Android Auto. Nazwy z bindingów Car App (Xamarin.AndroidX.Car.App.App 1.4.0.2) zweryfikowane z metadanymi DLL (2026-10-28), zgodne. IconCompat (Xamarin.AndroidX.Core) niezweryfikowany.
Usługa pierwszoplanowa GPS (LocationForegroundService, typ location) dodana 2026-10-29, nieskompilowana i nieprzetestowana.
Do zrobienia: uruchomienie w Android Auto (DHU / auto; sprawdzić tryb dewelopera i „Nieznane źródła” w aplikacji Android Auto).

Plany na przyszłość:
- Lotniska na mapie (pomysł z 2026-10-29). Źródło: OurAirports (airports.csv, domena publiczna, 86 tys. wierszy; large_airport 1175, medium_airport 4105, small_airport 42 783), dołączone do aplikacji jako przycięty zasób i wczytywane do indeksu siatkowego. Znaczniki zależne od zoomu (duże od ok. 5, średnie od ok. 8, małe od ok. 11, bez nakładania etykiet), etykieta kodem IATA, a gdy go brak ICAO, inny kolor dla wojskowych. Dotknięcie pokazuje nazwę, miasto, kraj i wysokość. Wyszukiwanie lotnisk w oknie wyszukiwania (kod lub nazwa) i centrowanie na nim. Warstwa działa też w Android Auto, bo MapController jest współdzielony. Atrybucja OurAirports obok OpenStreetMap. Drugi krok: małe lotniska i pasy startowe (runways.csv, od dużego zoomu).
- Pełne nazwy typów samolotów (np. A21N jako Airbus A321neo): tabela wbudowana albo zapytania do adsbdb po rejestracji. API ADS-B nie zwraca pola desc.
- Cache kafelków mapy na dysku, aby po uruchomieniu nie pobierać ich od nowa.
- Przycisk izolacji „I” w Android Auto (pasek akcji mapy mieści najwyżej 4 przyciski, jest 3).

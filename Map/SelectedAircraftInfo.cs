using AdsbCarMap.Data;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Dane głównego zaznaczonego samolotu i pozycji użytkownika, z których składa się karta samolotu.
    /// </summary>
    /// <param name="Hex">Adres ICAO samolotu.</param>
    /// <param name="Aircraft">Ostatnie dane samolotu lub null, gdy nie ma go w ostatniej odpowiedzi serwera, np. wyleciał poza zasięg.</param>
    /// <param name="Latitude">Szacowana bieżąca szerokość geograficzna samolotu. Bez znaczenia, gdy brak danych samolotu.</param>
    /// <param name="Longitude">Szacowana bieżąca długość geograficzna samolotu. Bez znaczenia, gdy brak danych samolotu.</param>
    /// <param name="AgeSeconds">Wiek ostatniej odpowiedzi serwera w sekundach.</param>
    /// <param name="HasUser">Informacja, czy znana jest pozycja użytkownika.</param>
    /// <param name="UserLatitude">Szerokość geograficzna użytkownika.</param>
    /// <param name="UserLongitude">Długość geograficzna użytkownika.</param>
    /// <param name="SelectedCount">Liczba wszystkich zaznaczonych samolotów.</param>
    public sealed record SelectedAircraftInfo(
        string Hex,
        Aircraft? Aircraft,
        double Latitude,
        double Longitude,
        double AgeSeconds,
        bool HasUser,
        double UserLatitude,
        double UserLongitude,
        int SelectedCount);
}

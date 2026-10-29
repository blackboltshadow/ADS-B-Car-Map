using AdsbCarMap.Data;

namespace AdsbCarMap.Services;

/// <summary>
/// Ostatnio pobrane samoloty i środek mapy, współdzielone przez Android Auto i ekran telefonu, oraz prośby o śledzenie samolotu.
/// </summary>
public static class LiveTraffic
{
    /// <summary>
    /// Blokada chroniąca stan.
    /// </summary>
    private static readonly object SyncRoot = new object();

    /// <summary>
    /// Ostatnio pobrana lista samolotów.
    /// </summary>
    private static IReadOnlyList<Aircraft> aircraft = Array.Empty<Aircraft>();

    /// <summary>
    /// Moment pobrania ostatniej listy samolotów, w UTC.
    /// </summary>
    private static DateTime timestampUtc = DateTime.MinValue;

    /// <summary>
    /// Szerokość geograficzna środka ostatniego zapytania.
    /// </summary>
    private static double centerLatitude = 52.2297;

    /// <summary>
    /// Długość geograficzna środka ostatniego zapytania.
    /// </summary>
    private static double centerLongitude = 21.0122;

    /// <summary>
    /// Czas, przez który zapamiętana prośba o śledzenie samolotu jest ważna.
    /// </summary>
    private static readonly TimeSpan PendingFocusLifetime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Adres ICAO samolotu z ostatniej prośby o śledzenie.
    /// </summary>
    private static string? pendingFocusHex;

    /// <summary>
    /// Moment ostatniej prośby o śledzenie samolotu, w UTC.
    /// </summary>
    private static DateTime pendingFocusUtc = DateTime.MinValue;

    /// <summary>
    /// Zdarzenie wywoływane, gdy użytkownik wybierze samolot do śledzenia. Argumentem jest adres ICAO.
    /// </summary>
    public static event Action<string>? FocusRequested;

    /// <summary>
    /// Zwraca ostatnią listę samolotów i moment jej pobrania.
    /// </summary>
    /// <returns>Lista samolotów i czas w UTC.</returns>
    public static (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) GetSnapshot()
    {
        lock (SyncRoot)
        {
            return (aircraft, timestampUtc);
        }
    }

    /// <summary>
    /// Zwraca środek ostatniego zapytania o samoloty.
    /// </summary>
    /// <returns>Szerokość i długość geograficzna.</returns>
    public static (double Latitude, double Longitude) GetCenter()
    {
        lock (SyncRoot)
        {
            return (centerLatitude, centerLongitude);
        }
    }

    /// <summary>
    /// Zapamiętuje nową listę samolotów oraz środek zapytania.
    /// </summary>
    /// <param name="fetched">Pobrane samoloty.</param>
    /// <param name="fetchedAtUtc">Moment pobrania, w UTC.</param>
    /// <param name="latitude">Szerokość geograficzna środka zapytania.</param>
    /// <param name="longitude">Długość geograficzna środka zapytania.</param>
    public static void Update(IReadOnlyList<Aircraft> fetched, DateTime fetchedAtUtc, double latitude, double longitude)
    {
        lock (SyncRoot)
        {
            aircraft = fetched;
            timestampUtc = fetchedAtUtc;
            centerLatitude = latitude;
            centerLongitude = longitude;
        }
    }

    /// <summary>
    /// Prosi aktywne mapy o wyśrodkowanie i śledzenie samolotu. Prośba jest też zapamiętana na krótko dla mapy, która uruchomi się chwilę później.
    /// </summary>
    /// <param name="hex">Adres ICAO samolotu.</param>
    public static void RequestFocus(string hex)
    {
        lock (SyncRoot)
        {
            pendingFocusHex = hex;
            pendingFocusUtc = DateTime.UtcNow;
        }

        FocusRequested?.Invoke(hex);
    }

    /// <summary>
    /// Zwraca adres ICAO samolotu z niedawnej prośby o śledzenie, której żadna aktywna mapa mogła nie odebrać.
    /// </summary>
    /// <returns>Adres ICAO lub null, gdy brak świeżej prośby.</returns>
    public static string? GetPendingFocus()
    {
        lock (SyncRoot)
        {
            return DateTime.UtcNow - pendingFocusUtc <= PendingFocusLifetime ? pendingFocusHex : null;
        }
    }
}

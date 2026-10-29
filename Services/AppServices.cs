using AdsbCarMap.Data;
using AdsbCarMap.Map;

namespace AdsbCarMap.Services;

/// <summary>
/// Długowieczne obiekty współdzielone przez ekrany Android Auto i ekran telefonu.
/// </summary>
public static class AppServices
{
    /// <summary>
    /// Wartość nagłówka User-Agent wysyłana do serwerów danych i kafelków. Zawiera kontakt z pliku użytkownika adsbcarmap-contact.props, jeśli istnieje. Pole musi być zadeklarowane przed klientami HTTP.
    /// </summary>
    public static readonly string UserAgent = string.IsNullOrEmpty(BuildInfo.Contact)
        ? "AdsbCarMap/" + BuildInfo.Version
        : "AdsbCarMap/" + BuildInfo.Version + " (kontakt: " + BuildInfo.Contact + ")";

    /// <summary>
    /// Klient HTTP używany do zapytań o samoloty.
    /// </summary>
    private static readonly HttpClient AdsbHttpClient = CreateHttpClient(AdsbClient.DefaultBaseAddress);

    /// <summary>
    /// Klient HTTP używany do pobierania kafelków mapy.
    /// </summary>
    private static readonly HttpClient TileHttpClient = CreateHttpClient(null);

    /// <summary>
    /// Klient HTTP używany do zapytań o trasy lotów.
    /// </summary>
    private static readonly HttpClient RouteHttpClient = CreateHttpClient(RouteClient.DefaultBaseAddress);

    /// <summary>
    /// Klient API ADS-B.
    /// </summary>
    public static AdsbClient Adsb { get; } = new AdsbClient(AdsbHttpClient);

    /// <summary>
    /// Pamięć tras lotów pobieranych z adsbdb.com.
    /// </summary>
    public static RouteCache Routes { get; } = new RouteCache(new RouteClient(RouteHttpClient));

    /// <summary>
    /// Dostawca kafelków mapy OpenStreetMap z cache w pamięci.
    /// </summary>
    public static TileProvider Tiles { get; } = new TileProvider(TileHttpClient, TileProvider.OpenStreetMapTemplate, 256);

    /// <summary>
    /// Tworzy klienta HTTP z limitem czasu i nagłówkiem User-Agent.
    /// </summary>
    /// <param name="baseAddress">Adres bazowy lub null, gdy nie jest potrzebny.</param>
    /// <returns>Skonfigurowany klient HTTP.</returns>
    private static HttpClient CreateHttpClient(string? baseAddress)
    {
        HttpClient client = new HttpClient();

        if (baseAddress != null)
        {
            client.BaseAddress = new Uri(baseAddress);
        }

        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        return client;
    }
}

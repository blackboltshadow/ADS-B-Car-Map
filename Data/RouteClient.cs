using System.Net;
using System.Text.Json;

namespace AdsbCarMap.Data;

/// <summary>
/// Klient API adsbdb.com zwracający trasę lotu dla znaku wywoławczego.
/// </summary>
public sealed class RouteClient
{
    /// <summary>
    /// Domyślny adres bazowy API adsbdb.com.
    /// </summary>
    public const string DefaultBaseAddress = "https://api.adsbdb.com/";

    /// <summary>
    /// Klient HTTP z ustawionym adresem bazowym API.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// Tworzy klienta korzystającego z podanego klienta HTTP.
    /// </summary>
    /// <param name="httpClient">Klient HTTP z adresem bazowym API.</param>
    public RouteClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Pobiera trasę lotu dla znaku wywoławczego.
    /// </summary>
    /// <param name="callsign">Znak wywoławczy, np. "LOT281".</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    /// <returns>Trasa lub null, gdy API nie zna tego znaku wywoławczego.</returns>
    /// <exception cref="HttpRequestException">Błąd sieci lub kod odpowiedzi inny niż sukces i 404.</exception>
    /// <exception cref="JsonException">Niepoprawna odpowiedź JSON.</exception>
    public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync("v0/callsign/" + Uri.EscapeDataString(callsign), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out JsonElement body)
            || body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("flightroute", out JsonElement route)
            || route.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        (string? Code, string? City) origin = ParseAirport(route, "origin");
        (string? Code, string? City) destination = ParseAirport(route, "destination");

        if (origin.Code == null || destination.Code == null)
        {
            return null;
        }

        string? airlineName = null;

        if (route.TryGetProperty("airline", out JsonElement airline) && airline.ValueKind == JsonValueKind.Object)
        {
            airlineName = GetString(airline, "name");
        }

        return new FlightRoute(origin.Code, origin.City, destination.Code, destination.City, string.IsNullOrWhiteSpace(airlineName) ? null : airlineName);
    }

    /// <summary>
    /// Odczytuje kod i miasto lotniska z obiektu trasy.
    /// </summary>
    /// <param name="route">Obiekt "flightroute".</param>
    /// <param name="name">Nazwa właściwości lotniska, "origin" albo "destination".</param>
    /// <returns>Kod IATA, a gdy go brak ICAO, oraz miasto. Null, gdy danych brakuje.</returns>
    private static (string? Code, string? City) ParseAirport(JsonElement route, string name)
    {
        if (!route.TryGetProperty(name, out JsonElement airport) || airport.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        string? iata = GetString(airport, "iata_code");
        string? icao = GetString(airport, "icao_code");
        string? city = GetString(airport, "municipality");

        return (string.IsNullOrEmpty(iata) ? icao : iata, city);
    }

    /// <summary>
    /// Odczytuje właściwość tekstową.
    /// </summary>
    /// <param name="element">Obiekt JSON.</param>
    /// <param name="name">Nazwa właściwości.</param>
    /// <returns>Wartość lub null, gdy właściwość nie istnieje lub nie jest tekstem.</returns>
    private static string? GetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}

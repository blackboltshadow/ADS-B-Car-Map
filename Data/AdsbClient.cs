using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AdsbCarMap.Data;

/// <summary>
/// Klient API adsb.lol zwracający samoloty w zadanym promieniu od punktu.
/// </summary>
public sealed class AdsbClient
{
    /// <summary>
    /// Domyślny adres bazowy API adsb.lol.
    /// </summary>
    public const string DefaultBaseAddress = "https://api.adsb.lol/";

    /// <summary>
    /// Maksymalny promień zapytania akceptowany przez API, w milach morskich.
    /// </summary>
    private const double MaxRadiusNauticalMiles = 250.0;

    /// <summary>
    /// Pierwsze wstrzymanie zapytań po odpowiedzi 429, podwajane przy kolejnych.
    /// </summary>
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Największe wstrzymanie wynikające z wykładniczego wydłużania.
    /// </summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Największe wstrzymanie żądane przez serwer nagłówkiem Retry-After.
    /// </summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Blokada chroniąca stan ograniczenia zapytań.
    /// </summary>
    private readonly object rateLimitLock = new object();

    /// <summary>
    /// Moment, do którego zapytania są wstrzymane po odpowiedzi 429, w UTC.
    /// </summary>
    private DateTime blockedUntilUtc = DateTime.MinValue;

    /// <summary>
    /// Liczba kolejnych odpowiedzi 429 bez udanego zapytania pomiędzy nimi.
    /// </summary>
    private int consecutiveRateLimits;

    /// <summary>
    /// Klient HTTP z ustawionym adresem bazowym API.
    /// </summary>
    private readonly HttpClient httpClient;

    /// <summary>
    /// Tworzy klienta korzystającego z podanego klienta HTTP.
    /// </summary>
    /// <param name="httpClient">Klient HTTP z adresem bazowym API.</param>
    public AdsbClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Pobiera samoloty w promieniu od podanego punktu. Pomija samoloty bez pozycji.
    /// </summary>
    /// <param name="latitude">Szerokość geograficzna środka.</param>
    /// <param name="longitude">Długość geograficzna środka.</param>
    /// <param name="radiusNauticalMiles">Promień w milach morskich, ograniczany do limitu API.</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    /// <returns>Lista samolotów.</returns>
    /// <exception cref="HttpRequestException">Błąd sieci lub kod odpowiedzi inny niż sukces.</exception>
    /// <exception cref="JsonException">Niepoprawna odpowiedź JSON.</exception>
    public async Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
    {
        double radius = Math.Clamp(radiusNauticalMiles, 1.0, MaxRadiusNauticalMiles);

        string path = string.Format(
            CultureInfo.InvariantCulture,
            "v2/point/{0:0.#####}/{1:0.#####}/{2:0}",
            latitude,
            longitude,
            Math.Ceiling(radius));

        TimeSpan remainingBlock = GetRemainingBlock();

        if (remainingBlock > TimeSpan.Zero)
        {
            throw new RateLimitedException(remainingBlock);
        }

        using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitedException(RegisterRateLimit(response.Headers.RetryAfter));
        }

        response.EnsureSuccessStatusCode();
        ResetRateLimit();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

        List<Aircraft> result = new List<Aircraft>();

        if (!document.RootElement.TryGetProperty("ac", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement element in list.EnumerateArray())
        {
            Aircraft? aircraft = ParseAircraft(element);

            if (aircraft != null)
            {
                result.Add(aircraft);
            }
        }

        return result;
    }

    /// <summary>
    /// Zwraca czas, przez jaki zapytania są jeszcze wstrzymane po odpowiedzi 429.
    /// </summary>
    /// <returns>Pozostały czas wstrzymania lub zero, gdy zapytania są dozwolone.</returns>
    private TimeSpan GetRemainingBlock()
    {
        lock (rateLimitLock)
        {
            TimeSpan remaining = blockedUntilUtc - DateTime.UtcNow;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Zapamiętuje odpowiedź 429 i wydłuża wstrzymanie zapytań wykładniczo, uwzględniając nagłówek Retry-After.
    /// </summary>
    /// <param name="retryAfter">Nagłówek Retry-After z odpowiedzi lub null.</param>
    /// <returns>Czas wstrzymania zapytań.</returns>
    private TimeSpan RegisterRateLimit(RetryConditionHeaderValue? retryAfter)
    {
        lock (rateLimitLock)
        {
            consecutiveRateLimits = Math.Min(consecutiveRateLimits + 1, 10);

            double backoffSeconds = Math.Min(MaxBackoff.TotalSeconds, BaseBackoff.TotalSeconds * Math.Pow(2.0, consecutiveRateLimits - 1));
            TimeSpan wait = TimeSpan.FromSeconds(backoffSeconds);
            TimeSpan? requested = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow);

            if (requested.HasValue && requested.Value > wait)
            {
                wait = requested.Value > MaxRetryAfter ? MaxRetryAfter : requested.Value;
            }

            blockedUntilUtc = DateTime.UtcNow + wait;

            return wait;
        }
    }

    /// <summary>
    /// Kasuje wstrzymanie zapytań po udanej odpowiedzi.
    /// </summary>
    private void ResetRateLimit()
    {
        lock (rateLimitLock)
        {
            consecutiveRateLimits = 0;
            blockedUntilUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Zamienia element JSON na samolot.
    /// </summary>
    /// <param name="element">Element tablicy "ac" z odpowiedzi API.</param>
    /// <returns>Samolot lub null, gdy brakuje adresu albo pozycji.</returns>
    private static Aircraft? ParseAircraft(JsonElement element)
    {
        string? hex = GetString(element, "hex");
        double? latitude = GetDouble(element, "lat");
        double? longitude = GetDouble(element, "lon");

        if (string.IsNullOrEmpty(hex) || !latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        string? callsign = GetString(element, "flight")?.Trim();

        bool onGround = false;
        int? altitudeFeet = null;

        if (element.TryGetProperty("alt_baro", out JsonElement altitude))
        {
            if (altitude.ValueKind == JsonValueKind.String && altitude.GetString() == "ground")
            {
                onGround = true;
            }
            else if (altitude.ValueKind == JsonValueKind.Number)
            {
                altitudeFeet = (int)Math.Round(altitude.GetDouble());
            }
        }

        double? track = GetDouble(element, "track") ?? GetDouble(element, "true_heading");
        string? typeCode = GetString(element, "t")?.Trim();
        string? registration = GetString(element, "r")?.Trim();
        string? category = GetString(element, "category")?.Trim();
        bool isMilitary = GetDouble(element, "dbFlags") is double flags && ((int)flags & 1) != 0;

        return new Aircraft(
            hex,
            string.IsNullOrEmpty(callsign) ? null : callsign,
            latitude.Value,
            longitude.Value,
            altitudeFeet,
            onGround,
            GetDouble(element, "gs"),
            track,
            GetDouble(element, "seen_pos") ?? 0.0,
            string.IsNullOrEmpty(typeCode) ? null : typeCode,
            string.IsNullOrEmpty(registration) ? null : registration,
            string.IsNullOrEmpty(category) ? null : category,
            isMilitary);
    }

    /// <summary>
    /// Odczytuje właściwość liczbową.
    /// </summary>
    /// <param name="element">Obiekt JSON.</param>
    /// <param name="name">Nazwa właściwości.</param>
    /// <returns>Wartość lub null, gdy właściwość nie istnieje lub nie jest liczbą.</returns>
    private static double? GetDouble(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return null;
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

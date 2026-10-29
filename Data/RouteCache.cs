using System.Collections.Concurrent;
using System.Text.Json;
using Android.Util;

namespace AdsbCarMap.Data;

/// <summary>
/// Pamięć tras lotów według znaku wywoławczego. Brakujące trasy pobiera w tle, po jednej na raz, aby nie obciążać API.
/// </summary>
public sealed class RouteCache
{
    /// <summary>
    /// Tag używany w logach Androida.
    /// </summary>
    private const string LogTag = "AdsbCarMap";

    /// <summary>
    /// Odstęp między kolejnymi zapytaniami o trasę w milisekundach.
    /// </summary>
    private const int RequestIntervalMilliseconds = 1000;

    /// <summary>
    /// Maksymalna liczba znaków wywoławczych czekających na pobranie trasy.
    /// </summary>
    private const int MaxQueueLength = 200;

    /// <summary>
    /// Czas, po którym ponawiane jest nieudane zapytanie o trasę.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Klient API tras.
    /// </summary>
    private readonly RouteClient client;

    /// <summary>
    /// Znane trasy według znaku wywoławczego. Wartość null oznacza, że API nie zna trasy.
    /// </summary>
    private readonly ConcurrentDictionary<string, FlightRoute?> routes = new ConcurrentDictionary<string, FlightRoute?>();

    /// <summary>
    /// Czas, po którym można ponowić nieudane zapytanie, według znaku wywoławczego.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> retryAfterUtc = new ConcurrentDictionary<string, DateTime>();

    /// <summary>
    /// Kolejka znaków wywoławczych do pobrania.
    /// </summary>
    private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

    /// <summary>
    /// Znaki wywoławcze aktualnie w kolejce, aby nie dodawać ich wielokrotnie.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> queued = new ConcurrentDictionary<string, byte>();

    /// <summary>
    /// Znacznik działającego wątku pobierającego: 1, gdy działa, 0 w przeciwnym razie.
    /// </summary>
    private int workerRunning;

    /// <summary>
    /// Tworzy pamięć tras korzystającą z podanego klienta.
    /// </summary>
    /// <param name="client">Klient API tras.</param>
    public RouteCache(RouteClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// Zwraca znaną trasę lub zleca jej pobranie w tle.
    /// </summary>
    /// <param name="callsign">Znak wywoławczy lub null.</param>
    /// <returns>Trasa lub null, gdy jest nieznana albo jeszcze niepobrana.</returns>
    public FlightRoute? Get(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            return null;
        }

        string key = callsign.Trim().ToUpperInvariant();

        if (routes.TryGetValue(key, out FlightRoute? route))
        {
            return route;
        }

        Request(key);

        return null;
    }

    /// <summary>
    /// Zwraca trasę tylko wtedy, gdy jest już znana, bez zlecania pobrania.
    /// </summary>
    /// <param name="callsign">Znak wywoławczy lub null.</param>
    /// <returns>Trasa lub null, gdy jest nieznana albo jeszcze niepobrana.</returns>
    public FlightRoute? Peek(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            return null;
        }

        return routes.TryGetValue(callsign.Trim().ToUpperInvariant(), out FlightRoute? route) ? route : null;
    }

    /// <summary>
    /// Dodaje znak wywoławczy do kolejki, jeśli nie czeka już na pobranie ani na ponowienie.
    /// </summary>
    /// <param name="key">Znormalizowany znak wywoławczy.</param>
    private void Request(string key)
    {
        if (retryAfterUtc.TryGetValue(key, out DateTime retryAfter) && retryAfter > DateTime.UtcNow)
        {
            return;
        }

        if (queue.Count >= MaxQueueLength || !queued.TryAdd(key, 0))
        {
            return;
        }

        queue.Enqueue(key);

        if (Interlocked.CompareExchange(ref workerRunning, 1, 0) == 0)
        {
            _ = Task.Run(WorkerAsync);
        }
    }

    /// <summary>
    /// Pobiera trasy z kolejki po jednej, z przerwą między zapytaniami, aż kolejka będzie pusta.
    /// </summary>
    /// <returns>Zadanie reprezentujące pracę wątku.</returns>
    private async Task WorkerAsync()
    {
        while (true)
        {
            while (queue.TryDequeue(out string? key))
            {
                try
                {
                    FlightRoute? route = await client.GetRouteAsync(key, CancellationToken.None);
                    routes[key] = route;
                    retryAfterUtc.TryRemove(key, out DateTime removedRetry);
                }
                catch (Exception exception) when (exception is HttpRequestException || exception is OperationCanceledException || exception is JsonException)
                {
                    retryAfterUtc[key] = DateTime.UtcNow + RetryDelay;
                    Log.Warn(LogTag, "Pobieranie trasy " + key + " nie powiodło się: " + exception.Message);
                }
                finally
                {
                    queued.TryRemove(key, out byte removedMarker);
                }

                await Task.Delay(RequestIntervalMilliseconds);
            }

            Interlocked.Exchange(ref workerRunning, 0);

            if (queue.IsEmpty || Interlocked.CompareExchange(ref workerRunning, 1, 0) != 0)
            {
                return;
            }
        }
    }
}

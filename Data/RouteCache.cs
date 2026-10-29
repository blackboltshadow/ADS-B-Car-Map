using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Android.Util;

namespace AdsbCarMap.Data
{
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
        /// Kolejka znaków wywoławczych do pobrania w pierwszej kolejności, np. samolotu pokazanego na karcie.
        /// </summary>
        private readonly ConcurrentQueue<string> priorityQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// Znaki wywoławcze aktualnie w kolejce priorytetowej, aby nie dodawać ich wielokrotnie.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> priorityQueued = new ConcurrentDictionary<string, byte>();

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
            EnsureWorker();
        }

        /// <summary>
        /// Uruchamia wątek pobierający, jeśli jeszcze nie działa.
        /// </summary>
        private void EnsureWorker()
        {
            if (Interlocked.CompareExchange(ref workerRunning, 1, 0) == 0)
            {
                _ = Task.Run(WorkerAsync);
            }
        }

        /// <summary>
        /// Zwraca stan wyszukiwania trasy: znana, nieznana serwerowi, nieudana lub w toku.
        /// </summary>
        /// <param name="callsign">Znak wywoławczy lub null.</param>
        /// <returns>Stan wyszukiwania trasy.</returns>
        public RouteLookupState GetState(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return RouteLookupState.NotApplicable;
            }

            string key = callsign.Trim().ToUpperInvariant();

            if (routes.TryGetValue(key, out FlightRoute? route))
            {
                return route != null ? RouteLookupState.Found : RouteLookupState.NotFound;
            }

            if (!queued.ContainsKey(key) && retryAfterUtc.TryGetValue(key, out DateTime retryAfter) && retryAfter > DateTime.UtcNow)
            {
                return RouteLookupState.Failed;
            }

            return RouteLookupState.Pending;
        }

        /// <summary>
        /// Wymusza ponowne pobranie trasy: zapomina dotychczasowy wynik, także informację o braku trasy lub o błędzie, i pobiera ją w pierwszej kolejności.
        /// </summary>
        /// <param name="callsign">Znak wywoławczy lub null.</param>
        public void Refresh(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return;
            }

            string key = callsign.Trim().ToUpperInvariant();

            routes.TryRemove(key, out FlightRoute? removedRoute);
            retryAfterUtc.TryRemove(key, out DateTime removedRetry);
            queued.TryAdd(key, 0);
            EnqueuePriority(key);
        }

        /// <summary>
        /// Przesuwa na początek kolejki trasę, która czeka na pobranie. Nie robi nic, gdy trasa jest już znana albo pobranie się nie udało.
        /// </summary>
        /// <param name="callsign">Znak wywoławczy lub null.</param>
        public void Prioritize(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign) || GetState(callsign) != RouteLookupState.Pending)
            {
                return;
            }

            string key = callsign.Trim().ToUpperInvariant();

            queued.TryAdd(key, 0);
            EnqueuePriority(key);
        }

        /// <summary>
        /// Dodaje znak wywoławczy do kolejki priorytetowej, jeśli jeszcze w niej nie ma, i uruchamia pobieranie.
        /// </summary>
        /// <param name="key">Znormalizowany znak wywoławczy.</param>
        private void EnqueuePriority(string key)
        {
            if (priorityQueued.TryAdd(key, 0))
            {
                priorityQueue.Enqueue(key);
                EnsureWorker();
            }
        }

        /// <summary>
        /// Pobiera następny znak wywoławczy do przetworzenia, najpierw z kolejki priorytetowej.
        /// </summary>
        /// <param name="key">Znak wywoławczy do pobrania.</param>
        /// <returns>True, gdy w którejś kolejce coś było.</returns>
        private bool TryDequeueNext([NotNullWhen(true)] out string? key)
        {
            if (priorityQueue.TryDequeue(out key))
            {
                priorityQueued.TryRemove(key, out byte removedPriorityMarker);

                return true;
            }

            return queue.TryDequeue(out key);
        }

        /// <summary>
        /// Pobiera trasy z kolejki po jednej, z przerwą między zapytaniami, aż kolejka będzie pusta.
        /// </summary>
        /// <returns>Zadanie reprezentujące pracę wątku.</returns>
        private async Task WorkerAsync()
        {
            while (true)
            {
                while (TryDequeueNext(out string? key))
                {
                    if (routes.ContainsKey(key))
                    {
                        queued.TryRemove(key, out byte removedDuplicateMarker);

                        continue;
                    }

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

                if ((queue.IsEmpty && priorityQueue.IsEmpty) || Interlocked.CompareExchange(ref workerRunning, 1, 0) != 0)
                {
                    return;
                }
            }
        }
    }
}

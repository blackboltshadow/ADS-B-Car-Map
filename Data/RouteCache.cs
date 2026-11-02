using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Android.Util;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Memory of the flight routes by callsign. The routes are kept for the time chosen in the settings, also in a file, so the same flight is not asked about again. Missing routes are downloaded in the background, one at a time, so that the API is not overloaded.
    /// </summary>
    public sealed class RouteCache
    {
        /// <summary>
        /// Tag used in the Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Interval between two route requests in milliseconds.
        /// </summary>
        private const int RequestIntervalMilliseconds = 1000;

        /// <summary>
        /// Maximum number of callsigns waiting for their route to be downloaded.
        /// </summary>
        private const int MaxQueueLength = 200;

        /// <summary>
        /// Time after which a failed route request is repeated.
        /// </summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Route API client.
        /// </summary>
        private readonly IRouteSource client;

        /// <summary>
        /// Known routes by callsign. A null value means that the API does not know the route.
        /// </summary>
        private readonly TimedCache<FlightRoute> routes;

        /// <summary>
        /// Time after which a failed request can be repeated, by callsign.
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> retryAfterUtc = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Queue of callsigns to download.
        /// </summary>
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

        /// <summary>
        /// Callsigns currently in the queue, so that they are not added many times.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> queued = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// Queue of callsigns to download first, e.g. of the aircraft shown on the card.
        /// </summary>
        private readonly ConcurrentQueue<string> priorityQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// Callsigns currently in the priority queue, so that they are not added many times.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> priorityQueued = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// Marker of the running download worker: 1 when it runs, 0 otherwise.
        /// </summary>
        private int workerRunning;

        /// <summary>
        /// Creates the route memory using the given client.
        /// </summary>
        /// <param name="client">Route API client.</param>
        /// <param name="routes">Memory in which the routes are kept for the time chosen in the settings.</param>
        public RouteCache(IRouteSource client, TimedCache<FlightRoute> routes)
        {
            ArgumentNullException.ThrowIfNull(routes);
            this.client = client;
            this.routes = routes;
        }

        /// <summary>
        /// Returns the known route or requests its download in the background.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The route, or null when it is unknown or not downloaded yet.</returns>
        public FlightRoute? Get(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return null;
            }

            string key = callsign.Trim().ToUpperInvariant();

            if (routes.TryGet(key, out FlightRoute? route))
            {
                return route;
            }

            Request(key);

            return null;
        }

        /// <summary>
        /// Returns the route only when it is already known, without requesting the download.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The route, or null when it is unknown or not downloaded yet.</returns>
        public FlightRoute? Peek(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return null;
            }

            return routes.TryGet(callsign.Trim().ToUpperInvariant(), out FlightRoute? route) ? route : null;
        }

        /// <summary>
        /// Adds a callsign to the queue unless it already waits for the download or for a retry.
        /// </summary>
        /// <param name="key">Normalized callsign.</param>
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
        /// Starts the download worker if it is not running yet.
        /// </summary>
        private void EnsureWorker()
        {
            if (Interlocked.CompareExchange(ref workerRunning, 1, 0) == 0)
            {
                _ = Task.Run(WorkerAsync);
            }
        }

        /// <summary>
        /// Returns the state of the route lookup: known, unknown to the server, failed or in progress.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>State of the route lookup.</returns>
        public RouteLookupState GetState(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return RouteLookupState.NotApplicable;
            }

            string key = callsign.Trim().ToUpperInvariant();

            if (routes.TryGet(key, out FlightRoute? route))
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
        /// Forces the route to be downloaded again: forgets the previous result, also the information about a missing route or an error, and downloads it first.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        public void Refresh(string? callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                return;
            }

            string key = callsign.Trim().ToUpperInvariant();

            routes.Remove(key);
            retryAfterUtc.TryRemove(key, out DateTime removedRetry);
            queued.TryAdd(key, 0);
            EnqueuePriority(key);
        }

        /// <summary>
        /// Moves a route that waits for the download to the front of the queue. Does nothing when the route is already known or the download failed.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
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
        /// Adds a callsign to the priority queue if it is not there yet and starts the download.
        /// </summary>
        /// <param name="key">Normalized callsign.</param>
        private void EnqueuePriority(string key)
        {
            if (priorityQueued.TryAdd(key, 0))
            {
                priorityQueue.Enqueue(key);
                EnsureWorker();
            }
        }

        /// <summary>
        /// Takes the next callsign to process, first from the priority queue.
        /// </summary>
        /// <param name="key">Callsign to download.</param>
        /// <returns>True when either queue had something.</returns>
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
        /// Downloads the routes from the queue one by one, with a pause between the requests, until the queue is empty.
        /// </summary>
        /// <returns>Task representing the work of the worker.</returns>
        private async Task WorkerAsync()
        {
            while (true)
            {
                while (TryDequeueNext(out string? key))
                {
                    if (routes.TryGet(key, out _))
                    {
                        queued.TryRemove(key, out byte removedDuplicateMarker);

                        continue;
                    }

                    try
                    {
                        FlightRoute? route = await client.GetRouteAsync(key, CancellationToken.None);
                        routes.Set(key, route);
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

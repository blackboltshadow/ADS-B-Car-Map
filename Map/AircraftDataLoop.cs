using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Loop that periodically downloads the aircraft within the range of the visible area and completes the data of the selected aircraft.
    /// </summary>
    internal sealed class AircraftDataLoop
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Source of the aircraft positions.
        /// </summary>
        private readonly IAircraftSource source;

        /// <summary>
        /// The same source as <see cref="source"/> when it can return all the military aircraft with one request, otherwise null.
        /// </summary>
        private readonly IMilitaryAircraftSource? militarySource;

        /// <summary>
        /// Tells whether the request for all the military aircraft is wanted now.
        /// </summary>
        private readonly Func<bool> wantsGlobalMilitary;

        /// <summary>
        /// Completes the data of the selected aircraft from the other sources, or null when it is not used.
        /// </summary>
        private readonly AircraftEnricher? enricher;

        /// <summary>
        /// Keeps the memory of the flight information in line with the visible aircraft, or null when it is not used.
        /// </summary>
        private readonly FlightCacheKeeper? keeper;

        /// <summary>
        /// Returns the center and the radius in nautical miles of the area to download.
        /// </summary>
        private readonly Func<(double Latitude, double Longitude, double RadiusNauticalMiles)> getQuery;

        /// <summary>
        /// Returns the hex code of the primary selected aircraft, or null when nothing is selected.
        /// </summary>
        private readonly Func<string?> getPrimaryHex;

        /// <summary>
        /// Stores the downloaded aircraft and updates their trails.
        /// </summary>
        private readonly Action<IReadOnlyList<Aircraft>, DateTime> publish;

        /// <summary>
        /// Merges the completed data of an aircraft into the current list.
        /// </summary>
        private readonly Action<Aircraft> mergeDetails;

        /// <summary>
        /// Released to start the next aircraft download before the regular interval ends.
        /// </summary>
        private readonly SemaphoreSlim wake = new SemaphoreSlim(0, 1);

        /// <summary>
        /// Non-zero while the aircraft download is in progress.
        /// </summary>
        private int loading;

        /// <summary>
        /// Moment the last aircraft download ended, in UTC ticks.
        /// </summary>
        private long loadEndedTicks;

        /// <summary>
        /// Non-zero when the last aircraft download failed, for example because the network is unavailable.
        /// </summary>
        private int failed;

        /// <summary>
        /// Creates the loop.
        /// </summary>
        /// <param name="source">Source of the aircraft positions.</param>
        /// <param name="enricher">Completes the data of the selected aircraft from the other sources, or null when it is not used.</param>
        /// <param name="keeper">Marks the visible flights as seen and closes the landed ones in the memory of the flight information, or null when it is not used.</param>
        /// <param name="getQuery">Returns the center and the radius in nautical miles of the area to download.</param>
        /// <param name="getPrimaryHex">Returns the hex code of the primary selected aircraft, or null when nothing is selected.</param>
        /// <param name="publish">Stores the downloaded aircraft and updates their trails.</param>
        /// <param name="mergeDetails">Merges the completed data of an aircraft into the current list.</param>
        /// <param name="wantsGlobalMilitary">Tells whether the request for all the military aircraft is wanted now.</param>
        public AircraftDataLoop(
            IAircraftSource source,
            AircraftEnricher? enricher,
            FlightCacheKeeper? keeper,
            Func<(double Latitude, double Longitude, double RadiusNauticalMiles)> getQuery,
            Func<string?> getPrimaryHex,
            Action<IReadOnlyList<Aircraft>, DateTime> publish,
            Action<Aircraft> mergeDetails,
            Func<bool> wantsGlobalMilitary)
        {
            this.source = source;
            this.militarySource = source as IMilitaryAircraftSource;
            this.wantsGlobalMilitary = wantsGlobalMilitary;
            this.enricher = enricher;
            this.keeper = keeper;
            this.getQuery = getQuery;
            this.getPrimaryHex = getPrimaryHex;
            this.publish = publish;
            this.mergeDetails = mergeDetails;
        }

        /// <summary>
        /// Whether an aircraft download is in progress.
        /// </summary>
        public bool IsLoading
        {
            get { return Volatile.Read(ref loading) != 0; }
        }

        /// <summary>
        /// Whether the last aircraft download failed.
        /// </summary>
        public bool HasFailed
        {
            get { return Volatile.Read(ref failed) != 0; }
        }

        /// <summary>
        /// How long the aircraft sources are still paused after limit responses. Zero when any source may be asked or when the source does not track limits.
        /// </summary>
        public TimeSpan RateLimitRemaining
        {
            get
            {
                if (source is not IRateLimitedSource limited)
                {
                    return TimeSpan.Zero;
                }

                RateLimitStatus status = limited.GetRateLimitStatus();

                return status.CanQuery ? TimeSpan.Zero : status.RemainingBlock;
            }
        }

        /// <summary>
        /// Moment the last aircraft download ended, in UTC.
        /// </summary>
        public DateTime LoadEndedUtc
        {
            get { return new DateTime(Volatile.Read(ref loadEndedTicks), DateTimeKind.Utc); }
        }

        /// <summary>
        /// Starts the next download before the regular interval ends.
        /// </summary>
        public void Wake()
        {
            if (wake.CurrentCount == 0)
            {
                wake.Release();
            }
        }

        /// <summary>
        /// Runs the download loop until it is cancelled.
        /// </summary>
        /// <param name="cancellationToken">Token that stops the loop.</param>
        /// <returns>Task representing the loop.</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan rateLimitRemaining = RateLimitRemaining;

                if (rateLimitRemaining > TimeSpan.Zero)
                {
                    try
                    {
                        await wake.WaitAsync(rateLimitRemaining, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                TimeSpan delay = AppSettings.FetchInterval;
                (double latitude, double longitude, double radiusNauticalMiles) = getQuery();
                bool globalUsed = false;

                Volatile.Write(ref loading, 1);

                try
                {
                    IReadOnlyList<Aircraft>? fetchedMilitary = militarySource != null && wantsGlobalMilitary() ? await militarySource.GetMilitaryAircraftAsync(cancellationToken) : null;
                    globalUsed = fetchedMilitary != null;
                    delay = MilitaryQueryPolicy.GetInterval(globalUsed, delay, AppSettings.MilitaryRefreshInterval);
                    IReadOnlyList<Aircraft> fetched = fetchedMilitary ?? await source.GetAircraftAsync(latitude, longitude, radiusNauticalMiles, cancellationToken);

                    if (enricher != null)
                    {
                        fetched = enricher.Apply(fetched);
                    }

                    keeper?.Observe(fetched);

                    DateTime fetchedAtUtc = DateTime.UtcNow;

                    publish(fetched, fetchedAtUtc);
                    LiveTraffic.Update(fetched, fetchedAtUtc, latitude, longitude);

                    if (!globalUsed)
                    {
                        AlertMonitor.Process(fetched);
                    }

                    RequestSelectedAircraftDetails(fetched, cancellationToken);
                    Volatile.Write(ref failed, 0);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (RateLimitedException exception)
                {
                    DevLogger.Warn(LogTag, "Pobieranie samolotów wstrzymane przez źródło (limit zapytań lub odmowa dostępu)");
                    delay = exception.RetryAfter > delay ? exception.RetryAfter : delay;
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Pobieranie samolotów nie powiodło się", exception);
                    Volatile.Write(ref failed, 1);
                }

                Volatile.Write(ref loadEndedTicks, DateTime.UtcNow.Ticks);
                Volatile.Write(ref loading, 0);

                try
                {
                    await wake.WaitAsync(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Starts asking the other sources for the type, the registration and the category of the selected aircraft when the main source did not give them. The answer is merged into the current list of aircraft.
        /// </summary>
        /// <param name="fetched">Aircraft just downloaded from the main source.</param>
        /// <param name="cancellationToken">Token that stops the request.</param>
        private void RequestSelectedAircraftDetails(IReadOnlyList<Aircraft> fetched, CancellationToken cancellationToken)
        {
            AircraftEnricher? lookup = enricher;
            string? hex = getPrimaryHex();

            if (lookup == null || hex == null)
            {
                return;
            }

            Aircraft? aircraft = fetched.FirstOrDefault(item => item.Hex == hex);

            if (aircraft == null || !lookup.NeedsLookup(aircraft))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    Aircraft? filled = await lookup.LookupAsync(aircraft, cancellationToken);

                    if (filled != null)
                    {
                        mergeDetails(filled);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Uzupełnianie danych samolotu nie powiodło się", exception);
                }
            }, cancellationToken);
        }
    }
}

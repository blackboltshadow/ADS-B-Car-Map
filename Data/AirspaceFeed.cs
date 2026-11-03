using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// What the feed of the airspace areas is doing now.
    /// </summary>
    public enum AirspaceFeedState
    {
        /// <summary>
        /// The feed is switched off.
        /// </summary>
        Stopped,

        /// <summary>
        /// The feed waits for the first answer.
        /// </summary>
        Loading,

        /// <summary>
        /// The feed holds data, downloaded successfully the last time.
        /// </summary>
        Ready,

        /// <summary>
        /// The last download failed; the feed keeps the older data, if any, and tries again.
        /// </summary>
        Offline,
    }

    /// <summary>
    /// The last failure of the feed of the airspace areas, kept after the feed stopped so that the state of the source can be shown later.
    /// </summary>
    /// <param name="Description">Short description of what failed.</param>
    /// <param name="OccurredUtc">Time of the failure.</param>
    public sealed record AirspaceFeedFailure(string Description, DateTime OccurredUtc);

    /// <summary>
    /// Keeps the list of the airspace areas up to date while it is started: downloads it, repeats the download after an interval and backs off after a failure or a rate limit. The areas change slowly, so the interval is counted in minutes and nothing is downloaded while the feed is stopped.
    /// </summary>
    public sealed class AirspaceFeed
    {
        /// <summary>
        /// Pause after the first failure; it doubles with every next one.
        /// </summary>
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Source of the areas.
        /// </summary>
        private readonly IAirspaceSource source;

        /// <summary>
        /// Gives the current time in UTC.
        /// </summary>
        private readonly Func<DateTime> clock;

        /// <summary>
        /// Waits for the given time; replaced in tests.
        /// </summary>
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        /// <summary>
        /// Gives the time between two successful downloads.
        /// </summary>
        private readonly Func<TimeSpan> refreshInterval;

        /// <summary>
        /// Receives the events of the source, e.g. a failure; may be null.
        /// </summary>
        private readonly Action<SourceEventKind, string, Exception?>? report;

        /// <summary>
        /// Lock protecting the start and the stop.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Cancels the running loop; null while the feed is stopped.
        /// </summary>
        private CancellationTokenSource? cancellation;

        /// <summary>
        /// The areas of the last successful download. Replaced as a whole, so it can be read without a lock.
        /// </summary>
        private volatile IReadOnlyList<AirspaceZone> zones = new List<AirspaceZone>();

        /// <summary>
        /// Current state.
        /// </summary>
        private volatile AirspaceFeedState state = AirspaceFeedState.Stopped;

        /// <summary>
        /// Time of the last successful download, in ticks of UTC; zero when there was none.
        /// </summary>
        private long lastUpdateTicks;

        /// <summary>
        /// The last failure.
        /// </summary>
        private volatile AirspaceFeedFailure? lastFailure;

        /// <summary>
        /// Number of failures since the last success, for the backoff.
        /// </summary>
        private int failureCount;

        /// <summary>
        /// Time before which no download should be made, in ticks of UTC: the end of the pause chosen after the last download; zero when there was none.
        /// </summary>
        private long nextRefreshTicks;

        /// <summary>
        /// Creates a feed. It is stopped until <see cref="Start"/>.
        /// </summary>
        /// <param name="source">Source of the areas.</param>
        /// <param name="clock">Gives the current time in UTC.</param>
        /// <param name="delay">Waits for the given time; the application passes <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
        /// <param name="refreshInterval">Gives the time between two successful downloads; read before every wait.</param>
        /// <param name="report">Receives the events of the source, or null.</param>
        public AirspaceFeed(IAirspaceSource source, Func<DateTime> clock, Func<TimeSpan, CancellationToken, Task> delay, Func<TimeSpan> refreshInterval, Action<SourceEventKind, string, Exception?>? report)
        {
            this.source = source;
            this.clock = clock;
            this.delay = delay;
            this.refreshInterval = refreshInterval;
            this.report = report;
        }

        /// <summary>
        /// Event raised, on any thread, after the list of the areas or the state of the feed changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public string Name => source.Name;

        /// <summary>
        /// Current state of the feed.
        /// </summary>
        public AirspaceFeedState State => state;

        /// <summary>
        /// The areas of the last successful download; empty before the first one. A new list replaces the old one, so it can be read without a lock.
        /// </summary>
        public IReadOnlyList<AirspaceZone> Zones => zones;

        /// <summary>
        /// Time of the last successful download, or null when there was none. It is kept after the feed stopped.
        /// </summary>
        public DateTime? LastUpdateUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref lastUpdateTicks);

                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        /// <summary>
        /// The last failure, or null when there was none. It is kept after the feed stopped.
        /// </summary>
        public AirspaceFeedFailure? LastFailure => lastFailure;

        /// <summary>
        /// Starts downloading the areas; nothing happens when the feed is already started.
        /// </summary>
        public void Start()
        {
            lock (syncRoot)
            {
                if (cancellation != null)
                {
                    return;
                }

                cancellation = new CancellationTokenSource();
                SetState(zones.Count > 0 ? AirspaceFeedState.Ready : AirspaceFeedState.Loading);
                CancellationToken token = cancellation.Token;

                _ = Task.Run(() => RunAsync(token));
            }
        }

        /// <summary>
        /// Stops downloading. The areas and the time of the last update stay, so the layer can show them when it is switched on again.
        /// </summary>
        public void Stop()
        {
            lock (syncRoot)
            {
                if (cancellation == null)
                {
                    return;
                }

                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
                SetState(AirspaceFeedState.Stopped);
            }
        }

        /// <summary>
        /// Downloads the areas once and updates the state.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The time to wait before the next download.</returns>
        public async Task<TimeSpan> RefreshAsync(CancellationToken cancellationToken)
        {
            TimeSpan wait = await DownloadAsync(cancellationToken);

            Interlocked.Exchange(ref nextRefreshTicks, clock().Ticks + wait.Ticks);

            return wait;
        }

        /// <summary>
        /// Downloads the areas when the pause after the last download is over. It lets a caller that has no loop of its own, such as the background check of the alerts, keep the data fresh without downloading more often than the loop of the map would, and without asking again soon after a failure or a rate limit.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when a download was made, whether it succeeded or not; false when the pause is not over.</returns>
        public async Task<bool> RefreshIfDueAsync(CancellationToken cancellationToken)
        {
            if (clock().Ticks < Interlocked.Read(ref nextRefreshTicks))
            {
                return false;
            }

            await RefreshAsync(cancellationToken);

            return true;
        }

        /// <summary>
        /// Downloads the areas once, updates the state and chooses the pause that follows.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The time to wait before the next download.</returns>
        private async Task<TimeSpan> DownloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<AirspaceZone> downloaded = await source.GetZonesAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                zones = downloaded;
                Interlocked.Exchange(ref lastUpdateTicks, clock().Ticks);
                failureCount = 0;
                SetState(AirspaceFeedState.Ready);

                return refreshInterval();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RateLimitedException exception)
            {
                Fail(Describe(exception), exception);

                return exception.RetryAfter > refreshInterval() ? exception.RetryAfter : refreshInterval();
            }
            catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
            {
                Fail(Describe(exception), exception);

                return GetRetryDelay();
            }
        }

        /// <summary>
        /// Downloads the areas again and again until the token is cancelled.
        /// </summary>
        /// <param name="token">Cancellation token raised by <see cref="Stop"/>.</param>
        /// <returns>The task of the loop.</returns>
        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TimeSpan wait = await RefreshAsync(token);

                    await delay(wait, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Calculates the pause after a failure: 30 s, 60 s, 120 s and so on, never longer than the normal interval.
        /// </summary>
        /// <returns>The pause.</returns>
        private TimeSpan GetRetryDelay()
        {
            double seconds = FirstRetryDelay.TotalSeconds * Math.Pow(2.0, Math.Min(failureCount - 1, 10));
            TimeSpan delayValue = TimeSpan.FromSeconds(seconds);
            TimeSpan limit = refreshInterval();

            return delayValue < limit ? delayValue : limit;
        }

        /// <summary>
        /// Records a failure, reports it and moves the feed to the offline state.
        /// </summary>
        /// <param name="description">Short description of the failure, a Polish source text that the display translates.</param>
        /// <param name="exception">The exception.</param>
        private void Fail(string description, Exception exception)
        {
            failureCount++;
            lastFailure = new AirspaceFeedFailure(description, clock());
            report?.Invoke(SourceEventKind.Failure, source.Name + ": " + description, exception);
            SetState(AirspaceFeedState.Offline);
        }

        /// <summary>
        /// Sets the state and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="newState">The new state.</param>
        private void SetState(AirspaceFeedState newState)
        {
            state = newState;
            Changed?.Invoke();
        }

        /// <summary>
        /// Describes an exception in a short form for the user and the log.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns>The description.</returns>
        private static string Describe(Exception exception)
        {
            if (exception is RateLimitedException)
            {
                return "limit zapytań (429)";
            }

            if (exception is System.Text.Json.JsonException)
            {
                return "nieprawidłowa odpowiedź";
            }

            if (exception is OperationCanceledException)
            {
                return "przekroczono czas oczekiwania";
            }

            return exception is HttpRequestException http && http.StatusCode.HasValue ? "błąd serwera" : "brak połączenia";
        }
    }
}

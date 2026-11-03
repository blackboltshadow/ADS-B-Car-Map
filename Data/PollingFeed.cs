using AdsbCarMap.DataSources.Management;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// What a polling feed is doing now.
    /// </summary>
    public enum PollingFeedState
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
    /// The last failure of a polling feed, kept after the feed stopped so that the state of the source can be shown later.
    /// </summary>
    /// <param name="Description">Short description of what failed, a Polish source text that the display translates.</param>
    /// <param name="OccurredUtc">Time of the failure.</param>
    public sealed record PollingFeedFailure(string Description, DateTime OccurredUtc);

    /// <summary>
    /// Keeps a list of items up to date while it is started: downloads it, repeats the download after an interval and backs off after a failure or a rate limit. Nothing is downloaded while the feed is stopped. What is downloaded is up to the function given by the owner, which may read the current settings at every call.
    /// </summary>
    /// <typeparam name="T">Type of the items.</typeparam>
    public sealed class PollingFeed<T>
    {
        /// <summary>
        /// Pause after the first failure; it doubles with every next one.
        /// </summary>
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Display name of the source.
        /// </summary>
        private readonly string name;

        /// <summary>
        /// Downloads the items.
        /// </summary>
        private readonly Func<CancellationToken, Task<IReadOnlyList<T>>> fetch;

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
        /// The items of the last successful download. Replaced as a whole, so it can be read without a lock.
        /// </summary>
        private volatile IReadOnlyList<T> items = new List<T>();

        /// <summary>
        /// Current state.
        /// </summary>
        private volatile PollingFeedState state = PollingFeedState.Stopped;

        /// <summary>
        /// Time of the last successful download, in ticks of UTC; zero when there was none.
        /// </summary>
        private long lastUpdateTicks;

        /// <summary>
        /// The last failure.
        /// </summary>
        private volatile PollingFeedFailure? lastFailure;

        /// <summary>
        /// Number of failures since the last success, for the backoff.
        /// </summary>
        private int failureCount;

        /// <summary>
        /// Time before which no download should be made, in ticks of UTC; zero when there is no pause.
        /// </summary>
        private long nextRefreshTicks;

        /// <summary>
        /// Creates a feed. It is stopped until <see cref="Start"/>.
        /// </summary>
        /// <param name="name">Display name of the source.</param>
        /// <param name="fetch">Downloads the items.</param>
        /// <param name="clock">Gives the current time in UTC.</param>
        /// <param name="delay">Waits for the given time; the application passes <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
        /// <param name="refreshInterval">Gives the time between two successful downloads; read before every wait.</param>
        /// <param name="report">Receives the events of the source, or null.</param>
        public PollingFeed(string name, Func<CancellationToken, Task<IReadOnlyList<T>>> fetch, Func<DateTime> clock, Func<TimeSpan, CancellationToken, Task> delay, Func<TimeSpan> refreshInterval, Action<SourceEventKind, string, Exception?>? report)
        {
            this.name = name;
            this.fetch = fetch;
            this.clock = clock;
            this.delay = delay;
            this.refreshInterval = refreshInterval;
            this.report = report;
        }

        /// <summary>
        /// Event raised, on any thread, after the list or the state of the feed changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Display name of the source.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Current state of the feed.
        /// </summary>
        public PollingFeedState State => state;

        /// <summary>
        /// The items of the last successful download; empty before the first one. A new list replaces the old one, so it can be read without a lock.
        /// </summary>
        public IReadOnlyList<T> Items => items;

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
        public PollingFeedFailure? LastFailure => lastFailure;

        /// <summary>
        /// Starts downloading; nothing happens when the feed is already started.
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
                SetState(items.Count > 0 ? PollingFeedState.Ready : PollingFeedState.Loading);
                CancellationToken token = cancellation.Token;

                _ = Task.Run(() => RunAsync(token));
            }
        }

        /// <summary>
        /// Stops downloading. The items and the time of the last update stay.
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
                SetState(PollingFeedState.Stopped);
            }
        }

        /// <summary>
        /// Downloads the items once and updates the state.
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
        /// Downloads the items when the pause after the last download is over.
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
        /// Downloads the items once, updates the state and chooses the pause that follows.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The time to wait before the next download.</returns>
        private async Task<TimeSpan> DownloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<T> downloaded = await fetch(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                items = downloaded;
                Interlocked.Exchange(ref lastUpdateTicks, clock().Ticks);
                failureCount = 0;
                SetState(PollingFeedState.Ready);

                return refreshInterval();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RateLimitedException exception)
            {
                Fail("limit zapytań (429)", exception);

                return exception.RetryAfter > refreshInterval() ? exception.RetryAfter : refreshInterval();
            }
            catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
            {
                Fail(Describe(exception), exception);

                return GetRetryDelay();
            }
        }

        /// <summary>
        /// Downloads the items again and again until the token is cancelled.
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
        /// <param name="description">Short description of the failure, a Polish source text.</param>
        /// <param name="exception">The exception.</param>
        private void Fail(string description, Exception exception)
        {
            failureCount++;
            lastFailure = new PollingFeedFailure(description, clock());
            report?.Invoke(SourceEventKind.Failure, name + ": " + description, exception);
            SetState(PollingFeedState.Offline);
        }

        /// <summary>
        /// Sets the state and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="newState">The new state.</param>
        private void SetState(PollingFeedState newState)
        {
            state = newState;
            Changed?.Invoke();
        }

        /// <summary>
        /// Describes an exception in a short form for the user and the log.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns>The description, a Polish source text.</returns>
        private static string Describe(Exception exception)
        {
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

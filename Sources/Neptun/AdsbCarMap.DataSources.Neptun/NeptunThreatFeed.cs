using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// How the feed obtains the threats.
    /// </summary>
    public enum ThreatTransport
    {
        /// <summary>
        /// Permanent WebSocket connection; while it does not work the feed downloads the list periodically and tries the connection again from time to time.
        /// </summary>
        WebSocketWithFallback,

        /// <summary>
        /// Permanent WebSocket connection only; after a failure the feed waits and connects again.
        /// </summary>
        WebSocketOnly,

        /// <summary>
        /// Periodic download of the list only.
        /// </summary>
        RestOnly,
    }

    /// <summary>
    /// Times used by the feed. The defaults suit the real server; tests use short ones.
    /// </summary>
    public sealed class NeptunFeedTimings
    {
        /// <summary>
        /// The server asks to download the list no more often than every 5 seconds.
        /// </summary>
        public static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Time between two downloads of the list.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = MinimumPollInterval;

        /// <summary>
        /// Time without any frame after which the connection is considered dead. The server sends a heartbeat every 15 seconds.
        /// </summary>
        public TimeSpan SilenceTimeout { get; set; } = TimeSpan.FromSeconds(45);

        /// <summary>
        /// Time allowed to open the connection.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Wait before the first retry; every next failure doubles it.
        /// </summary>
        public TimeSpan MinimumBackoff { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Longest wait between two connection attempts.
        /// </summary>
        public TimeSpan MaximumBackoff { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Waits for the given time; replaced in tests.
        /// </summary>
        public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (TimeSpan time, CancellationToken token) => Task.Delay(time, token);
    }

    /// <summary>
    /// Feed of the Neptun threats. Keeps the list up to date over a WebSocket and, depending on the chosen transport, falls back to downloading it periodically.
    /// </summary>
    public sealed class NeptunThreatFeed : IThreatFeed
    {
        /// <summary>
        /// Display name of the source.
        /// </summary>
        public const string SourceName = "NEPTUN";

        /// <summary>
        /// Address of the stream.
        /// </summary>
        public const string DefaultStreamAddress = "wss://neptun.in.ua/api/v1/stream";

        /// <summary>
        /// Address of the page that has to be linked next to the data, as the terms of use of the API require.
        /// </summary>
        public const string HomePage = "https://neptun.in.ua/";

        /// <summary>
        /// Downloads the list when the stream does not work.
        /// </summary>
        private readonly NeptunRestClient restClient;

        /// <summary>
        /// Creates a connection to the stream.
        /// </summary>
        private readonly Func<IThreatConnection> connectionFactory;

        /// <summary>
        /// Returns the transport chosen by the user, read again at every step.
        /// </summary>
        private readonly Func<ThreatTransport> getTransport;

        /// <summary>
        /// Receives the description of a problem and the exception that caused it, or null when the feed has no log.
        /// </summary>
        private readonly Action<string, Exception?>? report;

        /// <summary>
        /// Times used by the feed.
        /// </summary>
        private readonly NeptunFeedTimings timings;

        /// <summary>
        /// The threats.
        /// </summary>
        private readonly ThreatStore store = new ThreatStore();

        /// <summary>
        /// Lock protecting starting and stopping.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Cancels the running loop, or null when the feed is stopped.
        /// </summary>
        private CancellationTokenSource? cancellation;

        /// <summary>
        /// Current state, as the number of the enumeration value.
        /// </summary>
        private int state = (int)ThreatFeedState.Stopped;

        /// <summary>
        /// Time of the last frame or download in UTC ticks, or 0 when there was none.
        /// </summary>
        private long lastUpdateTicks;

        /// <summary>
        /// The last failure, or null when there was none.
        /// </summary>
        private ThreatFeedFailure? lastFailure;

        /// <summary>
        /// Whether the current connection has already delivered the snapshot.
        /// </summary>
        private bool sessionDelivered;

        /// <summary>
        /// Creates a feed that is stopped.
        /// </summary>
        /// <param name="restClient">Downloads the list when the stream does not work.</param>
        /// <param name="connectionFactory">Creates a connection to the stream.</param>
        /// <param name="getTransport">Returns the transport chosen by the user.</param>
        /// <param name="report">Receives the description of a problem and its exception, or null for no log.</param>
        /// <param name="timings">Times used by the feed, or null for the defaults.</param>
        public NeptunThreatFeed(NeptunRestClient restClient, Func<IThreatConnection> connectionFactory, Func<ThreatTransport> getTransport, Action<string, Exception?>? report = null, NeptunFeedTimings? timings = null)
        {
            this.restClient = restClient;
            this.connectionFactory = connectionFactory;
            this.getTransport = getTransport;
            this.report = report;
            this.timings = timings ?? new NeptunFeedTimings();
            store.Changed += () => Changed?.Invoke();
        }

        /// <inheritdoc />
        public event Action? Changed;

        /// <inheritdoc />
        public string Name => SourceName;

        /// <inheritdoc />
        public ThreatFeedState State => (ThreatFeedState)Volatile.Read(ref state);

        /// <inheritdoc />
        public IReadOnlyList<Threat> Threats => store.Threats;

        /// <inheritdoc />
        public DateTime? LastUpdateUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref lastUpdateTicks);

                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        /// <inheritdoc />
        public ThreatFeedFailure? LastFailure => Volatile.Read(ref lastFailure);

        /// <inheritdoc />
        public void Start()
        {
            CancellationTokenSource source;

            lock (syncRoot)
            {
                if (cancellation != null)
                {
                    return;
                }

                source = new CancellationTokenSource();
                cancellation = source;
            }

            SetState(ThreatFeedState.Connecting);
            _ = Task.Run(() => RunAsync(source.Token));
        }

        /// <inheritdoc />
        public void Stop()
        {
            CancellationTokenSource? source;

            lock (syncRoot)
            {
                source = cancellation;
                cancellation = null;
            }

            if (source == null)
            {
                return;
            }

            source.Cancel();
            SetState(ThreatFeedState.Stopped);
            store.Clear();
        }

        /// <summary>
        /// Runs until cancelled: keeps a connection to the stream or downloads the list, according to the chosen transport.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the feed is stopped.</param>
        /// <returns>Task finished after the feed was stopped.</returns>
        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            int failures = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (getTransport() == ThreatTransport.RestOnly)
                    {
                        await PollAsync(null, cancellationToken);

                        continue;
                    }

                    sessionDelivered = false;

                    try
                    {
                        await RunSessionAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        Report("Połączenie ze strumieniem zagrożeń nie powiodło się", exception);
                    }

                    failures = sessionDelivered ? 1 : failures + 1;

                    TimeSpan wait = GetBackoff(failures);

                    if (getTransport() == ThreatTransport.WebSocketWithFallback)
                    {
                        await PollAsync(wait, cancellationToken);
                    }
                    else
                    {
                        SetState(ThreatFeedState.Offline);
                        await timings.Delay(wait, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Keeps one connection to the stream until it breaks or is closed.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the feed is stopped.</param>
        /// <returns>Task finished when the connection ended without an error.</returns>
        /// <exception cref="TimeoutException">The connection could not be opened in time or stayed silent too long.</exception>
        private async Task RunSessionAsync(CancellationToken cancellationToken)
        {
            SetState(ThreatFeedState.Connecting);

            using IThreatConnection connection = connectionFactory();

            using (CancellationTokenSource connectLimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectLimit.CancelAfter(timings.ConnectTimeout);

                try
                {
                    await connection.ConnectAsync(connectLimit.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Nie udało się otworzyć połączenia w wyznaczonym czasie.");
                }
            }

            while (true)
            {
                string? frame;

                using (CancellationTokenSource silenceLimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    silenceLimit.CancelAfter(timings.SilenceTimeout);

                    try
                    {
                        frame = await connection.ReceiveAsync(silenceLimit.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException("Strumień zagrożeń nie przysłał żadnej ramki w wyznaczonym czasie.");
                    }
                }

                if (frame == null)
                {
                    return;
                }

                HandleFrame(NeptunParser.ParseFrame(frame), cancellationToken);
            }
        }

        /// <summary>
        /// Applies a frame of the stream to the list.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="cancellationToken">Cancelled when the feed is stopped; a stopped feed ignores the frames.</param>
        private void HandleFrame(NeptunFrame frame, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || frame.Kind == NeptunFrameKind.Unknown)
            {
                return;
            }

            switch (frame.Kind)
            {
                case NeptunFrameKind.Snapshot:
                    store.ApplySnapshot(frame.Threats);
                    sessionDelivered = true;
                    SetState(ThreatFeedState.Streaming);
                    break;
                case NeptunFrameKind.Upsert:
                    store.Upsert(frame.Threats[0]);
                    break;
                case NeptunFrameKind.Remove:
                    store.Remove(frame.RemovedId!);
                    break;
            }

            MarkUpdated();
        }

        /// <summary>
        /// Downloads the list periodically.
        /// </summary>
        /// <param name="duration">How long to download, or null to download while the REST transport is chosen.</param>
        /// <param name="cancellationToken">Cancelled when the feed is stopped.</param>
        /// <returns>Task finished when the time is up or the transport was changed.</returns>
        private async Task PollAsync(TimeSpan? duration, CancellationToken cancellationToken)
        {
            TimeSpan spent = TimeSpan.Zero;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan pause = timings.PollInterval;

                try
                {
                    IReadOnlyList<Threat> threats = await restClient.GetThreatsAsync(cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        store.ApplySnapshot(threats);
                        SetState(ThreatFeedState.Polling);
                        MarkUpdated();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (RateLimitedException exception)
                {
                    pause = pause > exception.RetryAfter ? pause : exception.RetryAfter;
                    SetState(ThreatFeedState.Offline);
                    Report("Serwer zagrożeń odmówił zapytania (limit 429)", exception);
                }
                catch (Exception exception)
                {
                    SetState(ThreatFeedState.Offline);
                    Report("Pobranie listy zagrożeń nie powiodło się", exception);
                }

                if (duration == null)
                {
                    if (getTransport() != ThreatTransport.RestOnly)
                    {
                        return;
                    }
                }
                else
                {
                    TimeSpan remaining = duration.Value - spent;

                    if (remaining <= TimeSpan.Zero)
                    {
                        return;
                    }

                    pause = pause < remaining ? pause : remaining;
                    spent += pause;
                }

                await timings.Delay(pause, cancellationToken);

                if (duration != null && spent >= duration.Value)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Calculates the wait before the next attempt to connect.
        /// </summary>
        /// <param name="failures">Number of failed attempts in a row, at least 1.</param>
        /// <returns>The minimum wait doubled for every failure after the first, at most the maximum wait.</returns>
        private TimeSpan GetBackoff(int failures)
        {
            double factor = Math.Pow(2.0, Math.Min(failures - 1, 16));
            TimeSpan wait = TimeSpan.FromTicks((long)Math.Min(timings.MinimumBackoff.Ticks * factor, timings.MaximumBackoff.Ticks));

            return wait < timings.MinimumBackoff ? timings.MinimumBackoff : wait;
        }

        /// <summary>
        /// Changes the state and raises <see cref="Changed"/> when it differs from the current one.
        /// </summary>
        /// <param name="next">New state.</param>
        private void SetState(ThreatFeedState next)
        {
            if (Interlocked.Exchange(ref state, (int)next) != (int)next)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Remembers that the source has just answered.
        /// </summary>
        private void MarkUpdated()
        {
            Interlocked.Exchange(ref lastUpdateTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Passes a problem to the log of the application.
        /// </summary>
        /// <param name="message">Description of the problem.</param>
        /// <param name="exception">Exception that caused it.</param>
        private void Report(string message, Exception exception)
        {
            Volatile.Write(ref lastFailure, new ThreatFeedFailure(message + ": " + exception.Message, DateTime.UtcNow));
            report?.Invoke(message, exception);
        }
    }
}

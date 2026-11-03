using System.Net;
using System.Net.Http.Headers;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of the feed: the stream, the fallback to downloading, the retries and stopping.
    /// </summary>
    public class NeptunThreatFeedTests
    {
        /// <summary>
        /// Marks a place in a script where the connection stays silent.
        /// </summary>
        private static readonly object Hang = new object();

        /// <summary>
        /// Snapshot with one threat.
        /// </summary>
        private const string SnapshotA = "{\"type\":\"snapshot\",\"data\":{\"threats\":[{\"id\":\"a\",\"type\":\"uav\",\"lat\":50,\"lon\":30}]}}";

        /// <summary>
        /// Connection that plays a script. An item is the text of a frame, null for a closed connection, an exception to throw or <see cref="Hang"/>; an empty script hangs.
        /// </summary>
        private sealed class ScriptedConnection : IThreatConnection
        {
            /// <summary>
            /// What the connection does, in order.
            /// </summary>
            private readonly Queue<object?> script;

            /// <summary>
            /// Error thrown when connecting, or null.
            /// </summary>
            private readonly Exception? connectError;

            /// <summary>
            /// Creates a connection.
            /// </summary>
            /// <param name="connectError">Error thrown when connecting, or null.</param>
            /// <param name="items">The script.</param>
            public ScriptedConnection(Exception? connectError, params object?[] items)
            {
                this.connectError = connectError;
                script = new Queue<object?>(items);
            }

            /// <inheritdoc />
            public Task ConnectAsync(CancellationToken cancellationToken)
            {
                return connectError == null ? Task.CompletedTask : Task.FromException(connectError);
            }

            /// <inheritdoc />
            public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
            {
                await Task.Yield();

                object? item = script.Count == 0 ? Hang : script.Dequeue();

                if (item == Hang)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                if (item is Exception error)
                {
                    throw error;
                }

                return (string?)item;
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Handler of the download that answers with a scripted response and counts the requests.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            /// <summary>
            /// Creates the response.
            /// </summary>
            private readonly Func<HttpResponseMessage> factory;

            /// <summary>
            /// Number of requests.
            /// </summary>
            private int requests;

            /// <summary>
            /// Creates a handler.
            /// </summary>
            /// <param name="factory">Creates the response.</param>
            public FakeHandler(Func<HttpResponseMessage> factory)
            {
                this.factory = factory;
            }

            /// <summary>
            /// Number of requests so far.
            /// </summary>
            public int Requests => Volatile.Read(ref requests);

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref requests);

                return Task.FromResult(factory());
            }
        }

        /// <summary>
        /// Everything needed to run a feed.
        /// </summary>
        private sealed class Rig
        {
            /// <summary>
            /// Lock protecting the list of the waits.
            /// </summary>
            private readonly object syncRoot = new object();

            /// <summary>
            /// Connections handed out in order; when they run out connections hang.
            /// </summary>
            private readonly Queue<ScriptedConnection> connections;

            /// <summary>
            /// Waits requested by the feed.
            /// </summary>
            private readonly List<TimeSpan> delays = new List<TimeSpan>();

            /// <summary>
            /// Number of connections created.
            /// </summary>
            private int connectionCount;

            /// <summary>
            /// Creates the rig.
            /// </summary>
            /// <param name="transport">Transport chosen by the user.</param>
            /// <param name="restResponse">Creates the response of the download.</param>
            /// <param name="connections">Connections handed out in order.</param>
            public Rig(ThreatTransport transport, Func<HttpResponseMessage> restResponse, params ScriptedConnection[] connections)
            {
                this.connections = new Queue<ScriptedConnection>(connections);
                Handler = new FakeHandler(restResponse);
                Transport = transport;

                NeptunFeedTimings timings = new NeptunFeedTimings
                {
                    PollInterval = TimeSpan.FromSeconds(5),
                    SilenceTimeout = TimeSpan.FromMilliseconds(80),
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    MinimumBackoff = TimeSpan.FromSeconds(1),
                    MaximumBackoff = TimeSpan.FromSeconds(4),
                    Delay = async (TimeSpan time, CancellationToken token) =>
                    {
                        lock (syncRoot)
                        {
                            delays.Add(time);
                        }

                        await Task.Delay(2, token);
                    },
                };

                Feed = new NeptunThreatFeed(
                    new NeptunRestClient(new HttpClient(Handler) { BaseAddress = new Uri("https://example.test/") }),
                    CreateConnection,
                    () => Transport,
                    (string message, Exception? exception) => { },
                    timings);
            }

            /// <summary>
            /// The feed under test.
            /// </summary>
            public NeptunThreatFeed Feed { get; }

            /// <summary>
            /// Handler of the download.
            /// </summary>
            public FakeHandler Handler { get; }

            /// <summary>
            /// Transport chosen by the user, changeable during a test.
            /// </summary>
            public ThreatTransport Transport { get; set; }

            /// <summary>
            /// Number of connections created so far.
            /// </summary>
            public int ConnectionCount => Volatile.Read(ref connectionCount);

            /// <summary>
            /// Copy of the waits requested so far.
            /// </summary>
            /// <returns>The waits in order.</returns>
            public List<TimeSpan> GetDelays()
            {
                lock (syncRoot)
                {
                    return new List<TimeSpan>(delays);
                }
            }

            /// <summary>
            /// Creates the next connection.
            /// </summary>
            /// <returns>The next scripted connection, or one that hangs when there are none left.</returns>
            private IThreatConnection CreateConnection()
            {
                Interlocked.Increment(ref connectionCount);

                lock (syncRoot)
                {
                    return connections.Count > 0 ? connections.Dequeue() : new ScriptedConnection(null);
                }
            }
        }

        /// <summary>
        /// Creates a response with one threat.
        /// </summary>
        /// <returns>The response.</returns>
        private static HttpResponseMessage RestWithThreat()
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"threats\":[{\"id\":\"rest\",\"type\":\"kab\",\"lat\":49,\"lon\":31}]}") };
        }

        /// <summary>
        /// Waits until a condition holds.
        /// </summary>
        /// <param name="condition">Condition to wait for.</param>
        /// <returns>Task finished when the condition holds.</returns>
        private static async Task WaitUntil(Func<bool> condition)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(5);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < limit, "Warunek nie został spełniony w czasie.");
                await Task.Delay(5);
            }
        }

        /// <summary>
        /// The stream builds the list from a snapshot and its changes and stopping clears it.
        /// </summary>
        [Fact]
        public async Task Stream_AppliesFramesAndStopClears()
        {
            string upsert = "{\"type\":\"upsert\",\"data\":{\"id\":\"b\",\"type\":\"missile\",\"lat\":51,\"lon\":31}}";
            string remove = "{\"type\":\"remove\",\"data\":{\"id\":\"a\"}}";
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, "{\"type\":\"heartbeat\"}", upsert, remove, "garbage", Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1 && rig.Feed.Threats[0].Id == "b");

            Assert.Equal(ThreatFeedState.Streaming, rig.Feed.State);
            Assert.NotNull(rig.Feed.LastUpdateUtc);
            Assert.Equal(0, rig.Handler.Requests);

            rig.Feed.Stop();

            Assert.Equal(ThreatFeedState.Stopped, rig.Feed.State);
            Assert.Empty(rig.Feed.Threats);
        }

        /// <summary>
        /// Starting twice and stopping a stopped feed do nothing.
        /// </summary>
        [Fact]
        public async Task StartTwiceAndStopTwice_AreHarmless()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Stop();
            rig.Feed.Start();
            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1);

            Assert.Equal(1, rig.ConnectionCount);

            rig.Feed.Stop();
            rig.Feed.Stop();

            Assert.Equal(ThreatFeedState.Stopped, rig.Feed.State);
        }

        /// <summary>
        /// The feed can be started again after it was stopped.
        /// </summary>
        [Fact]
        public async Task Restart_ConnectsAgain()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang), new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1);
            rig.Feed.Stop();
            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1 && rig.ConnectionCount == 2);

            rig.Feed.Stop();
        }

        /// <summary>
        /// With the fallback the feed downloads the list while the connection fails and tries the connection again.
        /// </summary>
        [Fact]
        public async Task Fallback_DownloadsWhileStreamFails()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketWithFallback, RestWithThreat, new ScriptedConnection(new HttpRequestException("brak sieci")));

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.State == ThreatFeedState.Polling && rig.Feed.Threats.Count == 1);

            Assert.Equal("rest", rig.Feed.Threats[0].Id);
            Assert.True(rig.Handler.Requests >= 1);

            await WaitUntil(() => rig.ConnectionCount >= 2);

            rig.Feed.Stop();
        }

        /// <summary>
        /// When the stream works again after the fallback, its snapshot replaces the downloaded list.
        /// </summary>
        [Fact]
        public async Task Fallback_StreamTakesOverAgain()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketWithFallback, RestWithThreat, new ScriptedConnection(new HttpRequestException("x")), new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.State == ThreatFeedState.Streaming && rig.Feed.Threats.Count == 1 && rig.Feed.Threats[0].Id == "a");

            rig.Feed.Stop();
        }

        /// <summary>
        /// With the stream only, nothing is downloaded and the waits double up to the maximum.
        /// </summary>
        [Fact]
        public async Task StreamOnly_BacksOffAndNeverDownloads()
        {
            Rig rig = new Rig(
                ThreatTransport.WebSocketOnly,
                RestWithThreat,
                new ScriptedConnection(new HttpRequestException("1")),
                new ScriptedConnection(new HttpRequestException("2")),
                new ScriptedConnection(new HttpRequestException("3")),
                new ScriptedConnection(new HttpRequestException("4")),
                new ScriptedConnection(new HttpRequestException("5")));

            rig.Feed.Start();
            await WaitUntil(() => rig.GetDelays().Count >= 5);

            Assert.Equal(0, rig.Handler.Requests);
            Assert.Equal(new List<TimeSpan> { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4) }, rig.GetDelays().Take(5).ToList());

            rig.Feed.Stop();
        }

        /// <summary>
        /// A connection that delivered data and then dropped starts the waits from the beginning.
        /// </summary>
        [Fact]
        public async Task StreamOnly_SuccessResetsBackoff()
        {
            Rig rig = new Rig(
                ThreatTransport.WebSocketOnly,
                RestWithThreat,
                new ScriptedConnection(new HttpRequestException("1")),
                new ScriptedConnection(new HttpRequestException("2")),
                new ScriptedConnection(null, SnapshotA, null),
                new ScriptedConnection(new HttpRequestException("4")));

            rig.Feed.Start();
            await WaitUntil(() => rig.GetDelays().Count >= 4);

            Assert.Equal(new List<TimeSpan> { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, rig.GetDelays().Take(4).ToList());

            rig.Feed.Stop();
        }

        /// <summary>
        /// With the download only, no connection is made and the list is downloaded at the poll interval.
        /// </summary>
        [Fact]
        public async Task RestOnly_DownloadsAtInterval()
        {
            Rig rig = new Rig(ThreatTransport.RestOnly, RestWithThreat);

            rig.Feed.Start();
            await WaitUntil(() => rig.GetDelays().Count >= 2);

            Assert.Equal(0, rig.ConnectionCount);
            Assert.Equal(ThreatFeedState.Polling, rig.Feed.State);
            Assert.Equal("rest", rig.Feed.Threats[0].Id);
            Assert.All(rig.GetDelays(), delay => Assert.Equal(TimeSpan.FromSeconds(5), delay));

            rig.Feed.Stop();
        }

        /// <summary>
        /// Changing the transport to the stream while downloading makes the feed connect.
        /// </summary>
        [Fact]
        public async Task TransportChange_SwitchesFromDownloadToStream()
        {
            Rig rig = new Rig(ThreatTransport.RestOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.State == ThreatFeedState.Polling);

            rig.Transport = ThreatTransport.WebSocketOnly;
            await WaitUntil(() => rig.Feed.State == ThreatFeedState.Streaming && rig.Feed.Threats[0].Id == "a");

            rig.Feed.Stop();
        }

        /// <summary>
        /// A limit answer makes the feed wait at least as long as the server asked.
        /// </summary>
        [Fact]
        public async Task RestOnly_429_WaitsRetryAfter()
        {
            Rig rig = new Rig(ThreatTransport.RestOnly, () =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

                return response;
            });

            rig.Feed.Start();
            await WaitUntil(() => rig.GetDelays().Count >= 1);

            Assert.Equal(TimeSpan.FromSeconds(30), rig.GetDelays()[0]);
            Assert.Equal(ThreatFeedState.Offline, rig.Feed.State);

            rig.Feed.Stop();
        }

        /// <summary>
        /// A connection that stays silent is dropped and opened again.
        /// </summary>
        [Fact]
        public async Task Silence_ReconnectsAfterTimeout()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang), new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.ConnectionCount >= 2);

            rig.Feed.Stop();
        }

        /// <summary>
        /// A connection that is closed by the server is opened again.
        /// </summary>
        [Fact]
        public async Task ClosedByServer_Reconnects()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, null), new ScriptedConnection(null, SnapshotA, Hang));

            rig.Feed.Start();
            await WaitUntil(() => rig.ConnectionCount >= 2);

            rig.Feed.Stop();
        }

        /// <summary>
        /// A feed notifies about changes of the list and of the state.
        /// </summary>
        [Fact]
        public async Task Changed_IsRaised()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang));
            int changes = 0;

            rig.Feed.Changed += () => Interlocked.Increment(ref changes);
            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1);

            Assert.True(Volatile.Read(ref changes) >= 2);

            rig.Feed.Stop();
        }

        /// <summary>
        /// The feed reports a failed connection to the log.
        /// </summary>
        [Fact]
        public async Task Failure_IsReported()
        {
            List<string> reports = new List<string>();
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(new HttpRequestException("x")));
            NeptunThreatFeed feed = new NeptunThreatFeed(
                new NeptunRestClient(new HttpClient(rig.Handler) { BaseAddress = new Uri("https://example.test/") }),
                () => new ScriptedConnection(new HttpRequestException("x")),
                () => ThreatTransport.WebSocketOnly,
                (string message, Exception? exception) =>
                {
                    lock (reports)
                    {
                        reports.Add(message);
                    }
                },
                new NeptunFeedTimings { Delay = (TimeSpan time, CancellationToken token) => Task.Delay(2, token) });

            Assert.Null(feed.LastFailure);

            feed.Start();
            await WaitUntil(() =>
            {
                lock (reports)
                {
                    return reports.Count >= 1;
                }
            });

            Assert.NotNull(feed.LastFailure);
            Assert.Contains("x", feed.LastFailure!.Description);
            Assert.True(DateTime.UtcNow - feed.LastFailure.OccurredUtc < TimeSpan.FromMinutes(1));

            feed.Stop();

            Assert.NotNull(feed.LastFailure);
        }

        /// <summary>
        /// The time of the last update is kept after the feed stopped, so that the state of the source can be shown later.
        /// </summary>
        [Fact]
        public async Task Stop_KeepsLastUpdate()
        {
            Rig rig = new Rig(ThreatTransport.WebSocketOnly, RestWithThreat, new ScriptedConnection(null, SnapshotA, Hang));

            Assert.Null(rig.Feed.LastUpdateUtc);

            rig.Feed.Start();
            await WaitUntil(() => rig.Feed.Threats.Count == 1);
            rig.Feed.Stop();

            Assert.NotNull(rig.Feed.LastUpdateUtc);
            Assert.Empty(rig.Feed.Threats);
        }

        /// <summary>
        /// The names of the feed and the address to link are fixed.
        /// </summary>
        [Fact]
        public void Constants_AreFixed()
        {
            Rig rig = new Rig(ThreatTransport.RestOnly, RestWithThreat);

            Assert.Equal("NEPTUN", rig.Feed.Name);
            Assert.Equal("https://neptun.in.ua/", NeptunThreatFeed.HomePage);
            Assert.Equal(TimeSpan.FromSeconds(5), NeptunFeedTimings.MinimumPollInterval);
        }
    }
}

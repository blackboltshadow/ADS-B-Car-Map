using System.Collections.Concurrent;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the flight route memory and its background download.
    /// </summary>
    public class RouteCacheTests
    {
        /// <summary>
        /// Maximum time to wait for the background worker in a test.
        /// </summary>
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Route source that answers from a dictionary and records the requested callsigns.
        /// </summary>
        private sealed class FakeRouteSource : IRouteSource
        {
            /// <summary>
            /// Routes by callsign. A callsign that is not in the dictionary is unknown to the source.
            /// </summary>
            public ConcurrentDictionary<string, FlightRoute> Routes { get; } = new ConcurrentDictionary<string, FlightRoute>();

            /// <summary>
            /// Callsigns requested so far, in order.
            /// </summary>
            public ConcurrentQueue<string> Requested { get; } = new ConcurrentQueue<string>();

            /// <summary>
            /// Callsigns that make the source fail with a network error.
            /// </summary>
            public ConcurrentDictionary<string, byte> Failing { get; } = new ConcurrentDictionary<string, byte>();

            /// <summary>
            /// Gate that the source waits for before answering, or null to answer at once.
            /// </summary>
            public SemaphoreSlim? Gate { get; set; }

            /// <summary>
            /// Name of the source.
            /// </summary>
            public string Name => "fake";

            /// <summary>
            /// Answers with the known route, null, or a network error.
            /// </summary>
            /// <param name="callsign">Callsign.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The route or null.</returns>
            public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
            {
                Requested.Enqueue(callsign);

                if (Gate != null)
                {
                    await Gate.WaitAsync(cancellationToken);
                }

                if (Failing.ContainsKey(callsign))
                {
                    throw new HttpRequestException("failure");
                }

                return Routes.TryGetValue(callsign, out FlightRoute? route) ? route : null;
            }
        }

        /// <summary>
        /// Creates a route for the tests.
        /// </summary>
        private static FlightRoute CreateRoute()
        {
            return new FlightRoute("WAW", "Warsaw", "LHR", "London");
        }

        /// <summary>
        /// Creates the memory used by the cache under test.
        /// </summary>
        private static TimedCache<FlightRoute> CreateMemory()
        {
            return new TimedCache<FlightRoute>(() => TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        /// <summary>
        /// Waits until the condition holds or the limit passes.
        /// </summary>
        private static void WaitFor(Func<bool> condition)
        {
            DateTime limit = DateTime.UtcNow + WaitLimit;

            while (!condition() && DateTime.UtcNow < limit)
            {
                Thread.Sleep(5);
            }

            Assert.True(condition(), "The condition was not met in time.");
        }

        /// <summary>
        /// A missing callsign gives no route and no request.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankCallsign_IsNotApplicable(string? callsign)
        {
            FakeRouteSource source = new FakeRouteSource();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            Assert.Null(cache.Get(callsign));
            Assert.Null(cache.Peek(callsign));
            Assert.Equal(RouteLookupState.NotApplicable, cache.GetState(callsign));
            cache.Refresh(callsign);
            cache.Prioritize(callsign);
            Assert.Empty(source.Requested);
        }

        /// <summary>
        /// The first call starts the download and returns nothing, the next call returns the route. The callsign is normalized.
        /// </summary>
        [Fact]
        public void Get_DownloadsInBackgroundAndNormalizesCallsign()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Routes["LOT281"] = CreateRoute();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            Assert.Null(cache.Get(" lot281 "));
            WaitFor(() => cache.Peek("LOT281") != null);

            Assert.Equal(CreateRoute(), cache.Get("lot281"));
            Assert.Equal(RouteLookupState.Found, cache.GetState("Lot281"));
            Assert.Equal(new string[] { "LOT281" }, source.Requested.ToArray());
        }

        /// <summary>
        /// Peek never starts a download.
        /// </summary>
        [Fact]
        public void Peek_DoesNotRequestDownload()
        {
            FakeRouteSource source = new FakeRouteSource();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            Assert.Null(cache.Peek("LOT281"));
            Assert.Empty(source.Requested);
            Assert.Equal(RouteLookupState.Pending, cache.GetState("LOT281"));
        }

        /// <summary>
        /// A route unknown to the source is remembered as not found and is not asked for again.
        /// </summary>
        [Fact]
        public void UnknownRoute_IsRememberedAsNotFound()
        {
            FakeRouteSource source = new FakeRouteSource();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("XYZ1");
            WaitFor(() => cache.GetState("XYZ1") == RouteLookupState.NotFound);
            cache.Get("XYZ1");

            Assert.Null(cache.Peek("XYZ1"));
            Assert.Single(source.Requested);
        }

        /// <summary>
        /// A failed download is reported, marks the lookup as failed and is not repeated at once.
        /// </summary>
        [Fact]
        public void FailedDownload_IsReportedAndNotRepeatedImmediately()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Failing["BAD1"] = 0;
            ConcurrentQueue<Exception> failures = new ConcurrentQueue<Exception>();
            RouteCache cache = new RouteCache(source, CreateMemory(), failures.Enqueue, 0);

            cache.Get("BAD1");
            WaitFor(() => cache.GetState("BAD1") == RouteLookupState.Failed);
            cache.Get("BAD1");
            cache.Prioritize("BAD1");

            Assert.IsType<HttpRequestException>(Assert.Single(failures));
            Assert.Single(source.Requested);
            Assert.Equal(RouteLookupState.Failed, cache.GetState("BAD1"));
        }

        /// <summary>
        /// A failure without a callback is ignored.
        /// </summary>
        [Fact]
        public void FailedDownload_WithoutCallback_DoesNotThrow()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Failing["BAD1"] = 0;
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("BAD1");

            WaitFor(() => cache.GetState("BAD1") == RouteLookupState.Failed);
        }

        /// <summary>
        /// Refresh forgets the previous result and downloads the route again.
        /// </summary>
        [Fact]
        public void Refresh_DownloadsAgainAfterFailure()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Failing["LOT281"] = 0;
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("LOT281");
            WaitFor(() => cache.GetState("LOT281") == RouteLookupState.Failed);
            source.Failing.TryRemove("LOT281", out byte removed);
            source.Routes["LOT281"] = CreateRoute();
            cache.Refresh("lot281");

            WaitFor(() => cache.GetState("LOT281") == RouteLookupState.Found);
            Assert.Equal(2, source.Requested.Count);
        }

        /// <summary>
        /// Prioritize moves a waiting callsign before the others in the queue.
        /// </summary>
        [Fact]
        public void Prioritize_DownloadsWaitingCallsignFirst()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Gate = new SemaphoreSlim(0);
            source.Routes["A1"] = CreateRoute();
            source.Routes["B2"] = CreateRoute();
            source.Routes["C3"] = CreateRoute();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("A1");
            WaitFor(() => source.Requested.Count == 1);
            cache.Get("B2");
            cache.Get("C3");
            cache.Prioritize("C3");
            source.Gate.Release(3);

            WaitFor(() => cache.GetState("B2") == RouteLookupState.Found);
            Assert.Equal(new string[] { "A1", "C3", "B2" }, source.Requested.ToArray());
        }

        /// <summary>
        /// Prioritize does nothing for a route that is already known.
        /// </summary>
        [Fact]
        public void Prioritize_KnownRoute_DoesNothing()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Routes["LOT281"] = CreateRoute();
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("LOT281");
            WaitFor(() => cache.GetState("LOT281") == RouteLookupState.Found);
            cache.Prioritize("LOT281");

            Assert.Single(source.Requested);
        }

        /// <summary>
        /// A callsign already in the memory is not downloaded again when it is queued.
        /// </summary>
        [Fact]
        public void KnownInMemory_IsNotRequested()
        {
            FakeRouteSource source = new FakeRouteSource();
            TimedCache<FlightRoute> memory = CreateMemory();
            memory.Set("LOT281", CreateRoute());
            RouteCache cache = new RouteCache(source, memory, null, 0);

            Assert.Equal(CreateRoute(), cache.Get("LOT281"));
            Assert.Empty(source.Requested);
        }

        /// <summary>
        /// The queue is limited, so callsigns above the limit are dropped instead of growing without bound.
        /// </summary>
        [Fact]
        public void QueueIsLimited()
        {
            FakeRouteSource source = new FakeRouteSource();
            source.Gate = new SemaphoreSlim(0);
            RouteCache cache = new RouteCache(source, CreateMemory(), null, 0);

            cache.Get("FIRST");
            WaitFor(() => source.Requested.Count == 1);

            for (int index = 0; index < 300; index++)
            {
                cache.Get("Q" + index);
            }

            source.Gate.Release(1000);
            WaitFor(() => source.Requested.Count == 201);
            Thread.Sleep(50);

            Assert.Equal(201, source.Requested.Count);
        }
    }
}

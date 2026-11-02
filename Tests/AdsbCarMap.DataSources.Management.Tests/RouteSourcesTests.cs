using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of pausing the route sources after limit responses and of asking the next route source.
    /// </summary>
    public class RouteSourcesTests
    {
        /// <summary>
        /// Route source that answers with a fixed route, with null, or fails; it also answers lookups by the ICAO address.
        /// </summary>
        private sealed class StubRouteSource : IRouteSource, IAircraftLookupSource
        {
            /// <summary>
            /// Creates the source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            public StubRouteSource(string name)
            {
                Name = name;
            }

            /// <inheritdoc />
            public string Name { get; }

            /// <summary>
            /// Route returned by the source or null when it does not know the flight.
            /// </summary>
            public FlightRoute? Route { get; set; }

            /// <summary>
            /// Exception thrown by the request or null when the source answers.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Number of route requests that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <summary>
            /// Number of lookups by the ICAO address that reached the source.
            /// </summary>
            public int Lookups { get; private set; }

            /// <inheritdoc />
            public Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
            {
                Calls++;

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult(Route);
            }

            /// <inheritdoc />
            public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
            {
                Lookups++;

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult<AircraftDetails?>(new AircraftDetails(hex, "A320", null, null, "A320-214"));
            }
        }

        /// <summary>
        /// Route source without lookups by the ICAO address.
        /// </summary>
        private sealed class RouteOnlySource : IRouteSource
        {
            /// <inheritdoc />
            public string Name => "route-only";

            /// <inheritdoc />
            public Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
            {
                return Task.FromResult<FlightRoute?>(null);
            }
        }

        /// <summary>
        /// Creates a route.
        /// </summary>
        /// <param name="origin">Origin code.</param>
        /// <returns>The route.</returns>
        private static FlightRoute Route(string origin)
        {
            return new FlightRoute(origin, null, "JFK", null);
        }

        /// <summary>
        /// Wraps a stub in the limit decorator.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The decorated source.</returns>
        private static RateLimitedRouteSource Wrap(StubRouteSource source)
        {
            return new RateLimitedRouteSource(source);
        }

        /// <summary>
        /// Creates the composite over the given sources.
        /// </summary>
        /// <param name="order">Names in the order of preference.</param>
        /// <param name="fallback">Whether the next sources may be asked.</param>
        /// <param name="sources">Sources in the order of registration.</param>
        /// <returns>The composite.</returns>
        private static FallbackRouteSource Create(List<string> order, bool fallback, params RateLimitedRouteSource[] sources)
        {
            return new FallbackRouteSource(sources, () => order, () => fallback);
        }

        /// <summary>
        /// After a limit response further route requests are paused locally.
        /// </summary>
        [Fact]
        public async Task Decorator_After429_BlocksFurtherRequests()
        {
            StubRouteSource inner = new StubRouteSource("a") { Failure = new RateLimitedException(TimeSpan.Zero) };
            RateLimitedRouteSource source = Wrap(inner);

            RateLimitedException first = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetRouteAsync("LOT1", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetRouteAsync("LOT1", CancellationToken.None));

            Assert.Equal(1, inner.Calls);
            Assert.Equal(TimeSpan.FromSeconds(5), first.RetryAfter);
            Assert.False(source.GetRateLimitStatus().CanQuery);
        }

        /// <summary>
        /// A lookup by the ICAO address goes through the same pause as the route requests, because it is the same service.
        /// </summary>
        [Fact]
        public async Task Decorator_LookupSharesThePauseWithRoutes()
        {
            StubRouteSource inner = new StubRouteSource("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(20)) };
            RateLimitedRouteSource source = Wrap(inner);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetRouteAsync("LOT1", CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftDetailsAsync("abc123", CancellationToken.None));

            Assert.Equal(0, inner.Lookups);
        }

        /// <summary>
        /// A source without lookups answers a lookup with null.
        /// </summary>
        [Fact]
        public async Task Decorator_SourceWithoutLookups_ReturnsNull()
        {
            RateLimitedRouteSource source = new RateLimitedRouteSource(new RouteOnlySource());

            Assert.Null(await source.GetAircraftDetailsAsync("abc123", CancellationToken.None));
            Assert.Equal("route-only", source.Name);
        }

        /// <summary>
        /// The first source that knows the flight answers and the others are not asked.
        /// </summary>
        [Fact]
        public async Task Fallback_FirstKnownRouteWins()
        {
            StubRouteSource first = new StubRouteSource("a") { Route = Route("WAW") };
            StubRouteSource second = new StubRouteSource("b") { Route = Route("KRK") };
            FallbackRouteSource source = Create(new List<string> { "a", "b" }, true, Wrap(first), Wrap(second));

            FlightRoute? route = await source.GetRouteAsync("LOT1", CancellationToken.None);

            Assert.Equal("WAW", route!.OriginCode);
            Assert.Equal(0, second.Calls);
        }

        /// <summary>
        /// A source that does not know the flight makes the next one answer.
        /// </summary>
        [Fact]
        public async Task Fallback_UnknownFlight_AsksNextSource()
        {
            StubRouteSource first = new StubRouteSource("a");
            StubRouteSource second = new StubRouteSource("b") { Route = Route("KRK") };
            FallbackRouteSource source = Create(new List<string> { "a", "b" }, true, Wrap(first), Wrap(second));

            FlightRoute? route = await source.GetRouteAsync("LOT1", CancellationToken.None);

            Assert.Equal("KRK", route!.OriginCode);
            Assert.Equal(1, first.Calls);
        }

        /// <summary>
        /// A failing or paused source is skipped and the next one answers.
        /// </summary>
        [Fact]
        public async Task Fallback_FailingSource_NextOneAnswers()
        {
            StubRouteSource first = new StubRouteSource("a") { Failure = new HttpRequestException("boom") };
            StubRouteSource second = new StubRouteSource("b") { Route = Route("KRK") };
            FallbackRouteSource source = Create(new List<string> { "a", "b" }, true, Wrap(first), Wrap(second));

            Assert.Equal("KRK", (await source.GetRouteAsync("LOT1", CancellationToken.None))!.OriginCode);

            StubRouteSource limited = new StubRouteSource("c") { Failure = new RateLimitedException(TimeSpan.Zero) };
            RateLimitedRouteSource paused = Wrap(limited);
            await Assert.ThrowsAsync<RateLimitedException>(() => paused.GetRouteAsync("LOT1", CancellationToken.None));
            FallbackRouteSource other = Create(new List<string> { "c", "b" }, true, paused, Wrap(second));

            Assert.Equal("KRK", (await other.GetRouteAsync("LOT1", CancellationToken.None))!.OriginCode);
            Assert.Equal(1, limited.Calls);
        }

        /// <summary>
        /// When one source answered that it does not know the flight and the others failed, the result is null and not an error.
        /// </summary>
        [Fact]
        public async Task Fallback_SomeAnsweredUnknown_ReturnsNull()
        {
            StubRouteSource first = new StubRouteSource("a") { Failure = new HttpRequestException("boom") };
            StubRouteSource second = new StubRouteSource("b");
            FallbackRouteSource source = Create(new List<string> { "a", "b" }, true, Wrap(first), Wrap(second));

            Assert.Null(await source.GetRouteAsync("LOT1", CancellationToken.None));
        }

        /// <summary>
        /// When no source answered the failure is reported, and when all are paused the shortest wait is reported.
        /// </summary>
        [Fact]
        public async Task Fallback_NoSourceAnswered_ReportsFailure()
        {
            StubRouteSource first = new StubRouteSource("a") { Failure = new HttpRequestException("boom") };
            StubRouteSource second = new StubRouteSource("b") { Failure = new HttpRequestException("boom too") };
            FallbackRouteSource failing = Create(new List<string> { "a", "b" }, true, Wrap(first), Wrap(second));

            await Assert.ThrowsAsync<HttpRequestException>(() => failing.GetRouteAsync("LOT1", CancellationToken.None));

            StubRouteSource limitedA = new StubRouteSource("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            StubRouteSource limitedB = new StubRouteSource("b") { Failure = new RateLimitedException(TimeSpan.FromSeconds(12)) };
            FallbackRouteSource limited = Create(new List<string> { "a", "b" }, true, Wrap(limitedA), Wrap(limitedB));

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => limited.GetRouteAsync("LOT1", CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
        }

        /// <summary>
        /// The user's order decides which source is asked first, and with the fallback off only that source is asked.
        /// </summary>
        [Fact]
        public async Task Fallback_OrderAndSwitch()
        {
            StubRouteSource first = new StubRouteSource("a") { Route = Route("WAW") };
            StubRouteSource second = new StubRouteSource("b") { Route = Route("KRK") };
            FallbackRouteSource source = Create(new List<string> { "b", "a" }, false, Wrap(first), Wrap(second));

            Assert.Equal("KRK", (await source.GetRouteAsync("LOT1", CancellationToken.None))!.OriginCode);
            Assert.Equal("b", source.Name);
            Assert.Equal(0, first.Calls);

            StubRouteSource unknown = new StubRouteSource("c");
            FallbackRouteSource single = Create(new List<string> { "c", "b" }, false, Wrap(unknown), Wrap(second));

            Assert.Null(await single.GetRouteAsync("LOT1", CancellationToken.None));
        }

        /// <summary>
        /// A route source that fails or answers with a limit response is reported, a source that does not know the flight is not.
        /// </summary>
        [Fact]
        public async Task Report_GetsFailuresOfRouteSources()
        {
            List<(SourceEventKind Kind, string Message, Exception? Exception)> events = new List<(SourceEventKind, string, Exception?)>();
            StubRouteSource limited = new StubRouteSource("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(5)) };
            StubRouteSource failing = new StubRouteSource("b") { Failure = new HttpRequestException("boom") };
            StubRouteSource unknown = new StubRouteSource("c");
            FallbackRouteSource source = new FallbackRouteSource(new List<RateLimitedRouteSource> { Wrap(limited), Wrap(failing), Wrap(unknown) }, () => new List<string> { "a", "b", "c" }, () => true, (SourceEventKind kind, string message, Exception? exception) => events.Add((kind, message, exception)));

            FlightRoute? route = await source.GetRouteAsync("LOT281", CancellationToken.None);

            Assert.Null(route);
            Assert.Equal(2, events.Count);
            Assert.Equal("Route source a answered with a limit response (429), paused", events[0].Message);
            Assert.Equal("Route source b failed (HttpRequestException)", events[1].Message);
            Assert.All(events, (item) => Assert.Equal(SourceEventKind.Failure, item.Kind));
        }
    }
}

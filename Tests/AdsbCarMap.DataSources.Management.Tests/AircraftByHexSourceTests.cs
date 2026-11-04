using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the request for aircraft by their ICAO addresses as it passes through the privacy decorator, the fallback and the pause of the source.
    /// </summary>
    public class AircraftByHexSourceTests
    {
        /// <summary>
        /// Source that can return aircraft by address; it answers, answers with null (no such request), pauses or fails as the test says.
        /// </summary>
        private sealed class ByHexStub : IRateLimitedAircraftSource, IAircraftByHexSource
        {
            /// <summary>
            /// Creates the source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            public ByHexStub(string name)
            {
                Name = name;
            }

            /// <inheritdoc />
            public string Name { get; }

            /// <summary>
            /// Whether the source has the request; false makes it answer with null.
            /// </summary>
            public bool Supports { get; set; } = true;

            /// <summary>
            /// Exception thrown by the request, or null when the source answers.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Remaining block reported by the source.
            /// </summary>
            public TimeSpan Block { get; set; }

            /// <summary>
            /// Number of requests by address that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <summary>
            /// Addresses of the last request.
            /// </summary>
            public IReadOnlyList<string> LastHexes { get; private set; } = new List<string>();

            /// <inheritdoc />
            public RateLimitStatus GetRateLimitStatus()
            {
                return new RateLimitStatus(DateTime.MinValue, Block, Block > TimeSpan.Zero ? 1 : 0);
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>?> GetAircraftByHexAsync(IReadOnlyList<string> hexes, CancellationToken cancellationToken)
            {
                Calls++;
                LastHexes = hexes;

                if (Failure is RateLimitedException rateLimited)
                {
                    Block = rateLimited.RetryAfter;
                }

                if (Failure != null)
                {
                    throw Failure;
                }

                IReadOnlyList<Aircraft>? result = Supports ? hexes.Select(hex => new Aircraft(hex, null, 50.0, 20.0, null, false, null, null, 0.0, null, null)).ToList() : null;

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Source that only has the request for an area.
        /// </summary>
        private sealed class AreaOnlySource : IRateLimitedAircraftSource
        {
            /// <inheritdoc />
            public string Name => "area";

            /// <inheritdoc />
            public RateLimitStatus GetRateLimitStatus()
            {
                return RateLimitStatus.None;
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Creates a fallback over the sources, asked in the given order.
        /// </summary>
        /// <param name="order">Names in the order of preference.</param>
        /// <param name="sources">The sources.</param>
        /// <returns>The fallback source.</returns>
        private static FallbackAircraftSource CreateFallback(List<string> order, params IRateLimitedAircraftSource[] sources)
        {
            return new FallbackAircraftSource(sources, () => order, () => true, null);
        }

        /// <summary>
        /// The first source that has the request answers and the others are not asked.
        /// </summary>
        [Fact]
        public async Task Fallback_UsesFirstSourceThatHasTheRequest()
        {
            ByHexStub first = new ByHexStub("a");
            ByHexStub second = new ByHexStub("b");

            IReadOnlyList<Aircraft>? result = await CreateFallback(new List<string>(), new AreaOnlySource(), first, second).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None);

            Assert.Equal("abc123", Assert.Single(result!).Hex);
            Assert.Equal(1, first.Calls);
            Assert.Equal(0, second.Calls);
        }

        /// <summary>
        /// When no source has the request the answer is null, and a source that answers with null is skipped.
        /// </summary>
        [Fact]
        public async Task Fallback_NoSourceHasTheRequest_ReturnsNull()
        {
            ByHexStub unsupported = new ByHexStub("a") { Supports = false };
            ByHexStub second = new ByHexStub("b");

            Assert.Null(await CreateFallback(new List<string>(), new AreaOnlySource()).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
            Assert.Single((await CreateFallback(new List<string>(), unsupported, second).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None))!);
            Assert.Equal(1, second.Calls);
        }

        /// <summary>
        /// A paused or failing source is skipped for the next one.
        /// </summary>
        [Fact]
        public async Task Fallback_SkipsPausedAndFailingSources()
        {
            ByHexStub limited = new ByHexStub("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            ByHexStub failing = new ByHexStub("b") { Failure = new IOException("Socket closed") };
            ByHexStub good = new ByHexStub("c");

            IReadOnlyList<Aircraft>? result = await CreateFallback(new List<string>(), limited, failing, good).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None);

            Assert.Single(result!);
            Assert.Equal(1, good.Calls);
        }

        /// <summary>
        /// When every source with the request is paused the shortest wait is reported, and when one failed the failure is rethrown.
        /// </summary>
        [Fact]
        public async Task Fallback_AllUnavailable_Throws()
        {
            FallbackAircraftSource paused = CreateFallback(new List<string>(), new ByHexStub("a") { Block = TimeSpan.FromSeconds(50) }, new ByHexStub("b") { Block = TimeSpan.FromSeconds(20) });
            RateLimitedException limit = await Assert.ThrowsAsync<RateLimitedException>(() => paused.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
            FallbackAircraftSource failing = CreateFallback(new List<string>(), new ByHexStub("a") { Failure = new HttpRequestException("boom") });

            Assert.Equal(TimeSpan.FromSeconds(20), limit.RetryAfter);
            await Assert.ThrowsAsync<HttpRequestException>(() => failing.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
        }

        /// <summary>
        /// A cancellation asked by the caller is not a failure of the source.
        /// </summary>
        [Fact]
        public async Task Fallback_Cancellation_IsNotSwallowed()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            ByHexStub cancelled = new ByHexStub("a") { Failure = new OperationCanceledException(cancellation.Token) };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateFallback(new List<string>(), cancelled).GetAircraftByHexAsync(new List<string> { "abc123" }, cancellation.Token));
        }

        /// <summary>
        /// The pause wrapper passes the request on and answers with null when the wrapped source has none.
        /// </summary>
        [Fact]
        public async Task RateLimited_PassesTheRequestAndHandlesMissingOne()
        {
            ByHexStub inner = new ByHexStub("a");

            Assert.Single((await new RateLimitedAircraftSource(inner).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None))!);
            Assert.Null(await new RateLimitedAircraftSource(new AreaOnlySourceInner()).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
            inner.Supports = false;
            Assert.Null(await new RateLimitedAircraftSource(inner).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
        }

        /// <summary>
        /// A limit response to the request by address pauses the whole source and the next request does not reach it.
        /// </summary>
        [Fact]
        public async Task RateLimited_LimitResponsePausesTheSource()
        {
            ByHexStub inner = new ByHexStub("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(40)) };
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));

            Assert.False(source.GetRateLimitStatus().CanQuery);
            Assert.Equal(1, inner.Calls);
        }

        /// <summary>
        /// A failure other than a limit response is counted by the pause wrapper and passed on.
        /// </summary>
        [Fact]
        public async Task RateLimited_NetworkFailureIsRethrown()
        {
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(new ByHexStub("a") { Failure = new HttpRequestException("boom") });

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
        }

        /// <summary>
        /// The privacy decorator passes the addresses on unchanged and answers with null when the wrapped source has no such request.
        /// </summary>
        [Fact]
        public async Task Privacy_PassesAddressesOn()
        {
            ByHexStub inner = new ByHexStub("a");
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => PrivacyOptions.Exact, null, null);

            Assert.Single((await source.GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None))!);
            Assert.Equal("abc123", Assert.Single(inner.LastHexes));
            Assert.Null(await new PrivacyAircraftSource(new AreaOnlySourceInner(), () => PrivacyOptions.Exact, null, null).GetAircraftByHexAsync(new List<string> { "abc123" }, CancellationToken.None));
        }

        /// <summary>
        /// Plain source of aircraft that has only the request for an area.
        /// </summary>
        private sealed class AreaOnlySourceInner : IAircraftSource
        {
            /// <inheritdoc />
            public string Name => "area";

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }
        }
    }
}

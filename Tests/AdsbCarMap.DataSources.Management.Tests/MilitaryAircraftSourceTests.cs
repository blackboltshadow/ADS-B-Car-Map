using System.Net;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the request for all the military aircraft as it passes through the privacy decorator, the fallback and the pause of the source.
    /// </summary>
    public class MilitaryAircraftSourceTests
    {
        /// <summary>
        /// Source that can return the military aircraft; it answers, answers with null (no such request), pauses or fails as the test says.
        /// </summary>
        private sealed class MilitaryStub : IRateLimitedAircraftSource, IMilitaryAircraftSource
        {
            /// <summary>
            /// Creates the source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            public MilitaryStub(string name)
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
            /// Exception thrown by the military request, or null when the source answers.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Remaining block reported by the source.
            /// </summary>
            public TimeSpan Block { get; set; }

            /// <summary>
            /// Number of military requests that reached the source.
            /// </summary>
            public int MilitaryCalls { get; private set; }

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
            public Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken)
            {
                MilitaryCalls++;

                if (Failure is RateLimitedException rateLimited)
                {
                    Block = rateLimited.RetryAfter;
                }

                if (Failure != null)
                {
                    throw Failure;
                }

                IReadOnlyList<Aircraft>? result = Supports ? new List<Aircraft> { new Aircraft(Name, null, 50.0, 20.0, null, true, null, null, 0.0, null, null) } : null;

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Source that only has the request for an area, like most of the sources.
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
        /// <param name="events">List that receives the reported events.</param>
        /// <param name="order">Names in the order of preference.</param>
        /// <param name="sources">The sources.</param>
        /// <returns>The fallback source.</returns>
        private static FallbackAircraftSource CreateFallback(List<string> events, List<string> order, params IRateLimitedAircraftSource[] sources)
        {
            return new FallbackAircraftSource(sources, () => order, () => true, (SourceEventKind kind, string message, Exception? exception) => events.Add(kind + ":" + message));
        }

        /// <summary>
        /// The first source that has the request answers, and the others are not asked.
        /// </summary>
        [Fact]
        public async Task Fallback_FirstSupportingSourceAnswers()
        {
            MilitaryStub first = new MilitaryStub("a");
            MilitaryStub second = new MilitaryStub("b");
            FallbackAircraftSource source = CreateFallback(new List<string>(), new List<string> { "a", "b" }, first, second);

            IReadOnlyList<Aircraft>? result = await source.GetMilitaryAircraftAsync(CancellationToken.None);

            Assert.Equal("a", Assert.Single(result!).Hex);
            Assert.Equal(0, second.MilitaryCalls);
        }

        /// <summary>
        /// A source without the request is skipped, also one that answers with null, and the next one answers.
        /// </summary>
        [Fact]
        public async Task Fallback_SkipsSourcesWithoutTheRequest()
        {
            MilitaryStub unsupported = new MilitaryStub("a") { Supports = false };
            MilitaryStub second = new MilitaryStub("c");
            FallbackAircraftSource source = CreateFallback(new List<string>(), new List<string> { "area", "a", "c" }, new AreaOnlySource(), unsupported, second);

            IReadOnlyList<Aircraft>? result = await source.GetMilitaryAircraftAsync(CancellationToken.None);

            Assert.Equal("c", Assert.Single(result!).Hex);
            Assert.Equal(1, unsupported.MilitaryCalls);
        }

        /// <summary>
        /// When no source has the request the answer is null, so that the caller asks for an area.
        /// </summary>
        [Fact]
        public async Task Fallback_NoSourceHasTheRequest_ReturnsNull()
        {
            FallbackAircraftSource none = CreateFallback(new List<string>(), new List<string>(), new AreaOnlySource());
            FallbackAircraftSource unsupported = CreateFallback(new List<string>(), new List<string>(), new MilitaryStub("a") { Supports = false });

            Assert.Null(await none.GetMilitaryAircraftAsync(CancellationToken.None));
            Assert.Null(await unsupported.GetMilitaryAircraftAsync(CancellationToken.None));
        }

        /// <summary>
        /// A limit response or a failure of one source is reported and the next source that has the request answers.
        /// </summary>
        [Fact]
        public async Task Fallback_LimitedOrFailingSource_NextOneAnswers()
        {
            List<string> events = new List<string>();
            MilitaryStub limited = new MilitaryStub("a") { Failure = new RateLimitedException(TimeSpan.FromSeconds(30)) };
            MilitaryStub failing = new MilitaryStub("b") { Failure = new IOException("Socket closed") };
            MilitaryStub good = new MilitaryStub("c");
            FallbackAircraftSource source = CreateFallback(events, new List<string> { "a", "b", "c" }, limited, failing, good);

            IReadOnlyList<Aircraft>? result = await source.GetMilitaryAircraftAsync(CancellationToken.None);

            Assert.Equal("c", Assert.Single(result!).Hex);
            Assert.Equal(2, events.Count);
            Assert.Contains("military", events[0]);
            Assert.Contains("failed (IOException)", events[1]);
        }

        /// <summary>
        /// A source that is paused is not asked, and when all the sources that have the request are paused the shortest pause is reported.
        /// </summary>
        [Fact]
        public async Task Fallback_AllPaused_ThrowsRateLimitedWithShortestWait()
        {
            MilitaryStub first = new MilitaryStub("a") { Block = TimeSpan.FromSeconds(50) };
            MilitaryStub second = new MilitaryStub("b") { Block = TimeSpan.FromSeconds(20) };
            FallbackAircraftSource source = CreateFallback(new List<string>(), new List<string> { "a", "b" }, first, second);

            RateLimitedException exception = await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMilitaryAircraftAsync(CancellationToken.None));

            Assert.Equal(TimeSpan.FromSeconds(20), exception.RetryAfter);
            Assert.Equal(0, first.MilitaryCalls + second.MilitaryCalls);
        }

        /// <summary>
        /// When every source that has the request failed, the failure reaches the caller.
        /// </summary>
        [Fact]
        public async Task Fallback_AllFailed_ThrowsTheFailure()
        {
            MilitaryStub first = new MilitaryStub("a") { Failure = new HttpRequestException("boom", null, HttpStatusCode.BadGateway) };
            FallbackAircraftSource source = CreateFallback(new List<string>(), new List<string> { "a" }, first);

            await Assert.ThrowsAsync<HttpRequestException>(() => source.GetMilitaryAircraftAsync(CancellationToken.None));
        }

        /// <summary>
        /// A cancellation that the caller asked for is not a failure of the source.
        /// </summary>
        [Fact]
        public async Task Fallback_RequestedCancellation_IsNotSwallowed()
        {
            MilitaryStub first = new MilitaryStub("a") { Failure = new OperationCanceledException() };
            MilitaryStub second = new MilitaryStub("b");
            FallbackAircraftSource source = CreateFallback(new List<string>(), new List<string> { "a", "b" }, first, second);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetMilitaryAircraftAsync(cancellation.Token));

            Assert.Equal(0, second.MilitaryCalls);
        }

        /// <summary>
        /// Source of aircraft that has the military request and counts the requests.
        /// </summary>
        private sealed class InnerMilitary : IAircraftSource, IMilitaryAircraftSource
        {
            /// <inheritdoc />
            public string Name => "inner";

            /// <summary>
            /// Exception thrown by the military request, or null.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <summary>
            /// Whether the request answers with null.
            /// </summary>
            public bool ReturnsNull { get; set; }

            /// <summary>
            /// Number of military requests that reached the source.
            /// </summary>
            public int Calls { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken)
            {
                Calls++;

                if (Failure != null)
                {
                    throw Failure;
                }

                IReadOnlyList<Aircraft>? result = ReturnsNull ? null : new List<Aircraft> { new Aircraft("m1", null, 1.0, 2.0, null, true, null, null, 0.0, null, null) };

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Source of aircraft that has only the request for an area.
        /// </summary>
        private sealed class InnerAreaOnly : IAircraftSource
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

        /// <summary>
        /// The pause wrapper passes the request on, answers with null when the wrapped source has no such request, and does not ask the source when it is paused.
        /// </summary>
        [Fact]
        public async Task RateLimited_PassesTheRequestAndHandlesMissingOne()
        {
            InnerMilitary inner = new InnerMilitary();
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            Assert.Equal("m1", Assert.Single((await source.GetMilitaryAircraftAsync(CancellationToken.None))!).Hex);
            Assert.Null(await new RateLimitedAircraftSource(new InnerAreaOnly()).GetMilitaryAircraftAsync(CancellationToken.None));
            inner.ReturnsNull = true;
            Assert.Null(await source.GetMilitaryAircraftAsync(CancellationToken.None));
            Assert.Equal(2, inner.Calls);
        }

        /// <summary>
        /// A limit response to the military request pauses the whole source, area requests included, and the next military request does not reach it.
        /// </summary>
        [Fact]
        public async Task RateLimited_LimitResponsePausesTheSource()
        {
            InnerMilitary inner = new InnerMilitary { Failure = new RateLimitedException(TimeSpan.FromSeconds(40)) };
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMilitaryAircraftAsync(CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetMilitaryAircraftAsync(CancellationToken.None));
            await Assert.ThrowsAsync<RateLimitedException>(() => source.GetAircraftAsync(1.0, 2.0, 10.0, CancellationToken.None));

            Assert.False(source.GetRateLimitStatus().CanQuery);
            Assert.Equal(1, inner.Calls);
        }

        /// <summary>
        /// A failure that is not a limit response is remembered and rethrown without pausing the source; a requested cancellation is not remembered.
        /// </summary>
        [Fact]
        public async Task RateLimited_FailureIsRemembered()
        {
            InnerMilitary inner = new InnerMilitary { Failure = new IOException("Socket closed") };
            RateLimitedAircraftSource source = new RateLimitedAircraftSource(inner);

            await Assert.ThrowsAsync<IOException>(() => source.GetMilitaryAircraftAsync(CancellationToken.None));

            Assert.True(source.GetRateLimitStatus().CanQuery);
            Assert.NotNull(source.GetRateLimitStatus().LastFailure);

            inner.Failure = new OperationCanceledException();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetMilitaryAircraftAsync(cancellation.Token));
        }

        /// <summary>
        /// The privacy decorator passes the military request on without any position, and answers with null when the wrapped source has no such request.
        /// </summary>
        [Fact]
        public async Task Privacy_PassesTheRequest()
        {
            InnerMilitary inner = new InnerMilitary();
            PrivacyAircraftSource source = new PrivacyAircraftSource(inner, () => PrivacyOptions.Exact);

            Assert.Equal("m1", Assert.Single((await source.GetMilitaryAircraftAsync(CancellationToken.None))!).Hex);
            Assert.Null(await new PrivacyAircraftSource(new InnerAreaOnly(), () => PrivacyOptions.Exact).GetMilitaryAircraftAsync(CancellationToken.None));
            Assert.Equal(1, inner.Calls);
        }
    }
}

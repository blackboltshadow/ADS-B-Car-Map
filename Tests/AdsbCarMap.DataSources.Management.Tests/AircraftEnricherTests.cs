using System.Net;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of completing the data of a single aircraft from the other sources.
    /// </summary>
    public class AircraftEnricherTests
    {
        /// <summary>
        /// Source that returns a list of aircraft and answers lookups by the ICAO address with fixed details.
        /// </summary>
        private sealed class LookupSource : IAircraftSource, IAircraftLookupSource
        {
            /// <summary>
            /// Creates the source.
            /// </summary>
            /// <param name="name">Name of the source.</param>
            public LookupSource(string name)
            {
                Name = name;
            }

            /// <inheritdoc />
            public string Name { get; }

            /// <summary>
            /// Details returned by the lookup, or null when the source does not know the aircraft.
            /// </summary>
            public AircraftDetails? Details { get; set; }

            /// <summary>
            /// Exception thrown by the lookup or null when it answers.
            /// </summary>
            public Exception? LookupFailure { get; set; }

            /// <summary>
            /// Number of lookups that reached the source.
            /// </summary>
            public int Lookups { get; private set; }

            /// <inheritdoc />
            public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
            {
                IReadOnlyList<Aircraft> result = new List<Aircraft>();

                return Task.FromResult(result);
            }

            /// <inheritdoc />
            public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
            {
                Lookups++;

                if (LookupFailure != null)
                {
                    throw LookupFailure;
                }

                return Task.FromResult(Details);
            }
        }

        /// <summary>
        /// Time returned to the enricher.
        /// </summary>
        private DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates an aircraft with the given descriptive data.
        /// </summary>
        /// <param name="typeCode">Type code or null.</param>
        /// <param name="registration">Registration or null.</param>
        /// <param name="category">Category or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(string? typeCode, string? registration, string? category)
        {
            return new Aircraft("abc123", "LOT1", 52.0, 21.0, 35000, false, 450.0, 90.0, 1.5, typeCode, registration, category);
        }

        /// <summary>
        /// Creates the enricher over two sources.
        /// </summary>
        /// <param name="main">Source asked first.</param>
        /// <param name="other">Source asked after it.</param>
        /// <param name="enabled">Whether the enrichment is switched on.</param>
        /// <returns>The enricher.</returns>
        private AircraftEnricher Create(LookupSource main, LookupSource other, bool enabled = true)
        {
            FallbackAircraftSource sources = new FallbackAircraftSource(
                new List<IRateLimitedAircraftSource> { new RateLimitedAircraftSource(main), new RateLimitedAircraftSource(other) },
                () => new List<string> { main.Name, other.Name },
                () => true);

            TimedCache<AircraftDetails> cache = new TimedCache<AircraftDetails>(() => TimeSpan.FromHours(24), TimeSpan.FromMinutes(10), () => now, 100);

            return new AircraftEnricher(sources, () => enabled, cache);
        }

        /// <summary>
        /// Gaps are filled from another source and the main source, which has no new information, is not asked again.
        /// </summary>
        [Fact]
        public async Task Lookup_FillsGapsFromOtherSource()
        {
            LookupSource main = new LookupSource("main") { Details = new AircraftDetails("abc123", null, null, null) };
            LookupSource other = new LookupSource("other") { Details = new AircraftDetails("abc123", "A320", "D-AIZZ", "A3") };
            AircraftEnricher enricher = Create(main, other);

            Aircraft? result = await enricher.LookupAsync(Create(null, null, null), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("A320", result.TypeCode);
            Assert.Equal("D-AIZZ", result.Registration);
            Assert.Equal("A3", result.Category);
            Assert.Equal(1, other.Lookups);
        }

        /// <summary>
        /// A complete aircraft is not looked up at all.
        /// </summary>
        [Fact]
        public async Task Lookup_CompleteAircraft_AsksNobody()
        {
            LookupSource main = new LookupSource("main");
            LookupSource other = new LookupSource("other");
            AircraftEnricher enricher = Create(main, other);

            Aircraft? result = await enricher.LookupAsync(Create("B738", "SP-LWA", "A3"), CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, main.Lookups + other.Lookups);
        }

        /// <summary>
        /// With the enrichment switched off nobody is asked.
        /// </summary>
        [Fact]
        public async Task Lookup_Disabled_AsksNobody()
        {
            LookupSource main = new LookupSource("main") { Details = new AircraftDetails("abc123", "A320", null, null) };
            LookupSource other = new LookupSource("other") { Details = new AircraftDetails("abc123", "A320", null, null) };
            AircraftEnricher enricher = Create(main, other, false);

            Assert.False(enricher.NeedsLookup(Create(null, null, null)));
            Assert.Null(await enricher.LookupAsync(Create(null, null, null), CancellationToken.None));
            Assert.Equal(0, main.Lookups + other.Lookups);
        }

        /// <summary>
        /// The same aircraft is not asked about again within the retry interval, even when nothing was found, and is asked again after it.
        /// </summary>
        [Fact]
        public async Task Lookup_NotRepeatedWithinInterval()
        {
            LookupSource main = new LookupSource("main");
            LookupSource other = new LookupSource("other");
            AircraftEnricher enricher = Create(main, other);
            Aircraft aircraft = Create(null, null, null);

            Assert.Null(await enricher.LookupAsync(aircraft, CancellationToken.None));
            Assert.False(enricher.NeedsLookup(aircraft));
            Assert.Null(await enricher.LookupAsync(aircraft, CancellationToken.None));
            Assert.Equal(2, main.Lookups + other.Lookups);

            now += TimeSpan.FromMinutes(11);

            Assert.True(enricher.NeedsLookup(aircraft));
            Assert.Null(await enricher.LookupAsync(aircraft, CancellationToken.None));
            Assert.Equal(4, main.Lookups + other.Lookups);
        }

        /// <summary>
        /// A failing source is skipped and the next one still answers.
        /// </summary>
        [Fact]
        public async Task Lookup_FailingSource_NextOneAnswers()
        {
            LookupSource main = new LookupSource("main") { LookupFailure = new HttpRequestException("boom", null, HttpStatusCode.InternalServerError) };
            LookupSource other = new LookupSource("other") { Details = new AircraftDetails("abc123", "A320", "D-AIZZ", "A3") };
            AircraftEnricher enricher = Create(main, other);

            Aircraft? result = await enricher.LookupAsync(Create(null, null, null), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("A320", result.TypeCode);
        }

        /// <summary>
        /// Data found earlier is applied to the next download of the list without any request.
        /// </summary>
        [Fact]
        public async Task Apply_FillsKnownAircraftFromMemory()
        {
            LookupSource main = new LookupSource("main");
            LookupSource other = new LookupSource("other") { Details = new AircraftDetails("abc123", "A320", "D-AIZZ", "A3") };
            AircraftEnricher enricher = Create(main, other);
            Aircraft aircraft = Create(null, null, null);
            Aircraft untouched = new Aircraft("def456", null, 50.0, 20.0, null, false, null, null, 0.0, null, null);

            await enricher.LookupAsync(aircraft, CancellationToken.None);
            int lookups = main.Lookups + other.Lookups;
            IReadOnlyList<Aircraft> applied = enricher.Apply(new List<Aircraft> { aircraft, untouched });

            Assert.Equal("A320", applied[0].TypeCode);
            Assert.Same(untouched, applied[1]);
            Assert.Equal(lookups, main.Lookups + other.Lookups);
        }

        /// <summary>
        /// Before any lookup the list is returned unchanged.
        /// </summary>
        [Fact]
        public void Apply_NothingFound_ReturnsSameList()
        {
            AircraftEnricher enricher = Create(new LookupSource("main"), new LookupSource("other"));
            IReadOnlyList<Aircraft> list = new List<Aircraft> { Create(null, null, null) };

            Assert.Same(list, enricher.Apply(list));
        }

        /// <summary>
        /// Data that was found stays valid longer than the answer that nothing was found, so a known aircraft is not asked about again after the short interval.
        /// </summary>
        [Fact]
        public async Task Lookup_FoundData_StaysValidLongerThanNothingFound()
        {
            LookupSource main = new LookupSource("main");
            LookupSource other = new LookupSource("other") { Details = new AircraftDetails("abc123", "A320", null, null) };
            AircraftEnricher enricher = Create(main, other);
            Aircraft aircraft = Create(null, null, null);

            await enricher.LookupAsync(aircraft, CancellationToken.None);
            now += TimeSpan.FromHours(2);

            Assert.False(enricher.NeedsLookup(aircraft));

            now += TimeSpan.FromHours(23);

            Assert.True(enricher.NeedsLookup(aircraft));
        }

        /// <summary>
        /// A missing memory is reported at once, not as a null reference during the first download.
        /// </summary>
        [Fact]
        public void Constructor_NullCache_ThrowsAtOnce()
        {
            FallbackAircraftSource sources = new FallbackAircraftSource(new List<IRateLimitedAircraftSource> { new RateLimitedAircraftSource(new LookupSource("main")) }, () => new List<string>(), () => true);

            Assert.Throws<ArgumentNullException>(() => new AircraftEnricher(sources, () => true, null!));
        }
    }
}

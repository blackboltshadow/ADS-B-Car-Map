using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Enrichment
{
    /// <summary>
    /// Fills the type code, the registration and the emitter category of a single aircraft from the other sources when the main source did not give them. The sources are asked by the ICAO address, one aircraft at a time, so the cost is a few requests, not a second download of the whole area.
    /// </summary>
    public sealed class AircraftEnricher
    {
        /// <summary>
        /// Lock protecting the requests in progress.
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// Sources that return the position and the data of an aircraft, in the order of preference.
        /// </summary>
        private readonly FallbackAircraftSource sources;

        /// <summary>
        /// Returns whether the enrichment is switched on.
        /// </summary>
        private readonly Func<bool> enabled;

        /// <summary>
        /// Data found earlier by the ICAO address. A stored null means that no source knew more, so the aircraft is not asked about again for a while.
        /// </summary>
        private readonly TimedCache<AircraftDetails> cache;

        /// <summary>
        /// ICAO addresses with a request in progress.
        /// </summary>
        private readonly HashSet<string> inProgress = new HashSet<string>();

        /// <summary>
        /// Sources that only return the data of an aircraft by its ICAO address, asked before the position sources.
        /// </summary>
        private readonly IReadOnlyList<IRateLimitedSource> detailSources;

        /// <summary>
        /// Creates the enricher.
        /// </summary>
        /// <param name="sources">Sources that return the position and the data of an aircraft, in the order of preference.</param>
        /// <param name="detailSources">Sources that only return the data of an aircraft by its ICAO address, in the order of preference; they are asked before the position sources.</param>
        /// <param name="enabled">Returns whether the enrichment is switched on.</param>
        /// <param name="cache">Memory of the data found earlier, shared with the other parts of the application.</param>
        public AircraftEnricher(FallbackAircraftSource sources, IReadOnlyList<IRateLimitedSource> detailSources, Func<bool> enabled, TimedCache<AircraftDetails> cache)
        {
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(detailSources);
            ArgumentNullException.ThrowIfNull(cache);
            this.sources = sources;
            this.detailSources = detailSources;
            this.enabled = enabled;
            this.cache = cache;
        }

        /// <summary>
        /// Checks whether the aircraft has gaps that the other sources could fill (type code, registration or full type name).
        /// </summary>
        /// <param name="aircraft">The aircraft to check.</param>
        /// <returns>True when the enrichment is on, the aircraft has gaps and it was not asked about recently.</returns>
        public bool NeedsLookup(Aircraft aircraft)
        {
            if (!enabled() || !AircraftMerger.HasGaps(aircraft))
            {
                return false;
            }

            lock (gate)
            {
                if (inProgress.Contains(aircraft.Hex))
                {
                    return false;
                }

                return !cache.TryGet(aircraft.Hex, out _);
            }
        }

        /// <summary>
        /// Asks the other sources for the data that the aircraft lacks.
        /// </summary>
        /// <param name="aircraft">The aircraft from the main source.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aircraft with the filled data, or null when nothing was added.</returns>
        public async Task<Aircraft?> LookupAsync(Aircraft aircraft, CancellationToken cancellationToken)
        {
            if (!NeedsLookup(aircraft))
            {
                return null;
            }

            lock (gate)
            {
                if (!inProgress.Add(aircraft.Hex))
                {
                    return null;
                }
            }

            Aircraft merged = aircraft;

            try
            {
                string? mainSource = sources.LastUsedSourceName;

                foreach (IRateLimitedSource source in detailSources.Concat(sources.GetAllInOrder()))
                {
                    if (!AircraftMerger.HasGaps(merged))
                    {
                        break;
                    }

                    if (source is not IAircraftLookupSource lookup || string.Equals(source.Name, mainSource, StringComparison.OrdinalIgnoreCase) || !source.GetRateLimitStatus().CanQuery)
                    {
                        continue;
                    }

                    try
                    {
                        AircraftDetails? details = await lookup.GetAircraftDetailsAsync(aircraft.Hex, cancellationToken);

                        if (details != null)
                        {
                            merged = AircraftMerger.FillMissing(merged, details);
                        }
                    }
                    catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
                    {
                    }
                }
            }
            finally
            {
                AircraftDetails? found = ReferenceEquals(merged, aircraft) ? null : new AircraftDetails(merged.Hex, merged.TypeCode, merged.Registration, merged.Category, merged.TypeName, merged.Manufacturer, merged.Owner);

                lock (gate)
                {
                    inProgress.Remove(aircraft.Hex);
                    cache.Set(aircraft.Hex, found);
                }
            }

            return ReferenceEquals(merged, aircraft) ? null : merged;
        }

        /// <summary>
        /// Fills the gaps of the aircraft in the list with the data found earlier and still valid, without any request.
        /// </summary>
        /// <param name="aircraft">Aircraft from the main source.</param>
        /// <returns>The same list when nothing was added, otherwise a copy with the filled aircraft.</returns>
        public IReadOnlyList<Aircraft> Apply(IReadOnlyList<Aircraft> aircraft)
        {
            lock (gate)
            {
                if (cache.Count == 0)
                {
                    return aircraft;
                }

                List<Aircraft>? result = null;

                for (int i = 0; i < aircraft.Count; i++)
                {
                    if (!cache.TryGet(aircraft[i].Hex, out AircraftDetails? details) || details == null)
                    {
                        continue;
                    }

                    Aircraft filled = AircraftMerger.FillMissing(aircraft[i], details);

                    if (!ReferenceEquals(filled, aircraft[i]))
                    {
                        result ??= new List<Aircraft>(aircraft);
                        result[i] = filled;
                    }
                }

                return result ?? aircraft;
            }
        }
    }
}

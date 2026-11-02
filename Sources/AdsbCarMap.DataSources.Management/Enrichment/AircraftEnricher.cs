using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
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
        /// Default time during which an aircraft is not asked about again after an attempt.
        /// </summary>
        public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Lock protecting the attempts.
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// Sources in the order of preference.
        /// </summary>
        private readonly FallbackAircraftSource sources;

        /// <summary>
        /// Returns whether the enrichment is switched on.
        /// </summary>
        private readonly Func<bool> enabled;

        /// <summary>
        /// Time during which an aircraft is not asked about again after an attempt.
        /// </summary>
        private readonly TimeSpan retryInterval;

        /// <summary>
        /// Source of the current time in UTC, replaceable in tests.
        /// </summary>
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Results of the attempts by the ICAO address: the moment of the attempt and the data found, or null when nothing was found.
        /// </summary>
        private readonly Dictionary<string, (DateTime AttemptedUtc, AircraftDetails? Details)> attempts = new Dictionary<string, (DateTime AttemptedUtc, AircraftDetails? Details)>();

        /// <summary>
        /// ICAO addresses with a request in progress.
        /// </summary>
        private readonly HashSet<string> inProgress = new HashSet<string>();

        /// <summary>
        /// Creates the enricher with the default retry interval and the system clock.
        /// </summary>
        /// <param name="sources">Sources in the order of preference.</param>
        /// <param name="enabled">Returns whether the enrichment is switched on.</param>
        public AircraftEnricher(FallbackAircraftSource sources, Func<bool> enabled)
            : this(sources, enabled, DefaultRetryInterval, () => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Creates the enricher.
        /// </summary>
        /// <param name="sources">Sources in the order of preference.</param>
        /// <param name="enabled">Returns whether the enrichment is switched on.</param>
        /// <param name="retryInterval">Time during which an aircraft is not asked about again after an attempt.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        public AircraftEnricher(FallbackAircraftSource sources, Func<bool> enabled, TimeSpan retryInterval, Func<DateTime> utcNow)
        {
            this.sources = sources;
            this.enabled = enabled;
            this.retryInterval = retryInterval;
            this.utcNow = utcNow;
        }

        /// <summary>
        /// Checks whether the aircraft has gaps that the other sources could fill.
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

                return !attempts.TryGetValue(aircraft.Hex, out (DateTime AttemptedUtc, AircraftDetails? Details) attempt) || utcNow() - attempt.AttemptedUtc >= retryInterval;
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

                foreach (IRateLimitedAircraftSource source in sources.GetAllInOrder())
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
                    catch (Exception exception) when (exception is HttpRequestException || exception is JsonException)
                    {
                    }
                }
            }
            finally
            {
                AircraftDetails? found = ReferenceEquals(merged, aircraft) ? null : new AircraftDetails(merged.Hex, merged.TypeCode, merged.Registration, merged.Category);

                lock (gate)
                {
                    inProgress.Remove(aircraft.Hex);
                    attempts[aircraft.Hex] = (utcNow(), found);
                }
            }

            return ReferenceEquals(merged, aircraft) ? null : merged;
        }

        /// <summary>
        /// Fills the gaps of the aircraft in the list with the data found earlier, without any request.
        /// </summary>
        /// <param name="aircraft">Aircraft from the main source.</param>
        /// <returns>The same list when nothing was added, otherwise a copy with the filled aircraft.</returns>
        public IReadOnlyList<Aircraft> Apply(IReadOnlyList<Aircraft> aircraft)
        {
            lock (gate)
            {
                if (attempts.Count == 0)
                {
                    return aircraft;
                }

                List<Aircraft>? result = null;

                for (int i = 0; i < aircraft.Count; i++)
                {
                    if (!attempts.TryGetValue(aircraft[i].Hex, out (DateTime AttemptedUtc, AircraftDetails? Details) attempt) || attempt.Details == null)
                    {
                        continue;
                    }

                    Aircraft filled = AircraftMerger.FillMissing(aircraft[i], attempt.Details);

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

using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Selection
{
    /// <summary>
    /// Aircraft source that queries the sources in the order chosen by the user and, when a source gives no data (limit response, network or server error, invalid answer), asks the next one.
    /// </summary>
    public sealed class FallbackAircraftSource : IAircraftSource, IRateLimitedSource, IMilitaryAircraftSource
    {
        /// <summary>
        /// Sources in the order of registration.
        /// </summary>
        private readonly DataSourceRegistry<IRateLimitedAircraftSource> registry;

        /// <summary>
        /// Returns the names of the sources in the order of preference, read at every query so that a changed setting takes effect immediately.
        /// </summary>
        private readonly Func<IReadOnlyList<string>> order;

        /// <summary>
        /// Returns whether the other sources may be asked when the preferred one gives no data.
        /// </summary>
        private readonly Func<bool> fallbackEnabled;

        /// <summary>
        /// Name of the source that gave the last successful answer.
        /// </summary>
        private volatile string? lastUsedSourceName;

        /// <summary>
        /// Receives the events worth reporting: the kind, a description that is the same for the same event, and the exception if there was one. Null when nobody listens.
        /// </summary>
        private readonly Action<SourceEventKind, string, Exception?>? report;

        /// <summary>
        /// Creates the source.
        /// </summary>
        /// <param name="sources">Sources in the order of registration, used for the sources that the order does not name.</param>
        /// <param name="order">Returns the names of the sources in the order of preference; unknown names are ignored and sources that are not named come last.</param>
        /// <param name="fallbackEnabled">Returns whether the other sources may be asked when the preferred one gives no data.</param>
        /// <param name="report">Receives the failures of the sources and the switches between them, or null.</param>
        public FallbackAircraftSource(IEnumerable<IRateLimitedAircraftSource> sources, Func<IReadOnlyList<string>> order, Func<bool> fallbackEnabled, Action<SourceEventKind, string, Exception?>? report = null)
        {
            registry = new DataSourceRegistry<IRateLimitedAircraftSource>(sources);
            this.order = order;
            this.fallbackEnabled = fallbackEnabled;
            this.report = report;
        }

        /// <summary>
        /// Name of the first source in the order of preference.
        /// </summary>
        public string Name => GetAllInOrder()[0].Name;

        /// <summary>
        /// Name of the source that gave the last successful answer, or null before the first one.
        /// </summary>
        public string? LastUsedSourceName => lastUsedSourceName;

        /// <summary>
        /// Reads the state of the limits: the source can be queried when any source that may be used can be queried, otherwise the shortest pause is reported.
        /// </summary>
        /// <returns>The state of the limits.</returns>
        public RateLimitStatus GetRateLimitStatus()
        {
            RateLimitStatus? shortest = null;

            foreach (IRateLimitedAircraftSource source in GetOrdered())
            {
                RateLimitStatus status = source.GetRateLimitStatus();

                if (status.CanQuery)
                {
                    return status;
                }

                if (shortest == null || status.RemainingBlock < shortest.RemainingBlock)
                {
                    shortest = status;
                }
            }

            return shortest ?? RateLimitStatus.None;
        }

        /// <summary>
        /// Downloads the aircraft from the preferred source, falling back to the other sources when it gives no data.
        /// </summary>
        /// <param name="latitude">Latitude of the center.</param>
        /// <param name="longitude">Longitude of the center.</param>
        /// <param name="radiusNauticalMiles">Radius in nautical miles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aircraft list from the first source that answered.</returns>
        /// <exception cref="RateLimitedException">Every source that may be used is paused after a limit response.</exception>
        /// <exception cref="HttpRequestException">Every source failed and at least one failure was not a limit response; the last such failure is rethrown.</exception>
        public async Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
        {
            Exception? hardFailure = null;
            TimeSpan? shortestWait = null;

            foreach (IRateLimitedAircraftSource source in GetOrdered())
            {
                RateLimitStatus status = source.GetRateLimitStatus();

                if (!status.CanQuery)
                {
                    shortestWait = Shorter(shortestWait, status.RemainingBlock);
                    continue;
                }

                try
                {
                    IReadOnlyList<Aircraft> result = await source.GetAircraftAsync(latitude, longitude, radiusNauticalMiles, cancellationToken);
                    string? previous = lastUsedSourceName;
                    lastUsedSourceName = source.Name;

                    if (previous != null && !string.Equals(previous, source.Name, StringComparison.Ordinal))
                    {
                        report?.Invoke(SourceEventKind.Information, "Aircraft source changed from " + previous + " to " + source.Name, null);
                    }

                    return result;
                }
                catch (RateLimitedException exception)
                {
                    shortestWait = Shorter(shortestWait, exception.RetryAfter);
                    report?.Invoke(SourceEventKind.Failure, "Aircraft source " + source.Name + " answered with a limit response (429), paused", null);
                }
                catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
                {
                    hardFailure = exception;
                    report?.Invoke(SourceEventKind.Failure, "Aircraft source " + source.Name + " failed (" + exception.GetType().Name + ")", exception);
                }
            }

            if (hardFailure != null)
            {
                throw hardFailure;
            }

            throw new RateLimitedException(shortestWait ?? TimeSpan.Zero);
        }

        /// <summary>
        /// Downloads all the military aircraft from the preferred source that has such a request, falling back to the next one that has it when a source is paused or fails.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The military aircraft, or null when no source has such a request.</returns>
        /// <exception cref="RateLimitedException">Every source that has the request is paused after a limit response.</exception>
        /// <exception cref="HttpRequestException">Every source that has the request failed and at least one failure was not a limit response; the last such failure is rethrown.</exception>
        public async Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken)
        {
            Exception? hardFailure = null;
            TimeSpan? shortestWait = null;
            bool anySupports = false;

            foreach (IRateLimitedAircraftSource source in GetOrdered())
            {
                if (source is not IMilitaryAircraftSource military)
                {
                    continue;
                }

                RateLimitStatus status = source.GetRateLimitStatus();

                if (!status.CanQuery)
                {
                    anySupports = true;
                    shortestWait = Shorter(shortestWait, status.RemainingBlock);
                    continue;
                }

                try
                {
                    IReadOnlyList<Aircraft>? result = await military.GetMilitaryAircraftAsync(cancellationToken);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (RateLimitedException exception)
                {
                    anySupports = true;
                    shortestWait = Shorter(shortestWait, exception.RetryAfter);
                    report?.Invoke(SourceEventKind.Failure, "Aircraft source " + source.Name + " answered with a limit response (429) to the request for military aircraft, paused", null);
                }
                catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
                {
                    anySupports = true;
                    hardFailure = exception;
                    report?.Invoke(SourceEventKind.Failure, "Aircraft source " + source.Name + " failed (" + exception.GetType().Name + ") on the request for military aircraft", exception);
                }
            }

            if (!anySupports)
            {
                return null;
            }

            if (hardFailure != null)
            {
                throw hardFailure;
            }

            throw new RateLimitedException(shortestWait ?? TimeSpan.Zero);
        }

        /// <summary>
        /// Returns all sources in the order of preference: first those named by the order, then the rest in the order of registration.
        /// </summary>
        /// <returns>The ordered sources.</returns>
        public IReadOnlyList<IRateLimitedAircraftSource> GetAllInOrder()
        {
            return SourceOrder.Arrange(registry.Sources, order());
        }

        /// <summary>
        /// Returns the sources that may be asked now: the first one in the order of preference and, when the fallback is enabled, the others after it.
        /// </summary>
        /// <returns>The ordered sources.</returns>
        private IReadOnlyList<IRateLimitedAircraftSource> GetOrdered()
        {
            IReadOnlyList<IRateLimitedAircraftSource> all = GetAllInOrder();

            return fallbackEnabled() ? all : all.Take(1).ToList();
        }

        /// <summary>
        /// Returns the shorter of two waits, treating null as no wait known yet.
        /// </summary>
        /// <param name="current">Shortest wait so far or null.</param>
        /// <param name="candidate">Wait to compare.</param>
        /// <returns>The shorter wait.</returns>
        private static TimeSpan Shorter(TimeSpan? current, TimeSpan candidate)
        {
            return current.HasValue && current.Value < candidate ? current.Value : candidate;
        }
    }
}

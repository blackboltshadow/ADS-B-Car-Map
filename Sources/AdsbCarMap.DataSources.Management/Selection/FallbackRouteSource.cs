using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Selection
{
    /// <summary>
    /// Route source that queries the sources in the order chosen by the user and asks the next one when a source gives no answer (limit response, network or server error, invalid answer) or does not know the flight.
    /// </summary>
    public sealed class FallbackRouteSource : IRouteSource
    {
        /// <summary>
        /// Sources in the order of registration.
        /// </summary>
        private readonly DataSourceRegistry<IRateLimitedRouteSource> registry;

        /// <summary>
        /// Returns the names of the sources in the order of preference, read at every query so that a changed setting takes effect immediately.
        /// </summary>
        private readonly Func<IReadOnlyList<string>> order;

        /// <summary>
        /// Returns whether the other sources may be asked when the first one gives no route.
        /// </summary>
        private readonly Func<bool> fallbackEnabled;

        /// <summary>
        /// Receives the failures of the sources: the kind, a description that is the same for the same event, and the exception if there was one. Null when nobody listens.
        /// </summary>
        private readonly Action<SourceEventKind, string, Exception?>? report;

        /// <summary>
        /// Creates the source.
        /// </summary>
        /// <param name="sources">Sources in the order of registration, used for the sources that the order does not name.</param>
        /// <param name="order">Returns the names of the sources in the order of preference; unknown names are ignored and sources that are not named come last.</param>
        /// <param name="fallbackEnabled">Returns whether the other sources may be asked when the first one gives no route.</param>
        /// <param name="report">Receives the failures of the sources, or null.</param>
        public FallbackRouteSource(IEnumerable<IRateLimitedRouteSource> sources, Func<IReadOnlyList<string>> order, Func<bool> fallbackEnabled, Action<SourceEventKind, string, Exception?>? report = null)
        {
            registry = new DataSourceRegistry<IRateLimitedRouteSource>(sources);
            this.order = order;
            this.fallbackEnabled = fallbackEnabled;
            this.report = report;
        }

        /// <summary>
        /// Name of the first source in the order of preference.
        /// </summary>
        public string Name => GetAllInOrder()[0].Name;

        /// <summary>
        /// Returns all sources in the order of preference.
        /// </summary>
        /// <returns>The ordered sources.</returns>
        public IReadOnlyList<IRateLimitedRouteSource> GetAllInOrder()
        {
            return SourceOrder.Arrange(registry.Sources, order());
        }

        /// <summary>
        /// Downloads the route from the first source that knows it. A source that does not know the flight, is paused or fails is skipped. When at least one source answered that it does not know the flight and none gave a route, the result is null; when no source answered at all, the failure is reported.
        /// </summary>
        /// <param name="callsign">Callsign, e.g. "LOT281".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The route or null when no source knows the flight.</returns>
        /// <exception cref="RateLimitedException">Every source that may be used is paused after a limit response.</exception>
        /// <exception cref="HttpRequestException">No source answered and at least one failure was not a limit response; the last such failure is rethrown.</exception>
        public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            IReadOnlyList<IRateLimitedRouteSource> all = GetAllInOrder();
            IEnumerable<IRateLimitedRouteSource> ordered = fallbackEnabled() ? all : all.Take(1);
            Exception? hardFailure = null;
            TimeSpan? shortestWait = null;
            bool anySourceAnswered = false;

            foreach (IRateLimitedRouteSource source in ordered)
            {
                RateLimitStatus status = source.GetRateLimitStatus();

                if (!status.CanQuery)
                {
                    shortestWait = Shorter(shortestWait, status.RemainingBlock);
                    continue;
                }

                try
                {
                    FlightRoute? route = await source.GetRouteAsync(callsign, cancellationToken);
                    anySourceAnswered = true;

                    if (route != null)
                    {
                        return route;
                    }
                }
                catch (RateLimitedException exception)
                {
                    shortestWait = Shorter(shortestWait, exception.RetryAfter);
                    report?.Invoke(SourceEventKind.Failure, "Route source " + source.Name + " answered with a limit response (429), paused", null);
                }
                catch (Exception exception) when (exception is HttpRequestException || exception is JsonException)
                {
                    hardFailure = exception;
                    report?.Invoke(SourceEventKind.Failure, "Route source " + source.Name + " failed (" + exception.GetType().Name + ")", exception);
                }
            }

            if (anySourceAnswered)
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

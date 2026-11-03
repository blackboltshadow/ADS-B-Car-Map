using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Wraps a route source and pauses its requests after limit responses, so that the source itself only has to report them. When the wrapped source can also return the data of an aircraft by its ICAO address, that request goes through the same pause, because it is the same service.
    /// </summary>
    public sealed class RateLimitedRouteSource : IRateLimitedRouteSource, IAircraftLookupSource
    {
        /// <summary>
        /// The wrapped source.
        /// </summary>
        private readonly IRouteSource inner;

        /// <summary>
        /// Pauses the requests after limit responses.
        /// </summary>
        private readonly BackoffRateLimiter limiter;

        /// <summary>
        /// Creates the wrapper with the default pauses.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        public RateLimitedRouteSource(IRouteSource inner)
            : this(inner, new BackoffRateLimiter())
        {
        }

        /// <summary>
        /// Creates the wrapper with the given limiter.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        /// <param name="limiter">Limiter holding the pause state.</param>
        public RateLimitedRouteSource(IRouteSource inner, BackoffRateLimiter limiter)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(limiter);
            this.inner = inner;
            this.limiter = limiter;
        }

        /// <inheritdoc />
        public string Name => inner.Name;

        /// <inheritdoc />
        public RateLimitStatus GetRateLimitStatus()
        {
            return limiter.GetStatus();
        }

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            return RunAsync(() => inner.GetRouteAsync(callsign, cancellationToken), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
        {
            if (inner is not IAircraftLookupSource lookup)
            {
                return Task.FromResult<AircraftDetails?>(null);
            }

            return RunAsync(() => lookup.GetAircraftDetailsAsync(hex, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Runs a request of the wrapped source through the pause.
        /// </summary>
        /// <typeparam name="T">Kind of the answer.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Token of the request.</param>
        /// <returns>The answer of the source.</returns>
        private async Task<T> RunAsync<T>(Func<Task<T>> request, CancellationToken cancellationToken)
        {
            TimeSpan remainingBlock = limiter.GetRemainingBlock();

            if (remainingBlock > TimeSpan.Zero)
            {
                throw new RateLimitedException(remainingBlock);
            }

            try
            {
                T result = await request();
                limiter.Reset();

                return result;
            }
            catch (RateLimitedException exception)
            {
                throw new RateLimitedException(limiter.RegisterRateLimit(exception.RetryAfter));
            }
            catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
            {
                limiter.RegisterFailure(exception);

                throw;
            }
        }
    }
}

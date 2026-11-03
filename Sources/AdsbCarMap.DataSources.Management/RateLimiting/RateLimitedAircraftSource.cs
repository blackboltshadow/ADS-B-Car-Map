using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Wraps an aircraft source and pauses its requests after limit responses, so that the source itself only has to report them.
    /// </summary>
    public sealed class RateLimitedAircraftSource : IRateLimitedAircraftSource, IAircraftLookupSource
    {
        /// <summary>
        /// The wrapped source.
        /// </summary>
        private readonly IAircraftSource inner;

        /// <summary>
        /// Pauses the requests after limit responses.
        /// </summary>
        private readonly BackoffRateLimiter limiter;

        /// <summary>
        /// Creates the wrapper with the default pauses.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        public RateLimitedAircraftSource(IAircraftSource inner)
            : this(inner, new BackoffRateLimiter())
        {
        }

        /// <summary>
        /// Creates the wrapper with the given limiter.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        /// <param name="limiter">Limiter holding the pause state.</param>
        public RateLimitedAircraftSource(IAircraftSource inner, BackoffRateLimiter limiter)
        {
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
        public async Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
        {
            if (inner is not IAircraftLookupSource lookup)
            {
                return null;
            }

            TimeSpan remainingBlock = limiter.GetRemainingBlock();

            if (remainingBlock > TimeSpan.Zero)
            {
                throw new RateLimitedException(remainingBlock);
            }

            try
            {
                AircraftDetails? result = await lookup.GetAircraftDetailsAsync(hex, cancellationToken);
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

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public async Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
        {
            TimeSpan remainingBlock = limiter.GetRemainingBlock();

            if (remainingBlock > TimeSpan.Zero)
            {
                throw new RateLimitedException(remainingBlock);
            }

            try
            {
                IReadOnlyList<Aircraft> result = await inner.GetAircraftAsync(latitude, longitude, radiusNauticalMiles, cancellationToken);
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

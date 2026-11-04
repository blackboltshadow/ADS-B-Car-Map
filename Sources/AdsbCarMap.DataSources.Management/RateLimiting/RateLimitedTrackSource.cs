using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Wraps a flight path source and pauses its requests after limit responses, so that the source itself only has to report them.
    /// </summary>
    public sealed class RateLimitedTrackSource : IFlightTrackSource
    {
        /// <summary>
        /// The wrapped source.
        /// </summary>
        private readonly IFlightTrackSource inner;

        /// <summary>
        /// Pauses the requests after limit responses.
        /// </summary>
        private readonly BackoffRateLimiter limiter;

        /// <summary>
        /// Creates the wrapper with the default pauses.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        public RateLimitedTrackSource(IFlightTrackSource inner)
            : this(inner, new BackoffRateLimiter())
        {
        }

        /// <summary>
        /// Creates the wrapper with the given limiter.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        /// <param name="limiter">Limiter holding the pause state.</param>
        public RateLimitedTrackSource(IFlightTrackSource inner, BackoffRateLimiter limiter)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(limiter);
            this.inner = inner;
            this.limiter = limiter;
        }

        /// <inheritdoc />
        public string Name => inner.Name;

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public async Task<FlightTrack?> GetTrackAsync(string hex, CancellationToken cancellationToken)
        {
            TimeSpan remainingBlock = limiter.GetRemainingBlock();

            if (remainingBlock > TimeSpan.Zero)
            {
                throw new RateLimitedException(remainingBlock);
            }

            try
            {
                FlightTrack? result = await inner.GetTrackAsync(hex, cancellationToken);
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

using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.RateLimiting
{
    /// <summary>
    /// Wraps a weather source and pauses its requests after limit responses, so that the source itself only has to report them.
    /// </summary>
    public sealed class RateLimitedWeatherSource : IRateLimitedWeatherSource
    {
        /// <summary>
        /// The wrapped source.
        /// </summary>
        private readonly IWeatherSource inner;

        /// <summary>
        /// Pauses the requests after limit responses.
        /// </summary>
        private readonly BackoffRateLimiter limiter;

        /// <summary>
        /// Creates the wrapper with the default pauses.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        public RateLimitedWeatherSource(IWeatherSource inner)
            : this(inner, new BackoffRateLimiter())
        {
        }

        /// <summary>
        /// Creates the wrapper with the given limiter.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        /// <param name="limiter">Limiter holding the pause state.</param>
        public RateLimitedWeatherSource(IWeatherSource inner, BackoffRateLimiter limiter)
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
        public Task<IReadOnlyList<Metar>> GetMetarsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
        {
            return RunAsync(() => inner.GetMetarsAsync(icaoCodes, cancellationToken), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public Task<IReadOnlyList<Metar>> GetMetarsNearAsync(double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken)
        {
            return RunAsync(() => inner.GetMetarsNearAsync(latitude, longitude, radiusKilometers, cancellationToken), cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="RateLimitedException">The source is paused or has just answered with a limit response.</exception>
        public Task<IReadOnlyList<Taf>> GetTafsAsync(IReadOnlyCollection<string> icaoCodes, CancellationToken cancellationToken)
        {
            return RunAsync(() => inner.GetTafsAsync(icaoCodes, cancellationToken), cancellationToken);
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

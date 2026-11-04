using System.Globalization;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Privacy
{
    /// <summary>
    /// Source of the aircraft that sends the wrapped source the center of a query rounded to a grid, with the radius enlarged so that the real area stays covered. The options are read at every query, so a changed setting takes effect at once.
    /// </summary>
    public sealed class PrivacyAircraftSource : IAircraftSource, IRateLimitedSource, IMilitaryAircraftSource
    {
        /// <summary>
        /// Wrapped source.
        /// </summary>
        private readonly IAircraftSource inner;

        /// <summary>
        /// Returns the current options.
        /// </summary>
        private readonly Func<PrivacyOptions> options;

        /// <summary>
        /// Receives the description of a query when the options ask for it, or null when nobody listens.
        /// </summary>
        private readonly Action<SourceEventKind, string, Exception?>? report;

        /// <summary>
        /// Creates a source that rounds the position of every query.
        /// </summary>
        /// <param name="inner">Source that receives the rounded queries.</param>
        /// <param name="options">Returns the current options.</param>
        /// <param name="report">Receives the description of a query when <see cref="PrivacyOptions.LogRequests"/> is on, or null.</param>
        public PrivacyAircraftSource(IAircraftSource inner, Func<PrivacyOptions> options, Action<SourceEventKind, string, Exception?>? report = null)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(options);

            this.inner = inner;
            this.options = options;
            this.report = report;
        }

        /// <inheritdoc />
        public string Name => inner.Name;

        /// <summary>
        /// Reads the state of the limits of the wrapped source.
        /// </summary>
        /// <returns>The state of the limits, or the state without any limit when the wrapped source does not track them.</returns>
        public RateLimitStatus GetRateLimitStatus()
        {
            return inner is IRateLimitedSource limited ? limited.GetRateLimitStatus() : RateLimitStatus.None;
        }

        /// <summary>
        /// Passes the request for all the military aircraft to the wrapped source. It carries no position, so there is nothing to round.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The military aircraft, or null when the wrapped source has no such request.</returns>
        public Task<IReadOnlyList<Aircraft>?> GetMilitaryAircraftAsync(CancellationToken cancellationToken)
        {
            return inner is IMilitaryAircraftSource military ? military.GetMilitaryAircraftAsync(cancellationToken) : Task.FromResult<IReadOnlyList<Aircraft>?>(null);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Aircraft>> GetAircraftAsync(double latitude, double longitude, double radiusNauticalMiles, CancellationToken cancellationToken)
        {
            PrivacyOptions current = options();
            QueryArea area = CoordinateRounder.Round(latitude, longitude, radiusNauticalMiles, current);

            if (current.LogRequests)
            {
                report?.Invoke(
                    SourceEventKind.Information,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Zapytanie o samoloty: położenie {0:0.#####}, {1:0.#####} promień {2:0.#} NM, wysłano {3:0.#####}, {4:0.#####} promień {5:0.#} NM",
                        latitude,
                        longitude,
                        radiusNauticalMiles,
                        area.Latitude,
                        area.Longitude,
                        area.RadiusNauticalMiles),
                    null);
            }

            return inner.GetAircraftAsync(area.Latitude, area.Longitude, area.RadiusNauticalMiles, cancellationToken);
        }
    }
}

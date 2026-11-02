using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Wraps a route source whose routes are only pairs of airport codes and completes them from the airport database of the application: the IATA code, the city and the coordinates, so that the card shows the route and the flight progress. Data that the source already gave is kept.
    /// </summary>
    public sealed class AirportCompletingRouteSource : IRouteSource, IAircraftLookupSource
    {
        /// <summary>
        /// The wrapped source.
        /// </summary>
        private readonly IRouteSource inner;

        /// <summary>
        /// Airports bundled with the application.
        /// </summary>
        private readonly AirportDatabase airports;

        /// <summary>
        /// Creates the wrapper.
        /// </summary>
        /// <param name="inner">The source to wrap.</param>
        /// <param name="airports">Airports used to complete the routes.</param>
        public AirportCompletingRouteSource(IRouteSource inner, AirportDatabase airports)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(airports);
            this.inner = inner;
            this.airports = airports;
        }

        /// <inheritdoc />
        public string Name => inner.Name;

        /// <inheritdoc />
        public async Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
        {
            FlightRoute? route = await inner.GetRouteAsync(callsign, cancellationToken);

            return route == null ? null : Complete(route);
        }

        /// <inheritdoc />
        public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
        {
            if (inner is not IAircraftLookupSource lookup)
            {
                return Task.FromResult<AircraftDetails?>(null);
            }

            return lookup.GetAircraftDetailsAsync(hex, cancellationToken);
        }

        /// <summary>
        /// Completes the origin and the destination of a route from the airport database.
        /// </summary>
        /// <param name="route">Route from the source.</param>
        /// <returns>The same route when nothing was added, otherwise a copy with the completed airports.</returns>
        public FlightRoute Complete(FlightRoute route)
        {
            Airport? origin = airports.Find(route.OriginCode);
            Airport? destination = airports.Find(route.DestinationCode);

            FlightRoute completed = route with
            {
                OriginCode = origin != null ? origin.Code : route.OriginCode,
                OriginCity = string.IsNullOrEmpty(route.OriginCity) && origin != null && origin.Municipality.Length > 0 ? origin.Municipality : route.OriginCity,
                OriginLatitude = route.OriginLatitude ?? origin?.Latitude,
                OriginLongitude = route.OriginLongitude ?? origin?.Longitude,
                DestinationCode = destination != null ? destination.Code : route.DestinationCode,
                DestinationCity = string.IsNullOrEmpty(route.DestinationCity) && destination != null && destination.Municipality.Length > 0 ? destination.Municipality : route.DestinationCity,
                DestinationLatitude = route.DestinationLatitude ?? destination?.Latitude,
                DestinationLongitude = route.DestinationLongitude ?? destination?.Longitude,
            };

            return completed == route ? route : completed;
        }
    }
}

using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of completing a route that has only airport codes from the airport database.
    /// </summary>
    public class AirportCompletingRouteSourceTests
    {
        /// <summary>
        /// Route source returning a fixed route and, optionally, fixed details.
        /// </summary>
        private sealed class StubSource : IRouteSource, IAircraftLookupSource
        {
            /// <summary>
            /// Creates the source.
            /// </summary>
            /// <param name="route">Route returned by the source or null.</param>
            public StubSource(FlightRoute? route)
            {
                Route = route;
            }

            /// <inheritdoc />
            public string Name => "stub";

            /// <summary>
            /// Route returned by the source.
            /// </summary>
            public FlightRoute? Route { get; }

            /// <inheritdoc />
            public Task<FlightRoute?> GetRouteAsync(string callsign, CancellationToken cancellationToken)
            {
                return Task.FromResult(Route);
            }

            /// <inheritdoc />
            public Task<AircraftDetails?> GetAircraftDetailsAsync(string hex, CancellationToken cancellationToken)
            {
                return Task.FromResult<AircraftDetails?>(new AircraftDetails(hex, "B738", null, null));
            }
        }

        /// <summary>
        /// Builds a line of the airports file.
        /// </summary>
        /// <param name="icao">ICAO code.</param>
        /// <param name="iata">IATA code.</param>
        /// <param name="city">City.</param>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <returns>The tab separated line.</returns>
        private static string AirportLine(string icao, string iata, string city, double latitude, double longitude)
        {
            return string.Join("\t", icao, iata, "L", "Airport " + icao, city, "PL", latitude.ToString(CultureInfo.InvariantCulture), longitude.ToString(CultureInfo.InvariantCulture), "100", "1", "0");
        }

        /// <summary>
        /// Creates a loaded airport database.
        /// </summary>
        /// <returns>The database with Warsaw and London.</returns>
        private static AirportDatabase CreateDatabase()
        {
            AirportDatabase database = new AirportDatabase();
            database.LoadFrom(new string[] { AirportLine("EPWA", "WAW", "Warsaw", 52.1657, 20.9671), AirportLine("EGLL", "LHR", "London", 51.4706, -0.4619), AirportLine("EPXX", string.Empty, "Nowhere", 50.0, 20.0) }, Array.Empty<string>());

            return database;
        }

        /// <summary>
        /// A route with only ICAO codes gets the IATA codes, the cities and the coordinates.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_IcaoCodes_AreCompleted()
        {
            AirportCompletingRouteSource source = new AirportCompletingRouteSource(new StubSource(new FlightRoute("EPWA", null, "EGLL", null)), CreateDatabase());

            FlightRoute? route = await source.GetRouteAsync("LOT281", CancellationToken.None);

            Assert.NotNull(route);
            Assert.Equal("WAW", route.OriginCode);
            Assert.Equal("Warsaw", route.OriginCity);
            Assert.Equal("LHR", route.DestinationCode);
            Assert.Equal("London", route.DestinationCity);
            Assert.True(route.HasCoordinates);
            Assert.Equal(52.1657, route.OriginLatitude);
            Assert.Equal(-0.4619, route.DestinationLongitude);
        }

        /// <summary>
        /// An airport without an IATA code keeps its ICAO code, and unknown airports are left untouched.
        /// </summary>
        [Fact]
        public async Task GetRouteAsync_NoIata_KeepsIcaoAndUnknownAirport()
        {
            AirportCompletingRouteSource source = new AirportCompletingRouteSource(new StubSource(new FlightRoute("EPXX", null, "ZZZZ", null)), CreateDatabase());

            FlightRoute? route = await source.GetRouteAsync("LOT281", CancellationToken.None);

            Assert.NotNull(route);
            Assert.Equal("EPXX", route.OriginCode);
            Assert.Equal("Nowhere", route.OriginCity);
            Assert.Equal("ZZZZ", route.DestinationCode);
            Assert.Null(route.DestinationLatitude);
            Assert.False(route.HasCoordinates);
        }

        /// <summary>
        /// Data that the source already gave is not replaced.
        /// </summary>
        [Fact]
        public void Complete_KeepsDataFromTheSource()
        {
            AirportCompletingRouteSource source = new AirportCompletingRouteSource(new StubSource(null), CreateDatabase());
            FlightRoute given = new FlightRoute("WAW", "Warszawa", "LHR", "Londyn", "LOT Polish Airlines", 1.0, 2.0, 3.0, 4.0);

            FlightRoute completed = source.Complete(given);

            Assert.Same(given, completed);
        }

        /// <summary>
        /// An unknown flight stays unknown and the lookup of an aircraft is passed to the wrapped source.
        /// </summary>
        [Fact]
        public async Task UnknownRoute_StaysNull_AndLookupIsPassedOn()
        {
            AirportCompletingRouteSource source = new AirportCompletingRouteSource(new StubSource(null), CreateDatabase());

            Assert.Null(await source.GetRouteAsync("XXX1", CancellationToken.None));
            Assert.Equal("stub", source.Name);

            AircraftDetails? details = await source.GetAircraftDetailsAsync("abc123", CancellationToken.None);

            Assert.Equal("B738", details?.TypeCode);
        }
    }
}

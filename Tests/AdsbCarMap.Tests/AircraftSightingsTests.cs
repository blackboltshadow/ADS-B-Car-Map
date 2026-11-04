using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the log of the flights of the tracked aircraft.
    /// </summary>
    public class AircraftSightingsTests
    {
        /// <summary>
        /// Time used as the start in the tests.
        /// </summary>
        private static readonly DateTime Start = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a sample.
        /// </summary>
        /// <param name="minutes">Minutes after the start.</param>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <returns>The sample.</returns>
        private static Sighting Sample(double minutes, double latitude = 52.0, double longitude = 21.0)
        {
            return new Sighting(Start.AddMinutes(minutes), latitude, longitude, 30000, 450.0, false);
        }

        /// <summary>
        /// Notes a sighting without a route.
        /// </summary>
        /// <param name="log">Log.</param>
        /// <param name="callsign">Callsign.</param>
        /// <param name="sample">Sample.</param>
        /// <returns>True when the log changed.</returns>
        private static bool Observe(AircraftSightingLog log, string? callsign, Sighting sample)
        {
            return log.Observe("48A124", callsign, sample, null, null, null, null, null);
        }

        /// <summary>
        /// Samples closer in time and place than the limits are not stored, but the time of the last notice moves on.
        /// </summary>
        [Fact]
        public void Observe_ThrottlesSamples()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            Assert.True(Observe(log, "LOT1", Sample(0)));
            Assert.False(Observe(log, "LOT1", Sample(1)));
            Assert.True(Observe(log, "LOT1", Sample(3)));
            Assert.True(Observe(log, "LOT1", Sample(3.5, 52.2, 21.0)));

            FlightSegment flight = Assert.Single(log.GetFlights("48a124")).Flight;

            Assert.Equal(3, flight.Points.Count);
            Assert.Equal(Start.AddMinutes(3.5), flight.LastSeenUtc);
        }

        /// <summary>
        /// A long break and a change of the callsign start a new flight; a missing callsign does not.
        /// </summary>
        [Fact]
        public void Observe_StartsNewFlightOnGapAndCallsignChange()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            Observe(log, "LOT1", Sample(0));
            Observe(log, null, Sample(10));
            Observe(log, "lot1 ", Sample(20));
            Assert.Single(log.GetFlights("48a124"));

            Observe(log, "LOT2", Sample(30));
            Assert.Equal(2, log.GetFlights("48a124").Count);

            Observe(log, "LOT2", Sample(100));
            Assert.Equal(3, log.GetFlights("48a124").Count);
        }

        /// <summary>
        /// A callsign that appears after the first sample is stored in the flight.
        /// </summary>
        [Fact]
        public void Observe_FillsMissingCallsign()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            Observe(log, null, Sample(0));
            Observe(log, "LOT1", Sample(3));

            Assert.Equal("LOT1", Assert.Single(log.GetFlights("48a124")).Flight.Callsign);
        }

        /// <summary>
        /// The route is stored once, when it becomes known, and later routes do not overwrite it.
        /// </summary>
        [Fact]
        public void Observe_StoresRouteOnce()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            Assert.True(log.Observe("48a124", "LOT1", Sample(0), null, null, null, null, null));
            Assert.True(log.Observe("48a124", "LOT1", Sample(0.5), "WAW", "Warszawa", "LHR", "Londyn", "LOT"));
            Assert.False(log.Observe("48a124", "LOT1", Sample(0.7), "WAW", "Warszawa", "LHR", "Londyn", "LOT"));
            Assert.False(log.Observe("48a124", "LOT1", Sample(0.9), "KRK", null, "LGW", null, null));

            FlightSegment flight = Assert.Single(log.GetFlights("48a124")).Flight;

            Assert.Equal("WAW>LHR", flight.RouteKey);
            Assert.Equal("Londyn", flight.DestinationCity);
            Assert.Equal("LOT", flight.Airline);
        }

        /// <summary>
        /// A new callsign or a new route is marked, the first flight never is and a repeated flight is not marked.
        /// </summary>
        [Fact]
        public void GetFlights_MarksNovelty()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            log.Observe("a1", "LOT1", Sample(0), "WAW", null, "LHR", null, null);
            log.Observe("a1", "LOT1", Sample(100), "WAW", null, "LHR", null, null);
            log.Observe("a1", "LOT3", Sample(200), "WAW", null, "LHR", null, null);
            log.Observe("a1", "LOT5", Sample(300), "WAW", null, "CDG", null, null);
            log.Observe("a1", null, Sample(400), null, null, null, null, null);

            List<FlightInsight> flights = log.GetFlights("A1");

            Assert.Equal(5, flights.Count);
            Assert.Equal(FlightNovelty.None, flights[0].Novelty);
            Assert.Equal(FlightNovelty.NewCallsign | FlightNovelty.NewRoute, flights[1].Novelty);
            Assert.Equal("LOT5", flights[1].Flight.Callsign);
            Assert.Equal(FlightNovelty.NewCallsign, flights[2].Novelty);
            Assert.Equal(FlightNovelty.None, flights[3].Novelty);
            Assert.Equal(FlightNovelty.None, flights[4].Novelty);
        }

        /// <summary>
        /// A route is not called new when no earlier flight had a known route.
        /// </summary>
        [Fact]
        public void GetFlights_RouteNotNewWithoutEarlierRoute()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            log.Observe("a1", "LOT1", Sample(0), null, null, null, null, null);
            log.Observe("a1", "LOT1", Sample(100), "WAW", null, "LHR", null, null);

            Assert.Equal(FlightNovelty.None, log.GetFlights("a1")[0].Novelty);
        }

        /// <summary>
        /// The limits of the samples and of the flights drop the oldest ones.
        /// </summary>
        [Fact]
        public void Observe_DropsOldestAboveLimits()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            for (int index = 0; index < AircraftSightingLog.MaxPointsPerFlight + 10; index++)
            {
                Observe(log, "LOT1", Sample(index * 3));
            }

            FlightSegment flight = Assert.Single(log.GetFlights("48a124")).Flight;

            Assert.Equal(AircraftSightingLog.MaxPointsPerFlight, flight.Points.Count);
            Assert.Equal(Start.AddMinutes(30), flight.FirstSeenUtc);

            AircraftSightingLog many = new AircraftSightingLog();

            for (int index = 0; index < AircraftSightingLog.MaxFlightsPerAircraft + 5; index++)
            {
                many.Observe("a1", "F" + index, Sample(index * 100), null, null, null, null, null);
            }

            List<FlightInsight> flights = many.GetFlights("a1");

            Assert.Equal(AircraftSightingLog.MaxFlightsPerAircraft, flights.Count);
            Assert.Equal("F" + (AircraftSightingLog.MaxFlightsPerAircraft + 4), flights[0].Flight.Callsign);
        }

        /// <summary>
        /// Remove and RemoveWhere drop the flights of the chosen aircraft.
        /// </summary>
        [Fact]
        public void Remove_DropsAircraft()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            log.Observe("a1", "A", Sample(0), null, null, null, null, null);
            log.Observe("a2", "B", Sample(0), null, null, null, null, null);
            log.Observe("a3", "C", Sample(0), null, null, null, null, null);
            log.Remove("A1");
            log.RemoveWhere((string hex) => hex == "a2");

            Assert.Equal(1, log.AircraftCount);
            Assert.Empty(log.GetFlights("a1"));
            Assert.Single(log.GetFlights("a2"));
        }

        /// <summary>
        /// The text format keeps all fields; damaged text gives an empty log and damaged parts are skipped.
        /// </summary>
        [Fact]
        public void SerializeParse_RoundTripAndDamagedText()
        {
            AircraftSightingLog log = new AircraftSightingLog();

            log.Observe("a1", "LOT1", new Sighting(Start, 52.12345, 21.54321, 31000, 455.5, false), "WAW", "Warszawa", "LHR", "Londyn", "LOT");
            log.Observe("a1", "LOT1", new Sighting(Start.AddMinutes(5), 52.5, 21.0, null, null, true), null, null, null, null, null);
            log.Observe("a1", "LOT1", Sample(5.5, 52.5, 21.0), null, null, null, null, null);

            AircraftSightingLog loaded = AircraftSightingLog.Parse(log.Serialize());
            FlightSegment flight = Assert.Single(loaded.GetFlights("a1")).Flight;

            Assert.Equal("LOT1", flight.Callsign);
            Assert.Equal("WAW>LHR", flight.RouteKey);
            Assert.Equal("Warszawa", flight.OriginCity);
            Assert.Equal("LOT", flight.Airline);
            Assert.Equal(2, flight.Points.Count);
            Assert.Equal(31000, flight.Points[0].AltitudeFeet);
            Assert.Equal(455.5, flight.Points[0].SpeedKnots);
            Assert.Equal(52.12345, flight.Points[0].Latitude);
            Assert.Null(flight.Points[1].AltitudeFeet);
            Assert.True(flight.Points[1].OnGround);
            Assert.Equal(Start.AddMinutes(5.5), flight.LastSeenUtc);

            Assert.Equal(0, AircraftSightingLog.Parse("not json").AircraftCount);
            Assert.Equal(0, AircraftSightingLog.Parse(null).AircraftCount);
            Assert.Equal(0, AircraftSightingLog.Parse("{}").AircraftCount);
            Assert.Equal(0, AircraftSightingLog.Parse("[1, {\"hex\":\"a\"}, {\"hex\":\"b\",\"flights\":[{\"points\":[[1,2]]}]}]").AircraftCount);
        }

        /// <summary>
        /// Coordinates and durations are formatted in a fixed way.
        /// </summary>
        [Fact]
        public void SightingFormat_FormatsPositionAndDuration()
        {
            Assert.Equal("52.17°N 21.01°E", SightingFormat.Coordinates(52.17, 21.012));
            Assert.Equal("33.87°S 151.21°E", SightingFormat.Coordinates(-33.87, 151.21));
            Assert.Equal("40.71°N 74.01°W", SightingFormat.Coordinates(40.71, -74.006));
            Assert.Equal("<1 min", SightingFormat.Duration(TimeSpan.FromSeconds(30)));
            Assert.Equal("33 min", SightingFormat.Duration(TimeSpan.FromMinutes(33)));
            Assert.Equal("2 h 05 min", SightingFormat.Duration(TimeSpan.FromMinutes(125)));
        }
    }
}

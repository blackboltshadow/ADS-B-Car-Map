using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of finding the runway ends in use from the traffic near an airport, and of the line about them.
    /// </summary>
    public class RunwayInUseDetectorTests
    {
        /// <summary>
        /// Longitude of the eastern end of the test runway, which lies about 3 km east of the western end at 52 degrees north.
        /// </summary>
        private const double EastEnd = 20.044;

        /// <summary>
        /// Degrees of longitude that make one kilometer at 52 degrees north.
        /// </summary>
        private const double DegreesPerKilometer = 1.0 / 68.46;

        /// <summary>
        /// Creates the airport, which has an elevation of 300 feet.
        /// </summary>
        /// <returns>The airport.</returns>
        private static Airport CreateAirport()
        {
            return new Airport("EPXX", string.Empty, AirportType.Medium, "Test", "Test", "PL", 52.0, 20.022, 300, false, false);
        }

        /// <summary>
        /// Creates a runway that lies east-west, with 09 in the west and 27 in the east.
        /// </summary>
        /// <param name="latitude">Latitude of both ends.</param>
        /// <param name="lowName">Designator of the western end.</param>
        /// <param name="highName">Designator of the eastern end.</param>
        /// <returns>The runway.</returns>
        private static Runway CreateRunway(double latitude = 52.0, string lowName = "09", string highName = "27")
        {
            return new Runway(9800, 150, "ASP", true, lowName, latitude, 20.0, highName, latitude, EastEnd);
        }

        /// <summary>
        /// Creates an aircraft.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="track">Ground track in degrees.</param>
        /// <param name="verticalRate">Vertical rate in feet per minute, or null.</param>
        /// <param name="altitude">Altitude in feet.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="speed">Ground speed in knots.</param>
        /// <param name="age">Age of the position in seconds.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreatePlane(double latitude, double longitude, double? track, int? verticalRate, int altitude = 1500, bool onGround = false, double speed = 120.0, double age = 1.0)
        {
            return new Aircraft("48" + Guid.NewGuid().ToString("N").Substring(0, 4), null, latitude, longitude, altitude, onGround, speed, track, age, null, null, null, false, verticalRate);
        }

        /// <summary>
        /// Creates an aircraft descending on the final approach to 27, the given distance east of the eastern end.
        /// </summary>
        /// <param name="kilometers">Distance from the threshold in kilometers.</param>
        /// <param name="latitude">Latitude.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Landing27(double kilometers, double latitude = 52.0)
        {
            return CreatePlane(latitude, EastEnd + kilometers * DegreesPerKilometer, 270.0, -700);
        }

        /// <summary>
        /// Creates an aircraft climbing away from 09, the given distance east of the eastern end of the runway.
        /// </summary>
        /// <param name="kilometers">Distance behind the far end in kilometers.</param>
        /// <param name="latitude">Latitude.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Departing09(double kilometers, double latitude = 52.0)
        {
            return CreatePlane(latitude, EastEnd + kilometers * DegreesPerKilometer, 90.0, 1500);
        }

        /// <summary>
        /// Two aircraft on the final approach to the same end give that end, with the approach counted.
        /// </summary>
        [Fact]
        public void Detect_AircraftOnFinal_ReportsTheEnd()
        {
            IReadOnlyList<RunwayTrafficEnd> ends = RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, new List<Aircraft> { Landing27(4.0), Landing27(8.0) });

            RunwayTrafficEnd end = Assert.Single(ends);
            Assert.Equal("27", end.EndName);
            Assert.True(end.IsHighEnd);
            Assert.Equal(2, end.Approaching);
            Assert.Equal(0, end.Departing);
            Assert.InRange(end.HeadingDegrees, 269.0, 271.0);
        }

        /// <summary>
        /// Climbing aircraft behind the far end of the runway took off from the near end.
        /// </summary>
        [Fact]
        public void Detect_ClimbingBehindTheFarEnd_CountsDepartures()
        {
            IReadOnlyList<RunwayTrafficEnd> ends = RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, new List<Aircraft> { Departing09(1.0), Departing09(5.0) });

            RunwayTrafficEnd end = Assert.Single(ends);
            Assert.Equal("09", end.EndName);
            Assert.Equal(2, end.Departing);
        }

        /// <summary>
        /// Fast aircraft on the ground along the runway are on a landing or takeoff run, and slow ones are taxiing and do not count.
        /// </summary>
        [Fact]
        public void Detect_GroundRun_CountsOnlyFastAircraft()
        {
            List<Aircraft> traffic = new List<Aircraft>
            {
                CreatePlane(52.0, 20.01, 270.0, 0, 300, true, 80.0),
                CreatePlane(52.0, 20.03, 270.0, 0, 300, true, 60.0),
                CreatePlane(52.0, 20.02, 270.0, 0, 300, true, 15.0),
            };

            RunwayTrafficEnd end = Assert.Single(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, traffic));

            Assert.Equal("27", end.EndName);
            Assert.Equal(2, end.OnRunway);
        }

        /// <summary>
        /// One aircraft is not enough, because it may just pass by.
        /// </summary>
        [Fact]
        public void Detect_SingleAircraft_ReportsNothing()
        {
            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, new List<Aircraft> { Landing27(4.0) }));
            Assert.Equal(2, RunwayInUseDetector.MinimumAircraft);
        }

        /// <summary>
        /// Aircraft that are not lined up are ignored: off the centerline, flying across, in level flight, with an old position, without a track or too far away.
        /// </summary>
        [Fact]
        public void Detect_AircraftNotLinedUp_AreIgnored()
        {
            List<Aircraft> traffic = new List<Aircraft>
            {
                Landing27(4.0, 52.02),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, 180.0, -700),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, 270.0, 0),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, 270.0, null),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, 270.0, -700, 1500, false, 120.0, 90.0),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, null, -700),
                CreatePlane(52.0, EastEnd + 4.0 * DegreesPerKilometer, 270.0, -700, 9000),
                CreatePlane(52.0, EastEnd + 40.0 * DegreesPerKilometer, 270.0, -700),
            };

            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, traffic));
        }

        /// <summary>
        /// The opposite end of the same runway is left out when it has fewer aircraft, and both are left out when they are equal.
        /// </summary>
        [Fact]
        public void Detect_OppositeEnds_BusierWins()
        {
            List<Aircraft> busier = new List<Aircraft> { Landing27(3.0), Landing27(6.0), Landing27(9.0), Departing09(2.0), Departing09(5.0) };
            List<Aircraft> equal = new List<Aircraft> { Landing27(3.0), Landing27(6.0), Departing09(2.0), Departing09(5.0) };

            RunwayTrafficEnd end = Assert.Single(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, busier));

            Assert.Equal("27", end.EndName);
            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, equal));
        }

        /// <summary>
        /// Each aircraft counts for the parallel runway whose centerline is closest, and both runways can be reported.
        /// </summary>
        [Fact]
        public void Detect_ParallelRunways_EachAircraftVotesForTheClosest()
        {
            double northLatitude = 52.0 + 0.35 / 111.19;
            List<Runway> runways = new List<Runway> { CreateRunway(52.0, "09R", "27L"), CreateRunway(northLatitude, "09L", "27R") };
            List<Aircraft> traffic = new List<Aircraft> { Landing27(5.0), Landing27(8.0), Landing27(5.0, northLatitude), Landing27(8.0, northLatitude) };

            IReadOnlyList<RunwayTrafficEnd> ends = RunwayInUseDetector.Detect(CreateAirport(), runways, traffic);

            Assert.Equal(2, ends.Count);
            Assert.Equal(new List<string> { "27L", "27R" }, ends.Select(end => end.EndName).OrderBy(name => name, StringComparer.Ordinal).ToList());
            Assert.All(ends, end => Assert.Equal(2, end.Approaching));
        }

        /// <summary>
        /// Without runways, without traffic or with runways that have no usable coordinates nothing is reported.
        /// </summary>
        [Fact]
        public void Detect_NoRunwaysOrTraffic_ReportsNothing()
        {
            Runway point = new Runway(0, 0, string.Empty, false, "H1", 52.0, 20.0, "H1", 52.0, 20.0);

            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway>(), new List<Aircraft> { Landing27(4.0), Landing27(8.0) }));
            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { CreateRunway() }, new List<Aircraft>()));
            Assert.Empty(RunwayInUseDetector.Detect(CreateAirport(), new List<Runway> { point }, new List<Aircraft> { Landing27(4.0), Landing27(8.0) }));
        }

        /// <summary>
        /// The line names the ends with the counts and leaves out the counts that are zero.
        /// </summary>
        [Fact]
        public void BuildLine_NamesEndsAndCounts()
        {
            Runway runway = CreateRunway();
            List<RunwayTrafficEnd> ends = new List<RunwayTrafficEnd>
            {
                new RunwayTrafficEnd(runway, true, "27", 270.0, 2, 1, 0),
                new RunwayTrafficEnd(runway, false, "09", 90.0, 0, 0, 3),
            };

            Assert.Equal("Pas wg ruchu: 27 (przyloty 2, odloty 1), 09 (na pasie 3)", RunwayTrafficText.BuildLine(ends));
            Assert.Equal(string.Empty, RunwayTrafficText.BuildLine(new List<RunwayTrafficEnd>()));
        }
    }
}

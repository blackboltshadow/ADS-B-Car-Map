using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the planning of the aircraft requests for the alerts.
    /// </summary>
    public class AlertQueryPlannerTests
    {
        /// <summary>
        /// Airport lookup that knows only EPWA.
        /// </summary>
        private static readonly Func<string, GeoPoint?> Airports = (string icao) => icao == "EPWA" ? new GeoPoint(52.1657, 20.9671) : null;

        /// <summary>
        /// Creates a circle.
        /// </summary>
        /// <param name="latitude">Latitude of the center.</param>
        /// <param name="longitude">Longitude of the center.</param>
        /// <param name="radius">Radius in kilometers.</param>
        /// <returns>The circle.</returns>
        private static AlertCircle Circle(double latitude, double longitude, double radius)
        {
            return new AlertCircle(new GeoPoint(latitude, longitude), radius);
        }

        /// <summary>
        /// Checks that a planned circle contains an original circle.
        /// </summary>
        /// <param name="planned">Planned circle.</param>
        /// <param name="original">Original circle.</param>
        /// <returns>True when the original circle lies inside the planned one.</returns>
        private static bool Contains(AlertCircle planned, AlertCircle original)
        {
            double distance = GeoMath.DistanceKilometers(planned.Center.Latitude, planned.Center.Longitude, original.Center.Latitude, original.Center.Longitude);

            return distance + original.RadiusKilometers <= planned.RadiusKilometers + 0.01;
        }

        /// <summary>
        /// A single area stays as it is.
        /// </summary>
        [Fact]
        public void Plan_SingleCircle_IsUnchanged()
        {
            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { Circle(52.0, 21.0, 50.0) });

            Assert.Single(plan);
            Assert.Equal(50.0, plan[0].RadiusKilometers);
        }

        /// <summary>
        /// Close areas are merged into one circle that contains both.
        /// </summary>
        [Fact]
        public void Plan_CloseCircles_AreMerged()
        {
            AlertCircle first = Circle(52.0, 21.0, 50.0);
            AlertCircle second = Circle(52.1, 21.0, 50.0);

            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { first, second });

            Assert.Single(plan);
            Assert.True(Contains(plan[0], first));
            Assert.True(Contains(plan[0], second));
        }

        /// <summary>
        /// A circle inside another one is absorbed without growing it.
        /// </summary>
        [Fact]
        public void Plan_ContainedCircle_IsAbsorbed()
        {
            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { Circle(52.0, 21.0, 100.0), Circle(52.05, 21.0, 10.0) });

            Assert.Single(plan);
            Assert.Equal(100.0, plan[0].RadiusKilometers);
        }

        /// <summary>
        /// Distant areas stay separate.
        /// </summary>
        [Fact]
        public void Plan_DistantCircles_StaySeparate()
        {
            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { Circle(52.0, 21.0, 50.0), Circle(50.0, 19.9, 50.0), Circle(54.4, 18.6, 50.0) });

            Assert.Equal(3, plan.Count);
        }

        /// <summary>
        /// Areas that are close but would need a circle much larger than the larger of them stay separate.
        /// </summary>
        [Fact]
        public void Plan_WouldGrowTooMuch_StaysSeparate()
        {
            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { Circle(52.0, 21.0, 20.0), Circle(52.6, 21.0, 20.0) });

            Assert.Equal(2, plan.Count);
        }

        /// <summary>
        /// A merge can enable another one, and the result still covers every area.
        /// </summary>
        [Fact]
        public void Plan_ChainOfCircles_CoversAll()
        {
            List<AlertCircle> circles = new List<AlertCircle> { Circle(52.0, 21.0, 60.0), Circle(52.3, 21.0, 60.0), Circle(52.15, 21.2, 60.0) };

            List<AlertCircle> plan = AlertQueryPlanner.Plan(circles);

            foreach (AlertCircle circle in circles)
            {
                Assert.Contains(plan, (AlertCircle planned) => Contains(planned, circle));
            }

            Assert.True(plan.Count < circles.Count);
        }

        /// <summary>
        /// A radius larger than the servers accept is shortened.
        /// </summary>
        [Fact]
        public void Plan_HugeRadius_IsCapped()
        {
            List<AlertCircle> plan = AlertQueryPlanner.Plan(new List<AlertCircle> { Circle(52.0, 21.0, 2000.0) });

            Assert.Equal(AlertQueryPlanner.MaximumRadiusNauticalMiles * AlertQueryPlanner.KilometersPerNauticalMile, plan[0].RadiusKilometers, 6);
        }

        /// <summary>
        /// Conversion to nautical miles rounds up and stays within the limits.
        /// </summary>
        [Fact]
        public void ToNauticalMiles_RoundsUpAndClamps()
        {
            Assert.Equal(1.0, AlertQueryPlanner.ToNauticalMiles(0.1));
            Assert.Equal(27.0, AlertQueryPlanner.ToNauticalMiles(50.0));
            Assert.Equal(250.0, AlertQueryPlanner.ToNauticalMiles(5000.0));
        }

        /// <summary>
        /// Only the rules with a known center give a circle.
        /// </summary>
        [Fact]
        public void CollectCircles_SkipsRulesWithoutCenter()
        {
            AlertRule me = new AlertRule("a", "Me", true, new AlertArea(AlertAreaKind.AroundMe, 30.0), null, true, true, 30);
            AlertRule waw = new AlertRule("b", "Waw", true, new AlertArea(AlertAreaKind.AroundAirport, 20.0, null, "EPWA"), null, true, true, 30);
            AlertRule unknown = new AlertRule("c", "Xxx", true, new AlertArea(AlertAreaKind.AroundAirport, 20.0, null, "XXXX"), null, true, true, 30);
            List<AlertRule> rules = new List<AlertRule> { me, waw, unknown };

            Assert.Single(AlertQueryPlanner.CollectCircles(rules, null, Airports));

            List<AlertCircle> withPosition = AlertQueryPlanner.CollectCircles(rules, new GeoPoint(50.0, 20.0), Airports);

            Assert.Equal(2, withPosition.Count);
            Assert.Equal(30.0, withPosition[0].RadiusKilometers);
            Assert.Equal(20.0, withPosition[1].RadiusKilometers);
        }
    }
}

using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of choosing the runway end that the wind favors.
    /// </summary>
    public class RunwayWindTests
    {
        /// <summary>
        /// Creates a runway that lies east-west near 52 degrees north, with the low end in the west.
        /// </summary>
        /// <param name="lowName">Designator of the western end.</param>
        /// <param name="highName">Designator of the eastern end.</param>
        /// <param name="lengthFeet">Length in feet.</param>
        /// <param name="latitude">Latitude of both ends.</param>
        /// <returns>The runway.</returns>
        private static Runway EastWest(string lowName, string highName, int lengthFeet = 9800, double latitude = 52.0)
        {
            return new Runway(lengthFeet, 150, "ASP", true, lowName, latitude, 20.0, highName, latitude, 20.1);
        }

        /// <summary>
        /// Creates a runway that lies north-south, with the low end in the south. An aircraft that uses it from the southern end flies north, so that end is 36 and the northern end is 18.
        /// </summary>
        /// <param name="lowName">Designator of the southern end.</param>
        /// <param name="highName">Designator of the northern end.</param>
        /// <param name="lengthFeet">Length in feet.</param>
        /// <returns>The runway.</returns>
        private static Runway NorthSouth(string lowName, string highName, int lengthFeet = 9800)
        {
            return new Runway(lengthFeet, 150, "ASP", true, lowName, 52.0, 20.0, highName, 52.1, 20.0);
        }

        /// <summary>
        /// Aircraft take off and land into the wind, so the favored end is the one whose direction of use points toward the place the wind blows from: a wind from the west favors the end used toward the west, which is the eastern end, 27.
        /// </summary>
        [Theory]
        [InlineData(270, "27", true)]
        [InlineData(90, "09", false)]
        [InlineData(260, "27", true)]
        [InlineData(100, "09", false)]
        public void Choose_PicksTheEndFacingTheWind(int windFrom, string expected, bool isHigh)
        {
            RunwayWindResult? result = RunwayWind.Choose(new List<Runway> { EastWest("09", "27") }, windFrom, false, 12);

            Assert.NotNull(result);
            Assert.Equal(expected, result.EndName);
            Assert.Equal(isHigh, result.IsHighEnd);
            Assert.True(result.HeadwindKnots > 10.0);
        }

        /// <summary>
        /// A wind straight along the runway has no crosswind, and a wind at an angle gives the components with the side it blows from.
        /// </summary>
        [Fact]
        public void Choose_ComponentsAndSide()
        {
            RunwayWindResult aligned = RunwayWind.Choose(new List<Runway> { EastWest("09", "27") }, 270, false, 10)!;
            RunwayWindResult fromLeft = RunwayWind.Choose(new List<Runway> { EastWest("09", "27") }, 250, false, 10)!;
            RunwayWindResult fromRight = RunwayWind.Choose(new List<Runway> { EastWest("09", "27") }, 290, false, 10)!;

            Assert.Equal(10.0, aligned.HeadwindKnots, 1);
            Assert.Equal(0.0, aligned.CrosswindKnots, 1);
            Assert.Equal(9.4, fromLeft.HeadwindKnots, 1);
            Assert.True(fromLeft.CrosswindKnots < -3.0);
            Assert.True(fromRight.CrosswindKnots > 3.0);
            Assert.Equal(270.0, aligned.HeadingDegrees, 0);
        }

        /// <summary>
        /// A wind across the runway gives almost no headwind and the whole wind as the crosswind.
        /// </summary>
        [Fact]
        public void Choose_WindAcrossTheRunway()
        {
            RunwayWindResult result = RunwayWind.Choose(new List<Runway> { EastWest("09", "27") }, 180, false, 10)!;

            Assert.InRange(result.HeadwindKnots, -0.5, 0.5);
            Assert.Equal(10.0, Math.Abs(result.CrosswindKnots), 1);
        }

        /// <summary>
        /// A wind that is too weak, variable or not known favors no runway.
        /// </summary>
        [Fact]
        public void Choose_WeakVariableOrUnknownWind_GivesNothing()
        {
            List<Runway> runways = new List<Runway> { EastWest("09", "27") };

            Assert.Null(RunwayWind.Choose(runways, 270, false, RunwayWind.MinimumWindKnots - 1));
            Assert.NotNull(RunwayWind.Choose(runways, 270, false, RunwayWind.MinimumWindKnots));
            Assert.Null(RunwayWind.Choose(runways, null, false, 12));
            Assert.Null(RunwayWind.Choose(runways, 270, true, 12));
            Assert.Null(RunwayWind.Choose(runways, 270, false, null));
        }

        /// <summary>
        /// Without runways, or with runways whose ends are in the same place, nothing is chosen.
        /// </summary>
        [Fact]
        public void Choose_NoUsableRunway_GivesNothing()
        {
            Runway point = new Runway(5000, 100, "ASP", false, "09", 52.0, 20.0, "27", 52.0, 20.0);

            Assert.Null(RunwayWind.Choose(new List<Runway>(), 270, false, 12));
            Assert.Null(RunwayWind.Choose(new List<Runway> { point }, 270, false, 12));
        }

        /// <summary>
        /// Of crossing runways the one with the largest headwind is chosen.
        /// </summary>
        [Fact]
        public void Choose_CrossingRunways_PicksTheBestEnd()
        {
            List<Runway> runways = new List<Runway> { EastWest("09", "27"), NorthSouth("36", "18") };

            Assert.Equal("36", RunwayWind.Choose(runways, 350, false, 12)!.EndName);
            Assert.Equal("27", RunwayWind.Choose(runways, 265, false, 12)!.EndName);
            Assert.Equal("18", RunwayWind.Choose(runways, 175, false, 12)!.EndName);
        }

        /// <summary>
        /// A runway that is much shorter than the longest one is not chosen even when it is aligned with the wind better.
        /// </summary>
        [Fact]
        public void Choose_IgnoresMuchShorterRunways()
        {
            List<Runway> runways = new List<Runway> { EastWest("09", "27", 9800), NorthSouth("36", "18", 2000) };

            Assert.Equal("27", RunwayWind.Choose(runways, 330, false, 12)!.EndName);
        }

        /// <summary>
        /// When two runways suit the wind about equally the longer one is chosen, and for equal runways the lower designator.
        /// </summary>
        [Fact]
        public void Choose_Ties_PreferTheLongerRunwayThenTheLowerDesignator()
        {
            List<Runway> unequal = new List<Runway> { EastWest("09L", "27R", 9000, 52.0), EastWest("09R", "27L", 11000, 52.01) };
            List<Runway> equal = new List<Runway> { EastWest("09R", "27L", 9000, 52.01), EastWest("09L", "27R", 9000, 52.0) };

            Assert.Equal("27L", RunwayWind.Choose(unequal, 270, false, 10)!.EndName);
            Assert.Equal("27L", RunwayWind.Choose(equal, 270, false, 10)!.EndName);
        }

        /// <summary>
        /// Directions on both sides of north are compared correctly.
        /// </summary>
        [Fact]
        public void Choose_WindAcrossNorth()
        {
            Runway nearlyNorth = new Runway(9800, 150, "ASP", true, "17", 52.0, 20.0, "35", 52.1, 19.98);
            RunwayWindResult result = RunwayWind.Choose(new List<Runway> { nearlyNorth }, 5, false, 10)!;

            Assert.Equal("17", result.EndName);
            Assert.False(result.IsHighEnd);
            Assert.True(result.HeadwindKnots > 9.0);
            Assert.InRange(result.HeadingDegrees, 340.0, 359.0);
        }
    }
}

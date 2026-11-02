using AdsbCarMap.DataSources.Management.Privacy;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of snapping the center of a query to a grid.
    /// </summary>
    public class CoordinateRounderTests
    {
        /// <summary>
        /// Mean radius of the Earth in kilometers.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0;

        /// <summary>
        /// Kilometers in a nautical mile.
        /// </summary>
        private const double KilometersPerNauticalMile = 1.852;

        /// <summary>
        /// Computes the great-circle distance between two points.
        /// </summary>
        /// <param name="latitude1">Latitude of the first point in degrees.</param>
        /// <param name="longitude1">Longitude of the first point in degrees.</param>
        /// <param name="latitude2">Latitude of the second point in degrees.</param>
        /// <param name="longitude2">Longitude of the second point in degrees.</param>
        /// <returns>Distance in nautical miles.</returns>
        private static double DistanceNauticalMiles(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double phi1 = latitude1 * Math.PI / 180.0;
            double phi2 = latitude2 * Math.PI / 180.0;
            double deltaPhi = phi2 - phi1;
            double deltaLambda = (longitude2 - longitude1) * Math.PI / 180.0;
            double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);

            return 2.0 * EarthRadiusKilometers * Math.Asin(Math.Min(1.0, Math.Sqrt(a))) / KilometersPerNauticalMile;
        }

        /// <summary>
        /// With the grid switched off the position and the radius are not changed.
        /// </summary>
        [Fact]
        public void Round_ExactOptions_ChangeNothing()
        {
            QueryArea area = CoordinateRounder.Round(52.123456, 21.654321, 77.0, PrivacyOptions.Exact);

            Assert.Equal(52.123456, area.Latitude);
            Assert.Equal(21.654321, area.Longitude);
            Assert.Equal(77.0, area.RadiusNauticalMiles);
        }

        /// <summary>
        /// Missing options are reported at once.
        /// </summary>
        [Fact]
        public void Round_NullOptions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CoordinateRounder.Round(0.0, 0.0, 1.0, null!));
        }

        /// <summary>
        /// The same input always gives the same output, so that repeated queries cannot be averaged.
        /// </summary>
        [Fact]
        public void Round_IsDeterministic()
        {
            PrivacyOptions options = new PrivacyOptions(5.0);

            Assert.Equal(CoordinateRounder.Round(52.123, 21.456, 40.0, options), CoordinateRounder.Round(52.123, 21.456, 40.0, options));
        }

        /// <summary>
        /// Rounding an already rounded center does not move it.
        /// </summary>
        [Theory]
        [InlineData(1.0, 52.123, 21.456)]
        [InlineData(10.0, -33.9, 151.2)]
        [InlineData(25.0, 64.2, -149.8)]
        public void Round_IsIdempotentForTheCenter(double grid, double latitude, double longitude)
        {
            PrivacyOptions options = new PrivacyOptions(grid);
            QueryArea first = CoordinateRounder.Round(latitude, longitude, 30.0, options);
            QueryArea second = CoordinateRounder.Round(first.Latitude, first.Longitude, 30.0, options);

            Assert.Equal(first.Latitude, second.Latitude, 9);
            Assert.Equal(first.Longitude, second.Longitude, 9);
        }

        /// <summary>
        /// Nearby positions inside one grid cell give the same center.
        /// </summary>
        [Fact]
        public void Round_NearbyPositions_ShareTheCenter()
        {
            PrivacyOptions options = new PrivacyOptions(10.0);
            QueryArea first = CoordinateRounder.Round(52.2300, 21.0100, 30.0, options);
            QueryArea second = CoordinateRounder.Round(52.2305, 21.0105, 30.0, options);

            Assert.Equal(first.Latitude, second.Latitude);
            Assert.Equal(first.Longitude, second.Longitude);
        }

        /// <summary>
        /// The growth of the radius does not depend on the position, so it reveals nothing about the place inside the cell.
        /// </summary>
        [Fact]
        public void Round_RadiusGrowthDoesNotDependOnPosition()
        {
            PrivacyOptions options = new PrivacyOptions(10.0);

            Assert.Equal(CoordinateRounder.Round(52.1, 21.1, 30.0, options).RadiusNauticalMiles, CoordinateRounder.Round(52.17, 21.19, 30.0, options).RadiusNauticalMiles);
            Assert.Equal(30.0 + CoordinateRounder.GetMaxDisplacementNauticalMiles(10.0), CoordinateRounder.Round(52.1, 21.1, 30.0, options).RadiusNauticalMiles, 9);
        }

        /// <summary>
        /// The enlarged circle covers the whole real circle for random centers at various latitudes.
        /// </summary>
        [Theory]
        [InlineData(1.0)]
        [InlineData(5.0)]
        [InlineData(10.0)]
        [InlineData(25.0)]
        public void Round_EnlargedCircleCoversTheRealOne(double grid)
        {
            Random random = new Random(1234);
            PrivacyOptions options = new PrivacyOptions(grid);

            for (int index = 0; index < 2000; index++)
            {
                double latitude = random.NextDouble() * 160.0 - 80.0;
                double longitude = random.NextDouble() * 360.0 - 180.0;
                double radius = 5.0 + random.NextDouble() * 100.0;
                QueryArea area = CoordinateRounder.Round(latitude, longitude, radius, options);
                double shift = DistanceNauticalMiles(latitude, longitude, area.Latitude, area.Longitude);

                Assert.True(shift + radius <= area.RadiusNauticalMiles, "grid " + grid + " at " + latitude + ", " + longitude + ": shift " + shift + " + radius " + radius + " > " + area.RadiusNauticalMiles);
            }
        }

        /// <summary>
        /// Without the growth the radius is only the real one.
        /// </summary>
        [Fact]
        public void Round_WithoutExpansion_KeepsTheRadius()
        {
            QueryArea area = CoordinateRounder.Round(52.1, 21.1, 30.0, new PrivacyOptions(10.0, 0.0, false));

            Assert.Equal(30.0, area.RadiusNauticalMiles);
        }

        /// <summary>
        /// The radius is rounded up to the step.
        /// </summary>
        [Theory]
        [InlineData(0.5, 5.0)]
        [InlineData(5.0, 5.0)]
        [InlineData(5.1, 10.0)]
        [InlineData(99.0, 100.0)]
        public void Round_RadiusStep_RoundsUp(double radius, double expected)
        {
            QueryArea area = CoordinateRounder.Round(52.1, 21.1, radius, new PrivacyOptions(0.0, 5.0));

            Assert.Equal(expected, area.RadiusNauticalMiles);
        }

        /// <summary>
        /// A longitude near the antimeridian stays within the range from -180 to 180 degrees.
        /// </summary>
        [Theory]
        [InlineData(179.999)]
        [InlineData(-179.999)]
        [InlineData(180.0)]
        public void Round_NearAntimeridian_StaysInRange(double longitude)
        {
            QueryArea area = CoordinateRounder.Round(10.0, longitude, 20.0, new PrivacyOptions(25.0));

            Assert.InRange(area.Longitude, -180.0, 180.0);
        }

        /// <summary>
        /// Near the pole the latitude stays within the range and nothing becomes not-a-number.
        /// </summary>
        [Theory]
        [InlineData(89.99, 10.0)]
        [InlineData(-89.99, -170.0)]
        [InlineData(90.0, 0.0)]
        public void Round_NearPole_StaysValid(double latitude, double longitude)
        {
            QueryArea area = CoordinateRounder.Round(latitude, longitude, 20.0, new PrivacyOptions(25.0));

            Assert.InRange(area.Latitude, -90.0, 90.0);
            Assert.InRange(area.Longitude, -180.0, 180.0);
            Assert.False(double.IsNaN(area.Longitude));
        }

        /// <summary>
        /// The cells are about the chosen size in kilometers north-south and east-west.
        /// </summary>
        [Fact]
        public void Round_GridHasTheChosenSize()
        {
            PrivacyOptions options = new PrivacyOptions(10.0);
            QueryArea first = CoordinateRounder.Round(50.0, 20.0, 10.0, options);
            QueryArea north = CoordinateRounder.Round(first.Latitude + 0.09, first.Longitude, 10.0, options);
            QueryArea east = CoordinateRounder.Round(first.Latitude, first.Longitude + 0.14, 10.0, options);

            Assert.InRange(DistanceNauticalMiles(first.Latitude, 20.0, north.Latitude, 20.0) * KilometersPerNauticalMile, 9.9, 10.1);
            Assert.InRange(DistanceNauticalMiles(first.Latitude, first.Longitude, east.Latitude, east.Longitude) * KilometersPerNauticalMile, 9.9, 10.1);
        }
    }
}

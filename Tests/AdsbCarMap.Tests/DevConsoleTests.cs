using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Privacy;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the text of the developer console. They check the numbers only, because the labels depend on the language, which other tests switch.
    /// </summary>
    public class DevConsoleTests
    {
        /// <summary>
        /// Moment used as the current one.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a snapshot with the position of the device and a query that was sent.
        /// </summary>
        /// <param name="sent">Area of the last query, or null.</param>
        /// <param name="gpsKnown">Whether the position of the device is known.</param>
        /// <returns>The snapshot.</returns>
        private static DevConsoleSnapshot CreateSnapshot(QueryArea? sent, bool gpsKnown = true)
        {
            return new DevConsoleSnapshot(
                8.5,
                9.0,
                52.0,
                21.0,
                true,
                1080,
                1920,
                gpsKnown,
                52.123456,
                21.654321,
                gpsKnown ? Now.AddSeconds(-3.7) : null,
                50.0,
                sent,
                sent == null ? null : Now.AddSeconds(-10.0),
                120,
                95,
                Now.AddSeconds(-5.0),
                "adsb.lol",
                TimeSpan.Zero);
        }

        /// <summary>
        /// The zoom, the map center and the position of the device appear with the fixed number of digits, whatever the culture of the thread.
        /// </summary>
        [Fact]
        public void Format_ShowsZoomCenterAndGpsWithInvariantNumbers()
        {
            string text = DevConsoleText.Format(CreateSnapshot(null), Now);

            Assert.Contains("8.50", text);
            Assert.Contains("9.00", text);
            Assert.Contains("52.00000, 21.00000", text);
            Assert.Contains("52.12346, 21.65432", text);
            Assert.Contains("1080", text);
            Assert.Contains("1920", text);
        }

        /// <summary>
        /// The age of the position of the device is rounded down to whole seconds.
        /// </summary>
        [Fact]
        public void Format_ShowsAgeOfGpsInWholeSeconds()
        {
            string text = DevConsoleText.Format(CreateSnapshot(null), Now);

            Assert.Contains("(3 ", text);
        }

        /// <summary>
        /// Without a position of the device no coordinates of the device are shown.
        /// </summary>
        [Fact]
        public void Format_WithoutGps_OmitsTheDeviceCoordinates()
        {
            string text = DevConsoleText.Format(CreateSnapshot(null, false), Now);

            Assert.DoesNotContain("52.12346", text);
            Assert.DoesNotContain("21.65432", text);
        }

        /// <summary>
        /// The sent area appears with its radius in kilometers and nautical miles and the distance from the map center.
        /// </summary>
        [Fact]
        public void Format_WithSentArea_ShowsPositionRadiusAndOffset()
        {
            QueryArea sent = new QueryArea(52.1, 21.0, 60.0);

            string text = DevConsoleText.Format(CreateSnapshot(sent), Now);

            double offset = GeoMath.DistanceKilometers(52.0, 21.0, 52.1, 21.0);

            Assert.Contains("52.10000, 21.00000", text);
            Assert.Contains("111.1 km (60.0 NM)", text);
            Assert.Contains(offset.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), text);
            Assert.Contains("92.6 km (50.0 NM)", text);
        }

        /// <summary>
        /// Without a query the coordinates of a sent area are not invented.
        /// </summary>
        [Fact]
        public void Format_WithoutSentArea_ShowsNoSentCoordinates()
        {
            string text = DevConsoleText.Format(CreateSnapshot(null), Now);

            Assert.DoesNotContain("52.10000", text);
        }

        /// <summary>
        /// The counts of the aircraft, the name of the source and the remaining pause appear.
        /// </summary>
        [Fact]
        public void Format_ShowsAircraftCountsSourceAndRateLimit()
        {
            DevConsoleSnapshot snapshot = CreateSnapshot(null) with { RateLimitRemaining = TimeSpan.FromSeconds(29.2) };

            string text = DevConsoleText.Format(snapshot, Now);

            Assert.Contains("120", text);
            Assert.Contains("95", text);
            Assert.Contains("adsb.lol", text);
            Assert.Contains("30", text);
        }

        /// <summary>
        /// A moment in the future, which a clock change can cause, gives an age of zero instead of a negative one.
        /// </summary>
        [Fact]
        public void Format_FutureMoment_GivesZeroAge()
        {
            DevConsoleSnapshot snapshot = CreateSnapshot(null) with { GpsAtUtc = Now.AddSeconds(30.0) };

            string text = DevConsoleText.Format(snapshot, Now);

            Assert.DoesNotContain("-", text.Split('\n')[4]);
        }

        /// <summary>
        /// A missing snapshot is rejected.
        /// </summary>
        [Fact]
        public void Format_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DevConsoleText.Format(null!, Now));
        }
    }
}

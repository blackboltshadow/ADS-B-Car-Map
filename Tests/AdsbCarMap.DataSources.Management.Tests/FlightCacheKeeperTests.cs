using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of keeping the memory of flight information in line with the visible aircraft.
    /// </summary>
    public class FlightCacheKeeperTests
    {
        /// <summary>
        /// Memory that only records what it was told.
        /// </summary>
        private sealed class RecordingCache : IExpiringCache
        {
            /// <summary>
            /// Keys that were marked as seen.
            /// </summary>
            public List<string> Touched { get; } = new List<string>();

            /// <summary>
            /// Keys that were closed.
            /// </summary>
            public List<string> Closed { get; } = new List<string>();

            /// <summary>
            /// Grace period given with the last closing.
            /// </summary>
            public TimeSpan LastGrace { get; private set; }

            /// <inheritdoc />
            public bool Touch(string key)
            {
                Touched.Add(key);

                return true;
            }

            /// <inheritdoc />
            public bool Close(string key, TimeSpan grace)
            {
                Closed.Add(key);
                LastGrace = grace;

                return true;
            }
        }

        /// <summary>
        /// Time returned to the keeper.
        /// </summary>
        private DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates an aircraft in the air or on the ground.
        /// </summary>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft Create(bool onGround, string? callsign = "LOT281")
        {
            return new Aircraft("abc123", callsign, 52.0, 21.0, onGround ? null : 30000, onGround, onGround ? 5.0 : 450.0, 90.0, 1.0, "B738", "SP-LWA");
        }

        /// <summary>
        /// Creates the keeper over two recording memories.
        /// </summary>
        /// <param name="routes">Memory of the routes.</param>
        /// <param name="aircraft">Memory of the aircraft data.</param>
        /// <returns>The keeper.</returns>
        private FlightCacheKeeper CreateKeeper(RecordingCache routes, RecordingCache aircraft)
        {
            return new FlightCacheKeeper(routes, aircraft, TimeSpan.FromMinutes(2), () => now);
        }

        /// <summary>
        /// An aircraft in the air is marked as seen both by its callsign and by its address.
        /// </summary>
        [Fact]
        public void Observe_Airborne_TouchesRouteAndAircraft()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();

            CreateKeeper(routes, aircraft).Observe(new List<Aircraft> { Create(false) });

            Assert.Equal(new List<string> { "LOT281" }, routes.Touched);
            Assert.Equal(new List<string> { "abc123" }, aircraft.Touched);
            Assert.Empty(routes.Closed);
        }

        /// <summary>
        /// An aircraft without a callsign has no route to touch.
        /// </summary>
        [Fact]
        public void Observe_NoCallsign_TouchesOnlyAircraft()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();

            CreateKeeper(routes, aircraft).Observe(new List<Aircraft> { Create(false, null) });

            Assert.Empty(routes.Touched);
            Assert.Single(aircraft.Touched);
        }

        /// <summary>
        /// An aircraft that was in the air and is now on the ground is closed with the grace period, once.
        /// </summary>
        [Fact]
        public void Observe_Landing_ClosesRouteAndAircraft()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();
            FlightCacheKeeper keeper = CreateKeeper(routes, aircraft);

            keeper.Observe(new List<Aircraft> { Create(false) });
            keeper.Observe(new List<Aircraft> { Create(true) });
            keeper.Observe(new List<Aircraft> { Create(true) });

            Assert.Equal(new List<string> { "LOT281" }, routes.Closed);
            Assert.Equal(new List<string> { "abc123" }, aircraft.Closed);
            Assert.Equal(TimeSpan.FromMinutes(2), routes.LastGrace);
            Assert.Single(routes.Touched);
        }

        /// <summary>
        /// An aircraft parked on the ground that was never seen in the air is not closed.
        /// </summary>
        [Fact]
        public void Observe_ParkedOnGround_IsTouchedNotClosed()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();

            CreateKeeper(routes, aircraft).Observe(new List<Aircraft> { Create(true) });

            Assert.Single(routes.Touched);
            Assert.Empty(routes.Closed);
        }

        /// <summary>
        /// When a landed aircraft takes off again it is marked as seen and can land and be closed again.
        /// </summary>
        [Fact]
        public void Observe_TakeOffAfterLanding_TouchesAgain()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();
            FlightCacheKeeper keeper = CreateKeeper(routes, aircraft);

            keeper.Observe(new List<Aircraft> { Create(false) });
            keeper.Observe(new List<Aircraft> { Create(true) });
            keeper.Observe(new List<Aircraft> { Create(false) });
            keeper.Observe(new List<Aircraft> { Create(true) });

            Assert.Equal(2, routes.Closed.Count);
            Assert.Equal(2, routes.Touched.Count);
        }

        /// <summary>
        /// A landing is not recognized when the aircraft was last seen in the air long before.
        /// </summary>
        [Fact]
        public void Observe_AirborneForgottenAfterLongAbsence_NoClosing()
        {
            RecordingCache routes = new RecordingCache();
            RecordingCache aircraft = new RecordingCache();
            FlightCacheKeeper keeper = CreateKeeper(routes, aircraft);

            keeper.Observe(new List<Aircraft> { Create(false) });
            now += TimeSpan.FromMinutes(30);
            keeper.Observe(new List<Aircraft>());
            keeper.Observe(new List<Aircraft> { Create(true) });

            Assert.Empty(routes.Closed);
        }

        /// <summary>
        /// Together with the real memory a visible flight stays while a landed one disappears soon, long before the retention ends.
        /// </summary>
        [Fact]
        public void Observe_WithRealMemory_LandedFlightIsForgottenSoon()
        {
            TimedCache<FlightRoute> routes = new TimedCache<FlightRoute>(() => TimeSpan.FromMinutes(10), TimeSpan.FromHours(6), () => now, 100);
            TimedCache<AircraftDetails> details = new TimedCache<AircraftDetails>(() => TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), () => now, 100);
            FlightCacheKeeper keeper = new FlightCacheKeeper(routes, details, TimeSpan.FromMinutes(2), () => now);
            routes.Set("LOT281", new FlightRoute("WAW", null, "JFK", null));
            details.Set("abc123", new AircraftDetails("abc123", "B738", "SP-LWA", "A3"));

            for (int i = 0; i < 20; i++)
            {
                now += TimeSpan.FromMinutes(5);
                keeper.Observe(new List<Aircraft> { Create(false) });
            }

            Assert.True(routes.TryGet("LOT281", out _));
            Assert.True(details.TryGet("abc123", out _));

            keeper.Observe(new List<Aircraft> { Create(true) });
            now += TimeSpan.FromMinutes(3);

            Assert.False(routes.TryGet("LOT281", out _));
            Assert.False(details.TryGet("abc123", out _));
        }

        /// <summary>
        /// A missing memory is reported at once.
        /// </summary>
        [Fact]
        public void Constructor_NullMemory_ThrowsAtOnce()
        {
            Assert.Throws<ArgumentNullException>(() => new FlightCacheKeeper(new RecordingCache(), null!));
            Assert.Throws<ArgumentNullException>(() => new FlightCacheKeeper(null!, new RecordingCache()));
        }
    }
}

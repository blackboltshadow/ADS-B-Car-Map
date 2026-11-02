using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Caching
{
    /// <summary>
    /// Watches the aircraft that the main source returns and keeps the memory of flight information in line with them: a flight that is visible is marked as seen, so it is not asked about again, and a flight that has just landed is closed, so it is forgotten soon. A flight that is only parked on the ground and was never seen in the air is not treated as landed.
    /// </summary>
    public sealed class FlightCacheKeeper
    {
        /// <summary>
        /// Default time after which the information about a landed flight is forgotten.
        /// </summary>
        public static readonly TimeSpan DefaultLandedGrace = TimeSpan.FromMinutes(2);

        /// <summary>
        /// How long an aircraft counts as having been in the air after it was last seen airborne.
        /// </summary>
        private static readonly TimeSpan AirborneMemory = TimeSpan.FromMinutes(15);

        /// <summary>
        /// How long an aircraft counts as landed, so that its closing is not cancelled while it taxis.
        /// </summary>
        private static readonly TimeSpan LandedMemory = TimeSpan.FromMinutes(45);

        /// <summary>
        /// Interval between two clean-ups of the lists of airborne and landed aircraft.
        /// </summary>
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Lock protecting the lists.
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// Memory of the routes by callsign.
        /// </summary>
        private readonly IExpiringCache routes;

        /// <summary>
        /// Memory of the aircraft data by the ICAO address.
        /// </summary>
        private readonly IExpiringCache aircraft;

        /// <summary>
        /// Time after which the information about a landed flight is forgotten.
        /// </summary>
        private readonly TimeSpan landedGrace;

        /// <summary>
        /// Source of the current time in UTC, replaceable in tests.
        /// </summary>
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Aircraft last seen in the air, by the ICAO address, with the moment of that sighting.
        /// </summary>
        private readonly Dictionary<string, DateTime> airborne = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Aircraft that have landed, by the ICAO address, with the moment of the landing.
        /// </summary>
        private readonly Dictionary<string, DateTime> landed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Moment of the last clean-up of the lists.
        /// </summary>
        private DateTime lastCleanupUtc = DateTime.MinValue;

        /// <summary>
        /// Creates the keeper with the default grace period and the system clock.
        /// </summary>
        /// <param name="routes">Memory of the routes by callsign.</param>
        /// <param name="aircraft">Memory of the aircraft data by the ICAO address.</param>
        public FlightCacheKeeper(IExpiringCache routes, IExpiringCache aircraft)
            : this(routes, aircraft, DefaultLandedGrace, () => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Creates the keeper.
        /// </summary>
        /// <param name="routes">Memory of the routes by callsign.</param>
        /// <param name="aircraft">Memory of the aircraft data by the ICAO address.</param>
        /// <param name="landedGrace">Time after which the information about a landed flight is forgotten.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        public FlightCacheKeeper(IExpiringCache routes, IExpiringCache aircraft, TimeSpan landedGrace, Func<DateTime> utcNow)
        {
            ArgumentNullException.ThrowIfNull(routes);
            ArgumentNullException.ThrowIfNull(aircraft);
            this.routes = routes;
            this.aircraft = aircraft;
            this.landedGrace = landedGrace;
            this.utcNow = utcNow;
        }

        /// <summary>
        /// Processes a download of the aircraft: marks the visible flights as seen and closes the flights that have just landed.
        /// </summary>
        /// <param name="visible">Aircraft returned by the main source.</param>
        public void Observe(IReadOnlyList<Aircraft> visible)
        {
            lock (gate)
            {
                DateTime now = utcNow();

                foreach (Aircraft item in visible)
                {
                    if (!item.OnGround)
                    {
                        airborne[item.Hex] = now;
                        landed.Remove(item.Hex);
                        Touch(item);
                    }
                    else if (airborne.Remove(item.Hex))
                    {
                        landed[item.Hex] = now;
                        Close(item);
                    }
                    else if (!landed.ContainsKey(item.Hex))
                    {
                        Touch(item);
                    }
                }

                if (now - lastCleanupUtc >= CleanupInterval)
                {
                    lastCleanupUtc = now;
                    Forget(airborne, now - AirborneMemory);
                    Forget(landed, now - LandedMemory);
                }
            }
        }

        /// <summary>
        /// Marks the information about an aircraft and its flight as seen now.
        /// </summary>
        /// <param name="item">The aircraft.</param>
        private void Touch(Aircraft item)
        {
            aircraft.Touch(item.Hex);

            if (!string.IsNullOrWhiteSpace(item.Callsign))
            {
                routes.Touch(item.Callsign.Trim());
            }
        }

        /// <summary>
        /// Closes the information about an aircraft and its flight, because it has landed.
        /// </summary>
        /// <param name="item">The aircraft.</param>
        private void Close(Aircraft item)
        {
            aircraft.Close(item.Hex, landedGrace);

            if (!string.IsNullOrWhiteSpace(item.Callsign))
            {
                routes.Close(item.Callsign.Trim(), landedGrace);
            }
        }

        /// <summary>
        /// Removes the entries that are older than the given moment.
        /// </summary>
        /// <param name="entries">The list to clean.</param>
        /// <param name="olderThan">Moment before which entries are removed.</param>
        private static void Forget(Dictionary<string, DateTime> entries, DateTime olderThan)
        {
            foreach (string key in entries.Where(entry => entry.Value < olderThan).Select(entry => entry.Key).ToList())
            {
                entries.Remove(key);
            }
        }
    }
}

using AdsbCarMap.Data;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Most recently downloaded aircraft and map center shared by Android Auto and the phone screen, requests to follow an aircraft, and the request limit backoff shared by both maps.
    /// </summary>
    public static class LiveTraffic
    {
        /// <summary>
        /// Lock protecting the state.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Most recently downloaded aircraft list.
        /// </summary>
        private static IReadOnlyList<Aircraft> aircraft = Array.Empty<Aircraft>();

        /// <summary>
        /// Moment the last aircraft list was downloaded, in UTC.
        /// </summary>
        private static DateTime timestampUtc = DateTime.MinValue;

        /// <summary>
        /// Latitude of the center of the last request.
        /// </summary>
        private static double centerLatitude = 52.2297;

        /// <summary>
        /// Longitude of the center of the last request.
        /// </summary>
        private static double centerLongitude = 21.0122;

        /// <summary>
        /// Time for which a remembered request to follow an aircraft stays valid.
        /// </summary>
        private static readonly TimeSpan PendingFocusLifetime = TimeSpan.FromSeconds(10);

        /// <summary>
        /// ICAO address of the aircraft from the last request to follow.
        /// </summary>
        private static string? pendingFocusHex;

        /// <summary>
        /// Moment of the last request to follow an aircraft, in UTC.
        /// </summary>
        private static DateTime pendingFocusUtc = DateTime.MinValue;

        /// <summary>
        /// Moment until which the server asked not to send requests, in UTC. Shared, because both maps use the same server limit.
        /// </summary>
        private static DateTime rateLimitedUntilUtc = DateTime.MinValue;

        /// <summary>
        /// Event raised when the user picks an aircraft to follow. The argument is the ICAO address.
        /// </summary>
        public static event Action<string>? FocusRequested;

        /// <summary>
        /// Event raised when the user picks an airport to show. The argument is the ICAO code.
        /// </summary>
        public static event Action<string>? AirportFocusRequested;

        /// <summary>
        /// ICAO code of the airport from the last request to show.
        /// </summary>
        private static string? pendingAirportIcao;

        /// <summary>
        /// Moment of the last request to show an airport, in UTC.
        /// </summary>
        private static DateTime pendingAirportUtc = DateTime.MinValue;

        /// <summary>
        /// Whether the position of the user is known.
        /// </summary>
        private static bool hasUserLocation;

        /// <summary>
        /// Latitude of the user.
        /// </summary>
        private static double userLatitude;

        /// <summary>
        /// Longitude of the user.
        /// </summary>
        private static double userLongitude;

        /// <summary>
        /// Returns the last aircraft list and the moment it was downloaded.
        /// </summary>
        /// <returns>Aircraft list and the time in UTC.</returns>
        public static (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) GetSnapshot()
        {
            lock (SyncRoot)
            {
                return (aircraft, timestampUtc);
            }
        }

        /// <summary>
        /// Returns the center of the last aircraft request.
        /// </summary>
        /// <returns>Latitude and longitude.</returns>
        public static (double Latitude, double Longitude) GetCenter()
        {
            lock (SyncRoot)
            {
                return (centerLatitude, centerLongitude);
            }
        }

        /// <summary>
        /// Stores a new aircraft list and the request center.
        /// </summary>
        /// <param name="fetched">Downloaded aircraft.</param>
        /// <param name="fetchedAtUtc">Moment of the download, in UTC.</param>
        /// <param name="latitude">Latitude of the request center.</param>
        /// <param name="longitude">Longitude of the request center.</param>
        public static void Update(IReadOnlyList<Aircraft> fetched, DateTime fetchedAtUtc, double latitude, double longitude)
        {
            lock (SyncRoot)
            {
                aircraft = fetched;
                timestampUtc = fetchedAtUtc;
                centerLatitude = latitude;
                centerLongitude = longitude;
            }
        }

        /// <summary>
        /// Remembers that the server refused requests, so that no map sends a request until the given time passes.
        /// </summary>
        /// <param name="retryAfter">Time after which requests may be sent again.</param>
        public static void ReportRateLimited(TimeSpan retryAfter)
        {
            DateTime until = DateTime.UtcNow + retryAfter;

            lock (SyncRoot)
            {
                if (until > rateLimitedUntilUtc)
                {
                    rateLimitedUntilUtc = until;
                }
            }
        }

        /// <summary>
        /// Returns how long requests to the server should still be withheld.
        /// </summary>
        /// <returns>Remaining time, or zero when requests are allowed.</returns>
        public static TimeSpan GetRateLimitRemaining()
        {
            lock (SyncRoot)
            {
                TimeSpan remaining = rateLimitedUntilUtc - DateTime.UtcNow;

                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Asks the active maps to center on and follow an aircraft. The request is also remembered for a short time for a map that starts a moment later.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public static void RequestFocus(string hex)
        {
            lock (SyncRoot)
            {
                pendingFocusHex = hex;
                pendingFocusUtc = DateTime.UtcNow;
            }

            FocusRequested?.Invoke(hex);
        }

        /// <summary>
        /// Stores the position of the user, so that screens without a map can show distances.
        /// </summary>
        /// <param name="latitude">Latitude of the user.</param>
        /// <param name="longitude">Longitude of the user.</param>
        public static void SetUserLocation(double latitude, double longitude)
        {
            lock (SyncRoot)
            {
                userLatitude = latitude;
                userLongitude = longitude;
                hasUserLocation = true;
            }
        }

        /// <summary>
        /// Returns the last known position of the user.
        /// </summary>
        /// <returns>Whether the position is known, the latitude and the longitude.</returns>
        public static (bool Known, double Latitude, double Longitude) GetUserLocation()
        {
            lock (SyncRoot)
            {
                return (hasUserLocation, userLatitude, userLongitude);
            }
        }

        /// <summary>
        /// Asks the active maps to select an airport and center on it. The request is also remembered for a short time for a map that starts a moment later.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        public static void RequestAirportFocus(string icao)
        {
            lock (SyncRoot)
            {
                pendingAirportIcao = icao;
                pendingAirportUtc = DateTime.UtcNow;
            }

            AirportFocusRequested?.Invoke(icao);
        }

        /// <summary>
        /// Returns the ICAO code of a recent request to show an airport that no active map may have received.
        /// </summary>
        /// <returns>ICAO code, or null when there is no recent request.</returns>
        public static string? GetPendingAirportFocus()
        {
            lock (SyncRoot)
            {
                return DateTime.UtcNow - pendingAirportUtc <= PendingFocusLifetime ? pendingAirportIcao : null;
            }
        }

        /// <summary>
        /// Returns the ICAO address of a recent request to follow that no active map may have received.
        /// </summary>
        /// <returns>ICAO address, or null when there is no recent request.</returns>
        public static string? GetPendingFocus()
        {
            lock (SyncRoot)
            {
                return DateTime.UtcNow - pendingFocusUtc <= PendingFocusLifetime ? pendingFocusHex : null;
            }
        }
    }
}

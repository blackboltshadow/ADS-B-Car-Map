using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Checks the alerts without the map. While it is switched on, it keeps the location service with its permanent notification running, downloads the aircraft around the areas of the alerts at a fixed interval and passes them to <see cref="AlertMonitor"/>. After the device boots it is started by <see cref="BootReceiver"/>, unless the user switched that off.
    /// </summary>
    public static class AlertBackgroundRunner
    {
        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "AlertBackground";

        /// <summary>
        /// Lock protecting the state.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Source of the position of the user while the runner works. Null when the runner is stopped.
        /// </summary>
        private static LocationTracker? tracker;

        /// <summary>
        /// Cancels the loop. Null when the runner is stopped.
        /// </summary>
        private static CancellationTokenSource? cancellation;

        /// <summary>
        /// Whether <see cref="Initialize"/> was already called.
        /// </summary>
        private static bool initialized;

        /// <summary>
        /// Text describing what the runner does now, for the alerts screen.
        /// </summary>
        private static string status = Loc.T("Wyłączone");

        /// <summary>
        /// Text describing what the runner does now.
        /// </summary>
        public static string Status
        {
            get
            {
                lock (SyncRoot)
                {
                    return status;
                }
            }
        }

        /// <summary>
        /// Starts watching the alert settings. Call once when the application starts.
        /// </summary>
        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (initialized)
                {
                    return;
                }

                initialized = true;
            }

            AppServices.Alerts.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// Starts or stops the background checking according to the settings.
        /// </summary>
        public static void Refresh()
        {
            lock (SyncRoot)
            {
                AlertRuleStore store = AppServices.Alerts;
                bool wanted = AlertBackgroundPolicy.ShouldRun(store.IsBackgroundEnabled, store.IsEnabled, store.GetActiveRules().Count);

                if (!wanted)
                {
                    StopLocked(store.IsBackgroundEnabled ? Loc.T("Czeka na włączony alert") : Loc.T("Wyłączone"));

                    return;
                }

                if (cancellation != null)
                {
                    return;
                }

                if (!LocationTracker.HasPermission())
                {
                    status = Loc.T("Brak zgody na lokalizację");

                    return;
                }

                LocationTracker newTracker = new LocationTracker(LiveTraffic.SetUserLocation);

                if (!newTracker.Start())
                {
                    status = "Nie udało się uruchomić usługi lokalizacji";

                    return;
                }

                tracker = newTracker;
                cancellation = new CancellationTokenSource();
                AlertMonitor.BackgroundActive = true;
                status = Loc.T("Uruchamianie");

                CancellationToken token = cancellation.Token;

                Task.Run(() => LoopAsync(token));
            }
        }

        /// <summary>
        /// Stops the loop and the location service. Must be called under the lock.
        /// </summary>
        /// <param name="newStatus">Status to show.</param>
        private static void StopLocked(string newStatus)
        {
            cancellation?.Cancel();
            cancellation = null;
            tracker?.Stop();
            tracker = null;
            AlertMonitor.BackgroundActive = false;
            status = newStatus;
        }

        /// <summary>
        /// Repeats the check until the runner is stopped.
        /// </summary>
        /// <param name="token">Token cancelled when the runner stops.</param>
        /// <returns>Task completed when the loop ends.</returns>
        private static async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TimeSpan delay = AppSettings.AlertInterval;

                try
                {
                    await CheckAsync(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (RateLimitedException exception)
                {
                    SetStatus(Loc.T("Wstrzymane przez źródło danych (limit zapytań)"), token);
                    delay = exception.RetryAfter > delay ? exception.RetryAfter : delay;
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Sprawdzenie alertów w tle nie powiodło się", exception);
                    SetStatus(Loc.T("Błąd pobierania, ponowna próba za chwilę"), token);
                }

                try
                {
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Downloads the aircraft around the areas of the alerts and checks them.
        /// </summary>
        /// <param name="token">Token cancelled when the runner stops.</param>
        /// <returns>Task completed when the check ends.</returns>
        private static async Task CheckAsync(CancellationToken token)
        {
            List<AlertRule> rules = AppServices.Alerts.GetActiveRules();

            if (rules.Count > AppSettings.AlertMaxBackground)
            {
                rules = rules.GetRange(0, AppSettings.AlertMaxBackground);
            }

            (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();
            GeoPoint? myPosition = known ? new GeoPoint(latitude, longitude) : null;
            List<AlertCircle> circles = AlertQueryPlanner.CollectCircles(rules, myPosition, AlertMonitor.FindAirport);

            if (circles.Count == 0)
            {
                SetStatus(rules.Count == 0 ? Loc.T("Czeka na włączony alert") : Loc.T("Czeka na pozycję GPS lub lotnisko"), token);

                return;
            }

            Dictionary<string, Aircraft> byHex = new Dictionary<string, Aircraft>();

            foreach (AlertCircle query in AlertQueryPlanner.Plan(circles))
            {
                IReadOnlyList<Aircraft> fetched = await AppServices.Adsb.GetAircraftAsync(query.Center.Latitude, query.Center.Longitude, AlertQueryPlanner.ToNauticalMiles(query.RadiusKilometers), token);

                foreach (Aircraft aircraft in fetched)
                {
                    byHex[aircraft.Hex] = aircraft;
                }
            }

            List<Aircraft> all = new List<Aircraft>(byHex.Values);

            token.ThrowIfCancellationRequested();
            RequestRoutes(rules, all, myPosition);
            AlertMonitor.ProcessBackground(all, rules);
            SetStatus(Loc.T("Działa, ostatnie sprawdzenie ") + DateTime.Now.ToString("HH:mm:ss") + ", co " + (int)AppSettings.AlertInterval.TotalSeconds + " s", token);
        }

        /// <summary>
        /// Requests the routes of the aircraft inside the areas of the rules that have a condition, because a condition can depend on the route.
        /// </summary>
        /// <param name="rules">Rules checked now.</param>
        /// <param name="aircraft">Downloaded aircraft.</param>
        /// <param name="myPosition">Current position of the user or null when it is not known.</param>
        private static void RequestRoutes(List<AlertRule> rules, List<Aircraft> aircraft, GeoPoint? myPosition)
        {
            foreach (AlertRule rule in rules)
            {
                GeoPoint? center = rule.Condition == null ? null : rule.Area.ResolveCenter(myPosition, AlertMonitor.FindAirport);

                if (center == null)
                {
                    continue;
                }

                foreach (Aircraft item in aircraft)
                {
                    if (GeoMath.DistanceKilometers(center.Latitude, center.Longitude, item.Latitude, item.Longitude) <= rule.Area.RadiusKilometers)
                    {
                        AppServices.Routes.Get(item.Callsign);
                    }
                }
            }
        }

        /// <summary>
        /// Changes the status unless the runner was stopped in the meantime.
        /// </summary>
        /// <param name="newStatus">Status to show.</param>
        /// <param name="token">Token of the loop that reports the status.</param>
        private static void SetStatus(string newStatus, CancellationToken token)
        {
            lock (SyncRoot)
            {
                if (!token.IsCancellationRequested)
                {
                    status = newStatus;
                }
            }
        }
    }
}

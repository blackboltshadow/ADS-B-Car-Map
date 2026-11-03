using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Checks every downloaded aircraft list against the alert rules and shows a notification for each aircraft that fired a rule. Called from the data loop, so it works for the phone map and for Android Auto.
    /// </summary>
    public static class AlertMonitor
    {
        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "AlertMonitor";

        /// <summary>
        /// Identifier of the notification channel of alerts.
        /// </summary>
        private const string ChannelId = "aircraft_alerts";

        /// <summary>
        /// Name of the intent extra with the ICAO address of the aircraft to show after a tap on a notification.
        /// </summary>
        public const string FocusHexExtra = "alert_focus_hex";

        /// <summary>
        /// Lock protecting the engine.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Engine remembering what was already reported.
        /// </summary>
        private static readonly AlertEngine Engine = new AlertEngine();

        /// <summary>
        /// Whether the background service checks the alerts. The aircraft downloaded by the map are then ignored, because they cover another area and would make the engine think that aircraft left and came back.
        /// </summary>
        public static volatile bool BackgroundActive;

        /// <summary>
        /// Checks the aircraft downloaded by the map against the active rules and notifies about the matches. Does nothing while the background service checks the alerts. Never throws.
        /// </summary>
        /// <param name="aircraft">Aircraft from the latest download.</param>
        public static void Process(IReadOnlyList<Aircraft> aircraft)
        {
            if (!BackgroundActive)
            {
                Run(aircraft, AppServices.Alerts.GetActiveRules());
            }
        }

        /// <summary>
        /// Checks the aircraft downloaded by the background service against the given rules and notifies about the matches. Never throws.
        /// </summary>
        /// <param name="aircraft">Aircraft from the latest download of all the areas.</param>
        /// <param name="rules">Rules checked in the background, with resolved conditions.</param>
        public static void ProcessBackground(IReadOnlyList<Aircraft> aircraft, List<AlertRule> rules)
        {
            Run(aircraft, rules);
        }

        /// <summary>
        /// Evaluates the rules and shows the notifications. Never throws.
        /// </summary>
        /// <param name="aircraft">Aircraft to check.</param>
        /// <param name="rules">Rules to check.</param>
        private static void Run(IReadOnlyList<Aircraft> aircraft, List<AlertRule> rules)
        {
            try
            {
                if (rules.Count == 0)
                {
                    return;
                }

                (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();
                GeoPoint? myPosition = known ? new GeoPoint(latitude, longitude) : null;
                List<AlertMatch> matches;

                lock (SyncRoot)
                {
                    matches = Engine.Evaluate(rules, aircraft, myPosition, FindAirport, (string? callsign) => AppServices.Routes.Peek(callsign), DateTime.UtcNow);
                }

                foreach (AlertMatch match in matches)
                {
                    Notify(match);
                }
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Sprawdzenie alertów nie powiodło się", exception);
            }
        }

        /// <summary>
        /// Counts the aircraft that satisfy a rule now, for the preview in the rule editor.
        /// </summary>
        /// <param name="rule">Rule to test; its condition must already be resolved.</param>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <returns>Number of aircraft inside the area that satisfy the condition.</returns>
        public static int CountMatching(AlertRule rule, IReadOnlyList<Aircraft> aircraft)
        {
            (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();
            GeoPoint? myPosition = known ? new GeoPoint(latitude, longitude) : null;

            return AlertEngine.CountMatching(rule, aircraft, myPosition, FindAirport, (string? callsign) => AppServices.Routes.Peek(callsign));
        }

        /// <summary>
        /// Finds the position of an airport.
        /// </summary>
        /// <param name="icao">ICAO code.</param>
        /// <returns>The position or null when the airport is not known.</returns>
        public static GeoPoint? FindAirport(string icao)
        {
            Airport? airport = AppServices.Airports.FindByIcao(icao);

            return airport == null ? null : new GeoPoint(airport.Latitude, airport.Longitude);
        }

        /// <summary>
        /// Shows the notification about a match when the notification permission is granted.
        /// </summary>
        /// <param name="match">Match to report.</param>
        private static void Notify(AlertMatch match)
        {
            Context context = Application.Context;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                && context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
            {
                return;
            }

            NotificationManager? manager = context.GetSystemService(Context.NotificationService)?.JavaCast<NotificationManager>();

            if (manager == null)
            {
                return;
            }

            Notification.Builder builder;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationChannel channel = new NotificationChannel(ChannelId, "Alerty o samolotach", NotificationImportance.High);
                manager.CreateNotificationChannel(channel);
                builder = new Notification.Builder(context, ChannelId);
            }
            else
            {
                builder = new Notification.Builder(context);
            }

            int notificationId = HashCode.Combine(match.Rule.Id, match.Aircraft.Hex) & 0x7FFFFFFF;
            Intent intent = new Intent(context, typeof(Activities.MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            intent.PutExtra(FocusHexExtra, match.Aircraft.Hex);
            PendingIntent? contentIntent = PendingIntent.GetActivity(context, notificationId, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            builder
                .SetSmallIcon(Resource.Drawable.ic_alert)
                .SetContentTitle(AlertText.Title(match))
                .SetContentText(AlertText.Body(match, AppSettings.Units))
                .SetAutoCancel(true)
                .SetCategory(Notification.CategoryEvent)
                .SetContentIntent(contentIntent);

            manager.Notify(notificationId, builder.Build());
        }
    }
}

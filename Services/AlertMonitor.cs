using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Car.App.Notification;
using AndroidX.Core.App;

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
        /// Identifier of the notification channel of the alerts with the default importance (sound, no banner).
        /// </summary>
        private const string ChannelIdDefault = "aircraft_alerts_default";

        /// <summary>
        /// Identifier of the notification channel of the quiet alerts (no sound, no banner).
        /// </summary>
        private const string ChannelIdLow = "aircraft_alerts_low";

        /// <summary>
        /// Value mixed into the request code of the mute button so that it differs from the request code of the notification.
        /// </summary>
        private const int MuteRequestCodeMask = 0x40000000;

        /// <summary>
        /// Name of the preferences file with the silenced alerts.
        /// </summary>
        private const string MutePreferencesName = "alert_mutes";

        /// <summary>
        /// Key of the silenced alerts in the preferences.
        /// </summary>
        private const string MutePreferencesKey = "list";

        /// <summary>
        /// Lock protecting the list of the silenced alerts.
        /// </summary>
        private static readonly object MuteSyncRoot = new object();

        /// <summary>
        /// Alerts that are silenced, read from the preferences when first needed.
        /// </summary>
        private static AlertMuteList? muteList;

        /// <summary>
        /// Name of the intent extra with the ICAO address of the aircraft to show after a tap on a notification.
        /// </summary>
        public const string FocusHexExtra = "alert_focus_hex";

        /// <summary>
        /// Name of the intent extra with the ICAO addresses of all aircraft of a grouped notification, to show together after a tap on it.
        /// </summary>
        public const string FocusHexesExtra = "alert_focus_hexes";

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
                    Engine.SkipInitialAppearance = AppSettings.AlertSkipInitialAppearance;
                    matches = Engine.Evaluate(rules, aircraft, myPosition, FindAirport, (string? callsign) => AppServices.Routes.Peek(callsign), DateTime.UtcNow, DateTime.Now);
                }

                matches.RemoveAll((AlertMatch match) => IsMuted(match.Rule.Id));

                if (matches.Count > 0)
                {
                    Report(matches);
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
        /// Checks whether the application may show notifications. Before Android 13 the permission does not exist.
        /// </summary>
        /// <returns>True when the notifications are allowed or the permission is not needed.</returns>
        public static bool HasNotificationPermission()
        {
            return Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu
                || Application.Context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted;
        }

        /// <summary>
        /// Silences an alert for a time, for example after the "mute" button of its notification.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <param name="duration">How long the alert stays silent.</param>
        public static void MuteRule(string ruleId, TimeSpan duration)
        {
            lock (MuteSyncRoot)
            {
                AlertMuteList list = GetMuteList();
                list.Prune(DateTime.UtcNow);
                list.Mute(ruleId, DateTime.UtcNow + duration);
                SaveMuteList(list);
            }
        }

        /// <summary>
        /// Tells until when an alert is silenced.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <returns>The end of the silence in local time or null when the alert is not silenced.</returns>
        public static DateTime? GetMutedUntilLocal(string ruleId)
        {
            lock (MuteSyncRoot)
            {
                return GetMuteList().GetMutedUntil(ruleId, DateTime.UtcNow)?.ToLocalTime();
            }
        }

        /// <summary>
        /// Checks whether an alert is silenced now.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <returns>True when the alert is silenced.</returns>
        private static bool IsMuted(string ruleId)
        {
            lock (MuteSyncRoot)
            {
                return GetMuteList().IsMuted(ruleId, DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Gives the list of the silenced alerts, reading it from the preferences the first time.
        /// </summary>
        /// <returns>The list. Call it only with <see cref="MuteSyncRoot"/> held.</returns>
        private static AlertMuteList GetMuteList()
        {
            if (muteList == null)
            {
                string? saved = Application.Context.GetSharedPreferences(MutePreferencesName, FileCreationMode.Private)?.GetString(MutePreferencesKey, null);
                muteList = AlertMuteList.Parse(saved);
            }

            return muteList;
        }

        /// <summary>
        /// Saves the list of the silenced alerts.
        /// </summary>
        /// <param name="list">List to save.</param>
        private static void SaveMuteList(AlertMuteList list)
        {
            ISharedPreferencesEditor? editor = Application.Context.GetSharedPreferences(MutePreferencesName, FileCreationMode.Private)?.Edit();
            editor?.PutString(MutePreferencesKey, list.Format());
            editor?.Apply();
        }

        /// <summary>
        /// Records the matches in the history, then shows the notifications: aircraft of one rule that fired together in numbers over the threshold are reported with one notification.
        /// </summary>
        /// <param name="matches">Matches that fired and are not silenced.</param>
        private static void Report(List<AlertMatch> matches)
        {
            string text = string.Empty;

            foreach (AlertMatch match in matches)
            {
                text = AlertText.Body(match, AppSettings.Units);
                AlertHistoryStore.Add(new AlertHistoryEntry(DateTime.UtcNow, match.Rule.Name, match.Aircraft.Hex, match.Aircraft.Callsign?.Trim(), match.Trigger, text));
            }

            foreach (AlertNotice notice in AlertGrouper.Group(matches, AppSettings.AlertGroupThreshold))
            {
                Notify(notice);
            }
        }

        /// <summary>
        /// Gives the identifier of the notification channel for an importance and creates the channel.
        /// </summary>
        /// <param name="manager">Notification manager.</param>
        /// <param name="importance">Importance of the alert.</param>
        /// <returns>Identifier of the channel.</returns>
        private static string EnsureChannel(NotificationManager manager, AlertImportance importance)
        {
            string id = importance == AlertImportance.Low ? ChannelIdLow : importance == AlertImportance.Default ? ChannelIdDefault : ChannelId;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string name = importance == AlertImportance.Low ? Loc.T("Alerty o samolotach (ciche)") : importance == AlertImportance.Default ? Loc.T("Alerty o samolotach (zwykłe)") : Loc.T("Alerty o samolotach");
                NotificationImportance level = importance == AlertImportance.Low ? NotificationImportance.Low : importance == AlertImportance.Default ? NotificationImportance.Default : NotificationImportance.High;
                manager.CreateNotificationChannel(new NotificationChannel(id, name, level));
            }

            return id;
        }

        /// <summary>
        /// Shows a notification about a match or about a group of matches when the notification permission is granted. It has the buttons "Pokaż na mapie" and "Wycisz na 1 h".
        /// </summary>
        /// <param name="notice">Notice to show.</param>
        private static void Notify(AlertNotice notice)
        {
            Context context = Application.Context;

            if (!HasNotificationPermission())
            {
                return;
            }

            NotificationManager? manager = context.GetSystemService(Context.NotificationService)?.JavaCast<NotificationManager>();

            if (manager == null)
            {
                return;
            }

            AlertMatch first = notice.Matches[0];
            AlertImportance importance = notice.Rule.Importance;
            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, EnsureChannel(manager, importance));
            string title = notice.IsGroup ? AlertText.GroupTitle(notice) : AlertText.Title(first);
            string body = notice.IsGroup ? AlertText.GroupBody(notice) : AlertText.Body(first, AppSettings.Units);

            int notificationId = (notice.IsGroup ? HashCode.Combine(notice.Rule.Id, "group") : HashCode.Combine(notice.Rule.Id, first.Aircraft.Hex)) & 0x7FFFFFFF;
            Intent intent = new Intent(context, typeof(Activities.MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            intent.PutExtra(FocusHexExtra, first.Aircraft.Hex);

            if (notice.IsGroup)
            {
                intent.PutExtra(FocusHexesExtra, notice.Matches.Select((AlertMatch match) => match.Aircraft.Hex).ToArray());
            }
            PendingIntent? contentIntent = PendingIntent.GetActivity(context, notificationId, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            Intent muteIntent = new Intent(context, typeof(AlertActionReceiver));
            muteIntent.SetAction(AlertActionReceiver.ActionMute);
            muteIntent.PutExtra(AlertActionReceiver.RuleIdExtra, notice.Rule.Id);
            muteIntent.PutExtra(AlertActionReceiver.NotificationIdExtra, notificationId);
            PendingIntent? mutePendingIntent = PendingIntent.GetBroadcast(context, notificationId ^ MuteRequestCodeMask, muteIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            builder
                .SetSmallIcon(Resource.Drawable.ic_alert)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetAutoCancel(true)
                .SetCategory(NotificationCompat.CategoryEvent)
                .SetPriority(importance == AlertImportance.High ? NotificationCompat.PriorityHigh : importance == AlertImportance.Default ? NotificationCompat.PriorityDefault : NotificationCompat.PriorityLow)
                .SetContentIntent(contentIntent)
                .AddAction(Resource.Drawable.ic_visibility, Loc.T("Pokaż na mapie"), contentIntent)
                .AddAction(Resource.Drawable.ic_alert, Loc.T("Wycisz na 1 h"), mutePendingIntent);

            if (notice.IsGroup)
            {
                builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
            }

            if (AppSettings.AlertCarNotifications)
            {
                int carImportance = importance == AlertImportance.High ? NotificationManagerCompat.ImportanceHigh : importance == AlertImportance.Default ? NotificationManagerCompat.ImportanceDefault : NotificationManagerCompat.ImportanceLow;

                builder.Extend(new CarAppExtender.Builder()
                    .SetContentTitle(title)
                    .SetContentText(body)
                    .SetSmallIcon(Resource.Drawable.ic_alert)
                    .SetImportance(carImportance)
                    .Build());
            }

            manager.Notify(notificationId, builder.Build());
        }
    }
}

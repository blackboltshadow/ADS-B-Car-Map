using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Notifies about airspace areas that become active near the user. It runs after the airspace feed downloaded the areas while the map is open, and in every cycle of the background check of the alerts. It only uses the position the application already knows; the kinds of areas and the band of heights are those chosen for the airspace layer.
    /// </summary>
    public static class AirspaceAlertMonitor
    {
        /// <summary>
        /// Tag of the log entries.
        /// </summary>
        private const string LogTag = "AirspaceAlerts";

        /// <summary>
        /// Identifier of the notification channel.
        /// </summary>
        private const string ChannelId = "airspace_alerts";

        /// <summary>
        /// Number of areas from which the notifications are joined into one.
        /// </summary>
        private const int GroupFrom = 4;

        /// <summary>
        /// Number of areas listed in the joined notification.
        /// </summary>
        private const int GroupedLines = 5;

        /// <summary>
        /// Identifier of the joined notification.
        /// </summary>
        private const int GroupNotificationId = 0x41495253;

        /// <summary>
        /// Lock protecting the engine.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Decides which areas to report; created at the first check from the memory saved on the device.
        /// </summary>
        private static AirspaceAlertEngine? engine;

        /// <summary>
        /// Downloads the areas if their time has come, then checks them. Used by the background check of the alerts, which has no airspace loop of its own. Never throws, except for the cancellation.
        /// </summary>
        /// <param name="token">Token cancelled when the background check stops.</param>
        /// <returns>Task completed when the check ends.</returns>
        public static async Task CheckAsync(CancellationToken token)
        {
            if (!AppServices.AirspaceConfig.AlertOptions.Enabled)
            {
                return;
            }

            try
            {
                await AppServices.Airspace.RefreshIfDueAsync(token);
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Pobranie stref w tle nie powiodło się", exception);
            }

            Check();
        }

        /// <summary>
        /// Checks the areas the feed holds against the position of the user and notifies about the ones that became active. Does nothing when the notifications are off, the position is not known or the feed has no areas. Never throws.
        /// </summary>
        public static void Check()
        {
            try
            {
                AirspaceConfigStore store = AppServices.AirspaceConfig;
                AirspaceAlertOptions options = store.AlertOptions;
                IReadOnlyList<DataSources.Models.AirspaceZone> zones = AppServices.Airspace.Items;
                (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();

                if (!options.Enabled || zones.Count == 0 || !known)
                {
                    return;
                }

                IReadOnlyList<AirspaceAlert> alerts;
                DateTime nowUtc = DateTime.UtcNow;
                bool seeded = store.AlertsSeeded;

                lock (SyncRoot)
                {
                    engine ??= new AirspaceAlertEngine(store.LoadReported());
                    alerts = engine.Evaluate(zones, store.Config, new GeoPoint(latitude, longitude), options, nowUtc, !seeded);

                    if (alerts.Count > 0 || !seeded)
                    {
                        store.SaveReported(engine.Reported);
                    }
                }

                if (!seeded)
                {
                    store.MarkAlertsSeeded();
                }

                if (alerts.Count > 0)
                {
                    Notify(alerts, nowUtc);
                }
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Sprawdzenie stref nie powiodło się", exception);
            }
        }

        /// <summary>
        /// Forgets the engine, so that the next check reads the memory from the device again. Used after the options changed.
        /// </summary>
        public static void Reset()
        {
            lock (SyncRoot)
            {
                engine = null;
            }
        }

        /// <summary>
        /// Shows the notifications: one per area, or a single one listing the nearest areas when there are many.
        /// </summary>
        /// <param name="alerts">The areas to report, the nearest first.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        private static void Notify(IReadOnlyList<AirspaceAlert> alerts, DateTime nowUtc)
        {
            Context context = Application.Context;

            if (!AlertMonitor.HasNotificationPermission())
            {
                return;
            }

            NotificationManager? manager = context.GetSystemService(Context.NotificationService)?.JavaCast<NotificationManager>();

            if (manager == null)
            {
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                manager.CreateNotificationChannel(new NotificationChannel(ChannelId, Loc.T("Alerty o strefach"), NotificationImportance.Default));
            }

            DisplayUnits units = AppSettings.Units;

            if (alerts.Count >= GroupFrom)
            {
                string body = string.Join("\n", alerts.Take(GroupedLines).Select((AirspaceAlert alert) => alert.Zone.Designator + ": " + AirspaceText.GetAlertBody(alert, units, TimeZoneInfo.Local)));

                manager.Notify(GroupNotificationId, CreateBuilder(context, Loc.F("Strefy przestrzeni powietrznej w pobliżu: {0}", alerts.Count), body, GroupNotificationId).SetStyle(new NotificationCompat.BigTextStyle().BigText(body)).Build());

                return;
            }

            foreach (AirspaceAlert alert in alerts)
            {
                int id = HashCode.Combine(alert.Zone.Designator, alert.Reservation.StartUtc.Ticks) & 0x7FFFFFFF;

                manager.Notify(id, CreateBuilder(context, AirspaceText.GetAlertTitle(alert, nowUtc), AirspaceText.GetAlertBody(alert, units, TimeZoneInfo.Local), id).Build());
            }
        }

        /// <summary>
        /// Creates a notification that opens the map when it is tapped.
        /// </summary>
        /// <param name="context">Application context.</param>
        /// <param name="title">Title.</param>
        /// <param name="body">Text.</param>
        /// <param name="id">Identifier of the notification.</param>
        /// <returns>The builder.</returns>
        private static NotificationCompat.Builder CreateBuilder(Context context, string title, string body, int id)
        {
            Intent intent = new Intent(context, typeof(Activities.MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            PendingIntent? contentIntent = PendingIntent.GetActivity(context, id, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, ChannelId);
            builder
                .SetSmallIcon(Resource.Drawable.ic_airspace)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetAutoCancel(true)
                .SetCategory(NotificationCompat.CategoryEvent)
                .SetPriority(NotificationCompat.PriorityDefault)
                .SetContentIntent(contentIntent);

            return builder;
        }
    }
}

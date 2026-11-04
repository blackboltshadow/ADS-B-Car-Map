using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Notifies about new official notices of the regional warning system. It runs after the feed downloaded the notices, whoever started the feed, and in every cycle of the background check of the alerts. It uses the categories and voivodeships chosen on the screen of the notices.
    /// </summary>
    public static class WarningAlertMonitor
    {
        /// <summary>
        /// Tag of the log entries.
        /// </summary>
        private const string LogTag = "WarningAlerts";

        /// <summary>
        /// Identifier of the notification channel of the notices.
        /// </summary>
        private const string ChannelId = "rso_warnings";

        /// <summary>
        /// Identifier of the notification channel of the notices marked as an alarm.
        /// </summary>
        private const string AlarmChannelId = "rso_alarms";

        /// <summary>
        /// Number of notices from which the notifications are joined into one.
        /// </summary>
        private const int GroupFrom = 4;

        /// <summary>
        /// Number of notices listed in the joined notification.
        /// </summary>
        private const int GroupedLines = 5;

        /// <summary>
        /// Identifier of the joined notification.
        /// </summary>
        private const int GroupNotificationId = 0x52534F47;

        /// <summary>
        /// Lock protecting the engine.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Decides which notices to report; created at the first check from the memory saved on the device.
        /// </summary>
        private static WarningAlertEngine? engine;

        /// <summary>
        /// Downloads the notices if their time has come, then checks them. Used by the background check of the alerts, which has no loop of the notices of its own. Never throws, except for the cancellation.
        /// </summary>
        /// <param name="token">Token cancelled when the background check stops.</param>
        /// <returns>Task completed when the check ends.</returns>
        public static async Task CheckAsync(CancellationToken token)
        {
            if (!AppServices.WarningConfig.AlertOptions.Enabled)
            {
                return;
            }

            try
            {
                await AppServices.Warnings.RefreshIfDueAsync(token);
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Pobranie ostrzeżeń w tle nie powiodło się", exception);
            }

            Check();
        }

        /// <summary>
        /// Checks the notices the feed holds and notifies about the new ones. Does nothing when the notifications are off or the feed has no notices. Never throws.
        /// </summary>
        public static void Check()
        {
            try
            {
                WarningConfigStore store = AppServices.WarningConfig;
                WarningAlertOptions options = store.AlertOptions;
                IReadOnlyList<Warning> warnings = AppServices.Warnings.Items;

                if (!options.Enabled || warnings.Count == 0 || AppServices.Warnings.LastUpdateUtc == null)
                {
                    return;
                }

                IReadOnlyList<Warning> fresh;
                DateTime nowUtc = DateTime.UtcNow;
                bool seeded = store.AlertsSeeded;

                lock (SyncRoot)
                {
                    engine ??= new WarningAlertEngine(store.LoadReported());
                    fresh = engine.Evaluate(warnings, store.Config, options, nowUtc, !seeded);

                    if (fresh.Count > 0 || !seeded)
                    {
                        store.SaveReported(engine.Reported);
                    }
                }

                if (!seeded)
                {
                    store.MarkAlertsSeeded();
                }

                if (fresh.Count > 0)
                {
                    Notify(fresh);
                }
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Sprawdzenie komunikatów nie powiodło się", exception);
            }
        }

        /// <summary>
        /// Forgets the engine, so that the next check reads the memory from the device again.
        /// </summary>
        public static void Reset()
        {
            lock (SyncRoot)
            {
                engine = null;
            }
        }

        /// <summary>
        /// Shows the notifications: one per notice, or a single one listing the first notices when there are many.
        /// </summary>
        /// <param name="fresh">The notices to report, alarms first.</param>
        private static void Notify(IReadOnlyList<Warning> fresh)
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
                manager.CreateNotificationChannel(new NotificationChannel(ChannelId, Loc.T("Komunikaty RSO"), NotificationImportance.Default));
                manager.CreateNotificationChannel(new NotificationChannel(AlarmChannelId, Loc.T("Komunikaty RSO oznaczone jako alarm"), NotificationImportance.High));
            }

            if (fresh.Count >= GroupFrom)
            {
                bool anyAlarm = fresh.Any((Warning warning) => warning.IsAlarm);
                string body = string.Join("\n", fresh.Take(GroupedLines).Select((Warning warning) => WarningText.GetAlertTitle(warning)));
                string title = WarningText.GetGroupTitle(fresh.Count, fresh.Count((Warning warning) => warning.IsAlarm));

                manager.Notify(GroupNotificationId, CreateBuilder(context, anyAlarm, title, body, GroupNotificationId).SetStyle(new NotificationCompat.BigTextStyle().BigText(body)).Build());

                return;
            }

            foreach (Warning warning in fresh)
            {
                int id = HashCode.Combine(warning.Category, warning.Id) & 0x7FFFFFFF;
                string body = WarningText.GetAlertBody(warning);

                manager.Notify(id, CreateBuilder(context, warning.IsAlarm, WarningText.GetAlertTitle(warning), body, id).SetStyle(new NotificationCompat.BigTextStyle().BigText(body)).Build());
            }
        }

        /// <summary>
        /// Creates a notification that opens the screen of the notices when it is tapped.
        /// </summary>
        /// <param name="context">Application context.</param>
        /// <param name="alarm">Whether the notification is about an alarm, which makes it more urgent.</param>
        /// <param name="title">Title.</param>
        /// <param name="body">Text.</param>
        /// <param name="id">Identifier of the notification.</param>
        /// <returns>The builder.</returns>
        private static NotificationCompat.Builder CreateBuilder(Context context, bool alarm, string title, string body, int id)
        {
            Intent intent = new Intent(context, typeof(Activities.WarningsActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            PendingIntent? contentIntent = PendingIntent.GetActivity(context, id, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, alarm ? AlarmChannelId : ChannelId);
            builder
                .SetSmallIcon(Resource.Drawable.ic_alert)
                .SetContentTitle(title)
                .SetContentText(body.Split('\n')[0])
                .SetAutoCancel(true)
                .SetCategory(NotificationCompat.CategoryEvent)
                .SetPriority(alarm ? NotificationCompat.PriorityHigh : NotificationCompat.PriorityDefault)
                .SetContentIntent(contentIntent);

            return builder;
        }
    }
}

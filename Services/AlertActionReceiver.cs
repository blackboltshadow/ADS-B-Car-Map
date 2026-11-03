using Android.App;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Handles the buttons of the alert notifications that do not open the application.
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class AlertActionReceiver : BroadcastReceiver
    {
        /// <summary>
        /// Action of the button that silences an alert for an hour.
        /// </summary>
        public const string ActionMute = "com.blackboltshadow.adsbcarmap.ALERT_MUTE";

        /// <summary>
        /// Name of the extra with the identifier of the rule.
        /// </summary>
        public const string RuleIdExtra = "alert_rule_id";

        /// <summary>
        /// Name of the extra with the identifier of the notification to remove.
        /// </summary>
        public const string NotificationIdExtra = "alert_notification_id";

        /// <summary>
        /// How long the alert stays silent after the button was pressed.
        /// </summary>
        private static readonly TimeSpan MuteDuration = TimeSpan.FromHours(1);

        /// <summary>
        /// Silences the alert and removes the notification.
        /// </summary>
        /// <param name="context">Application context.</param>
        /// <param name="intent">Intent of the button.</param>
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null || intent?.Action != ActionMute)
            {
                return;
            }

            string? ruleId = intent.GetStringExtra(RuleIdExtra);

            if (!string.IsNullOrEmpty(ruleId))
            {
                AlertMonitor.MuteRule(ruleId, MuteDuration);
            }

            int notificationId = intent.GetIntExtra(NotificationIdExtra, -1);

            if (notificationId >= 0)
            {
                NotificationManager? manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
                manager?.Cancel(notificationId);
            }
        }
    }
}

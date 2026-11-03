using AdsbCarMap.Data;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Starts the background checking of the alerts after the device boots or the application is updated, so the application does not have to be opened.
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced })]
    public class BootReceiver : BroadcastReceiver
    {
        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "BootReceiver";

        /// <summary>
        /// Handles the boot or update broadcast. Any other action is ignored, because the receiver is exported.
        /// </summary>
        /// <param name="context">Context of the receiver.</param>
        /// <param name="intent">Received broadcast.</param>
        public override void OnReceive(Context? context, Intent? intent)
        {
            string? action = intent?.Action;

            if (action != Intent.ActionBootCompleted && action != Intent.ActionMyPackageReplaced)
            {
                return;
            }

            try
            {
                AlertRuleStore store = AppServices.Alerts;

                if (!AlertBackgroundPolicy.ShouldStartOnBoot(AppSettings.AlertStartOnBoot, store.IsBackgroundEnabled, store.IsEnabled, store.GetActiveRules().Count))
                {
                    return;
                }

                AlertBackgroundRunner.Initialize();
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Nie udało się uruchomić trybu w tle po restarcie", exception);
            }
        }
    }
}

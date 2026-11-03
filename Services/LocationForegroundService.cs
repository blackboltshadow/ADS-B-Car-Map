using AdsbCarMap.Activities;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Foreground service of the location type that keeps position access alive when no application screen is visible on the phone.
    /// </summary>
    [Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Identifier of the service notification channel.
        /// </summary>
        private const string ChannelId = "location_tracking";

        /// <summary>
        /// Identifier of the foreground service notification.
        /// </summary>
        private const int NotificationId = 1001;

        /// <summary>
        /// Lock protecting the service user counter.
        /// </summary>
        private static readonly object ClientsLock = new object();

        /// <summary>
        /// Number of position receivers using the service, e.g. the phone map and the Android Auto map.
        /// </summary>
        private static int clientCount;

        /// <summary>
        /// Whether the system is starting the service but it has not moved to the foreground yet. Stopping the service in that time ends with a system exception.
        /// </summary>
        private static bool startPending;

        /// <summary>
        /// Whether the last user finished before the service moved to the foreground, so the service must stop right after the move.
        /// </summary>
        private static bool stopWhenStarted;

        /// <summary>
        /// Registers another user and starts the foreground service if it is the first. When the previous start is still in progress, cancels the deferred stop. A start failure is logged and does not interrupt the application.
        /// </summary>
        public static void Start()
        {
            lock (ClientsLock)
            {
                clientCount++;

                if (clientCount > 1)
                {
                    return;
                }

                if (startPending)
                {
                    stopWhenStarted = false;

                    return;
                }

                startPending = true;
                stopWhenStarted = false;
            }

            Context context = Android.App.Application.Context;
            Intent intent = new Intent(context, typeof(LocationForegroundService));

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    context.StartForegroundService(intent);
                }
                else
                {
                    context.StartService(intent);
                }
            }
            catch (Java.Lang.Exception exception)
            {
                DevLogger.Warn(LogTag, "Nie udało się uruchomić usługi lokalizacji: " + exception.Message);

                lock (ClientsLock)
                {
                    startPending = false;
                }
            }
        }

        /// <summary>
        /// Reports the end of using the service and stops it when there are no other users. If the service has not moved to the foreground yet, the stop is deferred until then.
        /// </summary>
        public static void Stop()
        {
            lock (ClientsLock)
            {
                if (clientCount == 0)
                {
                    return;
                }

                clientCount--;

                if (clientCount > 0)
                {
                    return;
                }

                if (startPending)
                {
                    stopWhenStarted = true;

                    return;
                }
            }

            Context context = Android.App.Application.Context;
            Intent intent = new Intent(context, typeof(LocationForegroundService));

            context.StopService(intent);
        }

        /// <summary>
        /// The service does not support binding.
        /// </summary>
        /// <param name="intent">Binding intent.</param>
        /// <returns>Always null.</returns>
        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }

        /// <summary>
        /// Moves the service to the foreground with a position tracking notification.
        /// </summary>
        /// <param name="intent">Intent that started the service.</param>
        /// <param name="flags">Start flags.</param>
        /// <param name="startId">Start identifier.</param>
        /// <returns>Mode without restarting after the process is killed.</returns>
        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            Log.Info(LogTag, "LocationForegroundService.OnStartCommand");

            bool enteredForeground = false;

            try
            {
                Notification notification = BuildNotification();

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
                }
                else
                {
                    StartForeground(NotificationId, notification);
                }

                enteredForeground = true;
            }
            catch (Exception exception)
            {
                DevLogger.Warn(LogTag, "Nie udało się przejść na pierwszy plan: " + exception.Message);
            }

            bool stopNow;

            lock (ClientsLock)
            {
                startPending = false;
                stopNow = stopWhenStarted || !enteredForeground;
                stopWhenStarted = false;
            }

            if (stopNow)
            {
                StopSelf();
            }

            return StartCommandResult.NotSticky;
        }

        /// <summary>
        /// Clears the in-progress start flag so that stopping the service is not deferred forever after it is destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            lock (ClientsLock)
            {
                startPending = false;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Creates the notification channel if the system requires it and builds the service notification.
        /// </summary>
        /// <returns>Persistent notification that opens the phone screen when tapped.</returns>
        private Notification BuildNotification()
        {
            Notification.Builder builder;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationManager? manager = GetSystemService(NotificationService)?.JavaCast<NotificationManager>();
                NotificationChannel channel = new NotificationChannel(ChannelId, "Śledzenie pozycji", NotificationImportance.Low);
                manager?.CreateNotificationChannel(channel);
                builder = new Notification.Builder(this, ChannelId);
            }
            else
            {
                builder = new Notification.Builder(this);
            }

            Intent activityIntent = new Intent(this, typeof(MainActivity));
            PendingIntent? contentIntent = PendingIntent.GetActivity(this, 0, activityIntent, PendingIntentFlags.Immutable);

            builder
                .SetSmallIcon(Resource.Drawable.ic_my_location)
                .SetContentTitle("ADS-B Car Map")
                .SetContentText("Śledzenie pozycji dla mapy samolotów")
                .SetOngoing(true)
                .SetContentIntent(contentIntent);

            return builder.Build();
        }
    }
}

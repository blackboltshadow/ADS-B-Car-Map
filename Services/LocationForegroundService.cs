using AdsbCarMap.Activities;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace AdsbCarMap.Services;

/// <summary>
/// Usługa pierwszoplanowa typu location, która utrzymuje dostęp do pozycji, gdy na telefonie nie ma widocznego ekranu aplikacji.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    /// <summary>
    /// Tag używany w logach Androida.
    /// </summary>
    private const string LogTag = "AdsbCarMap";

    /// <summary>
    /// Identyfikator kanału powiadomień usługi.
    /// </summary>
    private const string ChannelId = "location_tracking";

    /// <summary>
    /// Identyfikator powiadomienia usługi pierwszoplanowej.
    /// </summary>
    private const int NotificationId = 1001;

    /// <summary>
    /// Blokada chroniąca licznik użytkowników usługi.
    /// </summary>
    private static readonly object ClientsLock = new object();

    /// <summary>
    /// Liczba odbiorników pozycji, które korzystają z usługi, np. mapa telefonu i mapa Android Auto.
    /// </summary>
    private static int clientCount;

    /// <summary>
    /// Zgłasza kolejnego użytkownika i uruchamia usługę pierwszoplanową, jeśli jest pierwszy. Błąd uruchomienia jest logowany i nie przerywa działania aplikacji.
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
            Log.Warn(LogTag, "Nie udało się uruchomić usługi lokalizacji: " + exception.Message);
        }
    }

    /// <summary>
    /// Zgłasza zakończenie korzystania z usługi i zatrzymuje ją, gdy nie ma już innych użytkowników.
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
        }

        Context context = Android.App.Application.Context;
        Intent intent = new Intent(context, typeof(LocationForegroundService));

        context.StopService(intent);
    }

    /// <summary>
    /// Usługa nie obsługuje wiązania.
    /// </summary>
    /// <param name="intent">Intencja wiązania.</param>
    /// <returns>Zawsze null.</returns>
    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    /// <summary>
    /// Przenosi usługę na pierwszy plan z powiadomieniem o śledzeniu pozycji.
    /// </summary>
    /// <param name="intent">Intencja uruchamiająca usługę.</param>
    /// <param name="flags">Flagi uruchomienia.</param>
    /// <param name="startId">Identyfikator uruchomienia.</param>
    /// <returns>Tryb bez ponownego uruchamiania po zabiciu procesu.</returns>
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Log.Info(LogTag, "LocationForegroundService.OnStartCommand");

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
        }
        catch (Java.Lang.Exception exception)
        {
            Log.Warn(LogTag, "Nie udało się przejść na pierwszy plan: " + exception.Message);
            StopSelf();
        }

        return StartCommandResult.NotSticky;
    }

    /// <summary>
    /// Tworzy kanał powiadomień, jeśli system go wymaga, i buduje powiadomienie usługi.
    /// </summary>
    /// <returns>Stałe powiadomienie otwierające ekran telefonu po dotknięciu.</returns>
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

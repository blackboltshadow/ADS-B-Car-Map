using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using AndroidX.Car.App;
using AndroidX.Car.App.Validation;

namespace AdsbCarMap.Auto
{
    /// <summary>
    /// Car App service registered in Android Auto as a navigation application.
    /// </summary>
    [Service(Exported = true)]
    [IntentFilter(
        new string[] { "androidx.car.app.CarAppService" },
        Categories = new string[] { "androidx.car.app.category.NAVIGATION" })]
    public class AdsbCarService : CarAppService
    {
        /// <summary>
        /// Tag used in the Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Starts reporting the exceptions that nothing handles to the developer log.
        /// </summary>
        public override void OnCreate()
        {
            base.OnCreate();
            DevLogger.InstallGlobalHandlers();
        }

        /// <summary>
        /// Returns the validator that decides which hosts may connect. Only the hosts from the allowlist are accepted: the development list in Debug builds and the release list in Release builds (see Resources/values/allowed_hosts.xml). A Debug build also writes to the log whether the installed hosts match the list.
        /// </summary>
        /// <returns>Validator that accepts only the hosts from the allowlist.</returns>
        public override HostValidator CreateHostValidator()
        {
#if DEBUG
            int allowlist = Resource.Array.hosts_allowlist_dev;
            Android.Util.Log.Info(LogTag, "Host validator: development allowlist");
            HostAllowlistDiagnostics.Log(this, Resources!.GetStringArray(allowlist)!);
#else
            int allowlist = Resource.Array.hosts_allowlist_release;
#endif

            return new HostValidator.Builder(ApplicationContext!).AddAllowedHosts(allowlist)!.Build()!;
        }

        /// <summary>
        /// Creates a session for a connection to the car.
        /// </summary>
        /// <returns>New application session.</returns>
        public override Session OnCreateSession()
        {
            Android.Util.Log.Info("AdsbCarMap", "OnCreateSession");
            return new AdsbSession();
        }
    }

    /// <summary>
    /// Android Auto session that creates the screen with the map.
    /// </summary>
    public class AdsbSession : Session
    {
        /// <summary>
        /// Creates the first screen of the session.
        /// </summary>
        /// <param name="intent">Intent that started the session.</param>
        /// <returns>Screen with the map of the aircraft.</returns>
        public override Screen OnCreateScreen(Intent intent)
        {
            Android.Util.Log.Info("AdsbCarMap", "OnCreateScreen");
            return new MapScreen(CarContext);
        }
    }
}

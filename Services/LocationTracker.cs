using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Util;
using AdsbCarMap.Map;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Receives position updates from GPS and the network and passes them on through a delegate.
    /// </summary>
    public sealed class LocationTracker : Java.Lang.Object, ILocationListener
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Minimum time between updates in milliseconds.
        /// </summary>
        private const long MinTimeMilliseconds = 1000;

        /// <summary>
        /// Minimum position change in meters required for an update.
        /// </summary>
        private const float MinDistanceMeters = 0.0f;

        /// <summary>
        /// Delegate called when a new position arrives, with the latitude and longitude.
        /// </summary>
        private readonly System.Action<double, double> onLocationChanged;

        /// <summary>
        /// Delegate called when a position arrives that has a reliable bearing, with the speed in meters per second and the bearing in degrees, or null when the bearing is not used.
        /// </summary>
        private readonly System.Action<double, float>? onMotion;

        /// <summary>
        /// System location manager. Null until tracking is started.
        /// </summary>
        private LocationManager? locationManager;

        /// <summary>
        /// Whether updates are currently enabled.
        /// </summary>
        private bool isStarted;

        /// <summary>
        /// Creates a tracker that passes positions to the given delegate.
        /// </summary>
        /// <param name="onLocationChanged">Delegate called when a position arrives.</param>
        /// <param name="onMotion">Delegate called with the speed in meters per second and the bearing in degrees when a position arrives that moves fast enough for its bearing to be reliable, or null when the bearing is not used.</param>
        public LocationTracker(System.Action<double, double> onLocationChanged, System.Action<double, float>? onMotion = null)
        {
            this.onLocationChanged = onLocationChanged;
            this.onMotion = onMotion;
        }

        /// <summary>
        /// Checks whether the application has the permission for precise or approximate location.
        /// </summary>
        /// <returns>True when at least one of the location permissions is granted.</returns>
        public static bool HasPermission()
        {
            Context context = Android.App.Application.Context;

            bool fineGranted = context.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) == Permission.Granted;
            bool coarseGranted = context.CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation) == Permission.Granted;

            return fineGranted || coarseGranted;
        }

        /// <summary>
        /// Checks whether the application may read the location while it runs in the background, e.g. after being started by the device boot. Before Android 10 the permission does not exist and the location is available anyway.
        /// </summary>
        /// <returns>True when the background location is allowed or not needed.</returns>
        public static bool HasBackgroundPermission()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
            {
                return true;
            }

            return Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.AccessBackgroundLocation) == Permission.Granted;
        }

        /// <summary>
        /// Starts the foreground location service, enables position updates from all enabled providers and reports the last known position.
        /// </summary>
        /// <returns>True when updates are running. False when the permission or the location manager is missing.</returns>
        public bool Start()
        {
            if (isStarted)
            {
                return true;
            }

            if (!HasPermission())
            {
                return false;
            }

            Java.Lang.Object? service = Android.App.Application.Context.GetSystemService(Context.LocationService);
            locationManager = service?.JavaCast<LocationManager>();

            if (locationManager == null)
            {
                return false;
            }

            LocationForegroundService.Start();

            string[] providers = new string[] { LocationManager.GpsProvider, LocationManager.NetworkProvider };

            foreach (string provider in providers)
            {
                if (!locationManager.IsProviderEnabled(provider))
                {
                    continue;
                }

                try
                {
                    locationManager.RequestLocationUpdates(provider, MinTimeMilliseconds, MinDistanceMeters, this, Looper.MainLooper);

                    Location? lastKnown = locationManager.GetLastKnownLocation(provider);

                    if (lastKnown != null)
                    {
                        onLocationChanged(lastKnown.Latitude, lastKnown.Longitude);
                    }
                }
                catch (Java.Lang.SecurityException exception)
                {
                    DevLogger.Warn(LogTag, "Brak uprawnienia do lokalizacji: " + exception.Message);
                    locationManager.RemoveUpdates(this);
                    LocationForegroundService.Stop();
                    return false;
                }
            }

            isStarted = true;

            return true;
        }

        /// <summary>
        /// Disables position updates and stops the foreground location service.
        /// </summary>
        public void Stop()
        {
            if (!isStarted)
            {
                return;
            }

            locationManager?.RemoveUpdates(this);
            LocationForegroundService.Stop();
            isStarted = false;
        }

        /// <summary>
        /// Passes the new position to the delegate.
        /// </summary>
        /// <param name="location">New device position.</param>
        public void OnLocationChanged(Location location)
        {
            onLocationChanged(location.Latitude, location.Longitude);

            if (onMotion != null && MapRotation.IsBearingReliable(location.HasBearing, location.HasSpeed ? location.Speed : 0.0))
            {
                onMotion(location.Speed, location.Bearing);
            }
        }

        /// <summary>
        /// Handles a location provider being disabled. Ignored in the proof of concept.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        public void OnProviderDisabled(string provider)
        {
        }

        /// <summary>
        /// Handles a location provider being enabled. Ignored in the proof of concept.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        public void OnProviderEnabled(string provider)
        {
        }

        /// <summary>
        /// Handles a provider status change. Deprecated method, required by older Android versions.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="status">New provider status.</param>
        /// <param name="extras">Additional data.</param>
        public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
        {
        }
    }
}

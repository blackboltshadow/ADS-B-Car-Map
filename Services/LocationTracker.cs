using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Odbiera aktualizacje pozycji z GPS i sieci oraz przekazuje je przez delegat.
    /// </summary>
    public sealed class LocationTracker : Java.Lang.Object, ILocationListener
    {
        /// <summary>
        /// Tag używany w logach Androida.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Minimalny odstęp czasu między aktualizacjami w milisekundach.
        /// </summary>
        private const long MinTimeMilliseconds = 1000;

        /// <summary>
        /// Minimalna zmiana pozycji w metrach wymagana do aktualizacji.
        /// </summary>
        private const float MinDistanceMeters = 0.0f;

        /// <summary>
        /// Delegat wywoływany po otrzymaniu nowej pozycji, z szerokością i długością geograficzną.
        /// </summary>
        private readonly System.Action<double, double> onLocationChanged;

        /// <summary>
        /// Menedżer lokalizacji systemu. Null, dopóki śledzenie nie zostanie uruchomione.
        /// </summary>
        private LocationManager? locationManager;

        /// <summary>
        /// Informacja, czy aktualizacje są aktualnie włączone.
        /// </summary>
        private bool isStarted;

        /// <summary>
        /// Tworzy tracker przekazujący pozycje do podanego delegata.
        /// </summary>
        /// <param name="onLocationChanged">Delegat wywoływany po otrzymaniu pozycji.</param>
        public LocationTracker(System.Action<double, double> onLocationChanged)
        {
            this.onLocationChanged = onLocationChanged;
        }

        /// <summary>
        /// Sprawdza, czy aplikacja ma uprawnienie do dokładnej lub przybliżonej lokalizacji.
        /// </summary>
        /// <returns>True, gdy przyznano co najmniej jedno z uprawnień lokalizacji.</returns>
        public static bool HasPermission()
        {
            Context context = Android.App.Application.Context;

            bool fineGranted = context.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) == Permission.Granted;
            bool coarseGranted = context.CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation) == Permission.Granted;

            return fineGranted || coarseGranted;
        }

        /// <summary>
        /// Uruchamia usługę pierwszoplanową lokalizacji, włącza aktualizacje pozycji ze wszystkich włączonych dostawców i zgłasza ostatnią znaną pozycję.
        /// </summary>
        /// <returns>True, gdy aktualizacje działają. False, gdy brakuje uprawnienia lub menedżera lokalizacji.</returns>
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
                    Log.Warn(LogTag, "Brak uprawnienia do lokalizacji: " + exception.Message);
                    locationManager.RemoveUpdates(this);
                    LocationForegroundService.Stop();
                    return false;
                }
            }

            isStarted = true;

            return true;
        }

        /// <summary>
        /// Wyłącza aktualizacje pozycji i zatrzymuje usługę pierwszoplanową lokalizacji.
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
        /// Przekazuje nową pozycję do delegata.
        /// </summary>
        /// <param name="location">Nowa pozycja urządzenia.</param>
        public void OnLocationChanged(Location location)
        {
            onLocationChanged(location.Latitude, location.Longitude);
        }

        /// <summary>
        /// Obsługuje wyłączenie dostawcy lokalizacji. W PoC jest ignorowane.
        /// </summary>
        /// <param name="provider">Nazwa dostawcy.</param>
        public void OnProviderDisabled(string provider)
        {
        }

        /// <summary>
        /// Obsługuje włączenie dostawcy lokalizacji. W PoC jest ignorowane.
        /// </summary>
        /// <param name="provider">Nazwa dostawcy.</param>
        public void OnProviderEnabled(string provider)
        {
        }

        /// <summary>
        /// Obsługuje zmianę statusu dostawcy. Metoda przestarzała, wymagana przez starsze wersje Androida.
        /// </summary>
        /// <param name="provider">Nazwa dostawcy.</param>
        /// <param name="status">Nowy status dostawcy.</param>
        /// <param name="extras">Dodatkowe dane.</param>
        public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
        {
        }
    }
}

using AdsbCarMap.Services;
using Android.Graphics;
using Android.Runtime;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Navigation.Model;
using AndroidX.Core.Graphics.Drawable;
using CarAction = AndroidX.Car.App.Model.Action;
using MapController = AdsbCarMap.Map.MapController;

namespace AdsbCarMap.Auto;

/// <summary>
/// Ekran Android Auto z mapą, na której rysowane są samoloty.
/// </summary>
public class MapScreen : Screen
{
    /// <summary>
    /// Kontroler stanu mapy i rysowania.
    /// </summary>
    private readonly MapController controller;

    /// <summary>
    /// Callback obsługujący powierzchnię rysowania i gesty.
    /// </summary>
    private readonly MapSurfaceCallback surfaceCallback;

    /// <summary>
    /// Tworzy ekran i rejestruje callback powierzchni w menedżerze aplikacji.
    /// </summary>
    /// <param name="carContext">Kontekst samochodu przekazany przez sesję.</param>
    public MapScreen(CarContext carContext) : base(carContext)
    {
        controller = new MapController(AppServices.Adsb, AppServices.Tiles, AppServices.Routes);
        surfaceCallback = new MapSurfaceCallback(controller);

        if (!LocationTracker.HasPermission())
        {
            List<string> permissions = new List<string>
            {
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.AccessCoarseLocation
            };

            carContext.RequestPermissions(permissions, new PermissionResultListener(controller));
        }

        Java.Lang.Object? service = carContext.GetCarService(CarContext.AppService);
        AppManager appManager = service.JavaCast<AppManager>();
        appManager.SetSurfaceCallback(surfaceCallback);
    }

    /// <summary>
    /// Buduje szablon nawigacyjny z przyciskami wyszukiwania i centrowania u góry oraz przesuwaniem i zoomem w pasku mapy po prawej stronie.
    /// </summary>
    /// <returns>Szablon wyświetlany na ekranie samochodu.</returns>
    public override ITemplate OnGetTemplate()
    {
        CarAction recenterAction = new CarAction.Builder()
            .SetIcon(CreateIcon(Resource.Drawable.ic_my_location))
            .SetOnClickListener(new DelegateClickListener(controller.FollowUser))
            .Build();

        CarAction searchAction = new CarAction.Builder()
            .SetIcon(CreateIcon(Resource.Drawable.ic_search))
            .SetOnClickListener(new DelegateClickListener(OpenSearch))
            .Build();

        ActionStrip actionStrip = new ActionStrip.Builder()
            .AddAction(searchAction)
            .AddAction(recenterAction)
            .Build();

        CarAction zoomInAction = new CarAction.Builder()
            .SetIcon(CreateIcon(Resource.Drawable.ic_zoom_in))
            .SetOnClickListener(new DelegateClickListener(controller.ZoomIn))
            .Build();

        CarAction zoomOutAction = new CarAction.Builder()
            .SetIcon(CreateIcon(Resource.Drawable.ic_zoom_out))
            .SetOnClickListener(new DelegateClickListener(controller.ZoomOut))
            .Build();

        ActionStrip mapActionStrip = new ActionStrip.Builder()
            .AddAction(CarAction.Pan)
            .AddAction(zoomInAction)
            .AddAction(zoomOutAction)
            .Build();

        NavigationTemplate template = new NavigationTemplate.Builder()
            .SetActionStrip(actionStrip)
            .SetMapActionStrip(mapActionStrip)
            .Build();

        return template;
    }

    /// <summary>
    /// Otwiera ekran wyszukiwania samolotów.
    /// </summary>
    private void OpenSearch()
    {
        ScreenManager.Push(new SearchScreen(CarContext, controller));
    }

    /// <summary>
    /// Tworzy ikonę Android Auto z zasobu drawable.
    /// </summary>
    /// <param name="resourceId">Identyfikator zasobu drawable.</param>
    /// <returns>Ikona do użycia w akcji.</returns>
    private CarIcon CreateIcon(int resourceId)
    {
        IconCompat iconCompat = IconCompat.CreateWithResource(CarContext, resourceId)!;

        return new CarIcon.Builder(iconCompat).Build();
    }
}

/// <summary>
/// Callback powierzchni Android Auto, który przekazuje zdarzenia do kontrolera mapy.
/// </summary>
public class MapSurfaceCallback : Java.Lang.Object, ISurfaceCallback
{
    /// <summary>
    /// Kontroler stanu mapy i rysowania.
    /// </summary>
    private readonly MapController controller;

    /// <summary>
    /// Tworzy callback powiązany z kontrolerem mapy.
    /// </summary>
    /// <param name="controller">Kontroler mapy.</param>
    public MapSurfaceCallback(MapController controller)
    {
        this.controller = controller;
    }

    /// <summary>
    /// Uruchamia rysowanie, gdy powierzchnia staje się dostępna.
    /// </summary>
    /// <param name="surfaceContainer">Kontener z powierzchnią i jej parametrami.</param>
    public void OnSurfaceAvailable(SurfaceContainer surfaceContainer)
    {
        Android.Views.Surface? surface = surfaceContainer.Surface;

        Android.Util.Log.Info("AdsbCarMap", "OnSurfaceAvailable " + surfaceContainer.Width + "x" + surfaceContainer.Height);

        if (surface == null)
        {
            return;
        }

        controller.Start(surface, surfaceContainer.Width, surfaceContainer.Height, surfaceContainer.Dpi);
    }

    /// <summary>
    /// Zatrzymuje rysowanie, gdy powierzchnia przestaje być dostępna.
    /// </summary>
    /// <param name="surfaceContainer">Kontener z niszczoną powierzchnią.</param>
    public void OnSurfaceDestroyed(SurfaceContainer surfaceContainer)
    {
        Android.Util.Log.Info("AdsbCarMap", "OnSurfaceDestroyed");
        controller.Stop();
    }

    /// <summary>
    /// Obsługuje zmianę widocznego obszaru. W PoC jest ignorowana.
    /// </summary>
    /// <param name="visibleArea">Widoczny obszar powierzchni.</param>
    public void OnVisibleAreaChanged(Rect visibleArea)
    {
    }

    /// <summary>
    /// Obsługuje zmianę obszaru stabilnego. W PoC jest ignorowana.
    /// </summary>
    /// <param name="stableArea">Obszar niezasłaniany przez elementy interfejsu.</param>
    public void OnStableAreaChanged(Rect stableArea)
    {
    }

    /// <summary>
    /// Przesuwa mapę zgodnie z gestem przewijania.
    /// </summary>
    /// <param name="distanceX">Przesunięcie w poziomie od poprzedniego wywołania, w pikselach.</param>
    /// <param name="distanceY">Przesunięcie w pionie od poprzedniego wywołania, w pikselach.</param>
    public void OnScroll(float distanceX, float distanceY)
    {
        controller.Pan(distanceX, distanceY);
    }

    /// <summary>
    /// Uruchamia bezwładne przesuwanie mapy po geście przeciągnięcia z rozpędem.
    /// </summary>
    /// <param name="velocityX">Prędkość w poziomie.</param>
    /// <param name="velocityY">Prędkość w pionie.</param>
    public void OnFling(float velocityX, float velocityY)
    {
        controller.Fling(velocityX, velocityY);
    }

    /// <summary>
    /// Zmienia zoom zgodnie z gestem skalowania.
    /// </summary>
    /// <param name="focusX">Punkt ogniskowania w poziomie.</param>
    /// <param name="focusY">Punkt ogniskowania w pionie.</param>
    /// <param name="scaleFactor">Współczynnik skali względem poprzedniego wywołania.</param>
    public void OnScale(float focusX, float focusY, float scaleFactor)
    {
        controller.Scale(scaleFactor);
    }

    /// <summary>
    /// Obsługuje dotknięcie powierzchni. W PoC jest ignorowane.
    /// </summary>
    /// <param name="x">Pozycja dotknięcia w poziomie.</param>
    /// <param name="y">Pozycja dotknięcia w pionie.</param>
    public void OnClick(float x, float y)
    {
    }
}

/// <summary>
/// Adapter przekazujący kliknięcia przycisków Android Auto do delegata C#.
/// </summary>
public class DelegateClickListener : Java.Lang.Object, IOnClickListener
{
    /// <summary>
    /// Delegat wywoływany po kliknięciu.
    /// </summary>
    private readonly System.Action callback;

    /// <summary>
    /// Tworzy listener wywołujący podany delegat.
    /// </summary>
    /// <param name="callback">Delegat wywoływany po kliknięciu.</param>
    public DelegateClickListener(System.Action callback)
    {
        this.callback = callback;
    }

    /// <summary>
    /// Wywołuje delegat po kliknięciu przycisku.
    /// </summary>
    public void OnClick()
    {
        callback();
    }
}

/// <summary>
/// Odbiera wynik prośby o uprawnienia lokalizacji i uruchamia aktualizacje pozycji po ich przyznaniu.
/// </summary>
public class PermissionResultListener : Java.Lang.Object, IOnRequestPermissionsListener
{
    /// <summary>
    /// Kontroler mapy, który ma rozpocząć aktualizacje pozycji.
    /// </summary>
    private readonly MapController controller;

    /// <summary>
    /// Tworzy listener powiązany z kontrolerem mapy.
    /// </summary>
    /// <param name="controller">Kontroler mapy.</param>
    public PermissionResultListener(MapController controller)
    {
        this.controller = controller;
    }

    /// <summary>
    /// Uruchamia aktualizacje pozycji, jeśli przyznano którekolwiek z uprawnień lokalizacji.
    /// </summary>
    /// <param name="grantedPermissions">Przyznane uprawnienia.</param>
    /// <param name="rejectedPermissions">Odrzucone uprawnienia.</param>
    public void OnRequestPermissionsResult(IList<string> grantedPermissions, IList<string> rejectedPermissions)
    {
        bool fineGranted = grantedPermissions.Contains(Android.Manifest.Permission.AccessFineLocation);
        bool coarseGranted = grantedPermissions.Contains(Android.Manifest.Permission.AccessCoarseLocation);

        if (fineGranted || coarseGranted)
        {
            controller.StartLocationUpdates();
        }
    }
}

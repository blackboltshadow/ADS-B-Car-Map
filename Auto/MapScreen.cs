using System.Globalization;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Navigation.Model;
using AndroidX.Core.Graphics.Drawable;
using CarAction = AndroidX.Car.App.Model.Action;
using MapController = AdsbCarMap.Map.MapController;

namespace AdsbCarMap.Auto
{
    /// <summary>
    /// Android Auto screen with the map on which the aircraft are drawn.
    /// </summary>
    public class MapScreen : Screen
    {
        /// <summary>
        /// Controller of the map state and drawing.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Callback handling the drawing surface and the gestures.
        /// </summary>
        private readonly MapSurfaceCallback surfaceCallback;

        /// <summary>
        /// Main thread handler used to rebuild the template when the map modes change.
        /// </summary>
        private readonly Handler handler = new Handler(Looper.MainLooper!);

        /// <summary>
        /// Creates the screen and registers the surface callback in the app manager.
        /// </summary>
        /// <param name="carContext">Car context provided by the session.</param>
        public MapScreen(CarContext carContext) : base(carContext)
        {
            controller = new MapController(AppServices.Adsb, AppServices.Tiles, AppServices.Routes);
            controller.SelectionChanged += RefreshTemplate;
            controller.LabelSelectedAircraft = true;
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
        /// Builds the navigation template with the search, military filter, isolation and labels buttons at the top, and panning, zoom in, zoom out and recenter in the map action strip on the right side.
        /// </summary>
        /// <returns>Template shown on the car screen.</returns>
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

            CarAction militaryAction = new CarAction.Builder()
                .SetIcon(CreateIcon(Resource.Drawable.ic_fighter, controller.IsMilitaryOnly))
                .SetOnClickListener(new DelegateClickListener(ToggleMilitaryOnly))
                .Build();

            CarAction isolateAction = new CarAction.Builder()
                .SetIcon(CreateIcon(Resource.Drawable.ic_isolate, controller.IsIsolated))
                .SetOnClickListener(new DelegateClickListener(ToggleIsolation))
                .Build();

            CarAction labelsAction = new CarAction.Builder()
                .SetIcon(CreateIcon(Resource.Drawable.ic_labels, AppSettings.ShowLabels))
                .SetOnClickListener(new DelegateClickListener(ToggleLabels))
                .Build();

            ActionStrip actionStrip = new ActionStrip.Builder()
                .AddAction(searchAction)
                .AddAction(militaryAction)
                .AddAction(isolateAction)
                .AddAction(labelsAction)
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
                .AddAction(recenterAction)
                .Build();

            NavigationTemplate template = new NavigationTemplate.Builder()
                .SetActionStrip(actionStrip)
                .SetMapActionStrip(mapActionStrip)
                .Build();

            return template;
        }

        /// <summary>
        /// Opens the aircraft search screen.
        /// </summary>
        private void OpenSearch()
        {
            ScreenManager.Push(new SearchScreen(CarContext, controller));
        }

        /// <summary>
        /// Turns the filter that shows only military aircraft on or off and reports the result in a car toast.
        /// </summary>
        private void ToggleMilitaryOnly()
        {
            bool enabled = controller.ToggleMilitaryOnly(out int militaryCount);
            string message = !enabled
                ? "Wszystkie samoloty"
                : militaryCount > 0
                    ? string.Format(CultureInfo.InvariantCulture, "Tylko wojskowe: {0} w zasięgu", militaryCount)
                    : "Tylko wojskowe: żadnego w zasięgu";

            CarToast.MakeText(CarContext, message, CarToast.LengthShort)?.Show();
        }

        /// <summary>
        /// Shows or hides the labels of all aircraft, saves the choice in the settings shared with the phone, and reports it in a car toast.
        /// </summary>
        private void ToggleLabels()
        {
            bool show = !AppSettings.ShowLabels;
            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
            {
                { AppSettings.ShowLabelsSetting, show ? 1.0 : 0.0 },
            };

            AppSettings.Save(values);
            CarToast.MakeText(CarContext, show ? "Etykiety samolotów: widoczne" : "Etykiety samolotów: ukryte", CarToast.LengthShort)?.Show();
            RefreshTemplate();
        }

        /// <summary>
        /// Turns the isolation of the selected aircraft on or off and shows a hint in a car toast when nothing is selected.
        /// </summary>
        private void ToggleIsolation()
        {
            if (!controller.TryToggleIsolation(out bool isolated))
            {
                CarToast.MakeText(CarContext, "Najpierw zaznacz samolot: dotknij go na mapie lub wyszukaj", CarToast.LengthShort)?.Show();
            }
        }

        /// <summary>
        /// Rebuilds the template on the main thread so that the buttons reflect the current modes.
        /// </summary>
        private void RefreshTemplate()
        {
            handler.Post(Invalidate);
        }

        /// <summary>
        /// Creates an Android Auto icon from a drawable resource, tinted blue when the mode it represents is on.
        /// </summary>
        /// <param name="resourceId">Identifier of the drawable resource.</param>
        /// <param name="active">Whether the icon is tinted to show an active mode.</param>
        /// <returns>Icon to use in an action.</returns>
        private CarIcon CreateIcon(int resourceId, bool active = false)
        {
            IconCompat iconCompat = IconCompat.CreateWithResource(CarContext, resourceId)!;
            CarIcon.Builder builder = new CarIcon.Builder(iconCompat);

            if (active)
            {
                builder.SetTint(CarColor.Blue);
            }

            return builder.Build();
        }
    }

    /// <summary>
    /// Android Auto surface callback that forwards the events to the map controller.
    /// </summary>
    public class MapSurfaceCallback : Java.Lang.Object, ISurfaceCallback
    {
        /// <summary>
        /// Controller of the map state and drawing.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Creates a callback bound to the map controller.
        /// </summary>
        /// <param name="controller">Map controller.</param>
        public MapSurfaceCallback(MapController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// Starts drawing when the surface becomes available.
        /// </summary>
        /// <param name="surfaceContainer">Container with the surface and its parameters.</param>
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
        /// Stops drawing when the surface is no longer available.
        /// </summary>
        /// <param name="surfaceContainer">Container with the surface being destroyed.</param>
        public void OnSurfaceDestroyed(SurfaceContainer surfaceContainer)
        {
            Android.Util.Log.Info("AdsbCarMap", "OnSurfaceDestroyed");
            controller.Stop();
        }

        /// <summary>
        /// Handles a change of the visible area. Ignored in the PoC.
        /// </summary>
        /// <param name="visibleArea">Visible area of the surface.</param>
        public void OnVisibleAreaChanged(Rect visibleArea)
        {
        }

        /// <summary>
        /// Handles a change of the stable area. Ignored in the PoC.
        /// </summary>
        /// <param name="stableArea">Area not covered by the interface elements.</param>
        public void OnStableAreaChanged(Rect stableArea)
        {
        }

        /// <summary>
        /// Moves the map according to the scroll gesture.
        /// </summary>
        /// <param name="distanceX">Horizontal movement since the previous call, in pixels.</param>
        /// <param name="distanceY">Vertical movement since the previous call, in pixels.</param>
        public void OnScroll(float distanceX, float distanceY)
        {
            controller.Pan(distanceX, distanceY);
        }

        /// <summary>
        /// Starts the fling movement of the map after a fling gesture.
        /// </summary>
        /// <param name="velocityX">Horizontal velocity.</param>
        /// <param name="velocityY">Vertical velocity.</param>
        public void OnFling(float velocityX, float velocityY)
        {
            controller.Fling(velocityX, velocityY);
        }

        /// <summary>
        /// Changes the zoom according to the scale gesture.
        /// </summary>
        /// <param name="focusX">Horizontal focus point.</param>
        /// <param name="focusY">Vertical focus point.</param>
        /// <param name="scaleFactor">Scale factor relative to the previous call.</param>
        public void OnScale(float focusX, float focusY, float scaleFactor)
        {
            controller.Scale(scaleFactor);
        }

        /// <summary>
        /// Handles a tap on the surface by selecting the aircraft nearest to the tap.
        /// </summary>
        /// <param name="x">Horizontal tap position.</param>
        /// <param name="y">Vertical tap position.</param>
        public void OnClick(float x, float y)
        {
            controller.Tap(x, y);
        }
    }

    /// <summary>
    /// Adapter forwarding the Android Auto button clicks to a C# delegate.
    /// </summary>
    public class DelegateClickListener : Java.Lang.Object, IOnClickListener
    {
        /// <summary>
        /// Delegate invoked on click.
        /// </summary>
        private readonly System.Action callback;

        /// <summary>
        /// Creates a listener that invokes the given delegate.
        /// </summary>
        /// <param name="callback">Delegate invoked on click.</param>
        public DelegateClickListener(System.Action callback)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Invokes the delegate when the button is clicked.
        /// </summary>
        public void OnClick()
        {
            callback();
        }
    }

    /// <summary>
    /// Receives the result of the location permission request and starts the position updates after they are granted.
    /// </summary>
    public class PermissionResultListener : Java.Lang.Object, IOnRequestPermissionsListener
    {
        /// <summary>
        /// Map controller that is to start the position updates.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Creates a listener bound to the map controller.
        /// </summary>
        /// <param name="controller">Map controller.</param>
        public PermissionResultListener(MapController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// Starts the position updates if any of the location permissions was granted.
        /// </summary>
        /// <param name="grantedPermissions">Granted permissions.</param>
        /// <param name="rejectedPermissions">Rejected permissions.</param>
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
}

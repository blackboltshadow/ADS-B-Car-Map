using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Start activity on the phone with the aircraft map, zoom and center buttons, and a menu leading to the search and the settings. It also asks for the location and notification permissions.
    /// </summary>
    [Activity(
        Label = "ADS-B Car Map",
        MainLauncher = true,
        Exported = true,
        Theme = "@android:style/Theme.Material.NoActionBar",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout)]
    public class MainActivity : Activity
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Request code for the location and notification permissions.
        /// </summary>
        private const int PermissionRequestCode = 1;

        /// <summary>
        /// Identifier of the menu item that opens the search.
        /// </summary>
        private const int MenuSearchId = 1;

        /// <summary>
        /// Identifier of the menu item that opens the settings.
        /// </summary>
        private const int MenuSettingsId = 2;

        /// <summary>
        /// Controller of the map drawn on the phone screen.
        /// </summary>
        private readonly MapController controller = new MapController(AppServices.Adsb, AppServices.Tiles, AppServices.Routes, AppServices.Selection);

        /// <summary>
        /// Button that isolates the selected aircraft.
        /// </summary>
        private TextView? isolateButton;

        /// <summary>
        /// Button of the multi-select mode.
        /// </summary>
        private TextView? multiSelectButton;

        /// <summary>
        /// Button that shows and hides the aircraft description labels.
        /// </summary>
        private ImageButton? labelsButton;

        /// <summary>
        /// Button of the filter that shows only military aircraft.
        /// </summary>
        private ImageButton? militaryButton;

        /// <summary>
        /// Card of the primary selected aircraft.
        /// </summary>
        private AircraftCardView? aircraftCard;

        /// <summary>
        /// Map view filling the screen.
        /// </summary>
        private PhoneMapView? mapView;

        /// <summary>
        /// Layout that contains the map view as its first child.
        /// </summary>
        private FrameLayout? contentRoot;

        /// <summary>
        /// Main thread handler used to refresh the card periodically.
        /// </summary>
        private readonly Handler handler = new Handler(Looper.MainLooper!);

        /// <summary>
        /// Periodic card refresh task, null when the activity is not visible.
        /// </summary>
        private Java.Lang.Runnable? cardRefreshRunnable;

        /// <summary>
        /// Aircraft card refresh interval in milliseconds.
        /// </summary>
        private const int CardRefreshIntervalMilliseconds = 1000;

        /// <summary>
        /// Part of the screen width taken by the aircraft card in landscape, so that it does not cover the buttons on the right.
        /// </summary>
        private const float LandscapeCardWidthFraction = 0.55f;

        /// <summary>
        /// Size of the selection counter relative to the "M" letter.
        /// </summary>
        private const float CounterRelativeSize = 0.65f;

        /// <summary>
        /// Number of selected aircraft from which the counter is shown next to the "M" letter.
        /// </summary>
        private const int MinimumCounterCount = 2;

        /// <summary>
        /// Builds the screen with the map and buttons, and asks for the location and notification permissions if they are missing.
        /// </summary>
        /// <param name="savedInstanceState">Saved activity state.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Log.Info(LogTag, "MainActivity.OnCreate");

            controller.SelectionChanged += () => RunOnUiThread(OnSelectionChanged);
            SetContentView(BuildContent());

            List<string> missingPermissions = new List<string>();

            if (!LocationTracker.HasPermission())
            {
                missingPermissions.Add(Android.Manifest.Permission.AccessFineLocation);
                missingPermissions.Add(Android.Manifest.Permission.AccessCoarseLocation);
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                missingPermissions.Add(Android.Manifest.Permission.PostNotifications);
            }

            if (missingPermissions.Count > 0)
            {
                RequestPermissions(missingPermissions.ToArray(), PermissionRequestCode);
            }
        }

        /// <summary>
        /// Rebuilds the layout after a screen rotation without recreating the activity, so the map keeps its zoom, center and selection.
        /// </summary>
        /// <param name="newConfig">New screen configuration.</param>
        public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);

            SetContentView(BuildContent());
            UpdateCard();
        }

        /// <summary>
        /// Starts the periodic refresh of the aircraft card.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            controller.SurfaceBroken -= OnSurfaceBroken;
            controller.SurfaceBroken += OnSurfaceBroken;
            UpdateModeButtons();
            cardRefreshRunnable = new Java.Lang.Runnable(CardRefreshTick);
            handler.Post(cardRefreshRunnable);
        }

        /// <summary>
        /// Stops the periodic refresh of the aircraft card.
        /// </summary>
        protected override void OnPause()
        {
            controller.SurfaceBroken -= OnSurfaceBroken;

            if (cardRefreshRunnable != null)
            {
                handler.RemoveCallbacks(cardRefreshRunnable);
                cardRefreshRunnable = null;
            }

            base.OnPause();
        }

        /// <summary>
        /// Replaces the map view with a new one when its surface got stuck in the "already locked" state, for example after the screen was covered by the settings while a frame was being drawn. A new view has a new surface, so drawing works again.
        /// </summary>
        private void OnSurfaceBroken()
        {
            RunOnUiThread(() =>
            {
                if (contentRoot == null || mapView == null)
                {
                    return;
                }

                Log.Warn(LogTag, "Powierzchnia mapy zablokowana, tworzę widok mapy od nowa.");
                contentRoot.RemoveView(mapView);
                mapView = new PhoneMapView(this, controller);
                contentRoot.AddView(mapView, 0);
            });
        }

        /// <summary>
        /// Refreshes the card and schedules the next refresh while the activity is visible.
        /// </summary>
        private void CardRefreshTick()
        {
            UpdateCard();

            if (cardRefreshRunnable != null)
            {
                handler.PostDelayed(cardRefreshRunnable, CardRefreshIntervalMilliseconds);
            }
        }

        /// <summary>
        /// Shows the card of the primary selected aircraft with current data, or hides it when nothing is selected.
        /// </summary>
        private void UpdateCard()
        {
            if (aircraftCard == null)
            {
                return;
            }

            SelectedAircraftInfo? info = controller.GetPrimaryInfo();

            if (info == null || controller.IsMultiSelect)
            {
                aircraftCard.Visibility = ViewStates.Gone;
                UpdateCoveredArea();

                return;
            }

            string? callsign = info.Aircraft?.Callsign;
            FlightRoute? route = AppServices.Routes.Get(callsign);

            AppServices.Routes.Prioritize(callsign);

            aircraftCard.Bind(info, route, AppServices.Routes.GetState(callsign));
            aircraftCard.Visibility = ViewStates.Visible;
        }

        /// <summary>
        /// Tells the map which part of it is covered by the visible aircraft card, so that aircraft under the card cannot be tapped.
        /// </summary>
        private void UpdateCoveredArea()
        {
            if (aircraftCard == null || mapView == null || aircraftCard.Visibility != ViewStates.Visible || aircraftCard.Width == 0)
            {
                controller.SetCoveredArea(null);

                return;
            }

            controller.SetCoveredArea(new Android.Graphics.RectF(
                aircraftCard.Left - mapView.Left,
                aircraftCard.Top - mapView.Top,
                aircraftCard.Right - mapView.Left,
                aircraftCard.Bottom - mapView.Top));
        }

        /// <summary>
        /// Forces the route download for the primary selected aircraft and refreshes the card immediately so that it shows the "loading" state.
        /// </summary>
        private void OnRefreshRouteClicked()
        {
            AppServices.Routes.Refresh(controller.GetPrimaryInfo()?.Aircraft?.Callsign);
            UpdateCard();
        }

        /// <summary>
        /// Refreshes the mode buttons and the card after the selection changes.
        /// </summary>
        private void OnSelectionChanged()
        {
            UpdateModeButtons();
            UpdateCard();
        }

        /// <summary>
        /// Starts the location updates after the location permission is granted.
        /// </summary>
        /// <param name="requestCode">Request code.</param>
        /// <param name="permissions">Requested permissions.</param>
        /// <param name="grantResults">Request results.</param>
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == PermissionRequestCode && LocationTracker.HasPermission())
            {
                controller.StartLocationUpdates();
            }
        }

        /// <summary>
        /// Creates the screen view: the map on the whole surface, the menu in the top right corner, and the zoom and center buttons in the bottom right.
        /// </summary>
        /// <returns>Main view of the activity.</returns>
        private View BuildContent()
        {
            FrameLayout root = new FrameLayout(this);
            root.SetFitsSystemWindows(true);
            contentRoot = root;
            mapView = new PhoneMapView(this, controller);
            root.AddView(mapView);

            ImageButton menuButton = CreateRoundButton(Resource.Drawable.ic_menu, "Menu");
            menuButton.Click += (sender, args) => ShowMenu(menuButton);

            FrameLayout.LayoutParams menuParams = new FrameLayout.LayoutParams(Dp(48), Dp(48), GravityFlags.Top | GravityFlags.End);
            menuParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(12));
            root.AddView(menuButton, menuParams);

            isolateButton = CreateTextButton("I", "Izoluj zaznaczone samoloty");
            isolateButton.Click += (sender, args) => OnIsolateClicked();

            multiSelectButton = CreateTextButton("M", "Zaznaczanie wielu samolotów");
            multiSelectButton.Click += (sender, args) => OnMultiSelectClicked();

            bool landscape = Resources?.Configuration?.Orientation == Android.Content.Res.Orientation.Landscape;

            LinearLayout modeColumn = new LinearLayout(this);
            modeColumn.Orientation = landscape ? Orientation.Horizontal : Orientation.Vertical;

            labelsButton = CreateRoundButton(Resource.Drawable.ic_labels, "Pokaż lub ukryj etykiety samolotów");
            labelsButton.Click += (sender, args) => OnLabelsClicked();

            militaryButton = CreateRoundButton(Resource.Drawable.ic_fighter, "Tylko samoloty wojskowe");
            militaryButton.Click += (sender, args) => OnMilitaryClicked();

            foreach (View button in new View[] { isolateButton, multiSelectButton, labelsButton, militaryButton })
            {
                LinearLayout.LayoutParams buttonParams = new LinearLayout.LayoutParams(Dp(48), Dp(48));

                if (landscape)
                {
                    buttonParams.SetMargins(Dp(6), 0, Dp(6), 0);
                }
                else
                {
                    buttonParams.SetMargins(0, Dp(6), 0, Dp(6));
                }

                modeColumn.AddView(button, buttonParams);
            }

            FrameLayout.LayoutParams modeParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.End);

            if (landscape)
            {
                modeParams.SetMargins(Dp(12), Dp(12), Dp(72), Dp(12));
            }
            else
            {
                modeParams.SetMargins(Dp(12), Dp(66), Dp(12), Dp(12));
            }

            root.AddView(modeColumn, modeParams);

            aircraftCard = new AircraftCardView(this, landscape ? 1.0f : 1.25f);
            aircraftCard.Visibility = ViewStates.Gone;
            aircraftCard.FollowClicked += () => controller.FollowPrimary();
            aircraftCard.CloseClicked += () => controller.ClearSelection();
            aircraftCard.RefreshRouteClicked += OnRefreshRouteClicked;
            aircraftCard.LayoutChange += (sender, args) => UpdateCoveredArea();

            int cardWidth = landscape
                ? (int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * LandscapeCardWidthFraction)
                : ViewGroup.LayoutParams.MatchParent;

            FrameLayout.LayoutParams cardParams = new FrameLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.Start);
            cardParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
            root.AddView(aircraftCard, cardParams);

            UpdateModeButtons();

            ImageButton zoomInButton = CreateRoundButton(Resource.Drawable.ic_zoom_in, "Przybliż");
            zoomInButton.Click += (sender, args) => controller.ZoomIn();

            ImageButton zoomOutButton = CreateRoundButton(Resource.Drawable.ic_zoom_out, "Oddal");
            zoomOutButton.Click += (sender, args) => controller.ZoomOut();

            ImageButton centerButton = CreateRoundButton(Resource.Drawable.ic_my_location, "Centruj na mojej pozycji");
            centerButton.Click += (sender, args) => controller.FollowUser();

            LinearLayout buttonColumn = new LinearLayout(this);
            buttonColumn.Orientation = Orientation.Vertical;

            foreach (ImageButton button in new ImageButton[] { zoomInButton, zoomOutButton, centerButton })
            {
                LinearLayout.LayoutParams buttonParams = new LinearLayout.LayoutParams(Dp(48), Dp(48));
                buttonParams.SetMargins(0, Dp(6), 0, Dp(6));
                buttonColumn.AddView(button, buttonParams);
            }

            FrameLayout.LayoutParams columnParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.End);
            columnParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
            root.AddView(buttonColumn, columnParams);

            aircraftCard.BringToFront();

            return root;
        }

        /// <summary>
        /// Creates a round button with an icon, overlaid on the map.
        /// </summary>
        /// <param name="iconResourceId">Identifier of the drawable resource with the icon.</param>
        /// <param name="description">Button description for screen readers.</param>
        /// <returns>Button ready to be added to the layout.</returns>
        private ImageButton CreateRoundButton(int iconResourceId, string description)
        {
            ImageButton button = new ImageButton(this);
            button.SetBackgroundResource(Resource.Drawable.bg_map_button);
            button.SetImageResource(iconResourceId);
            button.SetScaleType(ImageView.ScaleType.Center);
            button.ContentDescription = description;

            return button;
        }

        /// <summary>
        /// Creates a round button with a letter, overlaid on the map.
        /// </summary>
        /// <param name="text">Letter on the button.</param>
        /// <param name="description">Button description for screen readers.</param>
        /// <returns>Button ready to be added to the layout.</returns>
        private TextView CreateTextButton(string text, string description)
        {
            TextView button = new TextView(this);
            button.Text = text;
            button.TextSize = 18f;
            button.SetTypeface(null, TypefaceStyle.Bold);
            button.SetTextColor(Color.White);
            button.Gravity = GravityFlags.Center;
            button.SetBackgroundResource(Resource.Drawable.bg_map_button);
            button.Clickable = true;
            button.ContentDescription = description;

            return button;
        }

        /// <summary>
        /// Turns the isolation of the selected aircraft on or off, and shows a hint when nothing is selected.
        /// </summary>
        private void OnIsolateClicked()
        {
            if (!controller.TryToggleIsolation(out bool isolated))
            {
                Toast.MakeText(this, "Najpierw zaznacz samolot, dotykając go na mapie", ToastLength.Short)?.Show();
            }
        }

        /// <summary>
        /// Turns the multi-select mode on or off.
        /// </summary>
        private void OnMultiSelectClicked()
        {
            bool enabled = controller.ToggleMultiSelect();
            string message = enabled
                ? "Zaznaczanie wielu samolotów: dotykaj samolotów, aby je dodać lub odjąć"
                : "Zaznaczanie wielu samolotów wyłączone";

            Toast.MakeText(this, message, ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Toggles the visibility of the aircraft description labels and saves the choice in the settings.
        /// </summary>
        private void OnLabelsClicked()
        {
            bool show = !AppSettings.ShowLabels;
            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
            {
                { AppSettings.ShowLabelsSetting, show ? 1.0 : 0.0 },
            };

            AppSettings.Save(values);
            UpdateModeButtons();
            Toast.MakeText(this, show ? "Etykiety samolotów: widoczne" : "Etykiety samolotów: ukryte", ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Turns the military aircraft filter on or off and reports how many of them are in the latest data.
        /// </summary>
        private void OnMilitaryClicked()
        {
            bool enabled = controller.ToggleMilitaryOnly(out int militaryCount);
            string message = !enabled
                ? "Wszystkie samoloty"
                : militaryCount > 0
                    ? string.Format(CultureInfo.InvariantCulture, "Tylko wojskowe: {0} w zasięgu", militaryCount)
                    : "Tylko wojskowe: żadnego w zasięgu";

            Toast.MakeText(this, message, ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Highlights the "I", "M", labels and military filter buttons when their modes are on, and shows the number of selected aircraft next to "M".
        /// </summary>
        private void UpdateModeButtons()
        {
            militaryButton?.SetBackgroundResource(controller.IsMilitaryOnly ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
            isolateButton?.SetBackgroundResource(controller.IsIsolated ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
            multiSelectButton?.SetBackgroundResource(controller.IsMultiSelect ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
            labelsButton?.SetBackgroundResource(AppSettings.ShowLabels ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);

            UpdateSelectionCounter();
        }

        /// <summary>
        /// Shows the number of selected aircraft in parentheses, slightly lowered and smaller, next to the "M" letter once at least two are selected, or just the letter otherwise.
        /// </summary>
        private void UpdateSelectionCounter()
        {
            if (multiSelectButton == null)
            {
                return;
            }

            int count = controller.SelectedCount;
            bool showCounter = count >= MinimumCounterCount;
            string text = showCounter ? "M(" + count.ToString(CultureInfo.InvariantCulture) + ")" : "M";
            SpannableString spannable = new SpannableString(text);

            if (showCounter)
            {
                spannable.SetSpan(new RelativeSizeSpan(CounterRelativeSize), 1, text.Length, SpanTypes.ExclusiveExclusive);
                spannable.SetSpan(new SubscriptSpan(), 1, text.Length, SpanTypes.ExclusiveExclusive);
            }

            multiSelectButton.SetText(spannable, TextView.BufferType.Normal);
            multiSelectButton.ContentDescription = count > 0
                ? string.Format(CultureInfo.InvariantCulture, "Zaznaczanie wielu samolotów, zaznaczono: {0}", count)
                : "Zaznaczanie wielu samolotów";
        }

        /// <summary>
        /// Shows the menu with the search and the settings under the menu button.
        /// </summary>
        /// <param name="anchor">Button under which the menu appears.</param>
        private void ShowMenu(View anchor)
        {
            PopupMenu popup = new PopupMenu(this, anchor);
            popup.Menu?.Add(0, MenuSearchId, 0, "Szukaj samolotu");
            popup.Menu?.Add(0, MenuSettingsId, 1, "Ustawienia");
            popup.MenuItemClick += (sender, args) =>
            {
                args.Handled = true;

                if (args.Item?.ItemId == MenuSearchId)
                {
                    StartActivity(new Intent(this, typeof(SearchActivity)));
                }
                else if (args.Item?.ItemId == MenuSettingsId)
                {
                    StartActivity(new Intent(this, typeof(SettingsActivity)));
                }
            };
            popup.Show();
        }

        /// <summary>
        /// Converts dp to pixels of the phone screen.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

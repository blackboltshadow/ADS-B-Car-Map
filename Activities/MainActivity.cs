using AdsbCarMap.Localization;
using AdsbCarMap.DataSources.Models;
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
        MainLauncher = true,
        Exported = true,
        Theme = "@style/AppTheme.Main",
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
        /// Identifier of the menu item that opens the information about the application.
        /// </summary>
        private const int MenuAboutId = 3;

        /// <summary>
        /// Identifier of the menu item that opens the favorite airports.
        /// </summary>
        private const int MenuFavoritesId = 4;

        /// <summary>
        /// Identifier of the menu item that opens the aircraft filters.
        /// </summary>
        private const int MenuFiltersId = 5;

        /// <summary>
        /// Identifier of the menu item that opens the developer log.
        /// </summary>
        private const int MenuDevLogId = 6;

        /// <summary>
        /// Identifier of the menu item that opens the alerts.
        /// </summary>
        private const int MenuAlertsId = 7;

        /// <summary>
        /// Identifier of the menu item that opens the official notices.
        /// </summary>
        private const int MenuWarningsId = 8;

        /// <summary>
        /// Identifier of the menu item that opens the history of the aircraft.
        /// </summary>
        private const int MenuAircraftHistoryId = 9;

        /// <summary>
        /// Diameter of the badge with the number of filters in dp.
        /// </summary>
        private const int BadgeSizeDp = 20;

        /// <summary>
        /// Controller of the map drawn on the phone screen.
        /// </summary>
        private readonly MapController controller = new MapController(AppServices.Adsb, AppServices.Tiles, AppServices.Routes, AppServices.Selection, AppServices.Enricher, AppServices.FlightKeeper, AppServices.FlightTracks);

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
        /// Button in the top left corner that shows or hides the developer console. Visible only while the developer console setting is on.
        /// </summary>
        private ImageButton? devConsoleButton;

        /// <summary>
        /// Developer console drawn over the map. A long press copies its text.
        /// </summary>
        private TextView? devConsoleText;

        /// <summary>
        /// Button that shows or hides the layer with the airborne threats.
        /// </summary>
        private ImageButton? threatsButton;

        /// <summary>
        /// Text with the link to the source of the threats, the state of the feed and the warning. Visible only while the layer is on.
        /// </summary>
        private TextView? threatsInfo;

        /// <summary>
        /// Button that shows or hides the layer with the airspace areas. A long press opens the choice of what the layer shows.
        /// </summary>
        private ImageButton? airspaceButton;

        /// <summary>
        /// Text with the source of the airspace areas and the state of the feed. Visible only while the layer is on.
        /// </summary>
        private TextView? airspaceInfo;

        /// <summary>
        /// Button that opens the list of aircraft filters. It is highlighted while any filter is on.
        /// </summary>
        private ImageButton? filtersButton;

        /// <summary>
        /// Badge on the filters button with the number of filters that are on. Hidden when none is on.
        /// </summary>
        private TextView? filtersBadge;

        /// <summary>
        /// Card of the primary selected aircraft.
        /// </summary>
        private AircraftCardView? aircraftCard;

        /// <summary>
        /// Card of the selected airport, shown instead of the aircraft card.
        /// </summary>
        private AirportCardView? airportCard;

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
        /// Language the screen was built with; the screen is recreated when the setting changes.
        /// </summary>
        private AppLanguage shownLanguage;

        /// <summary>
        /// Builds the screen with the map and buttons, and asks for the location and notification permissions if they are missing.
        /// </summary>
        /// <param name="savedInstanceState">Saved activity state.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            shownLanguage = Loc.Language;
            DevLogger.InstallGlobalHandlers();
            Log.Info(LogTag, "MainActivity.OnCreate");
            AlertBackgroundRunner.Initialize();

            controller.SelectionChanged += () => RunOnUiThread(OnSelectionChanged);
            controller.ThreatsAllowed = true;
            controller.ThreatTapped += OnThreatTapped;
            controller.AirspacesAllowed = true;
            controller.AirspaceTapped += OnAirspaceTapped;
            SetContentView(BuildContent());
            HandleAlertIntent(Intent);

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
        /// Handles a tap on an alert notification when the activity is already running.
        /// </summary>
        /// <param name="intent">New intent.</param>
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleAlertIntent(intent);
        }

        /// <summary>
        /// Asks the map to follow the aircraft from an alert notification.
        /// </summary>
        /// <param name="intent">Intent that started or resumed the activity.</param>
        private static void HandleAlertIntent(Intent? intent)
        {
            string[]? hexes = intent?.GetStringArrayExtra(AlertMonitor.FocusHexesExtra);
            string? hex = intent?.GetStringExtra(AlertMonitor.FocusHexExtra);

            intent?.RemoveExtra(AlertMonitor.FocusHexExtra);
            intent?.RemoveExtra(AlertMonitor.FocusHexesExtra);

            if (hexes != null && hexes.Length > 1)
            {
                LiveTraffic.RequestGroupFocus(hexes);
            }
            else if (!string.IsNullOrEmpty(hex))
            {
                LiveTraffic.RequestFocus(hex);
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

            if (shownLanguage != Loc.Language)
            {
                Recreate();

                return;
            }

            controller.SurfaceBroken -= OnSurfaceBroken;
            controller.SurfaceBroken += OnSurfaceBroken;
            AirportWeatherLoader.Updated -= OnAirportWeatherUpdated;
            AirportWeatherLoader.Updated += OnAirportWeatherUpdated;
            AppServices.Threats.Changed -= OnThreatFeedChanged;
            AppServices.Threats.Changed += OnThreatFeedChanged;
            ApplyThreatFeed();
            AppServices.Airspace.Changed -= OnAirspaceFeedChanged;
            AppServices.Airspace.Changed += OnAirspaceFeedChanged;
            AppServices.AirspaceConfig.Changed -= OnAirspaceFeedChanged;
            AppServices.AirspaceConfig.Changed += OnAirspaceFeedChanged;
            ApplyAirspaceFeed();
            ApplyWarningFeed();
            UpdateModeButtons();
            UpdateDevConsole();
            cardRefreshRunnable = new Java.Lang.Runnable(CardRefreshTick);
            handler.Post(cardRefreshRunnable);
        }

        /// <summary>
        /// Stops the periodic refresh of the aircraft card.
        /// </summary>
        protected override void OnPause()
        {
            controller.SurfaceBroken -= OnSurfaceBroken;
            AirportWeatherLoader.Updated -= OnAirportWeatherUpdated;
            AppServices.Threats.Changed -= OnThreatFeedChanged;
            AppServices.Threats.Stop();
            AppServices.Airspace.Changed -= OnAirspaceFeedChanged;
            AppServices.AirspaceConfig.Changed -= OnAirspaceFeedChanged;
            AppServices.Airspace.Stop();
            AppServices.Warnings.Stop();

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
            UpdateDevConsole();

            if (cardRefreshRunnable != null)
            {
                handler.PostDelayed(cardRefreshRunnable, CardRefreshIntervalMilliseconds);
            }
        }

        /// <summary>
        /// Shows or hides the developer console button according to the setting and, while the console is open, refreshes its text. The setting off also closes the console.
        /// </summary>
        private void UpdateDevConsole()
        {
            if (devConsoleButton == null || devConsoleText == null)
            {
                return;
            }

            if (!AppSettings.ShowDevConsole)
            {
                devConsoleButton.Visibility = ViewStates.Gone;
                devConsoleText.Visibility = ViewStates.Gone;

                return;
            }

            devConsoleButton.Visibility = ViewStates.Visible;

            if (devConsoleText.Visibility == ViewStates.Visible)
            {
                devConsoleText.Text = DevConsoleText.Format(controller.GetDevSnapshot(), DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Opens or closes the developer console.
        /// </summary>
        private void OnDevConsoleClicked()
        {
            if (devConsoleText == null)
            {
                return;
            }

            devConsoleText.Visibility = devConsoleText.Visibility == ViewStates.Visible ? ViewStates.Gone : ViewStates.Visible;
            UpdateDevConsole();
        }

        /// <summary>
        /// Copies the text of the developer console to the clipboard.
        /// </summary>
        private void CopyDevConsole()
        {
            Android.Content.ClipboardManager? clipboard = GetSystemService(ClipboardService) as Android.Content.ClipboardManager;

            if (clipboard == null || devConsoleText == null)
            {
                return;
            }

            clipboard.PrimaryClip = ClipData.NewPlainText(Loc.T("Konsola dev"), devConsoleText.Text);
            Toast.MakeText(this, Loc.T("Skopiowano konsolę dev"), ToastLength.Short)?.Show();
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

            Airport? airport = controller.GetSelectedAirport();

            if (airport != null && airportCard != null)
            {
                (bool Known, double Latitude, double Longitude) user = controller.GetUserLocation();

                aircraftCard.Visibility = ViewStates.Gone;
                airportCard.Bind(airport, AppServices.Airports.GetRunways(airport), AppServices.Airports.GetFrequencies(airport), user.Known, user.Latitude, user.Longitude, AppServices.Favorites.Contains(airport.Icao));
                UpdateAirportWeather(airport);
                airportCard.Visibility = ViewStates.Visible;
                UpdateCoveredArea();

                return;
            }

            if (airportCard != null)
            {
                airportCard.ResetSize();
                airportCard.Visibility = ViewStates.Gone;
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
            aircraftCard.SetWatched(AppServices.Alerts.Rules.Any((AlertRule rule) => rule.WatchedHex == info.Hex.ToLowerInvariant()));
            aircraftCard.Visibility = ViewStates.Visible;
        }

        /// <summary>
        /// Tells the map which part of it is covered by the visible aircraft card, so that aircraft under the card cannot be tapped.
        /// </summary>
        private void UpdateCoveredArea()
        {
            View? card = airportCard != null && airportCard.Visibility == ViewStates.Visible ? airportCard : aircraftCard;
            bool frozen = airportCard != null && airportCard.Visibility == ViewStates.Visible && airportCard.IsMaximized;

            if (card == null || mapView == null || card.Visibility != ViewStates.Visible || card.Width == 0)
            {
                controller.SetCoveredArea(null);
                controller.SetFramingFrozen(frozen);

                return;
            }

            controller.SetCoveredArea(new Android.Graphics.RectF(
                card.Left - mapView.Left,
                card.Top - mapView.Top,
                card.Right - mapView.Left,
                card.Bottom - mapView.Top));
            controller.SetFramingFrozen(frozen);
        }

        /// <summary>
        /// Creates the alert that watches the primary selected aircraft, or removes it when it already exists.
        /// </summary>
        private void OnWatchClicked()
        {
            SelectedAircraftInfo? info = controller.GetPrimaryInfo();

            if (info == null)
            {
                return;
            }

            string hex = info.Hex.ToLowerInvariant();
            List<AlertRule> existing = AppServices.Alerts.Rules.Where((AlertRule rule) => rule.WatchedHex == hex).ToList();

            if (existing.Count > 0)
            {
                foreach (AlertRule rule in existing)
                {
                    AppServices.Alerts.Remove(rule.Id);
                }

                Toast.MakeText(this, Loc.T("Alert obserwowania usunięty"), ToastLength.Short)?.Show();
            }
            else
            {
                string name = info.Aircraft == null ? hex.ToUpperInvariant() : AircraftSearch.GetName(info.Aircraft);

                AppServices.Alerts.Save(AlertBundle.CreateWatchRule(AlertRuleStore.CreateId(), Loc.T("Obserwuj: ") + name, hex));
                AircraftHistoryStore.MarkWatched(hex);
                Toast.MakeText(this, Loc.T("Alert obserwowania dodany (Alerty → edytuj, by zmienić obszar i zdarzenia)"), ToastLength.Long)?.Show();
            }

            UpdateCard();
        }

        /// <summary>
        /// Shows the stored weather and forecast of the selected airport and starts the downloads that are needed. The weather and the forecast are not touched while they are switched off in the settings.
        /// </summary>
        /// <param name="airport">Selected airport.</param>
        private void UpdateAirportWeather(Airport airport)
        {
            if (airportCard == null)
            {
                return;
            }

            AirportWeatherLoader.Snapshot snapshot = AirportWeatherLoader.Request(airport);

            airportCard.BindWeather(snapshot.Weather, snapshot.WeatherLoading);
            airportCard.BindForecast(snapshot.Forecast, snapshot.ForecastLoading);
        }

        /// <summary>
        /// Refreshes the card after a download of the weather or the forecast has ended.
        /// </summary>
        private void OnAirportWeatherUpdated()
        {
            RunOnUiThread(UpdateCard);
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
                AlertBackgroundRunner.Refresh();
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

            ImageButton menuButton = CreateRoundButton(Resource.Drawable.ic_menu, Loc.T("Menu"));
            menuButton.Click += (sender, args) => ShowMenu(menuButton);

            FrameLayout.LayoutParams menuParams = new FrameLayout.LayoutParams(Dp(48), Dp(48), GravityFlags.Top | GravityFlags.End);
            menuParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(12));
            root.AddView(menuButton, menuParams);

            isolateButton = CreateTextButton("I", Loc.T("Izoluj zaznaczone samoloty"));
            isolateButton.Click += (sender, args) => OnIsolateClicked();

            multiSelectButton = CreateTextButton("M", Loc.T("Zaznaczanie wielu samolotów"));
            multiSelectButton.Click += (sender, args) => OnMultiSelectClicked();

            bool landscape = Resources?.Configuration?.Orientation == Android.Content.Res.Orientation.Landscape;

            LinearLayout modeColumn = new LinearLayout(this);
            modeColumn.Orientation = landscape ? Orientation.Horizontal : Orientation.Vertical;

            labelsButton = CreateRoundButton(Resource.Drawable.ic_labels, Loc.T("Pokaż lub ukryj etykiety samolotów"));
            labelsButton.Click += (sender, args) => OnLabelsClicked();

            militaryButton = CreateRoundButton(Resource.Drawable.ic_fighter, Loc.T("Tylko samoloty wojskowe"));
            militaryButton.Click += (sender, args) => OnMilitaryClicked();

            threatsButton = CreateRoundButton(Resource.Drawable.ic_threat, Loc.T("Warstwa zagrożeń (NEPTUN)"));
            threatsButton.Click += (sender, args) => OnThreatsClicked();

            airspaceButton = CreateRoundButton(Resource.Drawable.ic_airspace, Loc.T("Warstwa stref przestrzeni powietrznej (PAŻP)"));
            airspaceButton.Click += (sender, args) => OnAirspaceClicked();
            airspaceButton.LongClick += (sender, args) => StartActivity(new Intent(this, typeof(AirspaceActivity)));

            filtersButton = CreateRoundButton(Resource.Drawable.ic_filter, Loc.T("Filtry samolotów"));
            filtersButton.Click += (sender, args) => StartActivity(new Intent(this, typeof(FiltersActivity)));

            filtersBadge = CreateBadge();

            FrameLayout filtersHost = new FrameLayout(this);
            filtersHost.AddView(filtersButton, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            filtersHost.AddView(filtersBadge, new FrameLayout.LayoutParams(Dp(BadgeSizeDp), Dp(BadgeSizeDp), GravityFlags.Top | GravityFlags.End));

            foreach (View button in new View[] { isolateButton, multiSelectButton, labelsButton, militaryButton, threatsButton, airspaceButton, filtersHost })
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

            threatsInfo = new TextView(this);
            threatsInfo.TextSize = 11f;
            threatsInfo.SetTextColor(Color.White);
            threatsInfo.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            threatsInfo.SetBackgroundColor(Color.Argb(150, 0, 0, 0));
            threatsInfo.SetMaxWidth((int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * 0.55f));
            threatsInfo.Clickable = true;
            threatsInfo.Visibility = ViewStates.Gone;
            threatsInfo.Click += (sender, args) => OpenThreatSource();

            airspaceInfo = new TextView(this);
            airspaceInfo.TextSize = 11f;
            airspaceInfo.SetTextColor(Color.White);
            airspaceInfo.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            airspaceInfo.SetBackgroundColor(Color.Argb(150, 0, 0, 0));
            airspaceInfo.SetMaxWidth((int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * 0.55f));
            airspaceInfo.Clickable = true;
            airspaceInfo.Visibility = ViewStates.Gone;
            airspaceInfo.Click += (sender, args) => OpenAirspaceSource();

            devConsoleButton = CreateRoundButton(Resource.Drawable.ic_bug, Loc.T("Konsola dev"));
            devConsoleButton.Visibility = ViewStates.Gone;
            devConsoleButton.Click += (sender, args) => OnDevConsoleClicked();

            devConsoleText = new TextView(this);
            devConsoleText.TextSize = 10f;
            devConsoleText.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
            devConsoleText.SetTextColor(Color.White);
            devConsoleText.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            devConsoleText.SetBackgroundColor(Color.Argb(190, 0, 0, 0));
            devConsoleText.SetMaxWidth((int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * 0.8f));
            devConsoleText.Visibility = ViewStates.Gone;
            devConsoleText.LongClick += (sender, args) => CopyDevConsole();

            LinearLayout infoColumn = new LinearLayout(this);
            infoColumn.Orientation = Orientation.Vertical;
            infoColumn.AddView(devConsoleButton, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
            LinearLayout.LayoutParams devConsoleTextParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            devConsoleTextParams.SetMargins(0, Dp(6), 0, Dp(6));
            infoColumn.AddView(devConsoleText, devConsoleTextParams);
            infoColumn.AddView(threatsInfo, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            LinearLayout.LayoutParams airspaceInfoParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            airspaceInfoParams.SetMargins(0, Dp(6), 0, 0);
            infoColumn.AddView(airspaceInfo, airspaceInfoParams);

            FrameLayout.LayoutParams infoColumnParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Start);
            infoColumnParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(12));
            root.AddView(infoColumn, infoColumnParams);

            aircraftCard = new AircraftCardView(this, landscape ? 1.0f : 1.25f);
            aircraftCard.Visibility = ViewStates.Gone;
            aircraftCard.FollowClicked += () => controller.FollowPrimary();
            aircraftCard.CloseClicked += () => controller.ClearSelection();
            aircraftCard.WatchClicked += OnWatchClicked;
            aircraftCard.RefreshRouteClicked += OnRefreshRouteClicked;
            aircraftCard.LayoutChange += (sender, args) => UpdateCoveredArea();

            int cardWidth = landscape
                ? (int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * LandscapeCardWidthFraction)
                : ViewGroup.LayoutParams.MatchParent;

            FrameLayout.LayoutParams cardParams = new FrameLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.Start);
            cardParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
            root.AddView(aircraftCard, cardParams);

            airportCard = new AirportCardView(this);
            airportCard.Visibility = ViewStates.Gone;
            airportCard.CenterClicked += () => controller.CenterOnSelectedAirport();
            airportCard.MaximizedChanged += maximized =>
            {
                if (maximized)
                {
                    controller.SetFramingFrozen(true);
                }
            };
            airportCard.CloseClicked += () => controller.ClearSelection();
            airportCard.FavoriteClicked += () =>
            {
                Airport? selected = controller.GetSelectedAirport();

                if (selected != null)
                {
                    AppServices.Favorites.Toggle(selected.Icao);
                    UpdateCard();
                }
            };
            airportCard.LayoutChange += (sender, args) => UpdateCoveredArea();

            FrameLayout.LayoutParams airportCardParams = new FrameLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.Start);
            airportCardParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
            root.AddView(airportCard, airportCardParams);

            UpdateModeButtons();

            ImageButton zoomInButton = CreateRoundButton(Resource.Drawable.ic_zoom_in, Loc.T("Przybliż"));
            zoomInButton.Click += (sender, args) => controller.ZoomIn();

            ImageButton zoomOutButton = CreateRoundButton(Resource.Drawable.ic_zoom_out, Loc.T("Oddal"));
            zoomOutButton.Click += (sender, args) => controller.ZoomOut();

            ImageButton centerButton = CreateRoundButton(Resource.Drawable.ic_my_location, Loc.T("Centruj na mojej pozycji"));
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
            airportCard?.BringToFront();

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
        /// Creates the small round badge with a number that sits on a corner of a button. It starts hidden.
        /// </summary>
        /// <returns>The badge.</returns>
        private TextView CreateBadge()
        {
            TextView badge = new TextView(this);
            badge.TextSize = 11f;
            badge.SetTypeface(null, TypefaceStyle.Bold);
            badge.SetTextColor(Color.White);
            badge.Gravity = GravityFlags.Center;
            badge.SetPadding(0, 0, 0, 0);
            badge.Visibility = ViewStates.Gone;

            Android.Graphics.Drawables.GradientDrawable background = new Android.Graphics.Drawables.GradientDrawable();
            background.SetShape(Android.Graphics.Drawables.ShapeType.Oval);
            background.SetColor(Color.Rgb(255, 152, 0));
            background.SetStroke(Dp(1), Color.White);
            badge.Background = background;

            return badge;
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
        /// Starts or stops the feed of the threats according to the setting and refreshes the button and the text of the layer.
        /// </summary>
        private void ApplyThreatFeed()
        {
            if (AppSettings.ShowThreats)
            {
                AppServices.Threats.Start();
            }
            else
            {
                AppServices.Threats.Stop();
            }

            UpdateThreatsUi();
        }

        /// <summary>
        /// Refreshes the text of the layer on the main thread after the feed changed.
        /// </summary>
        private void OnThreatFeedChanged()
        {
            RunOnUiThread(UpdateThreatsUi);
        }

        /// <summary>
        /// Highlights the button of the layer and shows the link to the source with the state of the feed while the layer is on.
        /// </summary>
        private void UpdateThreatsUi()
        {
            bool on = AppSettings.ShowThreats;

            threatsButton?.SetBackgroundResource(on ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);

            if (threatsInfo == null)
            {
                return;
            }

            threatsInfo.Visibility = on ? ViewStates.Visible : ViewStates.Gone;

            if (!on)
            {
                return;
            }

            DataSources.Interfaces.IThreatFeed feed = AppServices.Threats;
            int count = feed.Threats.Count((Threat threat) => ThreatText.HasDrawablePosition(threat));
            string state = ThreatText.BuildStateText(feed.State == DataSources.Interfaces.ThreatFeedState.Streaming, feed.State == DataSources.Interfaces.ThreatFeedState.Polling, feed.State == DataSources.Interfaces.ThreatFeedState.Connecting, count);
            string attribution = ThreatText.Attribution;
            SpannableString text = new SpannableString(attribution + " · " + state + "\n" + ThreatText.ShortDisclaimer);

            text.SetSpan(new UnderlineSpan(), 0, attribution.Length, SpanTypes.ExclusiveExclusive);
            text.SetSpan(new ForegroundColorSpan(Color.Rgb(120, 190, 255)), 0, attribution.Length, SpanTypes.ExclusiveExclusive);
            threatsInfo.SetText(text, TextView.BufferType.Spannable);
        }

        /// <summary>
        /// Opens the page of the source of the threats, which the terms of the source require to be linked.
        /// </summary>
        private void OpenThreatSource()
        {
            try
            {
                StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(DataSources.Neptun.NeptunThreatFeed.HomePage)));
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, DataSources.Neptun.NeptunThreatFeed.HomePage, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Switches the layer of the threats on or off and saves the choice in the settings. Switching it on shows the warning that the data is not an official alarm system.
        /// </summary>
        private void OnThreatsClicked()
        {
            bool show = !AppSettings.ShowThreats;
            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
            {
                { AppSettings.ShowThreatsSetting, show ? 1.0 : 0.0 },
            };

            AppSettings.Save(values);
            ApplyThreatFeed();

            if (show)
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
                builder.SetTitle(Loc.T("Zagrożenia (NEPTUN)"));
                builder.SetMessage(ThreatText.Disclaimer);
                builder.SetPositiveButton(Loc.T("Rozumiem"), (sender, args) => { });
                builder.SetNeutralButton(ThreatText.Attribution, (sender, args) => OpenThreatSource());
                builder.Show();
            }
            else
            {
                Toast.MakeText(this, Loc.T("Warstwa zagrożeń ukryta"), ToastLength.Short)?.Show();
            }
        }

        /// <summary>
        /// Shows the description of a threat that the user tapped on the map.
        /// </summary>
        /// <param name="threat">The tapped threat.</param>
        private void OnThreatTapped(Threat threat)
        {
            RunOnUiThread(() =>
            {
                List<string> lines = ThreatText.BuildLines(threat, DateTime.UtcNow, AppSettings.Units);

                AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
                builder.SetTitle(ThreatText.KindName(threat.Kind));
                builder.SetMessage(string.Join("\n", lines) + "\n\n" + ThreatText.Disclaimer);
                builder.SetPositiveButton(Loc.T("Zamknij"), (sender, args) => { });
                builder.SetNeutralButton(ThreatText.Attribution, (sender, args) => OpenThreatSource());
                builder.Show();
            });
        }

        /// <summary>
        /// Starts or stops the feed of the airspace areas according to the setting and refreshes the button and the text of the layer.
        /// </summary>
        private void ApplyAirspaceFeed()
        {
            if (AppSettings.ShowAirspace || AppServices.AirspaceConfig.AlertOptions.Enabled)
            {
                AppServices.Airspace.Start();
            }
            else
            {
                AppServices.Airspace.Stop();
            }

            UpdateAirspaceUi();
        }

        /// <summary>
        /// Starts the feed of the official notices while the notifications about them are on, so that new notices are noticed while the map is open. Without the notifications the feed runs only while the screen of the notices is open.
        /// </summary>
        private void ApplyWarningFeed()
        {
            if (AppServices.WarningConfig.AlertOptions.Enabled)
            {
                AppServices.Warnings.Start();
            }
        }

        /// <summary>
        /// Refreshes the text of the layer on the main thread after the feed or the configuration of the layer changed.
        /// </summary>
        private void OnAirspaceFeedChanged()
        {
            if (!AlertMonitor.BackgroundActive)
            {
                Task.Run(AirspaceAlertMonitor.Check);
            }

            RunOnUiThread(UpdateAirspaceUi);
        }

        /// <summary>
        /// Highlights the button of the layer and shows the link to the source with the state of the feed while the layer is on.
        /// </summary>
        private void UpdateAirspaceUi()
        {
            bool on = AppSettings.ShowAirspace;

            airspaceButton?.SetBackgroundResource(on ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);

            if (airspaceInfo == null)
            {
                return;
            }

            airspaceInfo.Visibility = on ? ViewStates.Visible : ViewStates.Gone;

            if (!on)
            {
                return;
            }

            PollingFeed<AirspaceZone> feed = AppServices.Airspace;
            int shown = AppServices.AirspaceConfig.Config.Select(feed.Items, DateTime.UtcNow).Count;
            string attribution = AirspaceText.Attribution;
            SpannableString text = new SpannableString(attribution + " · " + AirspaceText.BuildStateText(feed.State, shown));

            text.SetSpan(new UnderlineSpan(), 0, attribution.Length, SpanTypes.ExclusiveExclusive);
            text.SetSpan(new ForegroundColorSpan(Color.Rgb(120, 190, 255)), 0, attribution.Length, SpanTypes.ExclusiveExclusive);
            airspaceInfo.SetText(text, TextView.BufferType.Spannable);
        }

        /// <summary>
        /// Opens the airspace map of the source of the areas.
        /// </summary>
        private void OpenAirspaceSource()
        {
            try
            {
                StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(DataSources.Pansa.PansaClient.DefaultBaseAddress)));
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, DataSources.Pansa.PansaClient.DefaultBaseAddress, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Switches the layer of the airspace areas on or off and saves the choice in the settings. Switching it on shows the warning that the data is not for planning flights.
        /// </summary>
        private void OnAirspaceClicked()
        {
            bool show = !AppSettings.ShowAirspace;
            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
            {
                { AppSettings.ShowAirspaceSetting, show ? 1.0 : 0.0 },
            };

            AppSettings.Save(values);
            ApplyAirspaceFeed();

            if (show)
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
                builder.SetTitle(Loc.T("Strefy przestrzeni powietrznej (PAŻP)"));
                builder.SetMessage(AirspaceText.Disclaimer + "\n\n" + Loc.T("Dłuższe przytrzymanie przycisku otwiera wybór rodzajów i stanów stref."));
                builder.SetPositiveButton(Loc.T("Rozumiem"), (sender, args) => { });
                builder.SetNeutralButton(Loc.T("Wybierz strefy"), (sender, args) => StartActivity(new Intent(this, typeof(AirspaceActivity))));
                builder.Show();
            }
            else
            {
                Toast.MakeText(this, Loc.T("Warstwa stref ukryta"), ToastLength.Short)?.Show();
            }
        }

        /// <summary>
        /// Shows the description of an airspace area that the user tapped on the map.
        /// </summary>
        /// <param name="view">The tapped area with its state.</param>
        private void OnAirspaceTapped(AirspaceZoneView view)
        {
            RunOnUiThread(() =>
            {
                IReadOnlyList<string> lines = AirspaceText.GetLines(view, AppSettings.Units, TimeZoneInfo.Local);

                AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
                builder.SetTitle(AirspaceText.GetTitle(view.Zone));
                builder.SetMessage(string.Join("\n", lines) + "\n\n" + AirspaceText.Disclaimer);
                builder.SetPositiveButton(Loc.T("Zamknij"), (sender, args) => { });
                builder.SetNeutralButton(AirspaceText.Attribution, (sender, args) => OpenAirspaceSource());
                builder.Show();
            });
        }

        /// <summary>
        /// Turns the isolation of the selected aircraft on or off, and shows a hint when nothing is selected.
        /// </summary>
        private void OnIsolateClicked()
        {
            if (!controller.TryToggleIsolation(out bool isolated))
            {
                Toast.MakeText(this, Loc.T("Najpierw zaznacz samolot, dotykając go na mapie"), ToastLength.Short)?.Show();
            }
        }

        /// <summary>
        /// Turns the multi-select mode on or off.
        /// </summary>
        private void OnMultiSelectClicked()
        {
            bool enabled = controller.ToggleMultiSelect();
            string message = enabled
                ? Loc.T("Zaznaczanie wielu samolotów: dotykaj samolotów, aby je dodać lub odjąć")
                : Loc.T("Zaznaczanie wielu samolotów wyłączone");

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
            Toast.MakeText(this, show ? Loc.T("Etykiety samolotów: widoczne") : Loc.T("Etykiety samolotów: ukryte"), ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Turns the military aircraft filter on or off and reports how many of them are in the latest data.
        /// </summary>
        private void OnMilitaryClicked()
        {
            bool enabled = controller.ToggleMilitaryOnly(out int militaryCount);
            string message = !enabled
                ? Loc.T("Wszystkie samoloty")
                : militaryCount > 0
                    ? string.Format(CultureInfo.InvariantCulture, Loc.T("Tylko wojskowe: {0} w zasięgu"), militaryCount)
                    : Loc.T("Tylko wojskowe: żadnego w zasięgu");

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
            UpdateFiltersButton();
        }

        /// <summary>
        /// Highlights the filters button and shows the number of filters that are on in its badge, or hides the badge when none is on.
        /// </summary>
        private void UpdateFiltersButton()
        {
            if (filtersButton == null || filtersBadge == null)
            {
                return;
            }

            int count = AppServices.Filters.ActiveFilters.Count;

            filtersButton.SetBackgroundResource(count > 0 ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
            filtersBadge.Visibility = count > 0 ? ViewStates.Visible : ViewStates.Gone;
            filtersBadge.Text = count.ToString(CultureInfo.InvariantCulture);
            filtersButton.ContentDescription = count > 0
                ? string.Format(CultureInfo.InvariantCulture, Loc.T("Filtry samolotów, włączone: {0}"), count)
                : Loc.T("Filtry samolotów");
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
                ? string.Format(CultureInfo.InvariantCulture, Loc.T("Zaznaczanie wielu samolotów, zaznaczono: {0}"), count)
                : Loc.T("Zaznaczanie wielu samolotów");
        }

        /// <summary>
        /// Shows the menu with the search, the favorite airports, the aircraft filters, the settings and the information about the application under the menu button.
        /// </summary>
        /// <param name="anchor">Button under which the menu appears.</param>
        private void ShowMenu(View anchor)
        {
            PopupMenu popup = new PopupMenu(this, anchor);
            popup.Menu?.Add(0, MenuSearchId, 0, Loc.T("Wyszukaj"));
            popup.Menu?.Add(0, MenuFavoritesId, 1, Loc.T("Ulubione lotniska"));
            popup.Menu?.Add(0, MenuFiltersId, 2, Loc.T("Filtry samolotów"));
            popup.Menu?.Add(0, MenuAlertsId, 3, Loc.T("Alerty"));
            popup.Menu?.Add(0, MenuAircraftHistoryId, 3, Loc.T("Historia samolotów"));
            popup.Menu?.Add(0, MenuWarningsId, 3, Loc.T("Ostrzeżenia"));
            popup.Menu?.Add(0, MenuSettingsId, 4, Loc.T("Ustawienia"));
            popup.Menu?.Add(0, MenuAboutId, 5, Loc.T("O aplikacji"));

            if (AppSettings.ShowDevLog)
            {
                popup.Menu?.Add(0, MenuDevLogId, 6, Loc.T("Dev log"));
            }

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
                else if (args.Item?.ItemId == MenuFavoritesId)
                {
                    StartActivity(new Intent(this, typeof(FavoriteAirportsActivity)));
                }
                else if (args.Item?.ItemId == MenuFiltersId)
                {
                    StartActivity(new Intent(this, typeof(FiltersActivity)));
                }
                else if (args.Item?.ItemId == MenuAlertsId)
                {
                    StartActivity(new Intent(this, typeof(AlertsActivity)));
                }
                else if (args.Item?.ItemId == MenuAircraftHistoryId)
                {
                    StartActivity(new Intent(this, typeof(AircraftHistoryActivity)));
                }
                else if (args.Item?.ItemId == MenuWarningsId)
                {
                    StartActivity(new Intent(this, typeof(WarningsActivity)));
                }
                else if (args.Item?.ItemId == MenuAboutId)
                {
                    StartActivity(new Intent(this, typeof(AboutActivity)));
                }
                else if (args.Item?.ItemId == MenuDevLogId)
                {
                    StartActivity(new Intent(this, typeof(DevLogActivity)));
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

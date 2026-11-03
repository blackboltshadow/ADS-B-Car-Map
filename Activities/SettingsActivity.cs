using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views.InputMethods;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Settings screen of the application settings shared by the phone map and Android Auto. Numbers are edited in text fields, on/off settings with switches and lists of options with drop-down lists.
    /// </summary>
    [Activity(Label = "Ustawienia", Exported = false, Theme = "@android:style/Theme.Material")]
    public class SettingsActivity : Activity
    {
        /// <summary>
        /// Color of the titles and labels of the settings.
        /// </summary>
        private static readonly Color PrimaryTextColor = Color.White;

        /// <summary>
        /// Color of the hints and of the current values shown under the titles.
        /// </summary>
        private static readonly Color SecondaryTextColor = Color.Rgb(180, 180, 180);

        /// <summary>
        /// Keys of the collapsible cards that are currently expanded. Static, so the state survives the recreation of the activity on rotation.
        /// </summary>
        private static readonly HashSet<string> expandedCards = new HashSet<string>();

        /// <summary>
        /// Functions reading the current value of the control of each setting. Return null when the control holds no valid value.
        /// </summary>
        private readonly Dictionary<SettingDefinition, Func<double?>> settingReaders = new Dictionary<SettingDefinition, Func<double?>>();

        /// <summary>
        /// Functions writing a value into the control of each setting.
        /// </summary>
        private readonly Dictionary<SettingDefinition, System.Action<double>> settingWriters = new Dictionary<SettingDefinition, System.Action<double>>();

        /// <summary>
        /// Editor of the altitude color scale shared by all threshold settings, created together with the first threshold.
        /// </summary>
        private AltitudeScaleView? altitudeScale;

        /// <summary>
        /// Cards with a title shared by several settings, by key.
        /// </summary>
        private readonly Dictionary<string, LinearLayout> titledCards = new Dictionary<string, LinearLayout>();

        /// <summary>
        /// Card of the altitude colors: the unit, the color scale editor and the legend switch.
        /// </summary>
        private LinearLayout? altitudeScaleCard;

        /// <summary>
        /// Number of threshold settings already connected to <see cref="altitudeScale"/>.
        /// </summary>
        private int altitudeScaleThresholds;

        /// <summary>
        /// Undo buttons by setting. A button is visible only while the value in the control differs from the saved one.
        /// </summary>
        private readonly Dictionary<SettingDefinition, View> undoButtons = new Dictionary<SettingDefinition, View>();

        /// <summary>
        /// Card shared by the airport settings, created with the first of them.
        /// </summary>
        private LinearLayout? airportSettingsCard;

        /// <summary>
        /// Preview of sample airports inside <see cref="airportSettingsCard"/>.
        /// </summary>
        private AirportsPreview? airportsPreview;

        /// <summary>
        /// Switch that limits the small airports to those with a paved runway. Disabled while the small airports are switched off.
        /// </summary>
        private Switch? smallPavedToggle;

        /// <summary>
        /// Note under the switch that explains why it is disabled.
        /// </summary>
        private TextView? smallPavedHint;

        /// <summary>
        /// Views of the airport section that have no meaning while only the favorite airports are shown: the categories, the paved runway filter and the zoom range.
        /// </summary>
        private readonly List<View> favoritesOnlyInactiveViews = new List<View>();

        /// <summary>
        /// Control that sets the range of zoom levels in which the airports are visible, created with the first of its two settings.
        /// </summary>
        private ZoomRangeView? zoomRangeView;

        /// <summary>
        /// Number of settings already connected to <see cref="zoomRangeView"/>.
        /// </summary>
        private int zoomRangeBounds;

        /// <summary>
        /// Card shared by all trail settings, created with the first of them.
        /// </summary>
        private LinearLayout? trailCard;

        /// <summary>
        /// Button that opens the list of aircraft types missing from the type table.
        /// </summary>
        private Button? developerDataButton;

        /// <summary>
        /// Card shared by the settings of the developer log, created with the screen.
        /// </summary>
        private LinearLayout? devLogCard;

        /// <summary>
        /// Button that opens the developer log screen.
        /// </summary>
        private Button? devLogButton;

        /// <summary>
        /// List of the aircraft sources in the order in which they are asked, or null before the screen is built.
        /// </summary>
        private SourceOrderView? sourceOrderView;

        /// <summary>
        /// List of the flight route sources in the order in which they are asked, or null before the screen is built.
        /// </summary>
        private SourceOrderView? routeOrderView;

        /// <summary>
        /// Card shared by the tile cache settings, created with the first of them.
        /// </summary>
        private LinearLayout? tileCacheCard;

        /// <summary>
        /// Text with the current size of the tile cache on the disk, inside <see cref="tileCacheCard"/>.
        /// </summary>
        private TextView? tileCacheUsageText;

        /// <summary>
        /// Card shared by the settings of the flight information cache, created with the first of them.
        /// </summary>
        private LinearLayout? flightCacheCard;

        /// <summary>
        /// Text with the number of stored flights and aircraft, inside <see cref="flightCacheCard"/>.
        /// </summary>
        private TextView? flightCacheUsageText;

        /// <summary>
        /// Text with the state of the data sources, inside the source status card.
        /// </summary>
        private TextView? sourceStatusText;

        /// <summary>
        /// Refreshes the source status text and schedules the next refresh while the screen is shown.
        /// </summary>
        private Java.Lang.Runnable? sourceStatusRefresh;

        /// <summary>
        /// Preview of the sample aircraft trails inside <see cref="trailCard"/>.
        /// </summary>
        private TrailsPreview? trailsPreview;

        /// <summary>
        /// Card shared by the aircraft icon size and outline sliders, created with the first of them.
        /// </summary>
        private LinearLayout? aircraftIconCard;

        /// <summary>
        /// Preview of all aircraft icons inside <see cref="aircraftIconCard"/>.
        /// </summary>
        private AircraftIconsPreview? aircraftIconsPreview;

        /// <summary>
        /// Threshold settings shown in the altitude scale editor, in the order of their handles.
        /// </summary>
        private readonly List<SettingDefinition> altitudeScaleSettings = new List<SettingDefinition>();

        /// <summary>
        /// Builds the settings screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Updates the number of missing types, which may have changed while the list screen was open.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            UpdateDeveloperDataButton();
            UpdateDevLogButton();
            RefreshSourceStatus();
        }

        /// <summary>
        /// Commits a number that is still being typed, because a setting is saved when its field loses focus, and stops refreshing the source status.
        /// </summary>
        protected override void OnPause()
        {
            if (sourceStatusRefresh != null)
            {
                sourceStatusText?.RemoveCallbacks(sourceStatusRefresh);
            }

            CurrentFocus?.ClearFocus();
            base.OnPause();
        }

        /// <summary>
        /// Closes the screen when the back arrow is tapped.
        /// </summary>
        /// <param name="item">Selected bar item.</param>
        /// <returns>True when the item was handled.</returns>
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home)
            {
                Finish();

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        /// <summary>
        /// Creates the whole view of the screen.
        /// </summary>
        /// <returns>Scrollable view with the setting controls and buttons.</returns>
        private View BuildContent()
        {
            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(root);

            LinearLayout units = CreateAggregate(root, "Jednostki", "units", 20f);
            GetTitledCard("units", "Jednostki miary", units);

            LinearLayout appearance = CreateAggregate(root, "Wygląd", "appearance", 20f);
            LinearLayout aircraft = CreateAggregate(appearance, "Samoloty", "aircraft", 17f);
            GetAircraftIconCard(aircraft);
            GetTrailCard(aircraft);
            GetAltitudeCard(aircraft);
            GetAirportSettingsCard(appearance);
            GetTitledCard("map", "Mapa", appearance);

            LinearLayout alerts = CreateAggregate(root, "Alerty", "alertsSection", 20f);
            GetTitledCard("alerts", "Sprawdzanie w tle", alerts);

            LinearLayout advanced = CreateAggregate(root, "Zaawansowane", "advanced", 20f);
            GetTitledCard("dataSource", "Źródło danych", advanced);
            GetTitledCard("routeSources", "Źródła tras", advanced);
            GetSourceStatusCard(advanced);
            GetTitledCard("privacy", "Prywatność", advanced);
            GetTitledCard("refresh", "Odświeżanie", advanced);
            GetTitledCard("tiles", "Pobieranie kafelków", advanced);
            GetTileCacheCard(advanced);
            GetFlightCacheCard(advanced);

            LinearLayout developer = CreateAggregate(root, "Dev", "developer", 20f);

            TextView version = new TextView(this);
            version.Text = "ADS-B Car Map v" + BuildInfo.Version + " (" + BuildInfo.Build + ")";
            version.TextSize = 12f;
            version.SetPadding(Dp(4), Dp(4), 0, Dp(4));
            developer.AddView(version);

            GetDevLogCard(developer);
            GetTitledCard("privacyDev", "Prywatność (zaawansowane)", developer);

            foreach (SettingDefinition definition in AppSettings.All)
            {
                switch (definition.Kind)
                {
                    case SettingKind.Switch:
                        AddSwitch(appearance, definition);
                        break;
                    case SettingKind.Choice:
                        if (definition == AppSettings.DataSourceSetting || definition == AppSettings.RouteSourceSetting)
                        {
                            AddSourceOrder(appearance, definition);
                        }
                        else
                        {
                            AddChoice(appearance, definition);
                        }

                        break;
                    case SettingKind.Slider:
                        AddSlider(appearance, definition);
                        break;
                    case SettingKind.MultiChoice:
                        AddMultiChoice(appearance, definition);
                        break;
                    case SettingKind.ZoomRange:
                        AddZoomRangeBound(appearance, definition);
                        break;
                    case SettingKind.Scale:
                        AddScaleThreshold(appearance, definition);
                        break;
                    default:
                        AddNumber(GetNumberCard(definition, appearance), definition);
                        break;
                }
            }

            if (aircraftIconCard != null)
            {
                aircraftIconCard.AddView(CreateResetButton("Przywróć domyślne ikony i etykiety", new List<SettingDefinition> { AppSettings.IconScaleSetting, AppSettings.AircraftOutlineWidthSetting, AppSettings.AircraftShadowSetting, AppSettings.AircraftCockpitSetting, AppSettings.ShowLabelsSetting, AppSettings.LabelTextSizeSetting }, "Przywrócono domyślne ustawienia ikon i etykiet"));
            }

            if (airportSettingsCard != null)
            {
                airportSettingsCard.AddView(CreateResetButton("Przywróć domyślne lotniska", AppSettings.AirportSettings, "Przywrócono domyślne ustawienia lotnisk"));
            }

            if (tileCacheCard != null)
            {
                tileCacheCard.AddView(CreateResetButton("Przywróć domyślną pamięć podręczną", AppSettings.TileCacheSettings, "Przywrócono domyślne ustawienia pamięci podręcznej"));
            }

            if (flightCacheCard != null)
            {
                flightCacheCard.AddView(CreateResetButton("Przywróć domyślny czas przechowywania", AppSettings.FlightCacheSettings, "Przywrócono domyślny czas przechowywania informacji o lotach"));
            }

            titledCards["units"].AddView(CreateResetButton("Przywróć domyślne jednostki", AppSettings.UnitSettings, "Przywrócono domyślne jednostki"));
            titledCards["alerts"].AddView(CreateResetButton("Przywróć domyślne alerty", AppSettings.AlertSettings, "Przywrócono domyślne ustawienia alertów"));

            if (devLogCard != null)
            {
                devLogCard.AddView(CreateResetButton("Przywróć domyślny Dev log", AppSettings.DevLogSettings, "Przywrócono domyślne ustawienia Dev logu"));
            }

            titledCards["privacyDev"].AddView(CreateResetButton("Przywróć domyślną prywatność", AppSettings.PrivacyDevSettings.Concat(AppSettings.PrivacySettings).ToList(), "Przywrócono domyślne ustawienia prywatności"));

            if (trailCard != null)
            {
                trailCard.AddView(CreateResetButton("Przywróć domyślne ślady", AppSettings.TrailSettings, "Przywrócono domyślne ustawienia śladów"));
            }

            List<(LinearLayout?, string)> collapsibleCards = new List<(LinearLayout?, string)>
            {
                (airportSettingsCard, "airports"),
                (trailCard, "trails"),
                (tileCacheCard, "tileCache"),
                (flightCacheCard, "flightCache"),
                (devLogCard, "devLog"),
                (aircraftIconCard, "aircraftIcons"),
                (altitudeScaleCard, "altitudeScale"),
            };

            foreach (KeyValuePair<string, LinearLayout> titled in titledCards)
            {
                collapsibleCards.Add((titled.Value, titled.Key));
            }

            foreach ((LinearLayout? card, string key) in collapsibleCards)
            {
                if (card != null && card.ChildCount > 0)
                {
                    MakeCollapsible(card, card.GetChildAt(0)!, key);
                }
            }

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(8), 0, 0);
            advanced.AddView(buttons);

            buttons.AddView(CreateResetButton("Przywróć wszystkie domyślne", AppSettings.All, "Przywrócono ustawienia domyślne"));

            AddDeveloperDataCard(developer);

            LoadSettingInputs();
            UpdateDependentControls();

            return scrollView;
        }

        /// <summary>
        /// Makes a card collapsible: everything below the header is moved into a body that an arrow in the header shows or hides.
        /// </summary>
        /// <param name="card">Card to change.</param>
        /// <param name="header">First view of the card, which stays visible when the card is collapsed.</param>
        /// <param name="key">Key under which the expanded state is remembered while the application runs, so that it survives a rotation.</param>
        private void MakeCollapsible(LinearLayout card, View header, string key)
        {
            List<View> rest = new List<View>();

            for (int index = 1; index < card.ChildCount; index++)
            {
                rest.Add(card.GetChildAt(index)!);
            }

            card.RemoveViews(1, rest.Count);

            LinearLayout body = new LinearLayout(this);
            body.Orientation = Orientation.Vertical;

            foreach (View view in rest)
            {
                body.AddView(view);
            }

            LinearLayout headerRow;
            bool wrapped = header is not LinearLayout;

            if (wrapped)
            {
                card.RemoveView(header);

                headerRow = new LinearLayout(this);
                headerRow.Orientation = Orientation.Horizontal;
                headerRow.SetGravity(GravityFlags.CenterVertical);
                headerRow.AddView(header, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                card.AddView(headerRow, 0);
            }
            else
            {
                headerRow = (LinearLayout)header;
            }

            card.AddView(body);

            TextView arrow = CreateArrow();
            headerRow.AddView(arrow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            System.Action toggle = BindToggle(arrow, body, key);

            arrow.Click += (sender, args) => toggle();

            if (wrapped)
            {
                headerRow.Click += (sender, args) => toggle();
            }
        }

        /// <summary>
        /// Creates the arrow that shows or hides a collapsible section.
        /// </summary>
        /// <returns>The arrow, not yet added to any layout.</returns>
        private TextView CreateArrow()
        {
            TextView arrow = new TextView(this);
            arrow.TextSize = 22f;
            arrow.SetTextColor(Color.White);
            arrow.Gravity = GravityFlags.Center;
            arrow.SetPadding(Dp(12), 0, Dp(4), 0);

            return arrow;
        }

        /// <summary>
        /// Connects an arrow to the body it shows or hides and applies the remembered expanded state.
        /// </summary>
        /// <param name="arrow">Arrow whose symbol shows the state.</param>
        /// <param name="body">View shown when the section is expanded.</param>
        /// <param name="key">Key of the remembered expanded state.</param>
        /// <returns>Action that toggles the section.</returns>
        private static System.Action BindToggle(TextView arrow, View body, string key)
        {
            System.Action apply = () =>
            {
                bool expanded = expandedCards.Contains(key);

                body.Visibility = expanded ? ViewStates.Visible : ViewStates.Gone;
                arrow.Text = expanded ? "▴" : "▾";
                arrow.ContentDescription = expanded ? "Zwiń sekcję" : "Rozwiń sekcję";
            };

            apply();

            return () =>
            {
                if (!expandedCards.Remove(key))
                {
                    expandedCards.Add(key);
                }

                apply();
            };
        }

        /// <summary>
        /// Adds a collapsible group of cards: a header with a title and an arrow, followed by a body into which the cards are added. A group may contain other groups.
        /// </summary>
        /// <param name="parent">Container of the screen or of the group the new group belongs to.</param>
        /// <param name="title">Title of the group.</param>
        /// <param name="key">Key of the remembered expanded state.</param>
        /// <param name="titleSize">Size of the title text.</param>
        /// <returns>Body of the group.</returns>
        private LinearLayout CreateAggregate(LinearLayout parent, string title, string key, float titleSize)
        {
            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Horizontal;
            header.SetGravity(GravityFlags.CenterVertical);
            header.SetMinimumHeight(Dp(44));
            header.SetPadding(Dp(4), Dp(6), Dp(4), Dp(6));
            header.Clickable = true;
            parent.AddView(header);

            TextView label = new TextView(this);
            label.Text = title;
            label.TextSize = titleSize;
            label.SetTextColor(PrimaryTextColor);
            label.SetTypeface(null, TypefaceStyle.Bold);
            header.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            TextView arrow = CreateArrow();
            header.AddView(arrow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            LinearLayout body = new LinearLayout(this);
            body.Orientation = Orientation.Vertical;
            body.SetPadding(Dp(8), 0, 0, Dp(4));
            parent.AddView(body);

            System.Action toggle = BindToggle(arrow, body, key);
            header.Click += (sender, args) => toggle();

            return body;
        }

        /// <summary>
        /// Returns the card with a title shared by several settings, creating it at the first call.
        /// </summary>
        /// <param name="key">Key of the card, also the key of its expanded state.</param>
        /// <param name="title">Title shown in the header of the card.</param>
        /// <param name="container">Container in which the card is created.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetTitledCard(string key, string title, LinearLayout container)
        {
            if (titledCards.TryGetValue(key, out LinearLayout? existing))
            {
                return existing;
            }

            LinearLayout card = CreateCard(container);

            TextView titleView = new TextView(this);
            titleView.Text = title;
            StyleTitle(titleView);
            card.AddView(titleView);

            titledCards[key] = card;

            return card;
        }

        /// <summary>
        /// Returns the card to which a number setting belongs.
        /// </summary>
        /// <param name="definition">Number setting.</param>
        /// <param name="container">Container used when the setting belongs to no known card.</param>
        /// <returns>The card in which the field is placed.</returns>
        private LinearLayout GetNumberCard(SettingDefinition definition, LinearLayout container)
        {
            if (AppSettings.TrailSettings.Contains(definition))
            {
                return GetTrailCard(container);
            }

            if (definition == AppSettings.FetchIntervalSetting || definition == AppSettings.RenderIntervalSetting)
            {
                return GetTitledCard("refresh", "Odświeżanie", container);
            }

            if (definition == AppSettings.MaxCachedTilesSetting || definition == AppSettings.TileRetrySetting)
            {
                return GetTitledCard("tiles", "Pobieranie kafelków", container);
            }

            return GetTitledCard("map", "Mapa", container);
        }

        /// <summary>
        /// Returns the shared card to which a switch, list, slider or check box group belongs.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <param name="container">Container used when a card has to be created.</param>
        /// <returns>The shared card, or null when the setting has a card of its own.</returns>
        private LinearLayout? GetSharedCardFor(SettingDefinition definition, LinearLayout container)
        {
            if (AppSettings.UnitSettings.Contains(definition))
            {
                return titledCards["units"];
            }

            if (AppSettings.RouteSourceSettings.Contains(definition))
            {
                return titledCards["routeSources"];
            }

            if (AppSettings.DataSourceSettings.Contains(definition))
            {
                return titledCards["dataSource"];
            }

            if (AppSettings.AlertSettings.Contains(definition))
            {
                return titledCards["alerts"];
            }

            if (AppSettings.PrivacySettings.Contains(definition))
            {
                return titledCards["privacy"];
            }

            if (AppSettings.PrivacyDevSettings.Contains(definition))
            {
                return titledCards["privacyDev"];
            }

            if (AppSettings.AirportSettings.Contains(definition))
            {
                return GetAirportSettingsCard(container);
            }

            if (definition == AppSettings.IconScaleSetting || definition == AppSettings.AircraftOutlineWidthSetting || definition == AppSettings.AircraftShadowSetting || definition == AppSettings.AircraftCockpitSetting || definition == AppSettings.ShowLabelsSetting || definition == AppSettings.LabelTextSizeSetting)
            {
                return GetAircraftIconCard(container);
            }

            if (AppSettings.TrailSettings.Contains(definition))
            {
                return GetTrailCard(container);
            }

            if (AppSettings.FlightCacheSettings.Contains(definition))
            {
                return GetFlightCacheCard(container);
            }

            if (AppSettings.DevLogSettings.Contains(definition))
            {
                return GetDevLogCard(container);
            }

            if (AppSettings.TileCacheSettings.Contains(definition))
            {
                return GetTileCacheCard(container);
            }

            if (definition == AppSettings.ShowAltitudeLegendSetting)
            {
                return GetAltitudeCard(container);
            }

            return null;
        }

        /// <summary>
        /// Returns the card of the altitude colors, which holds the color scale editor and the legend switch, creating it at the first call.
        /// </summary>
        /// <param name="container">Container in which the card is created.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetAltitudeCard(LinearLayout container)
        {
            if (altitudeScaleCard != null)
            {
                return altitudeScaleCard;
            }

            LinearLayout card = CreateCard(container);

            TextView title = new TextView(this);
            title.Text = "Kolory wysokości";
            StyleTitle(title);
            card.AddView(title);

            altitudeScaleCard = card;

            return card;
        }

        /// <summary>
        /// Adds the last card of the screen, "Dane developerskie", with the button that opens the list of aircraft types missing from the type table. The button shows the current number of such types.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        private void AddDeveloperDataCard(LinearLayout root)
        {
            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Dane developerskie";
            StyleTitle(title);
            card.AddView(title);

            TextView hint = new TextView(this);
            hint.Text = "Dane zbierane podczas używania mapy, przydatne przy uzupełnianiu tabel w aplikacji.";
            StyleSecondary(hint);
            hint.SetPadding(0, Dp(4), 0, 0);
            card.AddView(hint);

            developerDataButton = new Button(this);
            developerDataButton.Click += (sender, args) => StartActivity(new Android.Content.Intent(this, typeof(DeveloperDataActivity)));
            card.AddView(developerDataButton);
            UpdateDeveloperDataButton();
        }

        /// <summary>
        /// Refreshes the text of the developer data button with the number of types missing from the type table.
        /// </summary>
        private void UpdateDeveloperDataButton()
        {
            developerDataButton?.Text = "Brakujące typy samolotów (" + AppServices.MissingTypes.Missing.Count + ")";
        }

        /// <summary>
        /// Adds a label with a numeric text field for a setting.
        /// </summary>
        /// <param name="container">Container of the screen or of the card the field belongs to.</param>
        /// <param name="definition">Setting.</param>
        private void AddNumber(LinearLayout container, SettingDefinition definition)
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(28));
            row.SetPadding(0, Dp(4), 0, 0);
            container.AddView(row);

            TextView label = new TextView(this);
            label.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  (domyślnie {1}, zakres {2}–{3})",
                definition.Label,
                FormatValue(definition, definition.DefaultValue),
                FormatValue(definition, definition.Minimum),
                FormatValue(definition, definition.Maximum));
            label.TextSize = 13f;
            label.SetTextColor(PrimaryTextColor);
            row.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddUndoButton(row, definition);
            AddInfoButton(row, definition);

            EditText input = new EditText(this);
            input.InputType = definition.IsInteger
                ? InputTypes.ClassNumber
                : InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            input.SetSingleLine(true);
            container.AddView(input);

            settingReaders[definition] = () =>
            {
                string text = (input.Text ?? string.Empty).Trim().Replace(',', '.');

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
            };
            settingWriters[definition] = value => input.Text = FormatValue(definition, value);

            input.ImeOptions = ImeAction.Done;

            System.Action commit = () =>
            {
                double saved = AppSettings.Get(definition);
                string text = (input.Text ?? string.Empty).Trim().Replace(',', '.');

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed))
                {
                    double normalized = definition.Normalize(typed);

                    if (Differs(normalized, saved))
                    {
                        SaveValue(definition, normalized);
                        RecordChange(definition, saved);
                    }
                }

                input.Text = FormatValue(definition, AppSettings.Get(definition));
            };

            input.FocusChange += (sender, args) =>
            {
                if (!args.HasFocus)
                {
                    commit();
                }
            };

            input.EditorAction += (sender, args) =>
            {
                if (args.ActionId == ImeAction.Done)
                {
                    input.ClearFocus();
                }

                args.Handled = false;
            };
        }

        /// <summary>
        /// Adds an on/off switch for a setting.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddSwitch(LinearLayout root, SettingDefinition definition)
        {
            bool isAirportMaster = definition == AppSettings.ShowAirportsSetting;
            LinearLayout? sharedCard = GetSharedCardFor(definition, root);
            bool isGrouped = sharedCard != null && !isAirportMaster;
            LinearLayout card = sharedCard ?? CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(28));
            card.AddView(row);

            Switch toggle = new Switch(this);
            toggle.Text = definition.Label;
            StyleTitle(toggle);

            if (isGrouped)
            {
                StyleSubTitle(toggle);
                row.SetPadding(0, Dp(6), 0, 0);
            }

            row.AddView(toggle, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddInfoButton(row, definition);

            if (definition == AppSettings.ShowOnlyFavoriteAirportsSetting)
            {
                TextView previewHint = new TextView(this);
                previewHint.Text = "Podgląd pokazuje rodzaje lotnisk z bieżącymi ustawieniami. Po wyłączeniu lotnisk reszta tej sekcji jest nieaktywna.";
                StyleSecondary(previewHint);
                previewHint.SetPadding(0, Dp(4), 0, 0);
                card.AddView(previewHint);

                airportsPreview = new AirportsPreview(this);

                LinearLayout.LayoutParams previewParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                previewParams.SetMargins(0, Dp(8), 0, 0);
                card.AddView(airportsPreview, previewParams);
            }

            if (definition == AppSettings.SmallAirportsPavedSetting)
            {
                smallPavedToggle = toggle;

                smallPavedHint = new TextView(this);
                smallPavedHint.Text = "Dostępne tylko wtedy, gdy zaznaczone są małe lotniska.";
                StyleSecondary(smallPavedHint);
                smallPavedHint.SetPadding(Dp(4), 0, 0, 0);
                card.AddView(smallPavedHint);
            }

            settingReaders[definition] = () => toggle.Checked ? 1.0 : 0.0;
            settingWriters[definition] = value => toggle.Checked = value >= 0.5;

            toggle.CheckedChange += (sender, args) =>
            {
                double value = args.IsChecked ? 1.0 : 0.0;

                if (Differs(value, AppSettings.Get(definition)))
                {
                    SaveValue(definition, value);
                }
            };
        }

        /// <summary>
        /// Adds the list of the aircraft sources or of the flight route sources that is dragged to change the order in which they are asked. The first row is the main source, the rows below it are the fallback sources in the order of the queries.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting that stores the order, for the aircraft sources or for the route sources.</param>
        private void AddSourceOrder(LinearLayout root, SettingDefinition definition)
        {
            bool isRoutes = definition == AppSettings.RouteSourceSetting;
            IReadOnlyList<IReadOnlyList<string>> orders = isRoutes ? AppSettings.RouteSourceOrders : AppSettings.DataSourceOrders;
            Func<IReadOnlyList<string>, int> indexOf = isRoutes ? AppSettings.GetRouteSourceOrderIndex : AppSettings.GetDataSourceOrderIndex;
            LinearLayout? sharedCard = GetSharedCardFor(definition, root);
            LinearLayout card = sharedCard ?? CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(28));
            row.SetPadding(0, Dp(10), 0, 0);
            card.AddView(row);

            TextView label = new TextView(this);
            label.Text = definition.Label;
            StyleSubTitle(label);
            row.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddUndoButton(row, definition);
            AddInfoButton(row, definition);

            TextView hint = new TextView(this);
            hint.Text = isRoutes
                ? "Przytrzymaj ≡ i przeciągnij. Pierwszy serwis jest głównym źródłem tras, kolejne są pytane po kolei, gdy poprzedni nie zna lotu albo nie odpowiada. Trasa jest przypisana do znaku wywoławczego, więc nie zawsze odpowiada bieżącemu przelotowi."
                : "Przytrzymaj ≡ i przeciągnij. Pierwszy serwis jest głównym źródłem, kolejne są pytane po kolei, gdy poprzedni nie zwraca danych.";
            StyleSecondary(hint);
            hint.SetPadding(0, Dp(4), 0, Dp(6));
            card.AddView(hint);

            SourceOrderView view = new SourceOrderView(this);
            view.SetOrder(isRoutes ? AppSettings.RouteSourceOrder : AppSettings.DataSourceOrder);
            view.SetFallbackEnabled(AppSettings.DataSourceFallback);
            card.AddView(view, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            if (isRoutes)
            {
                routeOrderView = view;
            }
            else
            {
                sourceOrderView = view;
            }

            view.OrderChanged += newOrder =>
            {
                double saved = AppSettings.Get(definition);
                double selected = indexOf(newOrder);

                if (Differs(selected, saved))
                {
                    SaveValue(definition, selected);
                    RecordChange(definition, saved);
                }
            };

            settingReaders[definition] = () => indexOf(view.Order);
            settingWriters[definition] = value => view.SetOrder(orders[Math.Clamp((int)Math.Round(value), 0, orders.Count - 1)]);
        }

        /// <summary>
        /// Adds a label with a drop-down list of options for a setting.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddChoice(LinearLayout root, SettingDefinition definition)
        {
            LinearLayout? sharedCard = GetSharedCardFor(definition, root);
            bool isGrouped = sharedCard != null;
            LinearLayout card = sharedCard ?? CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(28));
            card.AddView(row);

            TextView label = new TextView(this);
            label.Text = definition.Label;
            StyleTitle(label);

            if (isGrouped)
            {
                StyleSubTitle(label);
                row.SetPadding(0, Dp(10), 0, 0);
            }

            row.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddUndoButton(row, definition);
            AddInfoButton(row, definition);

            FrameLayout field = new FrameLayout(this);
            GradientDrawable fieldBackground = new GradientDrawable();
            fieldBackground.SetColor(Color.Argb(45, 255, 255, 255));
            fieldBackground.SetStroke(Dp(1), Color.Argb(160, 255, 255, 255));
            fieldBackground.SetCornerRadius(Dp(8));
            field.Background = fieldBackground;

            LinearLayout.LayoutParams fieldParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            fieldParams.SetMargins(0, Dp(6), 0, 0);
            card.AddView(field, fieldParams);

            Spinner spinner = new Spinner(this);
            spinner.Background = null;
            spinner.SetPadding(Dp(8), Dp(6), Dp(40), Dp(6));
            ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, definition.Choices!.ToList());
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            spinner.Adapter = adapter;
            field.AddView(spinner, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            TextView arrow = new TextView(this);
            arrow.Text = "▾";
            arrow.TextSize = 22f;
            arrow.SetTextColor(Color.White);
            arrow.Clickable = false;
            field.AddView(arrow, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(12) });

            spinner.ItemSelected += (sender, args) =>
            {
                double saved = AppSettings.Get(definition);
                double selected = spinner.SelectedItemPosition;

                if (Differs(selected, saved))
                {
                    SaveValue(definition, selected);
                    RecordChange(definition, saved);
                }
            };

            settingReaders[definition] = () => spinner.SelectedItemPosition;
            settingWriters[definition] = value => spinner.SetSelection((int)Math.Round(value));
        }

        /// <summary>
        /// Connects one end of the airport zoom range to the shared range control, creating the control with its title and info button at the first end.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting that holds one end of the range.</param>
        private void AddZoomRangeBound(LinearLayout root, SettingDefinition definition)
        {
            if (zoomRangeView == null)
            {
                LinearLayout card = GetAirportSettingsCard(root);

                LinearLayout titleRow = new LinearLayout(this);
                titleRow.Orientation = Orientation.Horizontal;
                titleRow.SetGravity(GravityFlags.CenterVertical);
                titleRow.SetMinimumHeight(Dp(28));
                titleRow.SetPadding(0, Dp(10), 0, 0);
                card.AddView(titleRow);
                favoritesOnlyInactiveViews.Add(titleRow);

                TextView label = new TextView(this);
                label.Text = "Zakres zoomu, w którym widać lotniska";
                StyleTitle(label);
                StyleSubTitle(label);
                titleRow.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                AddInfoButton(titleRow, AppSettings.AirportFromZoomSetting);

                zoomRangeView = new ZoomRangeView(this);
                zoomRangeView.RangeChanged += SaveZoomRange;
                card.AddView(zoomRangeView);
                favoritesOnlyInactiveViews.Add(zoomRangeView);
            }

            ZoomRangeView view = zoomRangeView;
            int index = zoomRangeBounds++;

            settingReaders[definition] = () => view.GetValue(index);
            settingWriters[definition] = value => view.SetValue(index, (int)Math.Round(value));
        }

        /// <summary>
        /// Saves the zoom range after the user moved a handle.
        /// </summary>
        private void SaveZoomRange()
        {
            if (zoomRangeView == null)
            {
                return;
            }

            AppSettings.Save(new Dictionary<SettingDefinition, double>
            {
                { AppSettings.AirportFromZoomSetting, zoomRangeView.GetValue(0) },
                { AppSettings.AirportToZoomSetting, zoomRangeView.GetValue(1) },
            });

            airportsPreview?.Invalidate();
        }

        /// <summary>
        /// Adds a group of check boxes for a setting stored as a bit mask. Every change is saved at once and can be undone with the undo button.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddMultiChoice(LinearLayout root, SettingDefinition definition)
        {
            LinearLayout? sharedCard = GetSharedCardFor(definition, root);
            bool isGrouped = sharedCard != null;
            LinearLayout card = sharedCard ?? CreateCard(root);

            LinearLayout titleRow = new LinearLayout(this);
            titleRow.Orientation = Orientation.Horizontal;
            titleRow.SetGravity(GravityFlags.CenterVertical);
            titleRow.SetMinimumHeight(Dp(28));
            titleRow.SetPadding(0, Dp(10), 0, 0);
            card.AddView(titleRow);
            favoritesOnlyInactiveViews.Add(titleRow);

            TextView label = new TextView(this);
            label.Text = definition.Label;
            StyleTitle(label);

            if (isGrouped)
            {
                StyleSubTitle(label);
            }

            titleRow.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddUndoButton(titleRow, definition);
            AddInfoButton(titleRow, definition);

            List<CheckBox> boxes = new List<CheckBox>();
            LinearLayout? boxRow = null;
            bool updating = false;
            IReadOnlyList<string> choices = definition.Choices!;

            for (int index = 0; index < choices.Count; index++)
            {
                if (index % 3 == 0)
                {
                    boxRow = new LinearLayout(this);
                    boxRow.Orientation = Orientation.Horizontal;
                    card.AddView(boxRow);
                    favoritesOnlyInactiveViews.Add(boxRow);
                }

                CheckBox box = new CheckBox(this);
                box.Text = choices[index];
                box.TextSize = 13f;
                box.SetTextColor(PrimaryTextColor);
                boxRow!.AddView(box, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                boxes.Add(box);
            }

            Func<int> readMask = () =>
            {
                int mask = 0;

                for (int index = 0; index < boxes.Count; index++)
                {
                    if (boxes[index].Checked)
                    {
                        mask |= 1 << index;
                    }
                }

                return mask;
            };

            settingReaders[definition] = () => readMask();
            settingWriters[definition] = value =>
            {
                int mask = (int)Math.Round(value);

                updating = true;

                for (int index = 0; index < boxes.Count; index++)
                {
                    boxes[index].Checked = (mask & (1 << index)) != 0;
                }

                updating = false;
            };

            foreach (CheckBox box in boxes)
            {
                box.CheckedChange += (sender, args) =>
                {
                    if (updating)
                    {
                        return;
                    }

                    double mask = readMask();
                    double saved = AppSettings.Get(definition);

                    if (Differs(mask, saved))
                    {
                        SaveValue(definition, mask);
                        RecordChange(definition, saved);
                    }
                };
            }
        }

        /// <summary>
        /// Adds a slider for a setting. The label shows the current value and, for the label size, a sample label drawn in that size.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddSlider(LinearLayout root, SettingDefinition definition)
        {
            bool isTrailSetting = AppSettings.TrailSettings.Contains(definition);
            LinearLayout? sharedCard = GetSharedCardFor(definition, root);
            bool isGrouped = sharedCard != null;
            LinearLayout card = sharedCard ?? CreateCard(root);

            LinearLayout titleRow = new LinearLayout(this);
            titleRow.Orientation = Orientation.Horizontal;
            titleRow.SetGravity(GravityFlags.CenterVertical);
            titleRow.SetMinimumHeight(Dp(28));
            card.AddView(titleRow);

            TextView title = new TextView(this);
            title.Text = definition.Label;
            StyleTitle(title);

            if (isGrouped)
            {
                title.TextSize = 13f;
                title.SetTypeface(null, TypefaceStyle.Normal);
                titleRow.SetPadding(0, Dp(10), 0, 0);
            }

            titleRow.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddUndoButton(titleRow, definition);
            AddInfoButton(titleRow, definition);

            TextView valueText = new TextView(this);
            StyleSecondary(valueText);
            valueText.SetTextColor(Color.Rgb(130, 180, 255));
            valueText.SetPadding(0, Dp(4), 0, Dp(4));
            valueText.Clickable = true;
            card.AddView(valueText);

            SeekBar bar = new SeekBar(this);
            bar.Max = (int)Math.Round((definition.Maximum - definition.Minimum) / definition.Step);
            card.AddView(bar);

            TextView? preview = null;

            if (definition == AppSettings.LabelTextSizeSetting)
            {
                preview = new TextView(this);
                preview.SetBackgroundColor(Color.Rgb(40, 44, 52));
                preview.SetTextColor(Color.White);
                preview.SetShadowLayer(3f, 0f, 0f, Color.Black);
                preview.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
                card.AddView(preview);
            }

            System.Action<double> refresh = value =>
            {
                valueText.Text = definition == AppSettings.IconScaleSetting
                    ? string.Format(CultureInfo.InvariantCulture, "×{0}  (domyślnie ×{1})", FormatValue(definition, value), FormatValue(definition, definition.DefaultValue))
                    : string.Format(CultureInfo.InvariantCulture, "{0}{1}  (domyślnie {2})", FormatValue(definition, value), definition.Unit, FormatValue(definition, definition.DefaultValue));
                valueText.Text += "  ✎";

                if (trailsPreview != null && isTrailSetting)
                {
                    if (definition == AppSettings.TrailWidthSetting)
                    {
                        trailsPreview.TrailWidth = (float)value;
                    }
                    else if (definition == AppSettings.TrailMinAlphaSetting)
                    {
                        trailsPreview.TrailMinAlpha = (int)Math.Round(value);
                    }
                    else if (definition == AppSettings.TrailMaxAlphaSetting)
                    {
                        trailsPreview.TrailMaxAlpha = (int)Math.Round(value);
                    }
                    else if (definition == AppSettings.TrailOutlineWidthSetting)
                    {
                        trailsPreview.TrailOutlineWidth = (float)value;
                    }
                    else if (definition == AppSettings.TrailOutlineAlphaSetting)
                    {
                        trailsPreview.TrailOutlineAlpha = (int)Math.Round(value);
                    }

                    trailsPreview.Invalidate();
                }

                if (aircraftIconsPreview != null && definition == AppSettings.IconScaleSetting)
                {
                    aircraftIconsPreview.IconScale = (float)value;
                }

                if (aircraftIconsPreview != null && definition == AppSettings.AircraftOutlineWidthSetting)
                {
                    aircraftIconsPreview.OutlineWidth = (float)value;
                }

                if (aircraftIconsPreview != null && definition == AppSettings.AircraftShadowSetting)
                {
                    aircraftIconsPreview.ShadowStrength = (float)(value / 100.0);
                }

                if (aircraftIconsPreview != null && definition == AppSettings.AircraftCockpitSetting)
                {
                    aircraftIconsPreview.CockpitStrength = (float)(value / 100.0);
                }

                preview?.SetTextSize(Android.Util.ComplexUnitType.Dip, (float)value);
                preview?.Text = "LOT1234 " + AltitudeFormatter.Format(35000);
                UpdateUndoVisibility(definition);
            };

            double current = definition.DefaultValue;

            System.Action<double> setValue = value =>
            {
                current = Math.Clamp(value, definition.Minimum, definition.Maximum);
                bar.Progress = (int)Math.Round((current - definition.Minimum) / definition.Step);
                refresh(current);
            };

            double gestureStart = definition.DefaultValue;

            bar.StartTrackingTouch += (sender, args) => gestureStart = AppSettings.Get(definition);

            bar.ProgressChanged += (sender, args) =>
            {
                if (args.FromUser)
                {
                    current = definition.Minimum + args.Progress * definition.Step;
                    refresh(current);
                    SaveValue(definition, current);
                }
            };

            bar.StopTrackingTouch += (sender, args) =>
            {
                if (Differs(gestureStart, AppSettings.Get(definition)))
                {
                    RecordChange(definition, gestureStart);
                }
            };

            System.Action<double> commitValue = value =>
            {
                double previous = AppSettings.Get(definition);

                setValue(value);
                SaveValue(definition, current);

                if (Differs(previous, AppSettings.Get(definition)))
                {
                    RecordChange(definition, previous);
                }
            };

            valueText.Click += (sender, args) => ShowExactValueDialog(definition, current, commitValue);

            settingReaders[definition] = () => current;
            settingWriters[definition] = setValue;
        }

        /// <summary>
        /// Opens a dialog in which the exact value of a slider setting can be typed, for example a value between two slider positions.
        /// </summary>
        /// <param name="definition">Slider setting.</param>
        /// <param name="current">Current value.</param>
        /// <param name="apply">Action that applies the typed value to the slider.</param>
        private void ShowExactValueDialog(SettingDefinition definition, double current, System.Action<double> apply)
        {
            EditText input = new EditText(this);
            input.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            input.SetSingleLine(true);
            input.Text = FormatValue(definition, current);
            input.SetSelectAllOnFocus(true);

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(definition.Label + " (" + FormatValue(definition, definition.Minimum) + "–" + FormatValue(definition, definition.Maximum) + definition.Unit + ")");
            builder.SetView(input);
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.SetPositiveButton("OK", (sender, args) =>
            {
                string text = (input.Text ?? string.Empty).Trim().Replace(',', '.');

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed))
                {
                    apply(typed);
                }
            });
            builder.Show();
        }

        /// <summary>
        /// Connects one threshold setting to the shared altitude scale editor, creating the editor with its header at the first threshold.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Threshold setting.</param>
        private void AddScaleThreshold(LinearLayout root, SettingDefinition definition)
        {
            if (altitudeScale == null)
            {
                LinearLayout card = GetAltitudeCard(root);

                TextView title = new TextView(this);
                title.Text = "Skala kolorów";
                StyleSubTitle(title);
                title.SetPadding(0, Dp(10), 0, 0);
                card.AddView(title);

                TextView hint = new TextView(this);
                hint.Text = "Przeciągnij znacznik, aby zmienić próg, albo dotknij go, aby wpisać wartość. Wartości są w jednostce wybranej wyżej.";
                StyleSecondary(hint);
                hint.SetPadding(0, Dp(4), 0, 0);
                card.AddView(hint);

                altitudeScale = new AltitudeScaleView(this);
                altitudeScale.ValuesChanged += SaveAltitudeScale;
                card.AddView(altitudeScale);

                Button resetColors = new Button(this);
                resetColors.Text = "Przywróć domyślne kolory";
                resetColors.Click += (sender, args) => ConfirmReset(resetColors.Text ?? string.Empty, () =>
                {
                    ResetSettings(altitudeScaleSettings);
                    Toast.MakeText(this, "Przywrócono domyślne kolory", ToastLength.Short)?.Show();
                });
                card.AddView(resetColors);
            }

            AltitudeScaleView scale = altitudeScale;
            int index = altitudeScaleThresholds++;

            altitudeScaleSettings.Add(definition);

            settingReaders[definition] = () => scale.GetValue(index);
            settingWriters[definition] = value => scale.SetValue(index, value);
        }

        /// <summary>
        /// Returns the card shared by the aircraft icon size and outline sliders, creating it with its title and the preview of all icons at the first call. The preview is placed above the sliders, so it stays visible under the finger while a slider is dragged.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetAircraftIconCard(LinearLayout root)
        {
            if (aircraftIconCard != null)
            {
                return aircraftIconCard;
            }

            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Ikony i etykiety samolotów";
            StyleTitle(title);
            card.AddView(title);

            aircraftIconsPreview = new AircraftIconsPreview(this);

            LinearLayout.LayoutParams previewParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            previewParams.SetMargins(0, Dp(8), 0, 0);
            card.AddView(aircraftIconsPreview, previewParams);

            aircraftIconCard = card;

            return card;
        }

        /// <summary>
        /// Returns the card shared by all trail settings, creating it with its title and the trail preview at the first call. The preview is placed above the controls, so it stays visible while a slider is dragged.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetTrailCard(LinearLayout root)
        {
            if (trailCard != null)
            {
                return trailCard;
            }

            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Ślady samolotów";
            StyleTitle(title);
            card.AddView(title);

            TextView hint = new TextView(this);
            hint.Text = "Podgląd pokazuje ślady drona, śmigłowca i samolotu pasażerskiego z bieżącymi ustawieniami. Dotknij niebieskiej wartości pod nazwą suwaka (✎), aby wpisać dokładną liczbę.";
            StyleSecondary(hint);
            hint.SetPadding(0, Dp(4), 0, 0);
            card.AddView(hint);

            trailsPreview = new TrailsPreview(this);

            LinearLayout.LayoutParams previewParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            previewParams.SetMargins(0, Dp(8), 0, 0);
            card.AddView(trailsPreview, previewParams);

            trailCard = card;

            return card;
        }

        /// <summary>
        /// Returns the card shared by the tile cache settings, creating it at the first call with its title, the current size of the cache and the button that clears it.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetTileCacheCard(LinearLayout root)
        {
            if (tileCacheCard != null)
            {
                return tileCacheCard;
            }

            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Pamięć podręczna map";
            StyleTitle(title);
            card.AddView(title);

            tileCacheUsageText = new TextView(this);
            StyleSecondary(tileCacheUsageText);
            tileCacheUsageText.SetPadding(0, Dp(4), 0, 0);
            card.AddView(tileCacheUsageText);

            Button clear = new Button(this);
            clear.Text = "Wyczyść teraz";
            clear.Click += (sender, args) =>
            {
                clear.Enabled = false;

                Task.Run(() =>
                {
                    AppServices.TileDisk.Clear();
                    RunOnUiThread(() =>
                    {
                        clear.Enabled = true;
                        UpdateTileCacheUsage();
                        Toast.MakeText(this, "Wyczyszczono pamięć podręczną map", ToastLength.Short)?.Show();
                    });
                });
            };
            card.AddView(clear);

            tileCacheCard = card;
            UpdateTileCacheUsage();

            return card;
        }

        /// <summary>
        /// Returns the card shared by the settings of the flight information cache, creating it at the first call. It shows how many flights and aircraft are stored and has a button that forgets them.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetFlightCacheCard(LinearLayout root)
        {
            if (flightCacheCard != null)
            {
                return flightCacheCard;
            }

            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Pamięć podręczna lotów";
            StyleTitle(title);
            card.AddView(title);

            flightCacheUsageText = new TextView(this);
            StyleSecondary(flightCacheUsageText);
            flightCacheUsageText.SetPadding(0, Dp(4), 0, 0);
            card.AddView(flightCacheUsageText);

            Button clear = new Button(this);
            clear.Text = "Wyczyść teraz";
            clear.Click += (sender, args) =>
            {
                AppServices.ClearFlightInfo();
                UpdateFlightCacheUsage();
                Toast.MakeText(this, "Wyczyszczono pamięć podręczną lotów", ToastLength.Short)?.Show();
            };
            card.AddView(clear);

            flightCacheCard = card;
            UpdateFlightCacheUsage();

            return card;
        }

        /// <summary>
        /// Returns the card shared by the settings of the developer log, creating it at the first call. Besides the settings it has a button that opens the log with the current number of entries.
        /// </summary>
        /// <param name="root">Container in which the card is created.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetDevLogCard(LinearLayout root)
        {
            if (devLogCard != null)
            {
                return devLogCard;
            }

            LinearLayout card = CreateCard(root);

            TextView title = new TextView(this);
            title.Text = "Dev log";
            StyleTitle(title);
            card.AddView(title);

            devLogButton = new Button(this);
            devLogButton.Click += (sender, args) => StartActivity(new Android.Content.Intent(this, typeof(DevLogActivity)));
            card.AddView(devLogButton);

            devLogCard = card;
            UpdateDevLogButton();

            return card;
        }

        /// <summary>
        /// Returns the card with the state of the aircraft and route sources, creating it at the first call.
        /// </summary>
        /// <param name="root">Container in which the card is created.</param>
        /// <returns>The card.</returns>
        private LinearLayout GetSourceStatusCard(LinearLayout root)
        {
            LinearLayout card = GetTitledCard("sourceStatus", "Stan źródeł", root);

            if (sourceStatusText == null)
            {
                sourceStatusText = new TextView(this);
                StyleSecondary(sourceStatusText);
                sourceStatusText.SetPadding(0, Dp(4), 0, 0);
                card.AddView(sourceStatusText);

                Button requestLog = new Button(this);
                requestLog.Text = "Pokaż log zapytań";
                requestLog.Click += (sender, args) => StartActivity(new Android.Content.Intent(this, typeof(RequestLogActivity)));
                card.AddView(requestLog);

                sourceStatusRefresh = new Java.Lang.Runnable(RefreshSourceStatus);
            }

            return card;
        }

        /// <summary>
        /// Shows the current state of the data sources and schedules the next refresh, so that the remaining pause counts down.
        /// </summary>
        private void RefreshSourceStatus()
        {
            if (sourceStatusText == null || sourceStatusRefresh == null)
            {
                return;
            }

            sourceStatusText.Text = SourceStateText.Format(AppServices.GetSourceStates(), DateTime.UtcNow);
            sourceStatusText.RemoveCallbacks(sourceStatusRefresh);
            sourceStatusText.PostDelayed(sourceStatusRefresh, 1000);
        }

        /// <summary>
        /// Refreshes the text of the button that opens the developer log with the number of entries.
        /// </summary>
        private void UpdateDevLogButton()
        {
            devLogButton?.Text = "Otwórz Dev log (" + DevLogger.Store.Log.Count + ")";
        }

        /// <summary>
        /// Shows the number of stored flights and aircraft next to the retention time.
        /// </summary>
        private void UpdateFlightCacheUsage()
        {
            flightCacheUsageText?.Text = string.Format(CultureInfo.CurrentCulture, "Zapisane informacje: {0} (trasy i dane samolotów, razem z odpowiedziami „nieznany”)", AppServices.FlightInfoCount);
        }

        /// <summary>
        /// Counts the tiles on the disk in the background and shows their number and size next to the limit.
        /// </summary>
        private void UpdateTileCacheUsage()
        {
            Task.Run(() =>
            {
                (long bytes, int count) = AppServices.TileDisk.GetUsage();

                RunOnUiThread(() =>
                {
                    if (tileCacheUsageText == null)
                    {
                        return;
                    }

                    tileCacheUsageText.Text = string.Format(CultureInfo.CurrentCulture, "Zajęte: {0:0.0} MB z {1:0} MB ({2} kafelków)", bytes / 1048576.0, AppSettings.TileCacheMaxBytes / 1048576.0, count);
                });
            });
        }

        /// <summary>
        /// Applies the style of a setting title: white, bold, the same size everywhere.
        /// </summary>
        /// <param name="view">Text view, switch or other text control.</param>
        private static void StyleTitle(TextView view)
        {
            view.TextSize = 15f;
            view.SetTextColor(PrimaryTextColor);
            view.SetTypeface(null, TypefaceStyle.Bold);
        }

        /// <summary>
        /// Applies the style of the name of a setting inside a card shared by several settings: white, regular and smaller than a card title.
        /// </summary>
        /// <param name="view">Text view, switch or other text control.</param>
        private static void StyleSubTitle(TextView view)
        {
            view.TextSize = 13f;
            view.SetTextColor(PrimaryTextColor);
            view.SetTypeface(null, TypefaceStyle.Normal);
        }

        /// <summary>
        /// Returns the card shared by the airport settings, creating it at the first call. The first control added to it is the main switch, followed by the preview of the airport categories.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The shared card.</returns>
        private LinearLayout GetAirportSettingsCard(LinearLayout root)
        {
            if (airportSettingsCard != null)
            {
                return airportSettingsCard;
            }

            LinearLayout card = CreateCard(root);

            airportSettingsCard = card;

            return card;
        }

        /// <summary>
        /// Applies the style of a secondary text such as a hint or the current value: gray and small.
        /// </summary>
        /// <param name="view">Text view.</param>
        private static void StyleSecondary(TextView view)
        {
            view.TextSize = 12f;
            view.SetTextColor(SecondaryTextColor);
        }

        /// <summary>
        /// Adds a framed card to the screen. It visually groups the controls of one setting or of a related set of settings.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <returns>The new card.</returns>
        private LinearLayout CreateCard(LinearLayout root)
        {
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));

            GradientDrawable background = new GradientDrawable();
            background.SetColor(Color.Argb(28, 255, 255, 255));
            background.SetStroke(Dp(1), Color.Argb(70, 255, 255, 255));
            background.SetCornerRadius(Dp(12));
            card.Background = background;

            LinearLayout.LayoutParams cardParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            cardParams.SetMargins(0, Dp(5), 0, Dp(5));
            root.AddView(card, cardParams);

            return card;
        }

        /// <summary>
        /// Creates a small round button with a symbol, used for the undo and info buttons.
        /// </summary>
        /// <param name="symbol">Text drawn in the button.</param>
        /// <param name="description">Description for accessibility services.</param>
        /// <returns>The button, not yet added to any layout.</returns>
        private TextView CreateRoundButton(string symbol, string description)
        {
            TextView button = new TextView(this);
            button.Text = symbol;
            button.TextSize = 14f;
            button.SetTextColor(Color.White);
            button.Gravity = GravityFlags.Center;
            button.ContentDescription = description;

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(Color.Argb(60, 255, 255, 255));
            circle.SetStroke(Dp(1), Color.Argb(150, 255, 255, 255));
            button.Background = circle;

            return button;
        }

        /// <summary>
        /// Shows the undo button of a setting only when the value in its control differs from the saved one.
        /// </summary>
        /// <param name="definition">Setting.</param>
        private void UpdateUndoVisibility(SettingDefinition definition)
        {
            if (!undoButtons.TryGetValue(definition, out View? button))
            {
                return;
            }

            button.Visibility = SettingsHistory.Count(definition.Key) > 0 ? ViewStates.Visible : ViewStates.Invisible;
        }

        /// <summary>
        /// Tells whether two setting values differ by more than the rounding of the stored float.
        /// </summary>
        /// <param name="first">First value.</param>
        /// <param name="second">Second value.</param>
        /// <returns>True when the values differ.</returns>
        private static bool Differs(double first, double second)
        {
            return Math.Abs(first - second) > 0.000001;
        }

        /// <summary>
        /// Saves a setting at once, so that the map, on the phone and in the car, uses the new value immediately.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <param name="value">New value.</param>
        private void SaveValue(SettingDefinition definition, double value)
        {
            AppSettings.Save(new Dictionary<SettingDefinition, double> { { definition, value } });

            if (definition == AppSettings.TileCacheSizeSetting)
            {
                Task.Run(() =>
                {
                    AppServices.TileDisk.Trim();
                    UpdateTileCacheUsage();
                });
            }

            if (definition == AppSettings.AirportTypesSetting || definition == AppSettings.ShowAirportsSetting || definition == AppSettings.ShowOnlyFavoriteAirportsSetting || definition == AppSettings.DataSourceFallbackSetting || definition == AppSettings.PrivacyDevOverrideSetting)
            {
                UpdateDependentControls();
            }

            if (AppSettings.AirportSettings.Contains(definition) || definition == AppSettings.IconScaleSetting)
            {
                airportsPreview?.Invalidate();
            }

            if (definition == AppSettings.AltitudeUnitSetting)
            {
                altitudeScale?.Invalidate();

                if (settingWriters.TryGetValue(AppSettings.LabelTextSizeSetting, out System.Action<double>? refreshLabelPreview))
                {
                    refreshLabelPreview(AppSettings.Get(AppSettings.LabelTextSizeSetting));
                }
            }
        }

        /// <summary>
        /// Enables or disables the controls that depend on other settings. The sources below the main one in the source order are dimmed while the fallback is switched off. When the airports are switched off, everything in their section except the main switch and the info buttons is grayed out; when only the favorite airports are shown, the categories, the paved runway filter and the zoom range are grayed out too. The paved runway filter additionally works only while the small airports are switched on. Info buttons stay active, because they explain the settings.
        /// </summary>
        private void UpdateDependentControls()
        {
            sourceOrderView?.SetFallbackEnabled(AppSettings.DataSourceFallback);
            routeOrderView?.SetFallbackEnabled(AppSettings.DataSourceFallback);

            bool privacyOverride = AppSettings.Get(AppSettings.PrivacyDevOverrideSetting) >= 0.5;

            if (titledCards.TryGetValue("privacyDev", out LinearLayout? privacyDevCard))
            {
                for (int index = 2; index < privacyDevCard.ChildCount - 1; index++)
                {
                    View? child = privacyDevCard.GetChildAt(index);

                    if (child != null)
                    {
                        ApplyGroupState(child, privacyOverride);
                    }
                }
            }

            bool airportsOn = AppSettings.ShowAirports;

            if (airportSettingsCard != null)
            {
                for (int index = 1; index < airportSettingsCard.ChildCount; index++)
                {
                    View? child = airportSettingsCard.GetChildAt(index);

                    if (child != null)
                    {
                        ApplyGroupState(child, airportsOn);
                    }
                }
            }

            bool onlyFavorites = AppSettings.ShowOnlyFavoriteAirports;

            if (airportsOn && onlyFavorites)
            {
                foreach (View inactive in favoritesOnlyInactiveViews)
                {
                    ApplyGroupState(inactive, false);
                }
            }

            if (smallPavedToggle == null)
            {
                return;
            }

            bool smallOn = AppSettings.IsAirportCategoryEnabled(AirportCategory.Small);
            bool available = airportsOn && !onlyFavorites && smallOn;

            smallPavedToggle.Enabled = available;
            smallPavedToggle.Alpha = available ? 1.0f : 0.45f;

            if (smallPavedHint != null)
            {
                smallPavedHint.Visibility = airportsOn && !onlyFavorites && !smallOn ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        /// <summary>
        /// Enables or dims a view and everything inside it, leaving the info buttons untouched.
        /// </summary>
        /// <param name="view">View to change.</param>
        /// <param name="on">Whether the controls are active.</param>
        /// <returns>True when the view is or contains an info button.</returns>
        private static bool ApplyGroupState(View view, bool on)
        {
            if (view.ContentDescription != null && view.ContentDescription.StartsWith("Opis ustawienia", StringComparison.Ordinal))
            {
                return true;
            }

            bool containsInfoButton = false;

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; index++)
                {
                    View? child = group.GetChildAt(index);

                    if (child != null && ApplyGroupState(child, on))
                    {
                        containsInfoButton = true;
                    }
                }
            }

            view.Enabled = on;

            if (!containsInfoButton)
            {
                view.Alpha = on ? 1.0f : 0.4f;
            }

            return containsInfoButton;
        }

        /// <summary>
        /// Remembers the value a setting had before it was changed, so that the change can be undone, and shows the undo button.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <param name="previous">Value before the change.</param>
        private void RecordChange(SettingDefinition definition, double previous)
        {
            SettingsHistory.Push(definition.Key, previous);
            UpdateUndoVisibility(definition);
        }

        /// <summary>
        /// Undoes the last change of a setting: saves the previous value and puts it into the control.
        /// </summary>
        /// <param name="definition">Setting.</param>
        private void UndoSetting(SettingDefinition definition)
        {
            if (!SettingsHistory.TryPop(definition.Key, out double previous))
            {
                return;
            }

            SaveValue(definition, previous);

            if (settingWriters.TryGetValue(definition, out System.Action<double>? write))
            {
                write(AppSettings.Get(definition));
            }

            UpdateUndoVisibility(definition);
        }

        /// <summary>
        /// Saves the thresholds of the altitude scale editor after the user changed them.
        /// </summary>
        private void SaveAltitudeScale()
        {
            if (altitudeScale == null)
            {
                return;
            }

            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>();

            for (int index = 0; index < altitudeScaleSettings.Count; index++)
            {
                values[altitudeScaleSettings[index]] = altitudeScale.GetValue(index);
            }

            AppSettings.Save(values);
        }

        /// <summary>
        /// Restores the default values of the given settings, saves them at once and remembers the previous values so that the reset can be undone.
        /// </summary>
        /// <param name="definitions">Settings to reset.</param>
        private void ResetSettings(IEnumerable<SettingDefinition> definitions)
        {
            foreach (SettingDefinition definition in definitions.ToList())
            {
                double previous = AppSettings.Get(definition);

                if (Differs(previous, definition.DefaultValue))
                {
                    SaveValue(definition, definition.DefaultValue);
                    RecordChange(definition, previous);
                }

                if (settingWriters.TryGetValue(definition, out System.Action<double>? write))
                {
                    write(AppSettings.Get(definition));
                }
            }
        }

        /// <summary>
        /// Creates a button that restores the default values of a group of settings.
        /// </summary>
        /// <param name="text">Text of the button.</param>
        /// <param name="definitions">Settings restored by the button.</param>
        /// <param name="message">Message shown after the reset.</param>
        /// <returns>The button, not yet added to any layout.</returns>
        private Button CreateResetButton(string text, IReadOnlyList<SettingDefinition> definitions, string message)
        {
            Button button = new Button(this);
            button.Text = text;
            button.Click += (sender, args) => ConfirmReset(text, () =>
            {
                ResetSettings(definitions);
                Toast.MakeText(this, message, ToastLength.Short)?.Show();
            });

            return button;
        }

        /// <summary>
        /// Asks the user to confirm a reset to the default values. The confirming button is disabled for the first ten seconds and shows the countdown, so that the reset cannot be made by an accidental tap.
        /// </summary>
        /// <param name="title">Title of the dialog.</param>
        /// <param name="reset">Action performed after the confirmation.</param>
        private void ConfirmReset(string title, System.Action reset)
        {
            const int DelaySeconds = 10;
            const string ConfirmText = "Przywróć domyślne wartości";

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(title);
            builder.SetMessage("Czy na pewno chcesz przywrócić domyślne wartości? Obecne wartości zostaną zastąpione (każdą zmianę można potem cofnąć przyciskiem ↺).");
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.SetPositiveButton(ConfirmText, (sender, args) => reset());

            AlertDialog dialog = builder.Create()!;
            dialog.Show();

            Android.Widget.Button? confirm = dialog.GetButton((int)Android.Content.DialogButtonType.Positive);

            if (confirm == null)
            {
                return;
            }

            Handler handler = new Handler(Looper.MainLooper!);
            bool closed = false;
            int remaining = DelaySeconds;
            System.Action? tick = null;

            dialog.DismissEvent += (sender, args) => closed = true;

            tick = () =>
            {
                if (closed)
                {
                    return;
                }

                if (remaining > 0)
                {
                    confirm.Enabled = false;
                    confirm.Text = ConfirmText + " (" + remaining + ")";
                    remaining--;
                    handler.PostDelayed(tick, 1000);
                }
                else
                {
                    confirm.Text = ConfirmText;
                    confirm.Enabled = true;
                }
            };

            tick();
        }

        /// <summary>
        /// Adds a round "undo" button that puts the last saved value of the setting back into its control. Nothing is saved until the Save button is pressed.
        /// </summary>
        /// <param name="row">Row to which the button is added.</param>
        /// <param name="definition">Setting.</param>
        private void AddUndoButton(LinearLayout row, SettingDefinition definition)
        {
            TextView button = CreateRoundButton("↺", "Cofnij do zapisanej wartości: " + definition.Label);
            button.Visibility = ViewStates.Invisible;
            undoButtons[definition] = button;

            button.Click += (sender, args) => UndoSetting(definition);

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(28), Dp(28));
            parameters.SetMargins(Dp(12), 0, 0, 0);
            row.AddView(button, parameters);
        }

        /// <summary>
        /// Adds a round "i" button that opens a popup with the description of the setting. Does nothing when the setting has no description.
        /// </summary>
        /// <param name="row">Row to which the button is added.</param>
        /// <param name="definition">Setting.</param>
        private void AddInfoButton(LinearLayout row, SettingDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.Help))
            {
                return;
            }

            TextView button = CreateRoundButton("i", "Opis ustawienia: " + definition.Label);
            button.SetTypeface(null, TypefaceStyle.BoldItalic);

            button.Click += (sender, args) =>
            {
                LinearLayout content = new LinearLayout(this);
                content.Orientation = Orientation.Vertical;
                content.SetPadding(Dp(24), Dp(8), Dp(24), 0);

                TextView description = new TextView(this);
                description.Text = definition.Help;
                description.TextSize = 14f;
                content.AddView(description);

                View? example = BuildHelpExample(definition);

                if (example != null)
                {
                    TextView caption = new TextView(this);
                    caption.Text = "Przykład:";
                    caption.TextSize = 12f;
                    caption.SetTextColor(Color.Rgb(180, 180, 180));
                    caption.SetPadding(0, Dp(16), 0, Dp(6));
                    content.AddView(caption);
                    content.AddView(example);
                }

                ScrollView scroll = new ScrollView(this);
                scroll.AddView(content);

                AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
                builder.SetTitle(definition.Label);
                builder.SetView(scroll);
                builder.SetPositiveButton("OK", (dialogSender, dialogArgs) => { });
                builder.Show();
            };

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(28), Dp(28));
            parameters.SetMargins(Dp(12), 0, 0, 0);
            row.AddView(button, parameters);
        }

        /// <summary>
        /// Builds the visual example shown in the help popup of a setting, using the current settings.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <returns>View with the example, or null when the setting has no example.</returns>
        private View? BuildHelpExample(SettingDefinition definition)
        {
            if (definition == AppSettings.ShowLabelsSetting)
            {
                TextView label = new TextView(this);
                label.Text = "LOT1234 " + AltitudeFormatter.Format(35000) + "\nB738 WAW→LHR";
                label.SetTextSize(Android.Util.ComplexUnitType.Dip, AppSettings.LabelTextSize);
                label.SetTextColor(Color.White);
                label.SetShadowLayer(3f, 0f, 0f, Color.Black);
                label.SetBackgroundColor(Color.Rgb(40, 44, 52));
                label.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));

                return label;
            }

            if (definition == AppSettings.ShowAltitudeLegendSetting)
            {
                return new AltitudeLegendPreview(this);
            }

            if (AppSettings.AirportSettings.Contains(definition))
            {
                return new AirportsPreview(this);
            }

            if (definition == AppSettings.AltitudeUnitSetting)
            {
                AltitudeUnit[] units = new AltitudeUnit[] { AltitudeUnit.Metric, AltitudeUnit.Feet, AltitudeUnit.FlightLevel };

                return BuildUnitExample(
                    "Samolot na wysokości 35 000 ft:",
                    new string[] { "Metry", "Stopy", "Poziom lotu" },
                    units.Select(unit => AltitudeFormatter.Format(35000, unit)).ToArray(),
                    Array.IndexOf(units, AppSettings.AltitudeUnit));
            }

            if (definition == AppSettings.DistanceUnitSetting)
            {
                DistanceUnit[] units = new DistanceUnit[] { DistanceUnit.Kilometers, DistanceUnit.NauticalMiles, DistanceUnit.Miles };

                return BuildUnitExample(
                    "Odległość 100 km:",
                    new string[] { "Kilometry", "Mile morskie", "Mile lądowe" },
                    units.Select(unit => UnitFormatter.FormatDistance(100.0, unit, 0)).ToArray(),
                    Array.IndexOf(units, AppSettings.DistanceUnit));
            }

            if (definition == AppSettings.SpeedUnitSetting)
            {
                SpeedUnit[] units = new SpeedUnit[] { SpeedUnit.KilometersPerHour, SpeedUnit.Knots, SpeedUnit.MilesPerHour };

                return BuildUnitExample(
                    "Prędkość 400 kt:",
                    new string[] { "Kilometry na godzinę", "Węzły", "Mile na godzinę" },
                    units.Select(unit => UnitFormatter.FormatSpeed(400.0, unit)).ToArray(),
                    Array.IndexOf(units, AppSettings.SpeedUnit));
            }

            if (definition == AppSettings.VerticalSpeedUnitSetting)
            {
                VerticalSpeedUnit[] units = new VerticalSpeedUnit[] { VerticalSpeedUnit.FeetPerMinute, VerticalSpeedUnit.MetersPerSecond };

                return BuildUnitExample(
                    "Wznoszenie 1 500 ft/min:",
                    new string[] { "Stopy na minutę", "Metry na sekundę" },
                    units.Select(unit => UnitFormatter.FormatVerticalSpeed(1500.0, unit)).ToArray(),
                    Array.IndexOf(units, AppSettings.VerticalSpeedUnit));
            }

            return null;
        }

        /// <summary>
        /// Builds the example of a unit setting: the same value written in every unit, with the chosen one highlighted.
        /// </summary>
        /// <param name="heading">Description of the example value.</param>
        /// <param name="names">Names of the units.</param>
        /// <param name="values">The example value written in each unit, in the order of the names.</param>
        /// <param name="selected">Index of the unit currently chosen.</param>
        /// <returns>View with one row per unit.</returns>
        private View BuildUnitExample(string heading, string[] names, string[] values, int selected)
        {
            LinearLayout rows = new LinearLayout(this);
            rows.Orientation = Orientation.Vertical;
            rows.SetBackgroundColor(Color.Rgb(40, 44, 52));
            rows.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));

            TextView headingView = new TextView(this);
            headingView.Text = heading;
            headingView.TextSize = 12f;
            headingView.SetTextColor(Color.Rgb(180, 180, 180));
            rows.AddView(headingView);

            for (int index = 0; index < names.Length; index++)
            {
                TextView row = new TextView(this);
                row.Text = names[index] + ":  " + values[index];
                row.TextSize = 15f;
                row.SetPadding(0, Dp(6), 0, 0);
                row.SetTextColor(index == selected ? Color.White : Color.Rgb(150, 150, 150));
                row.SetTypeface(null, index == selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                rows.AddView(row);
            }

            return rows;
        }

        /// <summary>
        /// Puts the current setting values into the controls.
        /// </summary>
        private void LoadSettingInputs()
        {
            foreach (KeyValuePair<SettingDefinition, System.Action<double>> entry in settingWriters)
            {
                entry.Value(AppSettings.Get(entry.Key));
                UpdateUndoVisibility(entry.Key);
            }
        }

        /// <summary>
        /// Formats a setting value for display in a text field.
        /// </summary>
        /// <param name="definition">Setting.</param>
        /// <param name="value">Value.</param>
        /// <returns>Text with a decimal point.</returns>
        private static string FormatValue(SettingDefinition definition, double value)
        {
            return value.ToString(definition.IsInteger ? "0" : "0.##", CultureInfo.InvariantCulture);
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

using AdsbCarMap.Localization;
using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Editor of one alert: name, area, filter, events and notification settings, with a button that counts the aircraft matching the alert right now.
    /// </summary>
    [Activity(Label = "Alert", Exported = false, Theme = "@android:style/Theme.Material", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class AlertEditorActivity : Activity
    {
        /// <summary>
        /// Name of the intent extra with the identifier of the rule to edit. Without it a new rule is created.
        /// </summary>
        public const string EditedIdExtra = "alert_edited_id";

        /// <summary>
        /// Smallest radius of the area in the distance unit from the settings.
        /// </summary>
        private const int MinimumRadius = 1;

        /// <summary>
        /// Largest radius of the area in kilometers.
        /// </summary>
        private const double MaximumRadiusKilometers = 300.0;

        /// <summary>
        /// Default radius of a new alert in kilometers.
        /// </summary>
        private const double DefaultRadiusKilometers = 50.0;

        /// <summary>
        /// Distance unit in which the radius is chosen, read when the editor is created.
        /// </summary>
        private readonly DistanceUnit distanceUnit = AppSettings.DistanceUnit;

        /// <summary>
        /// Default time of silence about the same aircraft in minutes.
        /// </summary>
        private const int DefaultCooldown = 30;

        /// <summary>
        /// Start of the quiet hours offered when the user switches them on, as minutes of the day (22:00).
        /// </summary>
        private const int DefaultQuietFromMinute = 22 * 60;

        /// <summary>
        /// End of the quiet hours offered when the user switches them on, as minutes of the day (06:00).
        /// </summary>
        private const int DefaultQuietToMinute = 6 * 60;

        /// <summary>
        /// Notification kinds in the order of the list.
        /// </summary>
        private static readonly AlertImportance[] ImportanceLevels = new AlertImportance[] { AlertImportance.High, AlertImportance.Default, AlertImportance.Low };

        /// <summary>
        /// Days of the week in the order in which the checkboxes are shown.
        /// </summary>
        private static readonly DayOfWeek[] ScheduleDays = new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        /// <summary>
        /// Identifier of the edited rule, or a new identifier when a rule is being created.
        /// </summary>
        private string ruleId = string.Empty;

        /// <summary>
        /// Whether the edited rule is switched on.
        /// </summary>
        private bool ruleEnabled = true;

        /// <summary>
        /// Address of the aircraft the edited rule watches, or null. The editor does not change it, only keeps it.
        /// </summary>
        private string? watchedHex;

        /// <summary>
        /// Chosen kind of the area.
        /// </summary>
        private AlertAreaKind areaKind = AlertAreaKind.AroundMe;

        /// <summary>
        /// Identifier of the chosen filter, or null for every aircraft.
        /// </summary>
        private string? filterId;

        /// <summary>
        /// Field with the name of the rule.
        /// </summary>
        private EditText? nameField;

        /// <summary>
        /// Text that says which aircraft the rule watches; hidden for other rules.
        /// </summary>
        private TextView? watchedText;

        /// <summary>
        /// Field with the airport code.
        /// </summary>
        private EditText? airportField;

        /// <summary>
        /// Field with the latitude of the center.
        /// </summary>
        private EditText? latitudeField;

        /// <summary>
        /// Field with the longitude of the center.
        /// </summary>
        private EditText? longitudeField;

        /// <summary>
        /// Field with the cooldown in minutes.
        /// </summary>
        private EditText? cooldownField;

        /// <summary>
        /// Slider of the radius.
        /// </summary>
        private SeekBar? radiusBar;

        /// <summary>
        /// Text showing the chosen radius.
        /// </summary>
        private TextView? radiusText;

        /// <summary>
        /// Container of the airport controls.
        /// </summary>
        private LinearLayout? airportPanel;

        /// <summary>
        /// Container of the circle controls.
        /// </summary>
        private LinearLayout? circlePanel;

        /// <summary>
        /// List of the notification kinds, in the order of <see cref="ImportanceLevels"/>.
        /// </summary>
        private Spinner? importanceSpinner;

        /// <summary>
        /// Container of the polygon controls.
        /// </summary>
        private LinearLayout? polygonPanel;

        /// <summary>
        /// Text describing the drawn polygon.
        /// </summary>
        private TextView? polygonText;

        /// <summary>
        /// Vertices of the drawn polygon or rectangle, empty when nothing was drawn yet.
        /// </summary>
        private List<GeoPoint> polygonVertices = new List<GeoPoint>();

        /// <summary>
        /// Checkboxes of the days of the week on which the alert may notify, in the order of <see cref="ScheduleDays"/>.
        /// </summary>
        private readonly List<CheckBox> dayBoxes = new List<CheckBox>();

        /// <summary>
        /// Checkbox that switches the quiet hours on.
        /// </summary>
        private CheckBox? quietBox;

        /// <summary>
        /// Button that chooses the start of the quiet hours.
        /// </summary>
        private Button? quietFromButton;

        /// <summary>
        /// Button that chooses the end of the quiet hours.
        /// </summary>
        private Button? quietToButton;

        /// <summary>
        /// Start of the quiet hours as minutes of the day.
        /// </summary>
        private int quietFromMinute = DefaultQuietFromMinute;

        /// <summary>
        /// End of the quiet hours as minutes of the day.
        /// </summary>
        private int quietToMinute = DefaultQuietToMinute;

        /// <summary>
        /// List of the filters.
        /// </summary>
        private Spinner? filterSpinner;

        /// <summary>
        /// Identifiers of the filters in the order of the spinner items, without the first item "every aircraft".
        /// </summary>
        private List<string> spinnerFilterIds = new List<string>();

        /// <summary>
        /// Set while the screen fills the spinner, so that the selection event is ignored.
        /// </summary>
        private bool fillingSpinner;

        /// <summary>
        /// Checkbox of the entry event.
        /// </summary>
        private CheckBox? entryBox;

        /// <summary>
        /// Checkbox of the appearance event.
        /// </summary>
        private CheckBox? appearanceBox;

        /// <summary>
        /// Checkbox of the exit event.
        /// </summary>
        private CheckBox? exitBox;

        /// <summary>
        /// Builds the screen and loads the edited rule.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Alert");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());

            string? editedId = Intent?.GetStringExtra(EditedIdExtra);
            AlertRule? edited = editedId == null ? null : AppServices.Alerts.Find(editedId);

            if (edited != null)
            {
                LoadRule(edited);
            }
            else
            {
                ruleId = AlertRuleStore.CreateId();
                radiusBar!.Progress = RadiusToProgress(DefaultRadiusKilometers);
                cooldownField!.Text = DefaultCooldown.ToString(CultureInfo.InvariantCulture);
            }

            UpdateAreaPanels();
            UpdateRadiusText();
        }

        /// <summary>
        /// Refreshes the list of filters, which may have changed in the filter editor.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            FillFilterSpinner();
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
        /// <returns>Scrollable form with the buttons at the bottom.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            LinearLayout form = new LinearLayout(this);
            form.Orientation = Orientation.Vertical;
            form.SetPadding(Dp(16), Dp(8), Dp(16), Dp(16));
            scrollView.AddView(form);

            AddLabel(form, Loc.T("Nazwa"));
            nameField = new EditText(this);
            nameField.Hint = Loc.T("np. Wojskowe nad domem");
            nameField.SetSingleLine(true);
            form.AddView(nameField);
            watchedText = new TextView(this);
            watchedText.TextSize = 13f;
            watchedText.SetTextColor(Color.Rgb(150, 200, 255));
            watchedText.Visibility = ViewStates.Gone;
            form.AddView(watchedText);

            AddLabel(form, Loc.T("Obszar"));
            RadioGroup areaGroup = new RadioGroup(this);
            areaGroup.AddView(CreateRadio(Loc.T("Wokół mojej pozycji (GPS)"), 1));
            areaGroup.AddView(CreateRadio(Loc.T("Wokół lotniska"), 2));
            areaGroup.AddView(CreateRadio(Loc.T("Okrąg o wybranym środku"), 3));
            areaGroup.AddView(CreateRadio(Loc.T("Wielokąt lub prostokąt narysowany na mapie"), 4));
            areaGroup.Check(1);
            areaGroup.CheckedChange += (sender, args) =>
            {
                areaKind = args.CheckedId == 2 ? AlertAreaKind.AroundAirport : args.CheckedId == 3 ? AlertAreaKind.Circle : args.CheckedId == 4 ? AlertAreaKind.Polygon : AlertAreaKind.AroundMe;
                UpdateAreaPanels();
            };
            form.AddView(areaGroup);

            airportPanel = new LinearLayout(this);
            airportPanel.Orientation = Orientation.Vertical;
            airportField = new EditText(this);
            airportField.Hint = Loc.T("Kod lotniska ICAO lub IATA, np. EPWA");
            airportField.SetSingleLine(true);
            airportField.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapCharacters;
            airportPanel.AddView(airportField);
            Button favoritesButton = new Button(this);
            favoritesButton.Text = Loc.T("Wybierz z ulubionych");
            favoritesButton.Click += (sender, args) => PickFavoriteAirport();
            airportPanel.AddView(favoritesButton);
            form.AddView(airportPanel);

            circlePanel = new LinearLayout(this);
            circlePanel.Orientation = Orientation.Vertical;
            latitudeField = CreateCoordinateField(Loc.T("Szerokość geograficzna, np. 52.2297"));
            longitudeField = CreateCoordinateField(Loc.T("Długość geograficzna, np. 21.0122"));
            circlePanel.AddView(latitudeField);
            circlePanel.AddView(longitudeField);
            Button mapCenterButton = new Button(this);
            mapCenterButton.Text = Loc.T("Użyj środka pobieranych danych");
            mapCenterButton.Click += (sender, args) =>
            {
                (double latitude, double longitude) = LiveTraffic.GetCenter();
                FillCoordinates(latitude, longitude);
            };
            circlePanel.AddView(mapCenterButton);
            Button myPositionButton = new Button(this);
            myPositionButton.Text = Loc.T("Użyj mojej pozycji");
            myPositionButton.Click += (sender, args) =>
            {
                (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();

                if (known)
                {
                    FillCoordinates(latitude, longitude);
                }
                else
                {
                    Toast.MakeText(this, Loc.T("Pozycja GPS nie jest jeszcze znana"), ToastLength.Short)?.Show();
                }
            };
            circlePanel.AddView(myPositionButton);
            Button pickCenterButton = new Button(this);
            pickCenterButton.Text = Loc.T("Wybierz środek na mapie");
            pickCenterButton.Click += (sender, args) => PickCenterOnMap();
            circlePanel.AddView(pickCenterButton);
            form.AddView(circlePanel);

            polygonPanel = new LinearLayout(this);
            polygonPanel.Orientation = Orientation.Vertical;
            polygonText = new TextView(this);
            polygonText.SetPadding(0, Dp(4), 0, Dp(4));
            polygonPanel.AddView(polygonText);
            Button drawButton = new Button(this);
            drawButton.Text = Loc.T("Narysuj na mapie");
            drawButton.Click += (sender, args) => DrawOnMap();
            polygonPanel.AddView(drawButton);
            form.AddView(polygonPanel);

            radiusText = new TextView(this);
            radiusText.SetPadding(0, Dp(8), 0, 0);
            form.AddView(radiusText);
            radiusBar = new SeekBar(this);
            radiusBar.Max = (int)Math.Round(UnitFormatter.KilometersToUnit(MaximumRadiusKilometers, distanceUnit)) - MinimumRadius;
            radiusBar.ProgressChanged += (sender, args) => UpdateRadiusText();
            form.AddView(radiusBar);

            AddLabel(form, Loc.T("Samoloty (filtr)"));
            filterSpinner = new Spinner(this);
            filterSpinner.ItemSelected += (sender, args) =>
            {
                if (!fillingSpinner)
                {
                    filterId = args.Position == 0 ? null : spinnerFilterIds[args.Position - 1];
                }
            };
            form.AddView(filterSpinner);
            Button newFilterButton = new Button(this);
            newFilterButton.Text = Loc.T("＋ Nowy filtr");
            newFilterButton.Click += (sender, args) => StartActivity(new Intent(this, typeof(FilterEditorActivity)));
            form.AddView(newFilterButton);

            AddLabel(form, Loc.T("Powiadom, gdy"));
            entryBox = new CheckBox(this);
            entryBox.Text = Loc.T("samolot wleci w obszar");
            entryBox.Checked = true;
            form.AddView(entryBox);
            appearanceBox = new CheckBox(this);
            appearanceBox.Text = Loc.T("samolot pojawi się w obszarze (np. po starcie)");
            appearanceBox.Checked = true;
            form.AddView(appearanceBox);
            exitBox = new CheckBox(this);
            exitBox.Text = Loc.T("samolot wyleci z obszaru");
            exitBox.Checked = false;
            form.AddView(exitBox);

            AddLabel(form, Loc.T("Cisza o tym samym samolocie (minuty)"));
            cooldownField = new EditText(this);
            cooldownField.InputType = Android.Text.InputTypes.ClassNumber;
            cooldownField.SetSingleLine(true);
            form.AddView(cooldownField);

            AddLabel(form, Loc.T("Rodzaj powiadomienia"));
            importanceSpinner = new Spinner(this);
            List<string> importanceNames = new List<string>();

            foreach (AlertImportance level in ImportanceLevels)
            {
                importanceNames.Add(AlertText.DescribeImportance(level));
            }

            ArrayAdapter<string> importanceAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, importanceNames);
            importanceAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            importanceSpinner.Adapter = importanceAdapter;
            form.AddView(importanceSpinner);

            AddLabel(form, Loc.T("Harmonogram"));
            LinearLayout daysRow = new LinearLayout(this);
            daysRow.Orientation = Orientation.Horizontal;

            foreach (DayOfWeek day in ScheduleDays)
            {
                CheckBox dayBox = new CheckBox(this);
                dayBox.Text = AlertText.ShortDayName(day);
                dayBox.Checked = true;
                dayBoxes.Add(dayBox);
                daysRow.AddView(dayBox, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            }

            form.AddView(daysRow);
            quietBox = new CheckBox(this);
            quietBox.Text = Loc.T("godziny ciszy (bez powiadomień)");
            quietBox.CheckedChange += (sender, args) => UpdateQuietButtons();
            form.AddView(quietBox);
            LinearLayout quietRow = new LinearLayout(this);
            quietRow.Orientation = Orientation.Horizontal;
            quietFromButton = new Button(this);
            quietFromButton.Click += (sender, args) => PickQuietTime(true);
            quietRow.AddView(quietFromButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            quietToButton = new Button(this);
            quietToButton.Click += (sender, args) => PickQuietTime(false);
            quietRow.AddView(quietToButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            form.AddView(quietRow);
            TextView scheduleHint = new TextView(this);
            scheduleHint.Text = Loc.T("Poza wybranymi dniami i w godzinach ciszy alert nie wysyła powiadomień. Samoloty nadal są śledzone, więc po końcu ciszy nie dostaniesz powiadomień o tych, które już tam były.");
            scheduleHint.TextSize = 12f;
            scheduleHint.SetPadding(0, Dp(2), 0, 0);
            form.AddView(scheduleHint);
            UpdateQuietButtons();

            Button testButton = new Button(this);
            testButton.Text = Loc.T("Sprawdź, ile samolotów pasuje teraz");
            testButton.Click += (sender, args) => RunTest();
            form.AddView(testButton);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(Dp(16), Dp(4), Dp(16), Dp(8));
            root.AddView(buttons, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Button cancelButton = new Button(this);
            cancelButton.Text = Loc.T("Anuluj");
            cancelButton.Click += (sender, args) => Finish();
            buttons.AddView(cancelButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            Button saveButton = new Button(this);
            saveButton.Text = Loc.T("Zapisz");
            saveButton.Click += (sender, args) => Save();
            buttons.AddView(saveButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            return root;
        }

        /// <summary>
        /// Fills the form from an existing rule.
        /// </summary>
        /// <param name="rule">Rule to edit.</param>
        private void LoadRule(AlertRule rule)
        {
            ruleId = rule.Id;
            ruleEnabled = rule.IsEnabled;
            areaKind = rule.Area.Kind;
            filterId = rule.ConditionFilterId;
            nameField!.Text = rule.Name;
            radiusBar!.Progress = RadiusToProgress(rule.Area.RadiusKilometers);
            airportField!.Text = rule.Area.AirportIcao ?? string.Empty;

            if (rule.Area.Center != null)
            {
                FillCoordinates(rule.Area.Center.Latitude, rule.Area.Center.Longitude);
            }

            entryBox!.Checked = rule.NotifyOnEntry;
            appearanceBox!.Checked = rule.NotifyOnAppearance;
            exitBox!.Checked = rule.NotifyOnExit;
            watchedHex = rule.WatchedHex;

            if (watchedHex != null)
            {
                watchedText!.Text = Loc.T("Obserwowany samolot: ") + watchedHex.ToUpperInvariant();
                watchedText.Visibility = ViewStates.Visible;
            }

            cooldownField!.Text = rule.CooldownMinutes.ToString(CultureInfo.InvariantCulture);

            if (rule.Area.Vertices != null)
            {
                polygonVertices = new List<GeoPoint>(rule.Area.Vertices);
            }

            importanceSpinner!.SetSelection(Math.Max(0, Array.IndexOf(ImportanceLevels, rule.Importance)));

            for (int i = 0; i < ScheduleDays.Length; i++)
            {
                dayBoxes[i].Checked = rule.Schedule.IsDayActive(ScheduleDays[i]);
            }

            if (rule.Schedule.QuietStartMinute is int quietStart && rule.Schedule.QuietEndMinute is int quietEnd)
            {
                quietFromMinute = quietStart;
                quietToMinute = quietEnd;
                quietBox!.Checked = true;
            }

            UpdateQuietButtons();

            RadioGroup? group = FindRadioGroup();
            group?.Check(areaKind == AlertAreaKind.AroundAirport ? 2 : areaKind == AlertAreaKind.Circle ? 3 : areaKind == AlertAreaKind.Polygon ? 4 : 1);
        }

        /// <summary>
        /// Finds the radio group that chooses the kind of the area.
        /// </summary>
        /// <returns>The group or null when it was not found.</returns>
        private RadioGroup? FindRadioGroup()
        {
            return airportPanel?.Parent is LinearLayout form ? Enumerable.Range(0, form.ChildCount).Select((int index) => form.GetChildAt(index)).OfType<RadioGroup>().FirstOrDefault() : null;
        }

        /// <summary>
        /// Fills the spinner with "every aircraft" and all filters, keeping the chosen filter selected.
        /// </summary>
        private void FillFilterSpinner()
        {
            if (filterSpinner == null)
            {
                return;
            }

            fillingSpinner = true;

            List<string> names = new List<string> { Loc.T("Każdy samolot") };

            spinnerFilterIds = new List<string>();

            foreach (AircraftFilter filter in AppServices.Filters.All)
            {
                names.Add(filter.Name);
                spinnerFilterIds.Add(filter.Id);
            }

            ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, names);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            filterSpinner.Adapter = adapter;

            int selected = filterId == null ? 0 : spinnerFilterIds.IndexOf(filterId) + 1;

            if (selected < 0 || (filterId != null && selected == 0))
            {
                filterId = null;
                selected = 0;
            }

            filterSpinner.SetSelection(selected);
            fillingSpinner = false;
        }

        /// <summary>
        /// Shows only the controls of the chosen kind of the area.
        /// </summary>
        private void UpdateAreaPanels()
        {
            if (airportPanel != null)
            {
                airportPanel.Visibility = areaKind == AlertAreaKind.AroundAirport ? ViewStates.Visible : ViewStates.Gone;
            }

            if (circlePanel != null)
            {
                circlePanel.Visibility = areaKind == AlertAreaKind.Circle ? ViewStates.Visible : ViewStates.Gone;
            }

            ViewStates radiusVisibility = areaKind == AlertAreaKind.Polygon ? ViewStates.Gone : ViewStates.Visible;

            if (polygonPanel != null)
            {
                polygonPanel.Visibility = areaKind == AlertAreaKind.Polygon ? ViewStates.Visible : ViewStates.Gone;
            }

            if (radiusText != null && radiusBar != null)
            {
                radiusText.Visibility = radiusVisibility;
                radiusBar.Visibility = radiusVisibility;
            }

            UpdatePolygonText();
        }

        /// <summary>
        /// Shows how many vertices the polygon has, or that nothing was drawn yet.
        /// </summary>
        private void UpdatePolygonText()
        {
            if (polygonText == null)
            {
                return;
            }

            polygonText.Text = polygonVertices.Count < AlertArea.MinimumVertices
                ? Loc.T("Obszar nie jest jeszcze narysowany.")
                : AlertText.DescribeArea(AlertArea.CreatePolygon(polygonVertices), distanceUnit);
        }

        /// <summary>
        /// Opens the map on which the center of the circle is tapped.
        /// </summary>
        private void PickCenterOnMap()
        {
            Intent intent = new Intent(this, typeof(AreaPickerActivity));
            intent.PutExtra(AreaPickerActivity.ModeExtra, AreaPickerActivity.ModeCenter);
            intent.PutExtra(AreaPickerActivity.RadiusExtra, UnitFormatter.UnitToKilometers(radiusBar!.Progress + MinimumRadius, distanceUnit));

            double? latitude = ParseCoordinate(latitudeField!.Text, 90.0);
            double? longitude = ParseCoordinate(longitudeField!.Text, 180.0);

            if (latitude != null && longitude != null)
            {
                intent.PutExtra(AreaPickerActivity.LatitudeExtra, latitude.Value);
                intent.PutExtra(AreaPickerActivity.LongitudeExtra, longitude.Value);
            }

            StartActivityForResult(intent, AreaPickerActivity.RequestCode);
        }

        /// <summary>
        /// Opens the map on which the polygon or the rectangle is drawn.
        /// </summary>
        private void DrawOnMap()
        {
            Intent intent = new Intent(this, typeof(AreaPickerActivity));
            intent.PutExtra(AreaPickerActivity.ModeExtra, AreaPickerActivity.ModePolygon);
            intent.PutExtra(AreaPickerActivity.VerticesExtra, AreaPickerActivity.FlattenVertices(polygonVertices));
            StartActivityForResult(intent, AreaPickerActivity.RequestCode);
        }

        /// <summary>
        /// Takes the center or the vertices chosen on the map.
        /// </summary>
        /// <param name="requestCode">Code of the request.</param>
        /// <param name="resultCode">Result of the map screen.</param>
        /// <param name="data">Chosen center or vertices.</param>
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode != AreaPickerActivity.RequestCode || resultCode != Result.Ok || data == null)
            {
                return;
            }

            double[]? flat = data.GetDoubleArrayExtra(AreaPickerActivity.VerticesExtra);

            if (flat != null)
            {
                polygonVertices = AreaPickerActivity.ReadVertices(flat);
                UpdatePolygonText();

                return;
            }

            double latitude = data.GetDoubleExtra(AreaPickerActivity.LatitudeExtra, double.NaN);
            double longitude = data.GetDoubleExtra(AreaPickerActivity.LongitudeExtra, double.NaN);

            if (!double.IsNaN(latitude) && !double.IsNaN(longitude))
            {
                FillCoordinates(latitude, longitude);
            }
        }

        /// <summary>
        /// Shows the quiet hours on their buttons and enables the buttons only when the quiet hours are switched on.
        /// </summary>
        private void UpdateQuietButtons()
        {
            if (quietFromButton == null || quietToButton == null || quietBox == null)
            {
                return;
            }

            quietFromButton.Text = Loc.T("od ") + AlertSchedule.FormatMinute(quietFromMinute);
            quietToButton.Text = Loc.T("do ") + AlertSchedule.FormatMinute(quietToMinute);
            quietFromButton.Enabled = quietBox.Checked;
            quietToButton.Enabled = quietBox.Checked;
        }

        /// <summary>
        /// Asks for the start or the end of the quiet hours.
        /// </summary>
        /// <param name="isStart">True for the start, false for the end.</param>
        private void PickQuietTime(bool isStart)
        {
            int current = isStart ? quietFromMinute : quietToMinute;
            TimePickerDialog dialog = new TimePickerDialog(this, (sender, args) =>
            {
                int minute = args.HourOfDay * 60 + args.Minute;

                if (isStart)
                {
                    quietFromMinute = minute;
                }
                else
                {
                    quietToMinute = minute;
                }

                UpdateQuietButtons();
            }, current / 60, current % 60, true);
            dialog.Show();
        }

        /// <summary>
        /// Builds the schedule from the checkboxes and the quiet hours.
        /// </summary>
        /// <returns>The schedule, or null when the quiet hours start and end at the same time.</returns>
        private AlertSchedule? BuildSchedule()
        {
            int days = 0;

            for (int i = 0; i < ScheduleDays.Length; i++)
            {
                if (dayBoxes[i].Checked)
                {
                    days |= 1 << (int)ScheduleDays[i];
                }
            }

            if (quietBox!.Checked)
            {
                if (quietFromMinute == quietToMinute)
                {
                    Toast.MakeText(this, Loc.T("Godziny ciszy muszą się różnić (początek i koniec)"), ToastLength.Long)?.Show();

                    return null;
                }

                return new AlertSchedule(days, quietFromMinute, quietToMinute);
            }

            return new AlertSchedule(days, null, null);
        }

        /// <summary>
        /// Shows the chosen radius.
        /// </summary>
        private void UpdateRadiusText()
        {
            if (radiusText != null && radiusBar != null)
            {
                radiusText.Text = Loc.T("Promień: ") + (radiusBar.Progress + MinimumRadius).ToString(CultureInfo.InvariantCulture) + " " + UnitFormatter.DistanceSymbol(distanceUnit);
            }
        }

        /// <summary>
        /// Converts a radius in kilometers to the position of the radius slider, which counts whole units of the distance unit from the settings.
        /// </summary>
        /// <param name="kilometers">Radius in kilometers.</param>
        /// <returns>Slider position within its range.</returns>
        private int RadiusToProgress(double kilometers)
        {
            return Math.Clamp((int)Math.Round(UnitFormatter.KilometersToUnit(kilometers, distanceUnit)) - MinimumRadius, 0, radiusBar!.Max);
        }

        /// <summary>
        /// Lets the user choose one of the favorite airports.
        /// </summary>
        private void PickFavoriteAirport()
        {
            IReadOnlyList<string> codes = AppServices.Favorites.Codes;

            if (codes.Count == 0)
            {
                Toast.MakeText(this, Loc.T("Brak ulubionych lotnisk"), ToastLength.Short)?.Show();

                return;
            }

            string[] items = codes.ToArray();
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Ulubione lotniska"));
            builder.SetItems(items, (sender, args) => airportField!.Text = items[args.Which]);
            builder.Show();
        }

        /// <summary>
        /// Puts coordinates in the fields of the circle center.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        private void FillCoordinates(double latitude, double longitude)
        {
            latitudeField!.Text = latitude.ToString("0.0000", CultureInfo.InvariantCulture);
            longitudeField!.Text = longitude.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Counts the aircraft of the last download that match the rule as it is filled in now.
        /// </summary>
        private void RunTest()
        {
            AlertRule? rule = BuildRule(false);

            if (rule == null)
            {
                return;
            }

            AlertRule? resolved = AlertRuleStore.Resolve(rule);

            if (resolved == null)
            {
                return;
            }

            (bool known, _, _) = LiveTraffic.GetUserLocation();

            if (rule.Area.Kind == AlertAreaKind.AroundMe && !known)
            {
                Toast.MakeText(this, Loc.T("Pozycja GPS nie jest jeszcze znana"), ToastLength.Long)?.Show();

                return;
            }

            IReadOnlyList<Aircraft> aircraft = LiveTraffic.GetSnapshot().Aircraft;
            int count = AlertMonitor.CountMatching(resolved, aircraft);

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Test alertu"));
            builder.SetMessage(Loc.T("Pasujących samolotów: ") + count.ToString(CultureInfo.InvariantCulture) + " (z " + aircraft.Count.ToString(CultureInfo.InvariantCulture) + Loc.T(" w ostatnio pobranych danych)."));
            builder.SetPositiveButton("OK", (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Validates the form and saves the rule.
        /// </summary>
        private void Save()
        {
            AlertRule? rule = BuildRule(true);

            if (rule == null)
            {
                return;
            }

            AppServices.Alerts.Save(rule);
            Finish();
        }

        /// <summary>
        /// Builds a rule from the form.
        /// </summary>
        /// <param name="fillName">True to put a generated name in the rule when the name field is empty.</param>
        /// <returns>The rule, or null when the form is invalid; the reason is shown to the user.</returns>
        private AlertRule? BuildRule(bool fillName)
        {
            double radius = UnitFormatter.UnitToKilometers(radiusBar!.Progress + MinimumRadius, distanceUnit);
            AlertArea? area = null;

            if (areaKind == AlertAreaKind.AroundMe)
            {
                area = new AlertArea(AlertAreaKind.AroundMe, radius);
            }
            else if (areaKind == AlertAreaKind.AroundAirport)
            {
                string code = airportField!.Text?.Trim() ?? string.Empty;
                Airport? airport = AppServices.Airports.Find(code);

                if (airport == null)
                {
                    Toast.MakeText(this, AppServices.Airports.IsLoaded ? Loc.T("Nie znaleziono lotniska o tym kodzie") : Loc.T("Baza lotnisk jest jeszcze wczytywana, spróbuj za chwilę"), ToastLength.Long)?.Show();

                    return null;
                }

                area = new AlertArea(AlertAreaKind.AroundAirport, radius, null, airport.Icao);
            }
            else if (areaKind == AlertAreaKind.Polygon)
            {
                if (polygonVertices.Count < AlertArea.MinimumVertices)
                {
                    Toast.MakeText(this, Loc.T("Narysuj obszar na mapie"), ToastLength.Long)?.Show();

                    return null;
                }

                if (!AlertArea.IsSimple(polygonVertices))
                {
                    Toast.MakeText(this, Loc.T("Boki wielokąta nie mogą się przecinać ani stykać"), ToastLength.Long)?.Show();

                    return null;
                }

                area = AlertArea.CreatePolygon(polygonVertices);
            }
            else
            {
                double? latitude = ParseCoordinate(latitudeField!.Text, 90.0);
                double? longitude = ParseCoordinate(longitudeField!.Text, 180.0);

                if (latitude == null || longitude == null)
                {
                    Toast.MakeText(this, Loc.T("Podaj poprawną szerokość i długość geograficzną"), ToastLength.Long)?.Show();

                    return null;
                }

                area = new AlertArea(AlertAreaKind.Circle, radius, new GeoPoint(latitude.Value, longitude.Value));
            }

            if (!int.TryParse(cooldownField!.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cooldown) || cooldown < 0 || cooldown > 1440)
            {
                Toast.MakeText(this, Loc.T("Cisza musi być liczbą minut od 0 do 1440"), ToastLength.Long)?.Show();

                return null;
            }

            string name = nameField!.Text?.Trim() ?? string.Empty;

            if (name.Length == 0 && fillName)
            {
                name = AlertText.DescribeArea(area, distanceUnit);
            }

            AlertSchedule? schedule = BuildSchedule();

            if (schedule == null)
            {
                return null;
            }

            return new AlertRule(ruleId, name, ruleEnabled, area, null, entryBox!.Checked, appearanceBox!.Checked, cooldown, filterId, schedule, ImportanceLevels[Math.Clamp(importanceSpinner!.SelectedItemPosition, 0, ImportanceLevels.Length - 1)], exitBox!.Checked, watchedHex);
        }

        /// <summary>
        /// Reads a coordinate typed by the user; a comma is accepted as the decimal separator.
        /// </summary>
        /// <param name="text">Typed text.</param>
        /// <param name="limit">Largest absolute value allowed.</param>
        /// <returns>The coordinate or null when the text is not a number within the limit.</returns>
        private static double? ParseCoordinate(string? text, double limit)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(',', '.');

            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && Math.Abs(value) <= limit)
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Adds a section label to the form.
        /// </summary>
        /// <param name="form">Form to add to.</param>
        /// <param name="text">Text of the label.</param>
        private void AddLabel(LinearLayout form, string text)
        {
            TextView label = new TextView(this);
            label.Text = text;
            label.TextSize = 14f;
            label.SetTextColor(Color.Rgb(150, 200, 255));
            label.SetTypeface(null, TypefaceStyle.Bold);
            label.SetPadding(0, Dp(16), 0, Dp(2));
            form.AddView(label);
        }

        /// <summary>
        /// Creates a radio button.
        /// </summary>
        /// <param name="text">Text of the button.</param>
        /// <param name="id">Identifier of the button in its group.</param>
        /// <returns>The button.</returns>
        private RadioButton CreateRadio(string text, int id)
        {
            RadioButton button = new RadioButton(this);
            button.Text = text;
            button.Id = id;

            return button;
        }

        /// <summary>
        /// Creates a field for a coordinate.
        /// </summary>
        /// <param name="hint">Hint shown in the empty field.</param>
        /// <returns>The field.</returns>
        private EditText CreateCoordinateField(string hint)
        {
            EditText field = new EditText(this);
            field.Hint = hint;
            field.SetSingleLine(true);
            field.InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal | Android.Text.InputTypes.NumberFlagSigned;

            return field;
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

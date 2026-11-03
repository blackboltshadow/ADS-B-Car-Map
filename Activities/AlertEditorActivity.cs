using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Services;
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
        /// Smallest radius of the area in kilometers.
        /// </summary>
        private const int MinimumRadius = 1;

        /// <summary>
        /// Largest radius of the area in kilometers.
        /// </summary>
        private const int MaximumRadius = 300;

        /// <summary>
        /// Default radius of a new alert in kilometers.
        /// </summary>
        private const int DefaultRadius = 50;

        /// <summary>
        /// Default time of silence about the same aircraft in minutes.
        /// </summary>
        private const int DefaultCooldown = 30;

        /// <summary>
        /// Identifier of the edited rule, or a new identifier when a rule is being created.
        /// </summary>
        private string ruleId = string.Empty;

        /// <summary>
        /// Whether the edited rule is switched on.
        /// </summary>
        private bool ruleEnabled = true;

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
        /// Builds the screen and loads the edited rule.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

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
                radiusBar!.Progress = DefaultRadius - MinimumRadius;
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

            AddLabel(form, "Nazwa");
            nameField = new EditText(this);
            nameField.Hint = "np. Wojskowe nad domem";
            nameField.SetSingleLine(true);
            form.AddView(nameField);

            AddLabel(form, "Obszar");
            RadioGroup areaGroup = new RadioGroup(this);
            areaGroup.AddView(CreateRadio("Wokół mojej pozycji (GPS)", 1));
            areaGroup.AddView(CreateRadio("Wokół lotniska", 2));
            areaGroup.AddView(CreateRadio("Okrąg o wybranym środku", 3));
            areaGroup.Check(1);
            areaGroup.CheckedChange += (sender, args) =>
            {
                areaKind = args.CheckedId == 2 ? AlertAreaKind.AroundAirport : args.CheckedId == 3 ? AlertAreaKind.Circle : AlertAreaKind.AroundMe;
                UpdateAreaPanels();
            };
            form.AddView(areaGroup);

            airportPanel = new LinearLayout(this);
            airportPanel.Orientation = Orientation.Vertical;
            airportField = new EditText(this);
            airportField.Hint = "Kod lotniska ICAO lub IATA, np. EPWA";
            airportField.SetSingleLine(true);
            airportField.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapCharacters;
            airportPanel.AddView(airportField);
            Button favoritesButton = new Button(this);
            favoritesButton.Text = "Wybierz z ulubionych";
            favoritesButton.Click += (sender, args) => PickFavoriteAirport();
            airportPanel.AddView(favoritesButton);
            form.AddView(airportPanel);

            circlePanel = new LinearLayout(this);
            circlePanel.Orientation = Orientation.Vertical;
            latitudeField = CreateCoordinateField("Szerokość geograficzna, np. 52.2297");
            longitudeField = CreateCoordinateField("Długość geograficzna, np. 21.0122");
            circlePanel.AddView(latitudeField);
            circlePanel.AddView(longitudeField);
            Button mapCenterButton = new Button(this);
            mapCenterButton.Text = "Użyj środka pobieranych danych";
            mapCenterButton.Click += (sender, args) =>
            {
                (double latitude, double longitude) = LiveTraffic.GetCenter();
                FillCoordinates(latitude, longitude);
            };
            circlePanel.AddView(mapCenterButton);
            Button myPositionButton = new Button(this);
            myPositionButton.Text = "Użyj mojej pozycji";
            myPositionButton.Click += (sender, args) =>
            {
                (bool known, double latitude, double longitude) = LiveTraffic.GetUserLocation();

                if (known)
                {
                    FillCoordinates(latitude, longitude);
                }
                else
                {
                    Toast.MakeText(this, "Pozycja GPS nie jest jeszcze znana", ToastLength.Short)?.Show();
                }
            };
            circlePanel.AddView(myPositionButton);
            form.AddView(circlePanel);

            radiusText = new TextView(this);
            radiusText.SetPadding(0, Dp(8), 0, 0);
            form.AddView(radiusText);
            radiusBar = new SeekBar(this);
            radiusBar.Max = MaximumRadius - MinimumRadius;
            radiusBar.ProgressChanged += (sender, args) => UpdateRadiusText();
            form.AddView(radiusBar);

            AddLabel(form, "Samoloty (filtr)");
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
            newFilterButton.Text = "＋ Nowy filtr";
            newFilterButton.Click += (sender, args) => StartActivity(new Intent(this, typeof(FilterEditorActivity)));
            form.AddView(newFilterButton);

            AddLabel(form, "Powiadom, gdy");
            entryBox = new CheckBox(this);
            entryBox.Text = "samolot wleci w obszar";
            entryBox.Checked = true;
            form.AddView(entryBox);
            appearanceBox = new CheckBox(this);
            appearanceBox.Text = "samolot pojawi się w obszarze (np. po starcie)";
            appearanceBox.Checked = true;
            form.AddView(appearanceBox);

            AddLabel(form, "Cisza o tym samym samolocie (minuty)");
            cooldownField = new EditText(this);
            cooldownField.InputType = Android.Text.InputTypes.ClassNumber;
            cooldownField.SetSingleLine(true);
            form.AddView(cooldownField);

            Button testButton = new Button(this);
            testButton.Text = "Sprawdź, ile samolotów pasuje teraz";
            testButton.Click += (sender, args) => RunTest();
            form.AddView(testButton);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(Dp(16), Dp(4), Dp(16), Dp(8));
            root.AddView(buttons, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Button cancelButton = new Button(this);
            cancelButton.Text = "Anuluj";
            cancelButton.Click += (sender, args) => Finish();
            buttons.AddView(cancelButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            Button saveButton = new Button(this);
            saveButton.Text = "Zapisz";
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
            radiusBar!.Progress = Math.Clamp((int)Math.Round(rule.Area.RadiusKilometers), MinimumRadius, MaximumRadius) - MinimumRadius;
            airportField!.Text = rule.Area.AirportIcao ?? string.Empty;

            if (rule.Area.Center != null)
            {
                FillCoordinates(rule.Area.Center.Latitude, rule.Area.Center.Longitude);
            }

            entryBox!.Checked = rule.NotifyOnEntry;
            appearanceBox!.Checked = rule.NotifyOnAppearance;
            cooldownField!.Text = rule.CooldownMinutes.ToString(CultureInfo.InvariantCulture);

            RadioGroup? group = FindRadioGroup();
            group?.Check(areaKind == AlertAreaKind.AroundAirport ? 2 : areaKind == AlertAreaKind.Circle ? 3 : 1);
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

            List<string> names = new List<string> { "Każdy samolot" };

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
        }

        /// <summary>
        /// Shows the chosen radius.
        /// </summary>
        private void UpdateRadiusText()
        {
            if (radiusText != null && radiusBar != null)
            {
                radiusText.Text = "Promień: " + (radiusBar.Progress + MinimumRadius).ToString(CultureInfo.InvariantCulture) + " km";
            }
        }

        /// <summary>
        /// Lets the user choose one of the favorite airports.
        /// </summary>
        private void PickFavoriteAirport()
        {
            IReadOnlyList<string> codes = AppServices.Favorites.Codes;

            if (codes.Count == 0)
            {
                Toast.MakeText(this, "Brak ulubionych lotnisk", ToastLength.Short)?.Show();

                return;
            }

            string[] items = codes.ToArray();
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Ulubione lotniska");
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
                Toast.MakeText(this, "Pozycja GPS nie jest jeszcze znana", ToastLength.Long)?.Show();

                return;
            }

            IReadOnlyList<Aircraft> aircraft = LiveTraffic.GetSnapshot().Aircraft;
            int count = AlertMonitor.CountMatching(resolved, aircraft);

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Test alertu");
            builder.SetMessage("Pasujących samolotów: " + count.ToString(CultureInfo.InvariantCulture) + " (z " + aircraft.Count.ToString(CultureInfo.InvariantCulture) + " w ostatnio pobranych danych).");
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
            double radius = radiusBar!.Progress + MinimumRadius;
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
                    Toast.MakeText(this, AppServices.Airports.IsLoaded ? "Nie znaleziono lotniska o tym kodzie" : "Baza lotnisk jest jeszcze wczytywana, spróbuj za chwilę", ToastLength.Long)?.Show();

                    return null;
                }

                area = new AlertArea(AlertAreaKind.AroundAirport, radius, null, airport.Icao);
            }
            else
            {
                double? latitude = ParseCoordinate(latitudeField!.Text, 90.0);
                double? longitude = ParseCoordinate(longitudeField!.Text, 180.0);

                if (latitude == null || longitude == null)
                {
                    Toast.MakeText(this, "Podaj poprawną szerokość i długość geograficzną", ToastLength.Long)?.Show();

                    return null;
                }

                area = new AlertArea(AlertAreaKind.Circle, radius, new GeoPoint(latitude.Value, longitude.Value));
            }

            if (!int.TryParse(cooldownField!.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cooldown) || cooldown < 0 || cooldown > 1440)
            {
                Toast.MakeText(this, "Cisza musi być liczbą minut od 0 do 1440", ToastLength.Long)?.Show();

                return null;
            }

            string name = nameField!.Text?.Trim() ?? string.Empty;

            if (name.Length == 0 && fillName)
            {
                name = AlertText.DescribeArea(area);
            }

            return new AlertRule(ruleId, name, ruleEnabled, area, null, entryBox!.Checked, appearanceBox!.Checked, cooldown, filterId);
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

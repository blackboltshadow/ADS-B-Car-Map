using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen where the user chooses what the airspace layer shows: the states of the areas and each kind of area separately. The list of the kinds is built from the data the source published, so a new kind appears here without a new version of the application.
    /// </summary>
    [Activity(Label = "Strefy przestrzeni powietrznej", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AirspaceActivity : Activity
    {
        /// <summary>
        /// Height of the upper edge of the band offered when the filter of heights is switched on, in feet.
        /// </summary>
        private const int DefaultCeilingFeet = 10000;

        /// <summary>
        /// Code of the request for the permission to show notifications.
        /// </summary>
        private const int NotificationRequestCode = 3101;

        /// <summary>
        /// Container of the whole content, rebuilt when the data changes.
        /// </summary>
        private LinearLayout? content;

        /// <summary>
        /// Whether this screen started the feed because the layer is off, so it has to stop it when the screen is left.
        /// </summary>
        private bool startedFeed;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Strefy przestrzeni powietrznej");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);

            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);
            content = new LinearLayout(this);
            content.Orientation = Orientation.Vertical;
            content.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(content);
            SetContentView(scrollView);
        }

        /// <summary>
        /// Starts the feed when it is not running, so the list of the kinds can be filled, and shows the current choice.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            AppServices.Airspace.Changed -= OnFeedChanged;
            AppServices.Airspace.Changed += OnFeedChanged;

            if (AppServices.Airspace.State == AirspaceFeedState.Stopped)
            {
                AppServices.Airspace.Start();
                startedFeed = true;
            }

            Rebuild();
        }

        /// <summary>
        /// Stops the feed if this screen started it.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Airspace.Changed -= OnFeedChanged;

            if (startedFeed && !AppSettings.ShowAirspace)
            {
                AppServices.Airspace.Stop();
            }

            startedFeed = false;
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
        /// Rebuilds the list on the main thread after the feed changed, unless the user is typing in a field, which a rebuild would interrupt.
        /// </summary>
        private void OnFeedChanged()
        {
            RunOnUiThread(() =>
            {
                if (content?.FindFocus() is EditText)
                {
                    return;
                }

                Rebuild();
            });
        }

        /// <summary>
        /// Fills the screen: the layer switch, the states, the treatment of new kinds, the kinds found in the data and the reset button.
        /// </summary>
        private void Rebuild()
        {
            if (content == null)
            {
                return;
            }

            content.RemoveAllViews();
            AirspaceLayerConfig config = AppServices.AirspaceConfig.Config;

            Switch layerSwitch = CreateSwitch(Loc.T("Pokazuj warstwę stref na mapie"), AppSettings.ShowAirspace);
            layerSwitch.CheckedChange += (sender, args) =>
            {
                Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
                {
                    { AppSettings.ShowAirspaceSetting, args.IsChecked ? 1.0 : 0.0 },
                };

                AppSettings.Save(values);
            };
            content.AddView(layerSwitch);

            AddSection(Loc.T("Stany stref"));
            AddText(Loc.T("Aktywna strefa ma potwierdzoną rezerwację, która obowiązuje teraz. Planowana ma rezerwację zapowiedzianą na teraz lub na później, ale jeszcze niepotwierdzoną. Nieaktywna nie ma już rezerwacji. Aktywne są rysowane pełną linią, planowane kreskowaną."), 13f, Color.Rgb(180, 180, 180));
            AddStateSwitch(Loc.T("Aktywne"), AirspaceZoneState.Active);
            AddStateSwitch(Loc.T("Planowane"), AirspaceZoneState.Planned);
            AddStateSwitch(Loc.T("Nieaktywne"), AirspaceZoneState.Inactive);

            AddSection(Loc.T("Rodzaje stref"));
            Switch newTypes = CreateSwitch(Loc.T("Pokazuj nowe rodzaje stref"), config.ShowNewTypes);
            newTypes.CheckedChange += (sender, args) => AppServices.AirspaceConfig.Save(AppServices.AirspaceConfig.Config.WithShowNewTypes(args.IsChecked));
            content.AddView(newTypes);
            AddText(Loc.T("Rodzaj, którego jeszcze nie ma na liście poniżej, a który pojawi się w danych PAŻP, będzie pokazany od razu (gdy włączone) albo czeka na Twoją decyzję (gdy wyłączone)."), 13f, Color.Rgb(180, 180, 180));

            AddTypeSwitches(config);

            AddAltitudeSection(config);
            AddAlertSection();

            Button reset = new Button(this);
            reset.Text = Loc.T("Przywróć domyślny wybór");
            reset.Click += (sender, args) =>
            {
                AppServices.AirspaceConfig.Save(AirspaceLayerConfig.Default);
                AppServices.AirspaceConfig.SaveAlertOptions(AirspaceAlertOptions.Default);
                Rebuild();
            };
            content.AddView(reset);

            TextView source = AddText(AirspaceText.Attribution + " (" + DataSources.Pansa.PansaClient.DefaultBaseAddress + ")", 13f, Color.Rgb(120, 190, 255));
            source.Click += (sender, args) => OpenSource();
            AddText(AirspaceText.Disclaimer, 13f, Color.Rgb(180, 180, 180));
        }

        /// <summary>
        /// Adds the filter that shows only the areas reaching into the band of heights the user flies in.
        /// </summary>
        /// <param name="config">Current configuration.</param>
        private void AddAltitudeSection(AirspaceLayerConfig config)
        {
            AltitudeUnit unit = AppSettings.Units.Altitude;

            AddSection(Loc.T("Filtr wysokości"));
            AddText(Loc.T("Pokazuje tylko strefy, których zarezerwowana przestrzeń zahacza o podany przedział wysokości, np. od ziemi do pułapu, na którym latasz. Strefa kończąca się poniżej przedziału albo zaczynająca powyżej niego jest ukryta. Poziomy lotu są porównywane jako wysokości w stopach, bez poprawki na ciśnienie, więc granica może się różnić o kilkaset stóp. Filtr dotyczy też powiadomień o strefach."), 13f, Color.Rgb(180, 180, 180));

            Switch filterSwitch = CreateSwitch(Loc.T("Pokazuj tylko strefy sięgające do mojej wysokości"), config.AltitudeRange != null);
            filterSwitch.CheckedChange += (sender, args) =>
            {
                AirspaceLayerConfig current = AppServices.AirspaceConfig.Config;
                AppServices.AirspaceConfig.Save(current.WithAltitudeRange(args.IsChecked ? current.AltitudeRange ?? new AirspaceAltitudeRange(0, DefaultCeilingFeet) : null));
                Rebuild();
            };
            content!.AddView(filterSwitch);

            if (config.AltitudeRange == null)
            {
                return;
            }

            AirspaceAltitudeRange range = config.AltitudeRange.Value;
            TextView rangeLabel = AddText(AirspaceText.FormatRange(range.MinFeet, range.MaxFeet, unit), 15f, Color.White);
            RangeSliderView slider = new RangeSliderView(this);

            slider.SetRange(AirspaceAltitudeScale.Count, AirspaceAltitudeScale.IndexOf(range.MinFeet), AirspaceAltitudeScale.IndexOf(range.MaxFeet));
            slider.RangeChanged += (low, high) => rangeLabel.Text = AirspaceText.FormatRange(AirspaceAltitudeScale.FeetAt(low), AirspaceAltitudeScale.FeetAt(high), unit);
            slider.RangeCommitted += (low, high) =>
            {
                AirspaceAltitudeRange chosen = new AirspaceAltitudeRange(AirspaceAltitudeScale.FeetAt(low), AirspaceAltitudeScale.FeetAt(high));

                if (AppServices.AirspaceConfig.Config.AltitudeRange != chosen)
                {
                    AppServices.AirspaceConfig.Save(AppServices.AirspaceConfig.Config.WithAltitudeRange(chosen));
                }
            };
            content.AddView(slider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        }

        /// <summary>
        /// Adds the notifications about areas that become active near the user.
        /// </summary>
        private void AddAlertSection()
        {
            DistanceUnit unit = AppSettings.Units.Distance;
            AirspaceAlertOptions options = AppServices.AirspaceConfig.AlertOptions;

            AddSection(Loc.T("Powiadomienia o aktywacji stref"));
            AddText(Loc.T("Powiadomienie, gdy w pobliżu Twojej pozycji strefa wybranego rodzaju (i wysokości) zostaje aktywowana albo ma być aktywowana za chwilę. Każda rezerwacja jest zgłaszana raz, także po ponownym uruchomieniu aplikacji. Zaraz po włączeniu aplikacja nie zgłasza stref, które już są aktywne. Sprawdzanie działa, gdy mapa jest otwarta, oraz razem z trybem w tle alertów (Alerty → Sprawdzanie w tle). Stany stref z warstwy nie mają tu znaczenia: liczą się tylko rezerwacje potwierdzone jako aktywne."), 13f, Color.Rgb(180, 180, 180));

            Switch enabledSwitch = CreateSwitch(Loc.T("Powiadamiaj o aktywacji stref w pobliżu"), options.Enabled);
            enabledSwitch.CheckedChange += (sender, args) =>
            {
                AppServices.AirspaceConfig.SaveAlertOptions(AppServices.AirspaceConfig.AlertOptions with { Enabled = args.IsChecked });
                AirspaceAlertMonitor.Reset();

                if (args.IsChecked && !AlertMonitor.HasNotificationPermission())
                {
                    RequestPermissions(new string[] { Android.Manifest.Permission.PostNotifications }, NotificationRequestCode);
                }

                Rebuild();
            };
            content!.AddView(enabledSwitch);

            if (!options.Enabled)
            {
                return;
            }

            EditText radius = CreateNumberField(Loc.T("Odległość od Ciebie") + " (" + UnitFormatter.DistanceSymbol(unit) + ")", FormatNumber(UnitFormatter.KilometersToUnit(options.RadiusKilometers, unit)));
            EditText lead = CreateNumberField(Loc.T("Uprzedzenie przed aktywacją (min, 0 = dopiero gdy obowiązuje)"), options.LeadMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            System.EventHandler<View.FocusChangeEventArgs> apply = (sender, args) =>
            {
                if (args.HasFocus)
                {
                    return;
                }

                if (TryReadNumber(radius, out double distance) && TryReadNumber(lead, out double minutes))
                {
                    double kilometers = Math.Clamp(UnitFormatter.UnitToKilometers(distance, unit), AirspaceAlertOptions.MinimumRadiusKilometers, AirspaceAlertOptions.MaximumRadiusKilometers);
                    int leadMinutes = (int)Math.Clamp(Math.Round(minutes), 0.0, AirspaceAlertOptions.MaximumLeadMinutes);

                    AppServices.AirspaceConfig.SaveAlertOptions(AppServices.AirspaceConfig.AlertOptions with { RadiusKilometers = kilometers, LeadMinutes = leadMinutes });
                }
            };

            radius.FocusChange += apply;
            lead.FocusChange += apply;
            content.AddView(radius);
            content.AddView(lead);
        }

        /// <summary>
        /// Creates a field for a number with a label in the hint.
        /// </summary>
        /// <param name="hint">Label of the field.</param>
        /// <param name="value">Initial text.</param>
        /// <returns>The field.</returns>
        private EditText CreateNumberField(string hint, string value)
        {
            EditText field = new EditText(this);
            field.Hint = hint;
            field.Text = value;
            field.InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal;
            field.SetSingleLine(true);
            field.SetTextColor(Color.White);
            field.SetHintTextColor(Color.Rgb(140, 140, 140));

            return field;
        }

        /// <summary>
        /// Reads a number typed in a field; a comma is accepted as the decimal separator.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The number.</param>
        /// <returns>True when the field holds a number.</returns>
        private static bool TryReadNumber(EditText field, out double value)
        {
            string text = (field.Text ?? string.Empty).Trim().Replace(',', '.');

            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Writes a number for a field: whole numbers without decimals, others with one.
        /// </summary>
        /// <param name="value">The number.</param>
        /// <returns>The text.</returns>
        private static string FormatNumber(double value)
        {
            return Math.Abs(value - Math.Round(value)) < 0.05 ? Math.Round(value).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Adds a switch for each kind of area found in the data and for each kind the user has decided about earlier.
        /// </summary>
        /// <param name="config">Current configuration.</param>
        private void AddTypeSwitches(AirspaceLayerConfig config)
        {
            AirspaceFeed feed = AppServices.Airspace;
            IReadOnlyList<AirspaceTypeCount> counts = AirspaceTypes.Count(feed.Zones, DateTime.UtcNow);
            List<AirspaceTypeCount> entries = new List<AirspaceTypeCount>(counts);

            foreach (string type in config.DecidedTypes)
            {
                if (!entries.Any((AirspaceTypeCount entry) => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase)))
                {
                    entries.Add(new AirspaceTypeCount(type, 0, 0));
                }
            }

            if (counts.Count == 0)
            {
                AddText(feed.State == AirspaceFeedState.Offline ? Loc.T("Nie udało się pobrać stref. Lista rodzajów pojawi się po udanym pobraniu.") : Loc.T("Pobieranie stref… Lista rodzajów pojawi się po pobraniu."), 13f, Color.Rgb(180, 180, 180));
            }

            entries.Sort((AirspaceTypeCount a, AirspaceTypeCount b) =>
            {
                int byOrder = AirspaceTypes.GetOrder(a.Type).CompareTo(AirspaceTypes.GetOrder(b.Type));

                return byOrder != 0 ? byOrder : string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            });

            foreach (AirspaceTypeCount entry in entries)
            {
                string type = entry.Type;
                string text = AirspaceTypes.GetName(type);

                if (entry.Total > 0)
                {
                    text += "\n" + Loc.F("stref: {0}, aktywnych: {1}", entry.Total, entry.Active);
                }

                Switch typeSwitch = CreateSwitch(text, config.IsTypeShown(type));
                typeSwitch.SetTextColor(Color.White);
                typeSwitch.CheckedChange += (sender, args) => AppServices.AirspaceConfig.Save(AppServices.AirspaceConfig.Config.WithType(type, args.IsChecked));
                content!.AddView(typeSwitch);
            }
        }

        /// <summary>
        /// Adds the switch of one state of the areas.
        /// </summary>
        /// <param name="text">Text of the switch.</param>
        /// <param name="state">The state it controls.</param>
        private void AddStateSwitch(string text, AirspaceZoneState state)
        {
            Switch stateSwitch = CreateSwitch(text, (AppServices.AirspaceConfig.Config.States & state) != AirspaceZoneState.None);
            stateSwitch.CheckedChange += (sender, args) =>
            {
                AirspaceLayerConfig current = AppServices.AirspaceConfig.Config;
                AirspaceZoneState states = args.IsChecked ? current.States | state : current.States & ~state;
                AppServices.AirspaceConfig.Save(current.WithStates(states));
            };
            content!.AddView(stateSwitch);
        }

        /// <summary>
        /// Creates a switch with white text.
        /// </summary>
        /// <param name="text">Text of the switch.</param>
        /// <param name="isChecked">Initial position.</param>
        /// <returns>The switch.</returns>
        private Switch CreateSwitch(string text, bool isChecked)
        {
            Switch result = new Switch(this);
            result.Text = text;
            result.Checked = isChecked;
            result.SetTextColor(Color.White);
            result.SetPadding(0, Dp(8), 0, Dp(8));

            return result;
        }

        /// <summary>
        /// Adds a section heading.
        /// </summary>
        /// <param name="text">Text of the heading.</param>
        private void AddSection(string text)
        {
            TextView heading = new TextView(this);
            heading.Text = text;
            heading.TextSize = 17f;
            heading.SetTextColor(Color.White);
            heading.SetTypeface(null, TypefaceStyle.Bold);
            heading.SetPadding(0, Dp(18), 0, Dp(6));
            content!.AddView(heading);
        }

        /// <summary>
        /// Adds a paragraph of text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <returns>The text view.</returns>
        private TextView AddText(string text, float size, Color color)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);
            view.SetPadding(0, Dp(4), 0, Dp(8));
            content!.AddView(view);

            return view;
        }

        /// <summary>
        /// Opens the airspace map of the source of the areas.
        /// </summary>
        private void OpenSource()
        {
            try
            {
                StartActivity(new Android.Content.Intent(Android.Content.Intent.ActionView, Android.Net.Uri.Parse(DataSources.Pansa.PansaClient.DefaultBaseAddress)));
            }
            catch (Android.Content.ActivityNotFoundException)
            {
                Toast.MakeText(this, DataSources.Pansa.PansaClient.DefaultBaseAddress, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Converts density-independent pixels to pixels.
        /// </summary>
        /// <param name="value">Size in dp.</param>
        /// <returns>Size in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1f));
        }
    }
}

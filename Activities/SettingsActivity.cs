using System.Globalization;
using AdsbCarMap.Settings;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
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
        /// Number of threshold settings already connected to <see cref="altitudeScale"/>.
        /// </summary>
        private int altitudeScaleThresholds;

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

            TextView version = new TextView(this);
            version.Text = "ADS-B Car Map v" + BuildInfo.Version;
            version.TextSize = 12f;
            root.AddView(version);

            foreach (SettingDefinition definition in AppSettings.All)
            {
                switch (definition.Kind)
                {
                    case SettingKind.Switch:
                        AddSwitch(root, definition);
                        break;
                    case SettingKind.Choice:
                        AddChoice(root, definition);
                        break;
                    case SettingKind.Slider:
                        AddSlider(root, definition);
                        break;
                    case SettingKind.Scale:
                        AddScaleThreshold(root, definition);
                        break;
                    default:
                        AddNumber(root, definition);
                        break;
                }
            }

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(8), 0, 0);
            root.AddView(buttons);

            Button saveButton = new Button(this);
            saveButton.Text = "Zapisz";
            saveButton.Click += (sender, args) => SaveSettings();
            buttons.AddView(saveButton);

            Button resetButton = new Button(this);
            resetButton.Text = "Przywróć domyślne";
            resetButton.Click += (sender, args) => ResetSettings();
            buttons.AddView(resetButton);

            LoadSettingInputs();

            return scrollView;
        }

        /// <summary>
        /// Adds a label with a numeric text field for a setting.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddNumber(LinearLayout root, SettingDefinition definition)
        {
            TextView label = new TextView(this);
            label.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  (domyślnie {1}, zakres {2}–{3})",
                definition.Label,
                FormatValue(definition, definition.DefaultValue),
                FormatValue(definition, definition.Minimum),
                FormatValue(definition, definition.Maximum));
            label.SetPadding(0, Dp(4), 0, 0);
            label.TextSize = 13f;
            root.AddView(label);

            EditText input = new EditText(this);
            input.InputType = definition.IsInteger
                ? InputTypes.ClassNumber
                : InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            input.SetSingleLine(true);
            root.AddView(input);

            settingReaders[definition] = () =>
            {
                string text = (input.Text ?? string.Empty).Trim().Replace(',', '.');

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
            };
            settingWriters[definition] = value => input.Text = FormatValue(definition, value);
        }

        /// <summary>
        /// Adds an on/off switch for a setting.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddSwitch(LinearLayout root, SettingDefinition definition)
        {
            LinearLayout card = CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            card.AddView(row);

            Switch toggle = new Switch(this);
            toggle.Text = definition.Label;
            toggle.TextSize = 15f;
            toggle.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(toggle, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddInfoButton(row, definition);

            settingReaders[definition] = () => toggle.Checked ? 1.0 : 0.0;
            settingWriters[definition] = value => toggle.Checked = value >= 0.5;
        }

        /// <summary>
        /// Adds a label with a drop-down list of options for a setting.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddChoice(LinearLayout root, SettingDefinition definition)
        {
            LinearLayout card = CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            card.AddView(row);

            TextView label = new TextView(this);
            label.Text = definition.Label;
            label.TextSize = 15f;
            label.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(label, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
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

            settingReaders[definition] = () => spinner.SelectedItemPosition;
            settingWriters[definition] = value => spinner.SetSelection((int)Math.Round(value));
        }

        /// <summary>
        /// Adds a slider for a setting. The label shows the current value and, for the label size, a sample label drawn in that size.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddSlider(LinearLayout root, SettingDefinition definition)
        {
            TextView label = new TextView(this);
            label.SetPadding(0, Dp(4), 0, 0);
            label.TextSize = 13f;
            root.AddView(label);

            SeekBar bar = new SeekBar(this);
            bar.Max = (int)Math.Round((definition.Maximum - definition.Minimum) / definition.Step);
            root.AddView(bar);

            TextView? preview = null;

            if (definition == AppSettings.LabelTextSizeSetting)
            {
                preview = new TextView(this);
                preview.SetBackgroundColor(Color.Rgb(40, 44, 52));
                preview.SetTextColor(Color.White);
                preview.SetShadowLayer(3f, 0f, 0f, Color.Black);
                preview.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
                root.AddView(preview);
            }

            System.Action<double> refresh = value =>
            {
                label.Text = string.Format(CultureInfo.InvariantCulture, "{0}: {1} dp  (domyślnie {2})", definition.Label, FormatValue(definition, value), FormatValue(definition, definition.DefaultValue));
                preview?.SetTextSize(Android.Util.ComplexUnitType.Dip, (float)value);
                preview?.Text = "LOT1234 " + AltitudeFormatter.Format(35000);
            };

            bar.ProgressChanged += (sender, args) => refresh(definition.Minimum + args.Progress * definition.Step);

            settingReaders[definition] = () => definition.Minimum + bar.Progress * definition.Step;
            settingWriters[definition] = value =>
            {
                bar.Progress = (int)Math.Round((value - definition.Minimum) / definition.Step);
                refresh(value);
            };
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
                LinearLayout card = CreateCard(root);

                TextView title = new TextView(this);
                title.Text = "Skala kolorów wysokości";
                title.TextSize = 16f;
                title.SetTypeface(null, TypefaceStyle.Bold);
                card.AddView(title);

                TextView hint = new TextView(this);
                hint.Text = "Przeciągnij znacznik, aby zmienić próg, albo dotknij go, aby wpisać wartość. Wartości są w jednostce wybranej wyżej.";
                hint.TextSize = 12f;
                hint.SetTextColor(Color.Rgb(180, 180, 180));
                hint.SetPadding(0, Dp(4), 0, 0);
                card.AddView(hint);

                altitudeScale = new AltitudeScaleView(this);
                card.AddView(altitudeScale);

                Button resetColors = new Button(this);
                resetColors.Text = "Przywróć domyślne kolory";
                resetColors.Click += (sender, args) => ResetAltitudeScale();
                card.AddView(resetColors);
            }

            AltitudeScaleView scale = altitudeScale;
            int index = altitudeScaleThresholds++;

            altitudeScaleSettings.Add(definition);

            settingReaders[definition] = () => scale.GetValue(index);
            settingWriters[definition] = value => scale.SetValue(index, value);
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

            TextView button = new TextView(this);
            button.Text = "i";
            button.TextSize = 14f;
            button.SetTypeface(null, TypefaceStyle.BoldItalic);
            button.SetTextColor(Color.White);
            button.Gravity = GravityFlags.Center;
            button.ContentDescription = "Opis ustawienia: " + definition.Label;

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(Color.Argb(60, 255, 255, 255));
            circle.SetStroke(Dp(1), Color.Argb(150, 255, 255, 255));
            button.Background = circle;

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

            if (definition == AppSettings.AltitudeUnitSetting)
            {
                LinearLayout rows = new LinearLayout(this);
                rows.Orientation = Orientation.Vertical;
                rows.SetBackgroundColor(Color.Rgb(40, 44, 52));
                rows.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));

                TextView heading = new TextView(this);
                heading.Text = "Samolot na wysokości 35 000 ft:";
                heading.TextSize = 12f;
                heading.SetTextColor(Color.Rgb(180, 180, 180));
                rows.AddView(heading);

                string[] names = new string[] { "Metry", "Stopy", "Poziom lotu" };
                AltitudeUnit[] units = new AltitudeUnit[] { AltitudeUnit.Metric, AltitudeUnit.Feet, AltitudeUnit.FlightLevel };

                for (int index = 0; index < units.Length; index++)
                {
                    TextView row = new TextView(this);
                    row.Text = names[index] + ":  " + AltitudeFormatter.Format(35000, units[index]);
                    row.TextSize = 15f;
                    row.SetPadding(0, Dp(6), 0, 0);
                    row.SetTextColor(units[index] == AppSettings.AltitudeUnit ? Color.White : Color.Rgb(150, 150, 150));
                    row.SetTypeface(null, units[index] == AppSettings.AltitudeUnit ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                    rows.AddView(row);
                }

                return rows;
            }

            return null;
        }

        /// <summary>
        /// Puts the default thresholds into the altitude scale editor. Like every other change, they are saved with the Save button.
        /// </summary>
        private void ResetAltitudeScale()
        {
            foreach (SettingDefinition definition in altitudeScaleSettings)
            {
                settingWriters[definition](definition.DefaultValue);
            }

            Toast.MakeText(this, "Przywrócono domyślne kolory, zapisz, aby zastosować", ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Puts the current setting values into the controls.
        /// </summary>
        private void LoadSettingInputs()
        {
            foreach (KeyValuePair<SettingDefinition, System.Action<double>> entry in settingWriters)
            {
                entry.Value(AppSettings.Get(entry.Key));
            }
        }

        /// <summary>
        /// Reads the controls and saves the settings. Empty or invalid fields keep the previous value.
        /// </summary>
        private void SaveSettings()
        {
            Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>();

            foreach (KeyValuePair<SettingDefinition, Func<double?>> entry in settingReaders)
            {
                double? value = entry.Value();

                if (value.HasValue)
                {
                    values[entry.Key] = value.Value;
                }
            }

            AppSettings.Save(values);
            LoadSettingInputs();
            Toast.MakeText(this, "Zapisano ustawienia", ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Restores the default settings and refreshes the controls.
        /// </summary>
        private void ResetSettings()
        {
            AppSettings.ResetToDefaults();
            LoadSettingInputs();
            Toast.MakeText(this, "Przywrócono ustawienia domyślne", ToastLength.Short)?.Show();
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

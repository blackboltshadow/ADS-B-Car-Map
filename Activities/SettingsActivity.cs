using System.Globalization;
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
        /// Undo buttons by setting. A button is visible only while the value in the control differs from the saved one.
        /// </summary>
        private readonly Dictionary<SettingDefinition, View> undoButtons = new Dictionary<SettingDefinition, View>();

        /// <summary>
        /// Card shared by all trail settings, created with the first of them.
        /// </summary>
        private LinearLayout? trailCard;

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
        /// Commits a number that is still being typed, because a setting is saved when its field loses focus.
        /// </summary>
        protected override void OnPause()
        {
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

            TextView version = new TextView(this);
            version.Text = "ADS-B Car Map v" + BuildInfo.Version + " (" + BuildInfo.Build + ")";
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
                        AddNumber(AppSettings.TrailSettings.Contains(definition) ? GetTrailCard(root) : root, definition);
                        break;
                }
            }

            if (aircraftIconCard != null)
            {
                aircraftIconCard.AddView(CreateResetButton("Przywróć domyślne ikony", new List<SettingDefinition> { AppSettings.IconScaleSetting, AppSettings.AircraftOutlineWidthSetting }, "Przywrócono domyślne ustawienia ikon"));
            }

            if (trailCard != null)
            {
                trailCard.AddView(CreateResetButton("Przywróć domyślne ślady", AppSettings.TrailSettings, "Przywrócono domyślne ustawienia śladów"));
            }

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(8), 0, 0);
            root.AddView(buttons);

            buttons.AddView(CreateResetButton("Przywróć wszystkie domyślne", AppSettings.All, "Przywrócono ustawienia domyślne"));

            LoadSettingInputs();

            return scrollView;
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
            LinearLayout card = CreateCard(root);
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetMinimumHeight(Dp(28));
            card.AddView(row);

            Switch toggle = new Switch(this);
            toggle.Text = definition.Label;
            StyleTitle(toggle);
            row.AddView(toggle, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            AddInfoButton(row, definition);

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
            row.SetMinimumHeight(Dp(28));
            card.AddView(row);

            TextView label = new TextView(this);
            label.Text = definition.Label;
            StyleTitle(label);
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
        /// Adds a slider for a setting. The label shows the current value and, for the label size, a sample label drawn in that size.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="definition">Setting.</param>
        private void AddSlider(LinearLayout root, SettingDefinition definition)
        {
            bool isIconSetting = definition == AppSettings.IconScaleSetting || definition == AppSettings.AircraftOutlineWidthSetting;
            bool isTrailSetting = AppSettings.TrailSettings.Contains(definition);
            bool isGrouped = isIconSetting || isTrailSetting;
            LinearLayout card = isIconSetting ? GetAircraftIconCard(root) : isTrailSetting ? GetTrailCard(root) : CreateCard(root);

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
                LinearLayout card = CreateCard(root);

                TextView title = new TextView(this);
                title.Text = "Skala kolorów wysokości";
                StyleTitle(title);
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
                resetColors.Click += (sender, args) =>
                {
                    ResetSettings(altitudeScaleSettings);
                    Toast.MakeText(this, "Przywrócono domyślne kolory", ToastLength.Short)?.Show();
                };
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
            title.Text = "Ikony samolotów";
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
            button.Click += (sender, args) =>
            {
                ResetSettings(definitions);
                Toast.MakeText(this, message, ToastLength.Short)?.Show();
            };

            return button;
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

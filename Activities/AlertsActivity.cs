using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen with the list of alerts. A switch at the top turns all alerts on or off, each alert has its own switch and can be edited and removed. A button opens the editor of a new alert.
    /// </summary>
    [Activity(Label = "Alerty", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AlertsActivity : Activity
    {
        /// <summary>
        /// Switch of all alerts.
        /// </summary>
        private Switch? masterSwitch;

        /// <summary>
        /// Container of the rows.
        /// </summary>
        private LinearLayout? listLayout;

        /// <summary>
        /// Switch of the background checking.
        /// </summary>
        private Switch? backgroundSwitch;

        /// <summary>
        /// Text with the state of the background checking.
        /// </summary>
        private TextView? backgroundStatus;

        /// <summary>
        /// Set while the screen changes the master switch itself, so that the change is not saved again.
        /// </summary>
        private bool updatingSwitch;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Alerty");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Starts listening for changes of the rules and shows the list.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            AppServices.Alerts.Changed += OnAlertsChanged;
            Render();
        }

        /// <summary>
        /// Stops listening for changes.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Alerts.Changed -= OnAlertsChanged;

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
        /// <returns>View with the master switch and the add button at the top and the scrollable list under it.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Vertical;
            header.SetPadding(Dp(16), Dp(8), Dp(16), Dp(4));
            root.AddView(header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            masterSwitch = new Switch(this);
            masterSwitch.Text = Loc.T("Alerty włączone");
            masterSwitch.TextSize = 17f;
            masterSwitch.CheckedChange += (sender, args) =>
            {
                if (!updatingSwitch)
                {
                    AppServices.Alerts.SetEnabled(args.IsChecked);
                }
            };
            header.AddView(masterSwitch);

            Button addButton = new Button(this);
            addButton.Text = Loc.T("＋ Nowy alert");
            addButton.Click += (sender, args) => OpenEditor(null);
            header.AddView(addButton);

            BuildBackgroundSection(header);

            TextView hint = new TextView(this);
            hint.TextSize = 12f;
            hint.SetTextColor(Color.Rgb(180, 180, 180));
            hint.SetPadding(0, Dp(4), 0, Dp(4));
            hint.Text = Loc.T("Alert powiadamia, gdy samolot spełniający wybrany filtr wleci w obszar albo pojawi się w nim. Bez trybu w tle alerty są sprawdzane tylko, gdy aplikacja pobiera samoloty (mapa na telefonie lub w Android Auto) i tylko w obszarze pobieranych danych. Tryb w tle sam pobiera samoloty wokół obszarów alertów, zużywa baterię i dane oraz wymaga zgody na lokalizację i powiadomienia. Częstotliwość i limit alertów w tle ustawia się w Ustawieniach → Alerty.");
            header.AddView(hint);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            listLayout = new LinearLayout(this);
            listLayout.Orientation = Orientation.Vertical;
            listLayout.SetPadding(0, 0, 0, Dp(32));
            scrollView.AddView(listLayout);

            return root;
        }

        /// <summary>
        /// Refreshes the list on the main thread after the rules changed.
        /// </summary>
        private void OnAlertsChanged()
        {
            RunOnUiThread(Render);
        }

        /// <summary>
        /// Shows the master switch and all rules.
        /// </summary>
        private void Render()
        {
            if (listLayout == null || masterSwitch == null)
            {
                return;
            }

            updatingSwitch = true;
            masterSwitch.Checked = AppServices.Alerts.IsEnabled;
            RenderBackgroundSection();
            updatingSwitch = false;

            listLayout.RemoveAllViews();

            IReadOnlyList<AlertRule> rules = AppServices.Alerts.Rules;

            if (rules.Count == 0)
            {
                TextView empty = new TextView(this);
                empty.Text = Loc.T("Brak alertów. Dotknij „Nowy alert”.");
                empty.SetTextColor(Color.Rgb(190, 190, 190));
                empty.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
                listLayout.AddView(empty);
            }

            for (int index = 0; index < rules.Count; index++)
            {
                listLayout.AddView(CreateRow(rules[index], index));
            }
        }

        /// <summary>
        /// Creates the row of a rule: the switch, the name with the description and the buttons.
        /// </summary>
        /// <param name="rule">Rule.</param>
        /// <param name="index">Position on the list.</param>
        /// <returns>The row.</returns>
        private View CreateRow(AlertRule rule, int index)
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(12), Dp(6), Dp(4), Dp(6));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);

            Switch toggle = new Switch(this);
            toggle.Checked = rule.IsEnabled;
            toggle.ContentDescription = Loc.T("Włącz alert ") + rule.Name;
            toggle.CheckedChange += (sender, args) => AppServices.Alerts.SetRuleEnabled(rule.Id, args.IsChecked);
            row.AddView(toggle, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            LinearLayout texts = new LinearLayout(this);
            texts.Orientation = Orientation.Vertical;
            texts.SetPadding(Dp(12), 0, Dp(4), 0);

            TextView title = new TextView(this);
            title.Text = rule.Name;
            title.TextSize = 17f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            texts.AddView(title);

            TextView details = new TextView(this);
            details.Text = DescribeRule(rule);
            details.TextSize = 13f;
            details.SetTextColor(Color.Rgb(190, 190, 190));
            texts.AddView(details);

            texts.Clickable = true;
            texts.Click += (sender, args) => OpenEditor(rule.Id);
            row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            row.AddView(CreateIconButton(Resource.Drawable.ic_edit, Loc.T("Edytuj alert"), () => OpenEditor(rule.Id)));
            row.AddView(CreateIconButton(Resource.Drawable.ic_delete, Loc.T("Usuń alert"), () => ConfirmRemove(rule)));

            return row;
        }

        /// <summary>
        /// Adds the controls of the background checking: the switch and the state. The interval and the number of alerts are in the settings.
        /// </summary>
        /// <param name="header">Container to add the controls to.</param>
        private void BuildBackgroundSection(LinearLayout header)
        {
            backgroundSwitch = new Switch(this);
            backgroundSwitch.Text = Loc.T("Sprawdzaj w tle (usługa ze stałym powiadomieniem)");
            backgroundSwitch.TextSize = 15f;
            backgroundSwitch.SetPadding(0, Dp(8), 0, 0);
            backgroundSwitch.CheckedChange += (sender, args) =>
            {
                if (!updatingSwitch)
                {
                    AppServices.Alerts.SetBackgroundEnabled(args.IsChecked);
                }
            };
            header.AddView(backgroundSwitch);

            backgroundStatus = new TextView(this);
            backgroundStatus.TextSize = 12f;
            backgroundStatus.SetTextColor(Color.Rgb(150, 200, 255));
            backgroundStatus.SetPadding(0, Dp(2), 0, 0);
            header.AddView(backgroundStatus);
        }

        /// <summary>
        /// Shows the current settings and state of the background checking.
        /// </summary>
        private void RenderBackgroundSection()
        {
            if (backgroundSwitch == null || backgroundStatus == null)
            {
                return;
            }

            AlertRuleStore store = AppServices.Alerts;

            backgroundSwitch.Checked = store.IsBackgroundEnabled;

            string text = store.IsBackgroundEnabled ? Loc.T("Tryb w tle: ") + AlertBackgroundRunner.Status : Loc.T("Tryb w tle wyłączony: alerty działają tylko, gdy mapa pobiera samoloty.");

            if (store.IsBackgroundEnabled && store.Rules.Count(rule => rule.IsEnabled) > AppSettings.AlertMaxBackground)
            {
                text += Loc.T("\nW tle sprawdzane jest tylko pierwszych ") + AppSettings.AlertMaxBackground + Loc.T(" aktywnych alertów (limit w Ustawieniach → Alerty).");
            }

            backgroundStatus.Text = text;
        }

        /// <summary>
        /// Describes a rule in three short lines: the area, the filter and the events.
        /// </summary>
        /// <param name="rule">Rule to describe.</param>
        /// <returns>Text for the list.</returns>
        private static string DescribeRule(AlertRule rule)
        {
            string filter = Loc.T("Każdy samolot");

            if (rule.ConditionFilterId != null)
            {
                AircraftFilter? found = AppServices.Filters.Find(rule.ConditionFilterId);

                filter = found == null ? Loc.T("Filtr usunięty, alert nieaktywny") : Loc.T("Filtr: ") + found.Name;
            }

            return AlertText.DescribeArea(rule.Area, AppSettings.DistanceUnit) + "\n" + filter + "\n" + AlertText.DescribeEvents(rule);
        }

        /// <summary>
        /// Opens the editor of a rule.
        /// </summary>
        /// <param name="id">Identifier of the rule to edit, or null to create a new one.</param>
        private void OpenEditor(string? id)
        {
            Intent intent = new Intent(this, typeof(AlertEditorActivity));

            if (id != null)
            {
                intent.PutExtra(AlertEditorActivity.EditedIdExtra, id);
            }

            StartActivity(intent);
        }

        /// <summary>
        /// Asks for confirmation and removes a rule.
        /// </summary>
        /// <param name="rule">Rule to remove.</param>
        private void ConfirmRemove(AlertRule rule)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Usunąć alert?"));
            builder.SetMessage(rule.Name);
            builder.SetPositiveButton(Loc.T("Usuń"), (sender, args) => AppServices.Alerts.Remove(rule.Id));
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Creates a small round button of a row that shows a vector icon.
        /// </summary>
        /// <param name="iconResource">Identifier of the vector drawable.</param>
        /// <param name="description">Description for accessibility services.</param>
        /// <param name="action">Action performed on a tap.</param>
        /// <returns>The button.</returns>
        private View CreateIconButton(int iconResource, string description, System.Action action)
        {
            ImageView button = new ImageView(this);
            button.SetImageResource(iconResource);
            button.SetScaleType(ImageView.ScaleType.CenterInside);
            button.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
            button.ContentDescription = description;

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(Color.Argb(60, 255, 255, 255));
            circle.SetStroke(Dp(1), Color.Argb(150, 255, 255, 255));
            button.Background = circle;
            button.Click += (sender, args) => action();

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(36), Dp(36));
            parameters.SetMargins(Dp(4), 0, Dp(4), 0);
            button.LayoutParameters = parameters;

            return button;
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

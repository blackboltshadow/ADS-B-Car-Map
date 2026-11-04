using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Services;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// List of the aircraft that the user selected on the map or watched with an alert, with buttons that show an aircraft on the map, pin it, watch it and remove it.
    /// </summary>
    [Activity(Label = "Historia samolotów", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AircraftHistoryActivity : Activity
    {
        /// <summary>
        /// Container of the rows.
        /// </summary>
        private LinearLayout? listLayout;

        /// <summary>
        /// Builds the screen.
        /// </summary>
        /// <param name="savedInstanceState">Saved state, not used.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Historia samolotów");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Shows the current entries every time the screen comes back.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            FillList();
        }

        /// <summary>
        /// Closes the screen on the arrow in the action bar.
        /// </summary>
        /// <param name="item">Chosen item.</param>
        /// <returns>True when handled.</returns>
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
        /// Creates the view with the clear button and the list.
        /// </summary>
        /// <returns>The root view.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            Button clearButton = new Button(this);
            clearButton.Text = Loc.T("Wyczyść historię (bez przypiętych)");
            clearButton.Click += (sender, args) => ConfirmClear();
            root.AddView(clearButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            listLayout = new LinearLayout(this);
            listLayout.Orientation = Orientation.Vertical;
            listLayout.SetPadding(Dp(16), Dp(8), Dp(16), Dp(16));
            scrollView.AddView(listLayout);

            return root;
        }

        /// <summary>
        /// Fills the list with the entries, the pinned ones first.
        /// </summary>
        private void FillList()
        {
            listLayout!.RemoveAllViews();
            List<AircraftHistoryEntry> entries = AircraftHistoryStore.GetForDisplay();

            if (entries.Count == 0)
            {
                TextView empty = new TextView(this);
                empty.Text = Loc.T("Historia jest pusta. Tu pojawią się samoloty, które zaznaczysz na mapie lub które zgłosi alert „Obserwuj”.");
                empty.SetPadding(0, Dp(16), 0, 0);
                listLayout.AddView(empty);

                return;
            }

            HashSet<string> watchedNow = new HashSet<string>(
                AppServices.Alerts.Rules.Where((AlertRule rule) => rule.WatchedHex != null).Select((AlertRule rule) => rule.WatchedHex!.ToLowerInvariant()));

            foreach (AircraftHistoryEntry entry in entries)
            {
                listLayout.AddView(CreateRow(entry, watchedNow.Contains(entry.Hex)));
            }
        }

        /// <summary>
        /// Creates the row of an entry: the name, the details and the buttons.
        /// </summary>
        /// <param name="entry">Entry to show.</param>
        /// <param name="watchedNow">Whether a watch alert for the aircraft exists now.</param>
        /// <returns>The row.</returns>
        private View CreateRow(AircraftHistoryEntry entry, bool watchedNow)
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Vertical;
            row.SetPadding(0, Dp(8), 0, Dp(8));

            string name = string.IsNullOrWhiteSpace(entry.Callsign) ? entry.Hex.ToUpperInvariant() : entry.Callsign!;
            string marks = (entry.Pinned ? "★ " : string.Empty) + (watchedNow ? "👁 " : string.Empty);
            TextView title = new TextView(this);
            title.Text = marks + name;
            title.TextSize = 16f;
            title.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(title);

            List<string> parts = new List<string>();
            parts.Add(entry.Hex.ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(entry.Registration))
            {
                parts.Add(entry.Registration!);
            }

            if (!string.IsNullOrWhiteSpace(entry.TypeCode))
            {
                parts.Add(entry.TypeCode!);
            }

            TextView details = new TextView(this);
            details.Text = string.Join(" · ", parts);
            details.TextSize = 13f;
            details.SetTextColor(Color.Rgb(180, 180, 180));
            row.AddView(details);

            TextView times = new TextView(this);
            times.Text = Loc.F("Ostatnio: {0} · pierwszy raz: {1} · razy: {2}", entry.LastSeenUtc.ToLocalTime().ToString("dd.MM HH:mm"), entry.FirstSeenUtc.ToLocalTime().ToString("dd.MM HH:mm"), entry.Count)
                + (entry.Watched ? " · " + Loc.T("obserwowany") : string.Empty);
            times.TextSize = 12f;
            times.SetTextColor(Color.Rgb(150, 150, 150));
            row.AddView(times);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.AddView(CreateButton(Loc.T("Pokaż"), () => ShowOnMap(entry)));
            buttons.AddView(CreateButton(entry.Pinned ? Loc.T("Odepnij") : Loc.T("Przypnij"), () =>
            {
                AircraftHistoryStore.SetPinned(entry.Hex, !entry.Pinned);
                FillList();
            }));
            buttons.AddView(CreateButton(watchedNow ? Loc.T("Nie obserwuj") : Loc.T("Obserwuj"), () => ToggleWatch(entry, watchedNow)));
            buttons.AddView(CreateButton(Loc.T("Usuń"), () =>
            {
                AircraftHistoryStore.Remove(entry.Hex);
                FillList();
            }));
            row.AddView(buttons);

            return row;
        }

        /// <summary>
        /// Creates a small button of a row.
        /// </summary>
        /// <param name="text">Caption.</param>
        /// <param name="onClick">Action run on a click.</param>
        /// <returns>The button.</returns>
        private Button CreateButton(string text, Action onClick)
        {
            Button button = new Button(this);
            button.Text = text;
            button.TextSize = 12f;
            button.SetAllCaps(false);
            button.Click += (sender, args) => onClick();

            return button;
        }

        /// <summary>
        /// Asks the map to show and follow the aircraft and closes the screen, so that the map is visible.
        /// </summary>
        /// <param name="entry">Entry to show.</param>
        private void ShowOnMap(AircraftHistoryEntry entry)
        {
            LiveTraffic.RequestFocus(entry.Hex);
            Finish();
        }

        /// <summary>
        /// Creates the alert that watches the aircraft, or removes the existing one.
        /// </summary>
        /// <param name="entry">Entry of the aircraft.</param>
        /// <param name="watchedNow">Whether a watch alert for the aircraft exists now.</param>
        private void ToggleWatch(AircraftHistoryEntry entry, bool watchedNow)
        {
            if (watchedNow)
            {
                foreach (AlertRule rule in AppServices.Alerts.Rules.Where((AlertRule item) => string.Equals(item.WatchedHex, entry.Hex, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    AppServices.Alerts.Remove(rule.Id);
                }

                Toast.MakeText(this, Loc.T("Alert obserwowania usunięty"), ToastLength.Short)?.Show();
            }
            else
            {
                string name = string.IsNullOrWhiteSpace(entry.Callsign) ? entry.Hex.ToUpperInvariant() : entry.Callsign!;

                AppServices.Alerts.Save(AlertBundle.CreateWatchRule(AlertRuleStore.CreateId(), Loc.T("Obserwuj: ") + name, entry.Hex));
                AircraftHistoryStore.MarkWatched(entry.Hex);
                Toast.MakeText(this, Loc.T("Alert obserwowania dodany (Alerty → edytuj, by zmienić obszar i zdarzenia)"), ToastLength.Long)?.Show();
            }

            FillList();
        }

        /// <summary>
        /// Asks before the history is cleared.
        /// </summary>
        private void ConfirmClear()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Wyczyścić historię?"));
            builder.SetMessage(Loc.T("Przypięte samoloty zostaną zachowane."));
            builder.SetPositiveButton(Loc.T("Wyczyść"), (sender, args) =>
            {
                AircraftHistoryStore.ClearUnpinned();
                FillList();
            });
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

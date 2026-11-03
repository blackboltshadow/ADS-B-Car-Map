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
    /// List of the notifications that the alerts showed, the newest first, with a button that clears it.
    /// </summary>
    [Activity(Label = "Historia alertów", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AlertHistoryActivity : Activity
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
            Title = Loc.T("Historia alertów");

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
            clearButton.Text = Loc.T("Wyczyść historię");
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
        /// Fills the list with the entries, the newest first.
        /// </summary>
        private void FillList()
        {
            listLayout!.RemoveAllViews();
            List<AlertHistoryEntry> entries = AlertHistoryStore.GetNewestFirst();

            if (entries.Count == 0)
            {
                TextView empty = new TextView(this);
                empty.Text = Loc.T("Historia jest pusta. Tu pojawią się powiadomienia, które pokazały alerty.");
                empty.SetPadding(0, Dp(16), 0, 0);
                listLayout.AddView(empty);

                return;
            }

            foreach (AlertHistoryEntry entry in entries)
            {
                listLayout.AddView(CreateRow(entry));
            }
        }

        /// <summary>
        /// Creates the row of an entry: the time, the alert and the aircraft, and the event with the details.
        /// </summary>
        /// <param name="entry">Entry to show.</param>
        /// <returns>The row.</returns>
        private View CreateRow(AlertHistoryEntry entry)
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Vertical;
            row.SetPadding(0, Dp(8), 0, Dp(8));

            string name = string.IsNullOrWhiteSpace(entry.RuleName) ? Loc.T("Alert") : entry.RuleName;
            TextView title = new TextView(this);
            title.Text = entry.TimeUtc.ToLocalTime().ToString("dd.MM HH:mm") + "  " + name;
            title.TextSize = 16f;
            title.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(title);

            TextView details = new TextView(this);
            details.Text = AlertText.DescribeTrigger(entry.Trigger) + ": " + entry.Summary;
            details.TextSize = 13f;
            details.SetTextColor(Color.Rgb(180, 180, 180));
            row.AddView(details);

            return row;
        }

        /// <summary>
        /// Asks before the history is cleared.
        /// </summary>
        private void ConfirmClear()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Wyczyścić historię?"));
            builder.SetPositiveButton(Loc.T("Wyczyść"), (sender, args) =>
            {
                AlertHistoryStore.Clear();
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

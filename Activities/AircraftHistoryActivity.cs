using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// List of the aircraft that the user selected on the map or watched with an alert. It has a search box and group filters at the top, a card with icon buttons for each aircraft and a menu with the clear action.
    /// </summary>
    [Activity(Label = "Historia samolotów", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AircraftHistoryActivity : Activity
    {
        /// <summary>
        /// Identifier of the menu item that clears the history.
        /// </summary>
        private const int MenuClearId = 1;

        /// <summary>
        /// Container of the cards.
        /// </summary>
        private LinearLayout? listLayout;

        /// <summary>
        /// Text with the number of the shown and all entries.
        /// </summary>
        private TextView? summaryText;

        /// <summary>
        /// Search box.
        /// </summary>
        private EditText? searchInput;

        /// <summary>
        /// Chips of the group filter, in the order of <see cref="AircraftHistoryFilterMode"/>.
        /// </summary>
        private readonly List<TextView> chips = new List<TextView>();

        /// <summary>
        /// Group shown now.
        /// </summary>
        private AircraftHistoryFilterMode mode = AircraftHistoryFilterMode.All;

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
        /// Adds the menu with the clear action to the action bar.
        /// </summary>
        /// <param name="menu">Menu of the bar.</param>
        /// <returns>True, so that the menu is shown.</returns>
        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            menu?.Add(0, MenuClearId, 0, Loc.T("Wyczyść historię"))?.SetShowAsAction(ShowAsAction.Never);

            return true;
        }

        /// <summary>
        /// Handles the arrow in the action bar and the menu.
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

            if (item.ItemId == MenuClearId)
            {
                ConfirmClear();

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        /// <summary>
        /// Creates the view with the search box, the group chips and the list.
        /// </summary>
        /// <returns>The root view.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Vertical;
            header.SetPadding(Dp(16), Dp(8), Dp(16), Dp(4));
            root.AddView(header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            searchInput = new EditText(this);
            searchInput.Hint = Loc.T("Szukaj: znak, rejestracja, typ, adres");
            searchInput.SetSingleLine(true);
            searchInput.ImeOptions = ImeAction.Search;
            searchInput.TextSize = 15f;
            searchInput.SetTextColor(Color.White);
            searchInput.SetHintTextColor(Color.Rgb(150, 150, 150));
            searchInput.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
            searchInput.SetCompoundDrawablesRelativeWithIntrinsicBounds(Resource.Drawable.ic_search, 0, 0, 0);
            searchInput.CompoundDrawablePadding = Dp(10);
            searchInput.Background = HistoryViews.RoundedBackground(this, Color.Argb(60, 255, 255, 255), Color.Argb(110, 255, 255, 255), 24);
            searchInput.TextChanged += (sender, args) => FillList();
            searchInput.EditorAction += (sender, args) =>
            {
                HideKeyboard();
                args.Handled = true;
            };
            header.AddView(searchInput, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            LinearLayout chipRow = new LinearLayout(this);
            chipRow.Orientation = Orientation.Horizontal;
            chipRow.SetPadding(0, Dp(10), 0, Dp(2));
            header.AddView(chipRow);

            AddChip(chipRow, Loc.T("Wszystkie"), AircraftHistoryFilterMode.All);
            AddChip(chipRow, Loc.T("Przypięte"), AircraftHistoryFilterMode.Pinned);
            AddChip(chipRow, Loc.T("Obserwowane"), AircraftHistoryFilterMode.Watched);
            UpdateChips();

            summaryText = new TextView(this);
            summaryText.TextSize = 12f;
            summaryText.SetTextColor(Color.Rgb(160, 160, 160));
            summaryText.SetPadding(Dp(20), Dp(4), Dp(20), Dp(4));
            root.AddView(summaryText);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            listLayout = new LinearLayout(this);
            listLayout.Orientation = Orientation.Vertical;
            listLayout.SetPadding(Dp(12), Dp(2), Dp(12), Dp(24));
            scrollView.AddView(listLayout);

            return root;
        }

        /// <summary>
        /// Adds a chip of the group filter.
        /// </summary>
        /// <param name="parent">Row of the chips.</param>
        /// <param name="text">Caption.</param>
        /// <param name="chipMode">Group that the chip selects.</param>
        private void AddChip(LinearLayout parent, string text, AircraftHistoryFilterMode chipMode)
        {
            TextView chip = new TextView(this);
            chip.Text = text;
            chip.TextSize = 13f;
            chip.SetPadding(Dp(14), Dp(7), Dp(14), Dp(7));
            chip.Click += (sender, args) =>
            {
                mode = chipMode;
                UpdateChips();
                FillList();
            };
            chips.Add(chip);

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, 0, Dp(8), 0);
            parent.AddView(chip, parameters);
        }

        /// <summary>
        /// Highlights the chip of the group shown now.
        /// </summary>
        private void UpdateChips()
        {
            for (int index = 0; index < chips.Count; index++)
            {
                HistoryViews.StyleChip(this, chips[index], index == (int)mode);
            }
        }

        /// <summary>
        /// Fills the list with the entries that match the search text and the group.
        /// </summary>
        private void FillList()
        {
            if (listLayout == null || summaryText == null)
            {
                return;
            }

            listLayout.RemoveAllViews();

            HashSet<string> watchedNow = new HashSet<string>(
                AppServices.Alerts.Rules.Where((AlertRule rule) => rule.WatchedHex != null).Select((AlertRule rule) => rule.WatchedHex!.ToLowerInvariant()));
            int total = AircraftHistoryStore.GetForDisplay(null, AircraftHistoryFilterMode.All, watchedNow).Count;
            List<AircraftHistoryEntry> entries = AircraftHistoryStore.GetForDisplay(searchInput?.Text, mode, watchedNow);

            summaryText.Text = total == 0 ? string.Empty : Loc.F("Pasuje: {0} z {1}", entries.Count, total);

            if (entries.Count == 0)
            {
                TextView empty = new TextView(this);
                empty.Text = total == 0
                    ? Loc.T("Historia jest pusta. Tu pojawią się samoloty, które zaznaczysz na mapie lub które zgłosi alert „Obserwuj”.")
                    : Loc.T("Nic nie pasuje do wyszukiwania i filtra.");
                empty.TextSize = 15f;
                empty.SetTextColor(Color.Rgb(190, 190, 190));
                empty.SetPadding(Dp(8), Dp(24), Dp(8), 0);
                listLayout.AddView(empty);

                return;
            }

            foreach (AircraftHistoryEntry entry in entries)
            {
                listLayout.AddView(CreateCard(entry, watchedNow.Contains(entry.Hex)));
            }
        }

        /// <summary>
        /// Creates the card of an entry: the name with the marks, the details, the times and the icon buttons.
        /// </summary>
        /// <param name="entry">Entry to show.</param>
        /// <param name="watchedNow">Whether a watch alert for the aircraft exists now.</param>
        /// <returns>The card.</returns>
        private View CreateCard(AircraftHistoryEntry entry, bool watchedNow)
        {
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(16), Dp(12), Dp(10), Dp(8));
            card.Background = HistoryViews.CardBackground(this);
            card.Clickable = true;
            card.Click += (sender, args) => OpenDetails(entry);

            LinearLayout.LayoutParams cardParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            cardParameters.SetMargins(0, Dp(5), 0, Dp(5));
            card.LayoutParameters = cardParameters;

            string name = string.IsNullOrWhiteSpace(entry.Callsign) ? entry.Hex.ToUpperInvariant() : entry.Callsign!;
            TextView title = new TextView(this);
            title.Text = name;
            title.TextSize = 19f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            title.SetSingleLine(true);
            title.Ellipsize = TextUtils.TruncateAt.End;
            card.AddView(title);

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
            details.TextSize = 14f;
            details.SetTextColor(Color.Rgb(120, 190, 255));
            card.AddView(details);

            TextView times = new TextView(this);
            times.Text = Loc.F("Ostatnio: {0} · pierwszy raz: {1} · razy: {2}", entry.LastSeenUtc.ToLocalTime().ToString("dd.MM HH:mm"), entry.FirstSeenUtc.ToLocalTime().ToString("dd.MM HH:mm"), entry.Count)
                + (entry.Watched || watchedNow ? " · " + Loc.T("obserwowany") : string.Empty);
            times.TextSize = 12f;
            times.SetTextColor(Color.Rgb(160, 160, 160));
            times.SetPadding(0, Dp(2), 0, Dp(6));
            card.AddView(times);

            TextView hint = new TextView(this);
            hint.Text = Loc.T("Dotknij, aby zobaczyć przeloty i obserwacje ›");
            hint.TextSize = 12f;
            hint.SetTextColor(Color.Rgb(120, 190, 255));
            card.AddView(hint);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetGravity(GravityFlags.End);
            buttons.AddView(HistoryViews.IconButton(this, Resource.Drawable.ic_my_location, Loc.T("Pokaż na mapie"), false, Color.White, () => ShowOnMap(entry)));
            buttons.AddView(HistoryViews.IconButton(this, Resource.Drawable.ic_pin, entry.Pinned ? Loc.T("Odepnij") : Loc.T("Przypnij"), entry.Pinned, Color.Rgb(255, 200, 60), () =>
            {
                AircraftHistoryStore.SetPinned(entry.Hex, !entry.Pinned);
                FillList();
            }));
            buttons.AddView(HistoryViews.IconButton(this, Resource.Drawable.ic_visibility, watchedNow ? Loc.T("Nie obserwuj") : Loc.T("Obserwuj"), watchedNow, Color.Rgb(120, 190, 255), () => ToggleWatch(entry, watchedNow)));
            buttons.AddView(HistoryViews.IconButton(this, Resource.Drawable.ic_delete, Loc.T("Usuń"), false, Color.White, () =>
            {
                AircraftHistoryStore.Remove(entry.Hex);
                FillList();
            }));
            card.AddView(buttons);

            return card;
        }

        /// <summary>
        /// Opens the screen with the flights and the sightings of an aircraft.
        /// </summary>
        /// <param name="entry">Entry of the aircraft.</param>
        private void OpenDetails(AircraftHistoryEntry entry)
        {
            Intent intent = new Intent(this, typeof(AircraftSightingsActivity));
            intent.PutExtra(AircraftSightingsActivity.HexExtra, entry.Hex);
            StartActivity(intent);
        }

        /// <summary>
        /// Hides the on-screen keyboard.
        /// </summary>
        private void HideKeyboard()
        {
            InputMethodManager? manager = GetSystemService(InputMethodService) as InputMethodManager;
            manager?.HideSoftInputFromWindow(searchInput?.WindowToken, HideSoftInputFlags.None);
            searchInput?.ClearFocus();
        }

        /// <summary>
        /// Asks the map to show and follow the aircraft and brings the map to the front.
        /// </summary>
        /// <param name="entry">Entry to show.</param>
        private void ShowOnMap(AircraftHistoryEntry entry)
        {
            HistoryViews.ShowOnMap(this, entry.Hex);
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

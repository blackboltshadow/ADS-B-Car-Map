using AdsbCarMap.Localization;
using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// "Log zapytań" screen: the requests that the application sent to external servers (aircraft, routes, aircraft details, map tiles), with a filter for errors and for a single host.
    /// </summary>
    [Activity(Label = "Log zapytań", Exported = false, Theme = "@android:style/Theme.Material")]
    public class RequestLogActivity : Activity
    {
        /// <summary>
        /// Maximum number of requests drawn on the screen.
        /// </summary>
        private const int MaxShownEntries = 200;

        /// <summary>
        /// Time in milliseconds by which a refresh is delayed, so that a burst of requests redraws the screen once.
        /// </summary>
        private const int RefreshDelayMilliseconds = 500;

        /// <summary>
        /// Text of the host filter entry that stands for every host.
        /// </summary>
        private const string AllHostsText = "Wszystkie hosty";

        /// <summary>
        /// Container of the rows of requests; rebuilt after the log or the filter changes.
        /// </summary>
        private LinearLayout? list;

        /// <summary>
        /// Summary line above the rows.
        /// </summary>
        private TextView? summary;

        /// <summary>
        /// Check box that limits the list to the failed requests.
        /// </summary>
        private CheckBox? onlyErrors;

        /// <summary>
        /// Drop-down list of the hosts that limits the list to one of them.
        /// </summary>
        private Spinner? hostSpinner;

        /// <summary>
        /// Hosts currently offered by <see cref="hostSpinner"/>, without the entry for every host.
        /// </summary>
        private List<string> offeredHosts = new List<string>();

        /// <summary>
        /// Handler of the main thread used to delay the refresh.
        /// </summary>
        private Handler? handler;

        /// <summary>
        /// Whether a refresh is already planned.
        /// </summary>
        private bool refreshPending;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Log zapytań");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            handler = new Handler(Looper.MainLooper!);

            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(root);

            root.AddView(CreateText(Loc.T("Log zapytań"), 20f, Color.White, true));
            root.AddView(CreateText(Loc.T("Zapytania wysyłane przez aplikację na zewnątrz: pozycje samolotów, trasy, dane samolotów i kafelki mapy. Log jest tylko w pamięci i znika po zamknięciu aplikacji. Adres zawiera wysyłane współrzędne (po zaokrągleniu według ustawień prywatności)."), 13f, Color.Rgb(180, 180, 180), false));

            onlyErrors = new CheckBox(this);
            onlyErrors.Text = Loc.T("Tylko błędy");
            onlyErrors.SetTextColor(Color.White);
            onlyErrors.CheckedChange += (sender, args) => Render();
            root.AddView(onlyErrors);

            hostSpinner = new Spinner(this);
            hostSpinner.ItemSelected += (sender, args) => Render();
            root.AddView(hostSpinner);

            Button clear = new Button(this);
            clear.Text = Loc.T("Wyczyść");
            clear.Click += (sender, args) => AppServices.Requests.Clear();
            root.AddView(clear);

            summary = CreateText(string.Empty, 14f, Color.Rgb(120, 190, 255), true);
            root.AddView(summary);

            list = new LinearLayout(this);
            list.Orientation = Orientation.Vertical;
            root.AddView(list);

            SetContentView(scrollView);
            UpdateHosts();
            Render();
        }

        /// <summary>
        /// Shows the log again and starts following its changes.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            AppServices.Requests.Changed += OnLogChanged;
            UpdateHosts();
            Render();
        }

        /// <summary>
        /// Stops following the changes of the log.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Requests.Changed -= OnLogChanged;
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
        /// Plans a refresh of the screen after the log changed, on any thread.
        /// </summary>
        private void OnLogChanged()
        {
            handler?.Post(() =>
            {
                if (refreshPending)
                {
                    return;
                }

                refreshPending = true;
                handler!.PostDelayed(() =>
                {
                    refreshPending = false;
                    UpdateHosts();
                    Render();
                }, RefreshDelayMilliseconds);
            });
        }

        /// <summary>
        /// Updates the host list when new hosts appeared, keeping the chosen host.
        /// </summary>
        private void UpdateHosts()
        {
            if (hostSpinner == null)
            {
                return;
            }

            List<string> hosts = AppServices.Requests.GetHosts();

            if (hosts.SequenceEqual(offeredHosts))
            {
                return;
            }

            string? chosen = GetChosenHost();
            offeredHosts = hosts;

            List<string> items = new List<string> { AllHostsText };
            items.AddRange(hosts);

            ArrayAdapter<string> adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, items);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            hostSpinner.Adapter = adapter;

            int index = chosen == null ? -1 : hosts.IndexOf(chosen);
            hostSpinner.SetSelection(index + 1);
        }

        /// <summary>
        /// Returns the host chosen in the drop-down list.
        /// </summary>
        /// <returns>The host or null for every host.</returns>
        private string? GetChosenHost()
        {
            int position = hostSpinner?.SelectedItemPosition ?? 0;

            return position > 0 && position <= offeredHosts.Count ? offeredHosts[position - 1] : null;
        }

        /// <summary>
        /// Fills the list with the requests that match the filter.
        /// </summary>
        private void Render()
        {
            if (list == null || summary == null)
            {
                return;
            }

            list.RemoveAllViews();

            List<RequestLogEntry> all = AppServices.Requests.GetEntries();
            List<RequestLogEntry> entries = RequestLog.Filter(all, onlyErrors?.Checked ?? false, GetChosenHost());
            int shown = Math.Min(entries.Count, MaxShownEntries);
            int errors = all.Count(entry => entry.IsError);

            summary.Text = Loc.F("Zapytań: {0}, błędów: {1}, na liście: {2}{3}", all.Count, errors, entries.Count, shown < entries.Count ? Loc.T(" (pokazano ") + shown + Loc.T(" najnowszych)") : string.Empty);

            for (int index = 0; index < shown; index++)
            {
                AddEntry(entries[index]);
            }
        }

        /// <summary>
        /// Adds the rows that describe one request.
        /// </summary>
        /// <param name="entry">The request.</param>
        private void AddEntry(RequestLogEntry entry)
        {
            LinearLayout block = new LinearLayout(this);
            block.Orientation = Orientation.Vertical;

            LinearLayout.LayoutParams blockParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            blockParams.SetMargins(0, Dp(10), 0, 0);
            list!.AddView(block, blockParams);

            string outcome = entry.Cancelled ? "anulowano" : entry.StatusCode.HasValue ? entry.StatusCode.Value.ToString(CultureInfo.InvariantCulture) : Loc.T("brak odpowiedzi");
            Color color = entry.IsError ? Color.Rgb(255, 110, 110) : entry.Cancelled ? Color.Rgb(150, 150, 150) : Color.White;

            block.AddView(CreateText(entry.Method + "  " + entry.Host + "  " + outcome + "  " + entry.Duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms", 14f, color, true));

            TextView path = CreateText(entry.PathAndQuery, 11f, Color.Rgb(200, 200, 200), false);
            path.Typeface = Typeface.Monospace;
            block.AddView(path);

            if (entry.Error != null)
            {
                block.AddView(CreateText(entry.Error, 12f, Color.Rgb(255, 180, 80), false));
            }

            block.AddView(CreateText(entry.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), 11f, Color.Rgb(150, 150, 150), false));
        }

        /// <summary>
        /// Creates a text view.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <returns>The text view.</returns>
        private TextView CreateText(string text, float size, Color color, bool bold)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);

            if (bold)
            {
                view.SetTypeface(null, TypefaceStyle.Bold);
            }

            return view;
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

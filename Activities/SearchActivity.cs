using System.Globalization;
using System.Text.Json;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Search window for aircraft by flight, registration, type and route, and for airports by code, name and city. Airports come before aircraft that match equally well. Picking an aircraft centers the map on it, picking an airport selects it and centers the map on it.
    /// </summary>
    [Activity(Label = "Wyszukaj", Exported = false, Theme = "@android:style/Theme.Material")]
    public class SearchActivity : Activity
    {
        /// <summary>
        /// Tag used in the Android log.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Maximum number of results in the list.
        /// </summary>
        private const int MaxResults = 100;

        /// <summary>
        /// Number of rows kept for aircraft when many airports match.
        /// </summary>
        private const int ReservedForAircraft = 20;

        /// <summary>
        /// Interval between refreshes of the results list in milliseconds.
        /// </summary>
        private const int RefreshIntervalMilliseconds = 2000;

        /// <summary>
        /// Age of the data in seconds after which the window downloads the aircraft itself, when no map refreshes them.
        /// </summary>
        private const double StaleDataSeconds = 10.0;

        /// <summary>
        /// Radius of the aircraft request made by this window, in nautical miles.
        /// </summary>
        private const double FetchRadiusNauticalMiles = 100.0;

        /// <summary>
        /// Main thread handler used for the periodic refresh.
        /// </summary>
        private readonly Handler handler = new Handler(Looper.MainLooper!);

        /// <summary>
        /// Search text field.
        /// </summary>
        private EditText? searchInput;

        /// <summary>
        /// Information about the number and the age of the data.
        /// </summary>
        private TextView? statusText;

        /// <summary>
        /// Container of the search results.
        /// </summary>
        private LinearLayout? resultsLayout;

        /// <summary>
        /// Periodic refresh task, null when the activity is not visible.
        /// </summary>
        private Java.Lang.Runnable? refreshRunnable;

        /// <summary>
        /// Whether the window is downloading aircraft at the moment.
        /// </summary>
        private bool isFetching;

        /// <summary>
        /// Builds the search screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Closes the window when the back arrow is tapped.
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
        /// Starts the periodic refresh of the results.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            refreshRunnable = new Java.Lang.Runnable(RefreshTick);
            handler.Post(refreshRunnable);
        }

        /// <summary>
        /// Stops the periodic refresh of the results.
        /// </summary>
        protected override void OnPause()
        {
            if (refreshRunnable != null)
            {
                handler.RemoveCallbacks(refreshRunnable);
                refreshRunnable = null;
            }

            base.OnPause();
        }

        /// <summary>
        /// Creates the whole view of the screen.
        /// </summary>
        /// <returns>View with a fixed search field at the top and a scrollable list of results under it.</returns>
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
            searchInput.Hint = "Szukaj: lot, rejestracja, typ, lotnisko";
            searchInput.SetSingleLine(true);
            searchInput.TextChanged += (sender, args) => RenderResults();
            header.AddView(searchInput);

            statusText = new TextView(this);
            statusText.SetPadding(0, Dp(4), 0, Dp(4));
            statusText.TextSize = 12f;
            header.AddView(statusText);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            resultsLayout = new LinearLayout(this);
            resultsLayout.Orientation = Orientation.Vertical;
            resultsLayout.SetPadding(0, 0, 0, Dp(32));
            scrollView.AddView(resultsLayout);

            return root;
        }

        /// <summary>
        /// Refreshes the results, downloads fresh data when needed and schedules the next refresh.
        /// </summary>
        private void RefreshTick()
        {
            double ageSeconds = (DateTime.UtcNow - LiveTraffic.GetSnapshot().TimestampUtc).TotalSeconds;

            if (ageSeconds > StaleDataSeconds && !isFetching)
            {
                _ = FetchAircraftAsync();
            }

            RenderResults();

            if (refreshRunnable != null)
            {
                handler.PostDelayed(refreshRunnable, RefreshIntervalMilliseconds);
            }
        }

        /// <summary>
        /// Downloads the aircraft around the center of the last request, when no map provides fresh data.
        /// </summary>
        /// <returns>Task representing the download.</returns>
        private async Task FetchAircraftAsync()
        {
            isFetching = true;

            try
            {
                (double Latitude, double Longitude) center = LiveTraffic.GetCenter();
                IReadOnlyList<Aircraft> fetched = await Task.Run(() => AppServices.Adsb.GetAircraftAsync(center.Latitude, center.Longitude, FetchRadiusNauticalMiles, CancellationToken.None));
                LiveTraffic.Update(fetched, DateTime.UtcNow, center.Latitude, center.Longitude);
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is System.OperationCanceledException || exception is JsonException)
            {
                Log.Warn(LogTag, "Pobieranie samolotów w oknie wyszukiwania nie powiodło się: " + exception.Message);
            }
            finally
            {
                isFetching = false;
            }

            RenderResults();
        }

        /// <summary>
        /// Shows the airports and aircraft matching the text of the search field.
        /// </summary>
        private void RenderResults()
        {
            if (resultsLayout == null || statusText == null || searchInput == null)
            {
                return;
            }

            (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) snapshot = LiveTraffic.GetSnapshot();
            (double Latitude, double Longitude) center = LiveTraffic.GetCenter();
            string query = searchInput.Text ?? string.Empty;
            List<SearchResult> results = AircraftSearch.SearchAll(snapshot.Aircraft, AppServices.Airports, AppServices.Routes, query, MaxResults, ReservedForAircraft, center.Latitude, center.Longitude);
            int aircraftCount = results.Count(item => item.Aircraft != null);

            statusText.Text = snapshot.Aircraft.Count == 0
                ? (isFetching ? "Pobieranie samolotów…" : "Brak danych o samolotach")
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Samolotów w zasięgu: {0}, pokazano {1} samolotów i {2} lotnisk, dane sprzed {3:0} s",
                    snapshot.Aircraft.Count,
                    aircraftCount,
                    results.Count - aircraftCount,
                    (DateTime.UtcNow - snapshot.TimestampUtc).TotalSeconds);

            resultsLayout.RemoveAllViews();

            for (int index = 0; index < results.Count; index++)
            {
                SearchResult result = results[index];

                resultsLayout.AddView(result.Airport != null ? CreateAirportRow(result.Airport, index) : CreateRow(result.Aircraft!, index));
            }
        }

        /// <summary>
        /// Creates a list row of an airport: the code and the name in the first line, the place, the size class and the elevation in the second. Even rows have a lighter background.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="index">Number of the row on the list.</param>
        /// <returns>Clickable list row.</returns>
        private View CreateAirportRow(Airport airport, int index)
        {
            string icao = airport.Icao;

            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Vertical;
            row.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);
            row.Clickable = true;
            row.Click += (sender, args) => SelectAirport(icao);

            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Horizontal;
            row.AddView(header);

            TextView name = new TextView(this);
            name.Text = AircraftSearch.GetAirportTitle(airport);
            name.TextSize = 18f;
            name.SetTypeface(null, TypefaceStyle.Bold);
            header.AddView(name, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            TextView kind = new TextView(this);
            kind.Text = "LOTNISKO";
            kind.TextSize = 11f;
            kind.SetTextColor(airport.IsMilitary ? Color.Rgb(235, 80, 80) : Color.Rgb(120, 190, 255));
            header.AddView(kind, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            TextView details = new TextView(this);
            details.Text = AircraftSearch.DescribeAirport(airport);
            details.TextSize = 13f;
            details.SetTextColor(Color.Rgb(190, 190, 190));
            row.AddView(details);

            return row;
        }

        /// <summary>
        /// Creates a list row of an aircraft: the flight and the registration in the first line, the airline in the second, the type, route and altitude in the third. Even rows have a lighter background.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="index">Number of the row on the list.</param>
        /// <returns>Clickable list row.</returns>
        private View CreateRow(Aircraft item, int index)
        {
            string hex = item.Hex;
            FlightRoute? route = AppServices.Routes.Get(item.Callsign);
            string airline = AircraftSearch.GetAirline(item, route);
            string description = AircraftSearch.Describe(item, route);

            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Vertical;
            row.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);
            row.Clickable = true;
            row.Click += (sender, args) => SelectAircraft(hex);

            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Horizontal;
            row.AddView(header);

            TextView name = new TextView(this);
            name.Text = AircraftSearch.GetName(item);
            name.TextSize = 18f;
            name.SetTypeface(null, TypefaceStyle.Bold);
            header.AddView(name, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            if (!string.IsNullOrEmpty(item.Registration))
            {
                TextView registration = new TextView(this);
                registration.Text = item.Registration;
                registration.TextSize = 13f;
                registration.SetTextColor(Color.Rgb(170, 170, 170));
                header.AddView(registration, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            }

            if (!string.IsNullOrEmpty(airline))
            {
                TextView airlineText = new TextView(this);
                airlineText.Text = airline;
                airlineText.TextSize = 14f;
                airlineText.SetTextColor(Color.Rgb(120, 190, 255));
                row.AddView(airlineText);
            }

            if (!string.IsNullOrEmpty(description))
            {
                TextView details = new TextView(this);
                details.Text = description;
                details.TextSize = 13f;
                details.SetTextColor(Color.Rgb(190, 190, 190));
                row.AddView(details);
            }

            return row;
        }

        /// <summary>
        /// Asks the maps to follow the picked aircraft and returns to the map.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        private void SelectAircraft(string hex)
        {
            LiveTraffic.RequestFocus(hex);
            Finish();
        }

        /// <summary>
        /// Asks the maps to select the picked airport and center on it, then returns to the map.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        private void SelectAirport(string icao)
        {
            LiveTraffic.RequestAirportFocus(icao);
            Finish();
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

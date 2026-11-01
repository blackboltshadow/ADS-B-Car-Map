using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen for managing the favorite airports. Favorites are always visible on the map, whatever the airport categories chosen in the settings. The list can be reordered, an entry shows the airport details with a button to show it on the map, and new airports are added with the search field at the top.
    /// </summary>
    [Activity(Label = "Ulubione lotniska", Exported = false, Theme = "@android:style/Theme.Material")]
    public class FavoriteAirportsActivity : Activity
    {
        /// <summary>
        /// Maximum number of airports listed as the result of a search.
        /// </summary>
        private const int MaxSearchResults = 30;

        /// <summary>
        /// Search field used to find airports to add.
        /// </summary>
        private EditText? searchInput;

        /// <summary>
        /// Hint shown under the search field.
        /// </summary>
        private TextView? hintText;

        /// <summary>
        /// Container of the rows.
        /// </summary>
        private LinearLayout? listLayout;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Starts listening for changes of the favorites and of the airport data, and shows the list.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            AppServices.Favorites.Changed += OnDataChanged;
            AppServices.Airports.Loaded += OnDataChanged;
            Render();
        }

        /// <summary>
        /// Stops listening for changes.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Favorites.Changed -= OnDataChanged;
            AppServices.Airports.Loaded -= OnDataChanged;

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
        /// <returns>View with the search field at the top and the scrollable list under it.</returns>
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
            searchInput.Hint = "Dodaj lotnisko: kod, nazwa lub miasto";
            searchInput.SetSingleLine(true);
            searchInput.TextChanged += (sender, args) => Render();
            header.AddView(searchInput);

            hintText = new TextView(this);
            hintText.TextSize = 12f;
            hintText.SetTextColor(Color.Rgb(180, 180, 180));
            hintText.SetPadding(0, Dp(4), 0, Dp(4));
            header.AddView(hintText);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            listLayout = new LinearLayout(this);
            listLayout.Orientation = Orientation.Vertical;
            listLayout.SetPadding(0, 0, 0, Dp(32));
            scrollView.AddView(listLayout);

            return root;
        }

        /// <summary>
        /// Refreshes the screen on the main thread after the favorites or the airport data changed.
        /// </summary>
        private void OnDataChanged()
        {
            RunOnUiThread(Render);
        }

        /// <summary>
        /// Shows the favorites, or the airports matching the text of the search field.
        /// </summary>
        private void Render()
        {
            if (listLayout == null || hintText == null || searchInput == null)
            {
                return;
            }

            listLayout.RemoveAllViews();

            if (!AppServices.Airports.IsLoaded)
            {
                hintText.Text = "Wczytywanie bazy lotnisk…";

                return;
            }

            string query = (searchInput.Text ?? string.Empty).Trim();

            if (query.Length > 0)
            {
                RenderSearchResults(query);

                return;
            }

            IReadOnlyList<string> codes = AppServices.Favorites.Codes;

            hintText.Text = codes.Count == 0
                ? "Brak ulubionych. Wpisz kod lub nazwę, aby dodać lotnisko, albo dotknij gwiazdki na karcie lotniska na mapie."
                : "Ulubione są zawsze widoczne na mapie (od zoomu 5), niezależnie od wybranych rodzajów lotnisk. Kolejność wpływa na listę tutaj i w wyszukiwarce.";

            for (int index = 0; index < codes.Count; index++)
            {
                listLayout.AddView(CreateFavoriteRow(codes[index], index, codes.Count));
            }
        }

        /// <summary>
        /// Shows the airports matching a query, each with a button that adds it to the favorites.
        /// </summary>
        /// <param name="query">Text of the search field.</param>
        private void RenderSearchResults(string query)
        {
            List<string> terms = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(AirportDatabase.Fold)
                .ToList();
            List<(Airport Airport, int Quality)> results = AppServices.Airports.Search(terms, MaxSearchResults, true, airport => AppServices.Favorites.Contains(airport.Icao));

            hintText!.Text = results.Count == 0 ? "Brak pasujących lotnisk." : "Dotknij „＋”, aby dodać lotnisko do ulubionych, albo wiersza, aby zobaczyć jego dane.";

            for (int index = 0; index < results.Count; index++)
            {
                listLayout!.AddView(CreateSearchRow(results[index].Airport, index));
            }
        }

        /// <summary>
        /// Creates the row of a favorite: the airport, buttons that move it and a button that removes it. Tapping the text opens the details.
        /// </summary>
        /// <param name="icao">ICAO code of the favorite.</param>
        /// <param name="index">Position on the list.</param>
        /// <param name="count">Number of favorites.</param>
        /// <returns>The row.</returns>
        private View CreateFavoriteRow(string icao, int index, int count)
        {
            Airport? airport = AppServices.Airports.FindByIcao(icao);

            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(12), Dp(6), Dp(4), Dp(6));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);

            LinearLayout texts = CreateTexts(airport != null ? AirportTitle(airport) : icao, airport != null ? AircraftSearch.DescribeAirport(airport) : "brak w bazie danych, można usunąć");
            texts.Clickable = airport != null;

            if (airport != null)
            {
                texts.Click += (sender, args) => ShowDetails(airport);
            }

            row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            row.AddView(CreateButton("▲", "Przesuń wyżej", index > 0, () => AppServices.Favorites.Move(icao, -1)));
            row.AddView(CreateButton("▼", "Przesuń niżej", index < count - 1, () => AppServices.Favorites.Move(icao, 1)));
            row.AddView(CreateButton("✕", "Usuń z ulubionych", true, () => AppServices.Favorites.Remove(icao)));

            return row;
        }

        /// <summary>
        /// Creates the row of a search result: the airport and a button that adds it to the favorites, or a star when it is already there.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="index">Position on the list.</param>
        /// <returns>The row.</returns>
        private View CreateSearchRow(Airport airport, int index)
        {
            bool isFavorite = AppServices.Favorites.Contains(airport.Icao);

            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(12), Dp(6), Dp(4), Dp(6));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);

            LinearLayout texts = CreateTexts(airport.Code + " · " + airport.Name, AircraftSearch.DescribeAirport(airport));
            texts.Clickable = true;
            texts.Click += (sender, args) => ShowDetails(airport);
            row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            row.AddView(isFavorite
                ? CreateButton("★", "Usuń z ulubionych", true, () => AppServices.Favorites.Remove(airport.Icao))
                : CreateButton("＋", "Dodaj do ulubionych", true, () => AppServices.Favorites.Add(airport.Icao)));

            return row;
        }

        /// <summary>
        /// Returns the title of a favorite row: the code and the name.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Title text.</returns>
        private static string AirportTitle(Airport airport)
        {
            return airport.Code + " · " + airport.Name;
        }

        /// <summary>
        /// Creates the two-line text block of a row.
        /// </summary>
        /// <param name="title">First line, bold.</param>
        /// <param name="details">Second line, gray.</param>
        /// <returns>The text block.</returns>
        private LinearLayout CreateTexts(string title, string details)
        {
            LinearLayout texts = new LinearLayout(this);
            texts.Orientation = Orientation.Vertical;

            TextView titleText = new TextView(this);
            titleText.Text = title;
            titleText.TextSize = 17f;
            titleText.SetTextColor(Color.White);
            titleText.SetTypeface(null, TypefaceStyle.Bold);
            texts.AddView(titleText);

            TextView detailsText = new TextView(this);
            detailsText.Text = details;
            detailsText.TextSize = 13f;
            detailsText.SetTextColor(Color.Rgb(190, 190, 190));
            texts.AddView(detailsText);

            return texts;
        }

        /// <summary>
        /// Creates a small round button of a row.
        /// </summary>
        /// <param name="symbol">Symbol drawn in the button.</param>
        /// <param name="description">Description for accessibility services.</param>
        /// <param name="enabled">Whether the button can be tapped. A disabled button is dimmed.</param>
        /// <param name="action">Action performed on a tap.</param>
        /// <returns>The button.</returns>
        private View CreateButton(string symbol, string description, bool enabled, System.Action action)
        {
            TextView button = new TextView(this);
            button.Text = symbol;
            button.TextSize = 16f;
            button.SetTextColor(Color.White);
            button.Gravity = GravityFlags.Center;
            button.ContentDescription = description;
            button.Alpha = enabled ? 1.0f : 0.25f;

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(Color.Argb(60, 255, 255, 255));
            circle.SetStroke(Dp(1), Color.Argb(150, 255, 255, 255));
            button.Background = circle;

            if (enabled)
            {
                button.Click += (sender, args) => action();
            }

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(36), Dp(36));
            parameters.SetMargins(Dp(4), 0, Dp(4), 0);
            button.LayoutParameters = parameters;

            return button;
        }

        /// <summary>
        /// Shows the details of an airport in a dialog with buttons that show it on the map and add it to the favorites or remove it.
        /// </summary>
        /// <param name="airport">Airport.</param>
        private void ShowDetails(Airport airport)
        {
            (bool Known, double Latitude, double Longitude) user = LiveTraffic.GetUserLocation();
            List<string> lines = new List<string> { AirportCardText.BuildDescriptor(airport) };

            lines.AddRange(AirportCardText.BuildLines(airport, AppServices.Airports.GetRunways(airport), user.Known, user.Latitude, user.Longitude));

            bool isFavorite = AppServices.Favorites.Contains(airport.Icao);
            string icao = airport.Icao;

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(AirportCardText.BuildTitle(airport));
            builder.SetMessage(string.Join("\n", lines));
            builder.SetPositiveButton("Pokaż na mapie", (sender, args) =>
            {
                LiveTraffic.RequestAirportFocus(icao);
                Finish();
            });
            builder.SetNeutralButton(isFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych", (sender, args) => AppServices.Favorites.Toggle(icao));
            builder.SetNegativeButton("Zamknij", (sender, args) => { });
            builder.Show();
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

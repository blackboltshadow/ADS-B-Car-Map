using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Card of the selected airport overlaid on the map: code, name, size class, codes, place, elevation, distance from the user and runways, followed by the panels of the weather report and of the radio frequencies, which open and close.
    /// </summary>
    public sealed class AirportCardView : LinearLayout
    {
        /// <summary>
        /// Smallest height in dp that dragging gives to the card: enough for the handle and the title.
        /// </summary>
        private const int MinimumHeightDp = 110;

        /// <summary>
        /// Distance in dp from the full height within which a drag that ends makes the card fill the screen.
        /// </summary>
        private const int MaximizeSnapDp = 40;

        /// <summary>
        /// Distance in dp from the natural height within which a drag that ends gives the card its natural height again.
        /// </summary>
        private const int NaturalSnapDp = 24;

        /// <summary>
        /// Largest height of the card as a fraction of the screen height. The part of the card under its title scrolls when the content is taller, so that the map stays visible and every panel can be reached.
        /// </summary>
        private const float MaxHeightFraction = 0.6f;

        /// <summary>
        /// Color of the header of the frequency panel.
        /// </summary>
        private static readonly Color FrequencyColor = Color.Rgb(120, 190, 255);

        /// <summary>
        /// Color of the header of the forecast panel.
        /// </summary>
        private static readonly Color ForecastColor = Color.Rgb(255, 200, 110);

        /// <summary>
        /// Code and name of the airport.
        /// </summary>
        private readonly TextView nameText;

        /// <summary>
        /// Size class of the airport and whether it is military.
        /// </summary>
        private readonly TextView descriptorText;

        /// <summary>
        /// Detail lines.
        /// </summary>
        private readonly TextView detailsText;

        /// <summary>
        /// Star that adds the airport to the favorites or removes it.
        /// </summary>
        private readonly TextView favoriteButton;

        /// <summary>
        /// Scrolling part of the card: the descriptor, the details and the panels.
        /// </summary>
        private readonly ScrollView scroll;

        /// <summary>
        /// Button that makes the card fill the screen or brings it back to its size. It is drawn like the buttons that center the map and close the card.
        /// </summary>
        private readonly ImageView maximizeButton;

        /// <summary>
        /// Handle at the top of the card with three lines that tells that the card can be expanded; it expands the card with a tap or a swipe up, and minimizes it with a swipe down.
        /// </summary>
        private readonly ExpandHandleView expandHandle;

        /// <summary>
        /// Whether the card fills the screen.
        /// </summary>
        private bool isMaximized;

        /// <summary>
        /// Height in pixels that the user gave the card by dragging the handle, or 0 when the card has the height that its content needs (up to <see cref="MaxHeightFraction"/> of the screen).
        /// </summary>
        private int userHeight;

        /// <summary>
        /// Height of the card in pixels when the last drag started, which the drag moves from.
        /// </summary>
        private int dragStartHeight;

        /// <summary>
        /// Height of the card in pixels that its content needed when the last drag started from that height; a drag that ends close to it gives the card that height again.
        /// </summary>
        private int naturalHeight;

        /// <summary>
        /// Width, height and gravity of the card before it was maximized, restored when it is brought back.
        /// </summary>
        private (int Width, int Height, GravityFlags Gravity)? normalLayout;

        /// <summary>
        /// Panel of the weather report of the airport.
        /// </summary>
        private readonly CollapsiblePanelView weatherPanel;

        /// <summary>
        /// Panel of the forecast (TAF) of the airport.
        /// </summary>
        private readonly CollapsiblePanelView forecastPanel;

        /// <summary>
        /// Panel of the radio frequencies of the airport.
        /// </summary>
        private readonly CollapsiblePanelView frequencyPanel;

        /// <summary>
        /// ICAO code of the airport shown now, or null before the first airport; the panels return to the state chosen in the settings when it changes.
        /// </summary>
        private string? boundIcao;

        /// <summary>
        /// Stored answer with the weather of the airport shown now, or null when there is none.
        /// </summary>
        private WeatherEntry? weatherEntry;

        /// <summary>
        /// Whether the weather of the airport shown now is being downloaded.
        /// </summary>
        private bool weatherLoading;

        /// <summary>
        /// Runways of the airport shown now, which give the line with the runway that the wind favors.
        /// </summary>
        private IReadOnlyList<Runway> currentRunways = Array.Empty<Runway>();

        /// <summary>
        /// Stored answer with the forecast of the airport shown now, or null when there is none.
        /// </summary>
        private ForecastEntry? forecastEntry;

        /// <summary>
        /// Whether the forecast of the airport shown now is being downloaded.
        /// </summary>
        private bool forecastLoading;

        /// <summary>
        /// Creates the card and its views.
        /// </summary>
        /// <param name="context">Activity context.</param>
        public AirportCardView(Context context) : base(context)
        {
            Orientation = Orientation.Vertical;
            SetBackgroundResource(Resource.Drawable.bg_card);
            SetPadding(Dp(14), Dp(2), Dp(14), Dp(12));
            Clickable = true;

            expandHandle = new ExpandHandleView(context);
            expandHandle.ContentDescription = Loc.T("Rozszerz kartę lotniska");
            expandHandle.Tapped += () => SetMaximized(!isMaximized);
            expandHandle.DragStarted += OnDragStarted;
            expandHandle.Dragged += OnDragged;
            expandHandle.DragEnded += OnDragEnded;
            AddView(expandHandle, new LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(24)));

            LinearLayout header = new LinearLayout(context);
            header.Orientation = Orientation.Horizontal;
            header.SetGravity(GravityFlags.CenterVertical);
            AddView(header, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            nameText = CreateText(20f, Color.White, true);
            header.AddView(nameText, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            favoriteButton = CreateText(22f, Color.Rgb(255, 215, 0), true);
            favoriteButton.SetPadding(Dp(4), Dp(2), Dp(8), Dp(2));
            favoriteButton.Clickable = true;
            favoriteButton.ContentDescription = Loc.T("Dodaj lub usuń z ulubionych");
            favoriteButton.Click += (sender, args) => FavoriteClicked?.Invoke();
            header.AddView(favoriteButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            header.AddView(HistoryViews.IconButton(context, Resource.Drawable.ic_my_location, Loc.T("Wyśrodkuj"), false, Color.White, () => CenterClicked?.Invoke()));

            maximizeButton = HistoryViews.IconButton(context, Resource.Drawable.ic_fullscreen, Loc.T("Maksymalizuj kartę lotniska"), false, Color.White, () => SetMaximized(!isMaximized));
            header.AddView(maximizeButton);

            header.AddView(HistoryViews.IconButton(context, Resource.Drawable.ic_close, Loc.T("Zamknij kartę lotniska"), false, Color.White, () => CloseClicked?.Invoke()));

            scroll = new ScrollView(context);
            scroll.VerticalScrollBarEnabled = true;
            scroll.VerticalFadingEdgeEnabled = true;
            scroll.SetFadingEdgeLength(Dp(28));
            scroll.OverScrollMode = OverScrollMode.Never;
            AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            LinearLayout content = new LinearLayout(context);
            content.Orientation = Orientation.Vertical;
            scroll.AddView(content, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            descriptorText = CreateText(14f, Color.Rgb(120, 190, 255), false);
            content.AddView(descriptorText);

            detailsText = CreateText(13f, Color.White, false);
            detailsText.SetPadding(0, Dp(8), 0, 0);
            content.AddView(detailsText);

            weatherPanel = new CollapsiblePanelView(context);
            LayoutParams weatherParams = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            weatherParams.SetMargins(0, Dp(10), 0, 0);
            content.AddView(weatherPanel, weatherParams);

            forecastPanel = new CollapsiblePanelView(context);
            LayoutParams forecastParams = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            forecastParams.SetMargins(0, Dp(8), 0, 0);
            content.AddView(forecastPanel, forecastParams);

            frequencyPanel = new CollapsiblePanelView(context);
            LayoutParams frequencyParams = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            frequencyParams.SetMargins(0, Dp(8), 0, 0);
            content.AddView(frequencyPanel, frequencyParams);
        }

        /// <inheritdoc />
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int maxHeight = (int)(MaxHeightFraction * (Resources?.DisplayMetrics?.HeightPixels ?? 0));
            MeasureSpecMode mode = MeasureSpec.GetMode(heightMeasureSpec);

            if (!isMaximized && userHeight > 0)
            {
                int height = mode == MeasureSpecMode.Unspecified ? userHeight : Math.Min(userHeight, MeasureSpec.GetSize(heightMeasureSpec));

                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
            }
            else if (!isMaximized && maxHeight > 0 && (mode == MeasureSpecMode.Unspecified || MeasureSpec.GetSize(heightMeasureSpec) > maxHeight))
            {
                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(maxHeight, MeasureSpecMode.AtMost);
            }

            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        /// <summary>
        /// Whether the card fills the screen.
        /// </summary>
        public bool IsMaximized => isMaximized;

        /// <summary>
        /// Makes the card fill the screen, as a popup over the map like before, or brings it back to its size and place. The scrolling part takes all the height that is left under the title while the card is maximized.
        /// </summary>
        /// <param name="maximized">Whether the card should fill the screen.</param>
        public void SetMaximized(bool maximized)
        {
            if (maximized == isMaximized)
            {
                return;
            }

            isMaximized = maximized;
            userHeight = 0;
            MaximizedChanged?.Invoke(maximized);
            maximizeButton.SetImageResource(maximized ? Resource.Drawable.ic_fullscreen_exit : Resource.Drawable.ic_fullscreen);
            maximizeButton.ContentDescription = maximized ? Loc.T("Minimalizuj kartę lotniska") : Loc.T("Maksymalizuj kartę lotniska");
            expandHandle.IsExpanded = maximized;
            expandHandle.ContentDescription = maximized ? Loc.T("Minimalizuj kartę lotniska") : Loc.T("Rozszerz kartę lotniska");

            if (LayoutParameters is FrameLayout.LayoutParams parameters)
            {
                if (maximized)
                {
                    normalLayout = (parameters.Width, parameters.Height, parameters.Gravity);
                    parameters.Width = ViewGroup.LayoutParams.MatchParent;
                    parameters.Height = ViewGroup.LayoutParams.MatchParent;
                }
                else if (normalLayout.HasValue)
                {
                    parameters.Width = normalLayout.Value.Width;
                    parameters.Height = normalLayout.Value.Height;
                    parameters.Gravity = normalLayout.Value.Gravity;
                }

                LayoutParameters = parameters;
            }

            ApplyScrollFill(maximized);
            RequestLayout();
        }

        /// <summary>
        /// Brings the card back to the height that its content needs, whatever its size was before: it is no longer maximized and no longer has a height given by dragging.
        /// </summary>
        public void ResetSize()
        {
            SetMaximized(false);
            userHeight = 0;
            ApplyScrollFill(false);
            RequestLayout();
        }

        /// <summary>
        /// Lets the scrolling part take all the height that is left under the title, which is needed when the height of the card does not depend on its content, or lets it take only what its content needs.
        /// </summary>
        /// <param name="fill">Whether the scrolling part fills the card.</param>
        private void ApplyScrollFill(bool fill)
        {
            scroll.LayoutParameters = fill
                ? new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f)
                : new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        }

        /// <summary>
        /// Starts a drag of the handle: remembers the height the card has, and when the card fills the screen it stops doing so but keeps its height, so that it does not jump.
        /// </summary>
        private void OnDragStarted()
        {
            dragStartHeight = Height;

            if (isMaximized)
            {
                int size = Height;

                SetMaximized(false);
                userHeight = size;
                ApplyScrollFill(true);
            }
            else if (userHeight == 0)
            {
                naturalHeight = Height;
            }
        }

        /// <summary>
        /// Gives the card the height that follows the finger: dragging up makes it taller, dragging down makes it lower, between a minimum and the height of the screen.
        /// </summary>
        /// <param name="distance">Distance in pixels that the finger has moved since the drag started; negative when it moved up.</param>
        private void OnDragged(float distance)
        {
            userHeight = Math.Clamp(dragStartHeight - (int)distance, Dp(MinimumHeightDp), GetMaximumHeight());
            ApplyScrollFill(true);
            RequestLayout();
        }

        /// <summary>
        /// Ends a drag: a card that is almost as high as the screen fills it, and a card that is almost as high as its content needs gets that height again; any other height stays.
        /// </summary>
        /// <param name="distance">Distance in pixels that the finger has moved in total.</param>
        private void OnDragEnded(float distance)
        {
            if (userHeight >= GetMaximumHeight() - Dp(MaximizeSnapDp))
            {
                SetMaximized(true);
            }
            else if (naturalHeight > 0 && Math.Abs(userHeight - naturalHeight) <= Dp(NaturalSnapDp))
            {
                userHeight = 0;
                ApplyScrollFill(false);
                RequestLayout();
            }
        }

        /// <summary>
        /// Returns the largest height that the card can have: the height of the view that holds it without the margins.
        /// </summary>
        /// <returns>Height in pixels.</returns>
        private int GetMaximumHeight()
        {
            int available = Parent is View parent && parent.Height > 0 ? parent.Height : Resources?.DisplayMetrics?.HeightPixels ?? 0;

            if (LayoutParameters is ViewGroup.MarginLayoutParams margins)
            {
                available -= margins.TopMargin + margins.BottomMargin;
            }

            return Math.Max(available, Dp(MinimumHeightDp));
        }

        /// <summary>
        /// Event raised when the card is maximized or brought back to its size, before the layout changes, so that the map can be kept still.
        /// </summary>
        public event System.Action<bool>? MaximizedChanged;

        /// <summary>
        /// Event raised when the "Wyśrodkuj" (center) button is tapped.
        /// </summary>
        public event System.Action? CenterClicked;

        /// <summary>
        /// Event raised when the star is tapped.
        /// </summary>
        public event System.Action? FavoriteClicked;

        /// <summary>
        /// Event raised when the close button is tapped.
        /// </summary>
        public event System.Action? CloseClicked;

        /// <summary>
        /// Fills the card with the data of an airport.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="frequencies">Radio frequencies of the airport.</param>
        /// <param name="hasUserLocation">Whether the user position is known.</param>
        /// <param name="userLatitude">Latitude of the user.</param>
        /// <param name="userLongitude">Longitude of the user.</param>
        /// <param name="isFavorite">Whether the airport is a favorite.</param>
        public void Bind(Airport airport, IReadOnlyList<Runway> runways, IReadOnlyList<AirportFrequency> frequencies, bool hasUserLocation, double userLatitude, double userLongitude, bool isFavorite)
        {
            currentRunways = runways;
            nameText.Text = AirportCardText.BuildTitle(airport);
            favoriteButton.Text = isFavorite ? "★" : "☆";
            descriptorText.Text = AirportCardText.BuildDescriptor(airport);
            List<string> details = AirportCardText.BuildLines(airport, runways, hasUserLocation, userLatitude, userLongitude);
            string trafficLine = AirportRunwayTraffic.BuildLine(airport, runways);

            if (trafficLine.Length > 0)
            {
                details.Add(trafficLine);
            }

            detailsText.Text = string.Join("\n", details);

            if (!string.Equals(boundIcao, airport.Icao, StringComparison.Ordinal))
            {
                boundIcao = airport.Icao;
                ResetSize();
                scroll.ScrollTo(0, 0);
                weatherEntry = null;
                weatherLoading = false;
                forecastEntry = null;
                forecastLoading = false;
                weatherPanel.Expanded = AppSettings.AirportPanelsExpanded;
                forecastPanel.Expanded = AppSettings.AirportPanelsExpanded;
                frequencyPanel.Expanded = AppSettings.AirportPanelsExpanded;
            }

            if (AppSettings.ShowAirportFrequencies)
            {
                List<IReadOnlyList<string>> rows = AirportCardText.BuildFrequencyRows(frequencies).Select((FrequencyRow row) => (IReadOnlyList<string>)new List<string> { row.Type, row.Megahertz, row.Description }).ToList();

                frequencyPanel.SetTableContent(AirportCardText.BuildFrequencyHeader(frequencies, false), AirportCardText.BuildFrequencyHeader(frequencies, true), AirportCardText.BuildFrequencyColumns(), rows, FrequencyColor);
            }
            else
            {
                frequencyPanel.SetContent(string.Empty, string.Empty, Array.Empty<string>(), FrequencyColor);
            }

            ShowWeather();
            ShowForecast();
        }

        /// <summary>
        /// Shows the forecast of the airport, or hides its panel when the forecast is switched off in the settings.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the forecast is being downloaded.</param>
        public void BindForecast(ForecastEntry? entry, bool isLoading)
        {
            forecastEntry = entry;
            forecastLoading = isLoading;
            ShowForecast();
        }

        /// <summary>
        /// Fills the forecast panel from the last bound answer: a table of the periods with the original text under it.
        /// </summary>
        private void ShowForecast()
        {
            if (!AppSettings.ShowAirportForecast)
            {
                forecastPanel.SetContent(string.Empty, string.Empty, Array.Empty<string>(), Color.White);

                return;
            }

            DisplayUnits units = AppSettings.Units;
            TimeZoneInfo zone = AirportForecastText.GetZone(AppSettings.ForecastUsesLocalTime);
            DateTime now = DateTime.UtcNow;
            Taf? taf = forecastEntry?.Forecast;
            string collapsed = AirportForecastText.BuildHeader(forecastEntry, forecastLoading, false, units, zone, now);
            string expanded = AirportForecastText.BuildHeader(forecastEntry, forecastLoading, true, units, zone, now);

            if (taf == null)
            {
                forecastPanel.SetContent(collapsed, expanded, Array.Empty<string>(), ForecastColor);

                return;
            }

            List<IReadOnlyList<string>> rows = AirportForecastText.BuildRows(taf, units, zone).Select((ForecastRow row) => (IReadOnlyList<string>)new List<string> { row.Period, row.Change, row.Conditions }).ToList();

            forecastPanel.SetTableContent(collapsed, expanded, AirportForecastText.BuildColumns(), rows, ForecastColor, AirportForecastText.BuildFooter(taf), -1);
        }

        /// <summary>
        /// Shows the weather of the airport, or hides its panel when the weather is switched off in the settings.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the weather is being downloaded.</param>
        public void BindWeather(WeatherEntry? entry, bool isLoading)
        {
            weatherEntry = entry;
            weatherLoading = isLoading;
            ShowWeather();
        }

        /// <summary>
        /// Fills the weather panel from the last bound answer.
        /// </summary>
        private void ShowWeather()
        {
            if (!AppSettings.ShowAirportWeather)
            {
                weatherPanel.SetContent(string.Empty, string.Empty, Array.Empty<string>(), Color.White);

                return;
            }

            DisplayUnits units = AppSettings.Units;
            DateTime now = DateTime.UtcNow;
            Metar? metar = weatherEntry?.Report;
            Color color = Color.White;
            int? categoryColor = weatherEntry?.DistanceKilometers == null ? AirportWeatherText.GetCategoryColor(metar?.FlightCategory) : null;

            if (categoryColor.HasValue)
            {
                color = new Color(categoryColor.Value);
            }

            weatherPanel.SetContent(
                AirportWeatherText.BuildHeader(weatherEntry, weatherLoading, false, units, now),
                AirportWeatherText.BuildHeader(weatherEntry, weatherLoading, true, units, now),
                metar == null ? Array.Empty<string>() : AirportWeatherText.BuildLines(metar, units, weatherEntry?.DistanceKilometers, currentRunways),
                color);
        }

        /// <summary>
        /// Creates a text view with the given size and color.
        /// </summary>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <returns>The text view.</returns>
        private TextView CreateText(float size, Color color, bool bold)
        {
            TextView text = new TextView(Context);
            text.TextSize = size;
            text.SetTextColor(color);

            if (bold)
            {
                text.SetTypeface(null, TypefaceStyle.Bold);
            }

            return text;
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

using AdsbCarMap.Data;
using AdsbCarMap.Map;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Card of the selected airport overlaid on the map: code, name, size class, codes, place, elevation, distance from the user and runways.
    /// </summary>
    public sealed class AirportCardView : LinearLayout
    {
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
        /// Creates the card and its views.
        /// </summary>
        /// <param name="context">Activity context.</param>
        public AirportCardView(Context context) : base(context)
        {
            Orientation = Orientation.Vertical;
            SetBackgroundResource(Resource.Drawable.bg_card);
            SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
            Clickable = true;

            LinearLayout header = new LinearLayout(context);
            header.Orientation = Orientation.Horizontal;
            header.SetGravity(GravityFlags.CenterVertical);
            AddView(header, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            nameText = CreateText(20f, Color.White, true);
            header.AddView(nameText, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            favoriteButton = CreateText(22f, Color.Rgb(255, 215, 0), true);
            favoriteButton.SetPadding(Dp(4), Dp(2), Dp(8), Dp(2));
            favoriteButton.Clickable = true;
            favoriteButton.ContentDescription = "Dodaj lub usuń z ulubionych";
            favoriteButton.Click += (sender, args) => FavoriteClicked?.Invoke();
            header.AddView(favoriteButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            TextView centerButton = CreateText(14f, Color.Rgb(120, 190, 255), true);
            centerButton.Text = "Wyśrodkuj";
            centerButton.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            centerButton.Clickable = true;
            centerButton.Click += (sender, args) => CenterClicked?.Invoke();
            header.AddView(centerButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            TextView closeButton = CreateText(18f, Color.White, true);
            closeButton.Text = "✕";
            closeButton.SetPadding(Dp(8), Dp(2), Dp(2), Dp(2));
            closeButton.Clickable = true;
            closeButton.ContentDescription = "Zamknij kartę lotniska";
            closeButton.Click += (sender, args) => CloseClicked?.Invoke();
            header.AddView(closeButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            descriptorText = CreateText(14f, Color.Rgb(120, 190, 255), false);
            AddView(descriptorText);

            detailsText = CreateText(13f, Color.White, false);
            detailsText.SetPadding(0, Dp(8), 0, 0);
            AddView(detailsText);
        }

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
        /// <param name="hasUserLocation">Whether the user position is known.</param>
        /// <param name="userLatitude">Latitude of the user.</param>
        /// <param name="userLongitude">Longitude of the user.</param>
        /// <param name="isFavorite">Whether the airport is a favorite.</param>
        public void Bind(Airport airport, IReadOnlyList<Runway> runways, bool hasUserLocation, double userLatitude, double userLongitude, bool isFavorite)
        {
            nameText.Text = AirportCardText.BuildTitle(airport);
            favoriteButton.Text = isFavorite ? "★" : "☆";
            descriptorText.Text = AirportCardText.BuildDescriptor(airport);
            detailsText.Text = string.Join("\n", AirportCardText.BuildLines(airport, runways, hasUserLocation, userLatitude, userLongitude));
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

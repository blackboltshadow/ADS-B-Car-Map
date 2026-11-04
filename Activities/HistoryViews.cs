using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Builders of the views that the history screens share: rounded backgrounds, round icon buttons and filter chips.
    /// </summary>
    public static class HistoryViews
    {
        /// <summary>
        /// Asks the map to center on and follow an aircraft and brings the map screen to the front, closing the screens above it.
        /// </summary>
        /// <param name="activity">Screen that the request comes from.</param>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public static void ShowOnMap(Activity activity, string hex)
        {
            LiveTraffic.RequestFocus(hex);

            Intent intent = new Intent(activity, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            activity.StartActivity(intent);
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="context">Context that gives the screen density.</param>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        public static int Dp(Context context, int value)
        {
            return (int)(value * (context.Resources?.DisplayMetrics?.Density ?? 1.0f));
        }

        /// <summary>
        /// Creates a rounded rectangle background.
        /// </summary>
        /// <param name="context">Context that gives the screen density.</param>
        /// <param name="fill">Fill color.</param>
        /// <param name="stroke">Outline color.</param>
        /// <param name="radiusDp">Corner radius in dp.</param>
        /// <returns>The background.</returns>
        public static GradientDrawable RoundedBackground(Context context, Color fill, Color stroke, int radiusDp)
        {
            GradientDrawable background = new GradientDrawable();
            background.SetColor(fill);
            background.SetStroke(Dp(context, 1), stroke);
            background.SetCornerRadius(Dp(context, radiusDp));

            return background;
        }

        /// <summary>
        /// Gives the background of a card.
        /// </summary>
        /// <param name="context">Context that gives the screen density.</param>
        /// <returns>The background.</returns>
        public static GradientDrawable CardBackground(Context context)
        {
            return RoundedBackground(context, Color.Argb(235, 30, 34, 40), Color.Argb(85, 255, 255, 255), 16);
        }

        /// <summary>
        /// Creates a round icon button; an active one is tinted and has a filled background.
        /// </summary>
        /// <param name="context">Context of the views.</param>
        /// <param name="iconResource">Identifier of the vector drawable.</param>
        /// <param name="description">Description for accessibility services; it is also shown as a hint on a long press.</param>
        /// <param name="active">Whether the button shows an enabled state.</param>
        /// <param name="activeColor">Tint of the icon and the outline when the button is active.</param>
        /// <param name="action">Action run on a tap.</param>
        /// <returns>The button.</returns>
        public static ImageView IconButton(Context context, int iconResource, string description, bool active, Color activeColor, System.Action action)
        {
            ImageView button = new ImageView(context);
            button.SetImageResource(iconResource);
            button.SetScaleType(ImageView.ScaleType.CenterInside);
            button.SetPadding(Dp(context, 9), Dp(context, 9), Dp(context, 9), Dp(context, 9));
            button.ContentDescription = description;
            button.TooltipText = description;
            button.SetColorFilter(active ? activeColor : Color.White);

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(active ? Color.Argb(70, activeColor.R, activeColor.G, activeColor.B) : Color.Argb(40, 255, 255, 255));
            circle.SetStroke(Dp(context, 1), active ? activeColor : Color.Argb(130, 255, 255, 255));
            button.Background = circle;
            button.Click += (sender, args) => action();

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(context, 40), Dp(context, 40));
            parameters.SetMargins(Dp(context, 6), 0, 0, 0);
            button.LayoutParameters = parameters;

            return button;
        }

        /// <summary>
        /// Styles a chip of a filter as selected or not.
        /// </summary>
        /// <param name="context">Context that gives the screen density.</param>
        /// <param name="chip">Chip to style.</param>
        /// <param name="selected">Whether the chip is selected.</param>
        public static void StyleChip(Context context, TextView chip, bool selected)
        {
            chip.SetTextColor(selected ? Color.White : Color.Rgb(200, 200, 200));
            chip.SetTypeface(null, selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            chip.Background = selected
                ? RoundedBackground(context, Color.Argb(190, 40, 110, 190), Color.Argb(255, 120, 190, 255), 20)
                : RoundedBackground(context, Color.Argb(30, 255, 255, 255), Color.Argb(110, 255, 255, 255), 20);
        }

        /// <summary>
        /// Creates a small label with a colored rounded background, such as the mark of a new route.
        /// </summary>
        /// <param name="context">Context of the view.</param>
        /// <param name="text">Caption.</param>
        /// <param name="color">Color of the label.</param>
        /// <returns>The label.</returns>
        public static TextView Pill(Context context, string text, Color color)
        {
            TextView pill = new TextView(context);
            pill.Text = text;
            pill.TextSize = 11f;
            pill.SetTextColor(color);
            pill.SetTypeface(null, TypefaceStyle.Bold);
            pill.SetPadding(Dp(context, 8), Dp(context, 2), Dp(context, 8), Dp(context, 2));
            pill.Background = RoundedBackground(context, Color.Argb(50, color.R, color.G, color.B), color, 10);

            return pill;
        }
    }
}

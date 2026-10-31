using AdsbCarMap.Data;
using AdsbCarMap.Map;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Card of the primary selected aircraft overlaid on the map: flight, registration, airline, type, route with the flight progress, flight parameters, and the distance and direction from the user.
    /// </summary>
    public sealed class AircraftCardView : LinearLayout
    {
        /// <summary>
        /// Scale factor of the whole card: texts and spacing.
        /// </summary>
        private readonly float sizeScale;

        /// <summary>
        /// Flight and the number of selected aircraft.
        /// </summary>
        private readonly TextView nameText;

        /// <summary>
        /// Aircraft registration.
        /// </summary>
        private readonly TextView registrationText;

        /// <summary>
        /// Airline, type and kind of the aircraft.
        /// </summary>
        private readonly TextView airlineText;

        /// <summary>
        /// Departure and arrival airports.
        /// </summary>
        private readonly TextView routeText;

        /// <summary>
        /// Button that forces the route download, visible when the route is not known.
        /// </summary>
        private readonly TextView routeActionText;

        /// <summary>
        /// Flight progress bar.
        /// </summary>
        private readonly RouteProgressView progressView;

        /// <summary>
        /// Progress description: flown part, distance to the destination and estimated time.
        /// </summary>
        private readonly TextView progressText;

        /// <summary>
        /// Left column of the flight parameters.
        /// </summary>
        private readonly TextView leftColumn;

        /// <summary>
        /// Right column of the flight parameters.
        /// </summary>
        private readonly TextView rightColumn;

        /// <summary>
        /// Creates the card and its views.
        /// </summary>
        /// <param name="context">Activity context.</param>
        /// <param name="sizeScale">Scale factor of texts and spacing, 1 is the base size.</param>
        public AircraftCardView(Context context, float sizeScale) : base(context)
        {
            this.sizeScale = sizeScale;
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

            registrationText = CreateText(13f, Color.Rgb(170, 170, 170), false);
            registrationText.SetPadding(Dp(8), 0, Dp(8), 0);
            header.AddView(registrationText, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            TextView followButton = CreateText(14f, Color.Rgb(120, 190, 255), true);
            followButton.Text = "Śledź";
            followButton.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            followButton.Clickable = true;
            followButton.Click += (sender, args) => FollowClicked?.Invoke();
            header.AddView(followButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            TextView closeButton = CreateText(18f, Color.White, true);
            closeButton.Text = "✕";
            closeButton.SetPadding(Dp(8), Dp(2), Dp(2), Dp(2));
            closeButton.Clickable = true;
            closeButton.ContentDescription = "Zamknij kartę i wyczyść zaznaczenie";
            closeButton.Click += (sender, args) => CloseClicked?.Invoke();
            header.AddView(closeButton, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            airlineText = CreateText(14f, Color.Rgb(120, 190, 255), false);
            AddView(airlineText);

            routeText = CreateText(15f, Color.White, true);
            routeText.SetPadding(0, Dp(8), 0, 0);
            AddView(routeText);

            routeActionText = CreateText(14f, Color.Rgb(120, 190, 255), true);
            routeActionText.SetPadding(0, Dp(6), 0, Dp(6));
            routeActionText.Clickable = true;
            routeActionText.Click += (sender, args) => RefreshRouteClicked?.Invoke();
            routeActionText.Visibility = ViewStates.Gone;
            AddView(routeActionText);

            progressView = new RouteProgressView(context);
            AddView(progressView, new LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(22)));

            progressText = CreateText(12f, Color.Rgb(190, 190, 190), false);
            AddView(progressText);

            LinearLayout columns = new LinearLayout(context);
            columns.Orientation = Orientation.Horizontal;
            columns.SetPadding(0, Dp(8), 0, 0);
            AddView(columns, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            leftColumn = CreateText(13f, Color.White, false);
            columns.AddView(leftColumn, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            rightColumn = CreateText(13f, Color.White, false);
            columns.AddView(rightColumn, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        }

        /// <summary>
        /// Event raised when the "Śledź" (follow) button is tapped.
        /// </summary>
        public event System.Action? FollowClicked;

        /// <summary>
        /// Event raised when the close button is tapped.
        /// </summary>
        public event System.Action? CloseClicked;

        /// <summary>
        /// Event raised when the button that forces the route download is tapped.
        /// </summary>
        public event System.Action? RefreshRouteClicked;

        /// <summary>
        /// Fills the card with the aircraft data.
        /// </summary>
        /// <param name="info">Data of the selected aircraft and the user position.</param>
        /// <param name="route">Flight route, or null when unknown or not downloaded yet.</param>
        /// <param name="routeState">Route lookup state that explains why the route is missing.</param>
        public void Bind(SelectedAircraftInfo info, FlightRoute? route, RouteLookupState routeState)
        {
            Aircraft? aircraft = info.Aircraft;

            nameText.Text = AircraftCardText.BuildTitle(info);

            if (aircraft == null)
            {
                registrationText.Text = string.Empty;
                airlineText.Visibility = ViewStates.Gone;
                routeText.Visibility = ViewStates.Gone;
                routeActionText.Visibility = ViewStates.Gone;
                progressView.Visibility = ViewStates.Gone;
                progressText.Visibility = ViewStates.Gone;
                leftColumn.Text = "Samolot jest poza zasięgiem danych.";
                rightColumn.Text = string.Empty;

                return;
            }

            registrationText.Text = aircraft.Registration ?? string.Empty;

            string descriptors = AircraftCardText.BuildDescriptors(aircraft, route);

            airlineText.Text = descriptors;
            airlineText.Visibility = descriptors.Length > 0 ? ViewStates.Visible : ViewStates.Gone;

            BindRoute(info, aircraft, route, routeState);

            leftColumn.Text = string.Join("\n", AircraftCardText.BuildLeftLines(info, aircraft));
            rightColumn.Text = string.Join("\n", AircraftCardText.BuildRightLines(info, aircraft));
        }

        /// <summary>
        /// Fills the route part: the airports, the progress bar and the description.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <param name="route">Route or null.</param>
        /// <param name="routeState">Route lookup state.</param>
        private void BindRoute(SelectedAircraftInfo info, Aircraft aircraft, FlightRoute? route, RouteLookupState routeState)
        {
            routeText.Visibility = ViewStates.Visible;
            routeText.Text = AircraftCardText.BuildRouteText(route, routeState);
            routeText.SetTypeface(null, route == null ? TypefaceStyle.Normal : TypefaceStyle.Bold);

            string? actionText = route == null ? AircraftCardText.BuildRouteActionText(routeState) : null;

            routeActionText.Text = actionText ?? string.Empty;
            routeActionText.Visibility = actionText != null ? ViewStates.Visible : ViewStates.Gone;

            if (AircraftCardText.TryBuildProgress(info, aircraft, route, out float fraction, out string progress))
            {
                progressView.SetProgress(fraction);
                progressView.Visibility = ViewStates.Visible;
                progressText.Text = progress;
                progressText.Visibility = ViewStates.Visible;
            }
            else
            {
                progressView.Visibility = ViewStates.Gone;
                progressText.Visibility = ViewStates.Gone;
            }
        }

        /// <summary>
        /// Creates a text view of the card.
        /// </summary>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <returns>Text view.</returns>
        private TextView CreateText(float size, Color color, bool bold)
        {
            TextView text = new TextView(Context);
            text.TextSize = size * sizeScale;
            text.SetTextColor(color);

            if (bold)
            {
                text.SetTypeface(null, TypefaceStyle.Bold);
            }

            return text;
        }

        /// <summary>
        /// Converts dp to screen pixels, taking the card scale into account.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * sizeScale * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

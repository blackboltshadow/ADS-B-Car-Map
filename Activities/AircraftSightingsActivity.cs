using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen of one aircraft from the history: its flights, with the new callsigns and routes marked, and the list of all the sightings with the time, the place and the route.
    /// </summary>
    [Activity(Label = "Przeloty i obserwacje", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AircraftSightingsActivity : Activity
    {
        /// <summary>
        /// Name of the intent extra with the ICAO address of the aircraft.
        /// </summary>
        public const string HexExtra = "hex";

        /// <summary>
        /// Number of the newest sightings shown in the list of sightings.
        /// </summary>
        private const int MaxSightingRows = 300;

        /// <summary>
        /// Farthest airport, in kilometers, that is named as the place of a sighting.
        /// </summary>
        private const double NearAirportKilometers = 150.0;

        /// <summary>
        /// Time since the last notice within which a flight is shown as active.
        /// </summary>
        private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(10);

        /// <summary>
        /// ICAO address of the aircraft, in lower case.
        /// </summary>
        private string hex = string.Empty;

        /// <summary>
        /// Whether the list of the sightings is shown instead of the list of the flights.
        /// </summary>
        private bool showSightings;

        /// <summary>
        /// Container of the whole content.
        /// </summary>
        private LinearLayout? contentLayout;

        /// <summary>
        /// Builds the screen.
        /// </summary>
        /// <param name="savedInstanceState">Saved state, not used.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Przeloty i obserwacje");
            hex = (Intent?.GetStringExtra(HexExtra) ?? string.Empty).ToLowerInvariant();

            ActionBar?.SetDisplayHomeAsUpEnabled(true);

            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            contentLayout = new LinearLayout(this);
            contentLayout.Orientation = Orientation.Vertical;
            contentLayout.SetPadding(Dp(12), Dp(8), Dp(12), Dp(24));
            scrollView.AddView(contentLayout);

            SetContentView(scrollView);
        }

        /// <summary>
        /// Saves the newest samples and shows the current content every time the screen comes back.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            AircraftSightingStore.Flush();
            Fill();
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
        /// Builds the header, the chips and the chosen list.
        /// </summary>
        private void Fill()
        {
            if (contentLayout == null)
            {
                return;
            }

            contentLayout.RemoveAllViews();

            List<FlightInsight> flights = AircraftSightingStore.GetFlights(hex);
            AircraftHistoryEntry? entry = AircraftHistoryStore.GetForDisplay(hex, AircraftHistoryFilterMode.All, new HashSet<string>()).FirstOrDefault((AircraftHistoryEntry item) => item.Hex == hex);

            contentLayout.AddView(CreateHeader(entry, flights));
            contentLayout.AddView(CreateChips());

            if (flights.Count == 0)
            {
                TextView empty = new TextView(this);
                empty.Text = Loc.T("Brak obserwacji. Pozycje samolotu są zapisywane przy każdym pobraniu danych, gdy jest w zasięgu: przy otwartej mapie lub w trybie w tle alertów.");
                empty.TextSize = 15f;
                empty.SetTextColor(Color.Rgb(190, 190, 190));
                empty.SetPadding(Dp(8), Dp(20), Dp(8), 0);
                contentLayout.AddView(empty);

                return;
            }

            if (showSightings)
            {
                AddSightings(flights);
            }
            else
            {
                foreach (FlightInsight flight in flights)
                {
                    contentLayout.AddView(CreateFlightCard(flight));
                }
            }
        }

        /// <summary>
        /// Creates the card at the top: the name, the aircraft data, the totals, the callsigns and the routes seen.
        /// </summary>
        /// <param name="entry">Entry of the history or null when it was removed.</param>
        /// <param name="flights">Flights of the aircraft, the newest first.</param>
        /// <returns>The card.</returns>
        private View CreateHeader(AircraftHistoryEntry? entry, List<FlightInsight> flights)
        {
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(16), Dp(12), Dp(10), Dp(12));
            card.Background = HistoryViews.CardBackground(this);

            LinearLayout top = new LinearLayout(this);
            top.Orientation = Orientation.Horizontal;
            top.SetGravity(GravityFlags.CenterVertical);
            card.AddView(top);

            string name = entry == null || string.IsNullOrWhiteSpace(entry.Callsign) ? hex.ToUpperInvariant() : entry.Callsign!;
            TextView title = new TextView(this);
            title.Text = name;
            title.TextSize = 22f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            title.SetSingleLine(true);
            title.Ellipsize = TextUtils.TruncateAt.End;
            top.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            top.AddView(HistoryViews.IconButton(this, Resource.Drawable.ic_my_location, Loc.T("Pokaż na mapie"), false, Color.White, () => HistoryViews.ShowOnMap(this, hex)));

            List<string> parts = new List<string>();
            parts.Add(hex.ToUpperInvariant());

            if (entry != null && !string.IsNullOrWhiteSpace(entry.Registration))
            {
                parts.Add(entry.Registration!);
            }

            if (entry != null && !string.IsNullOrWhiteSpace(entry.TypeCode))
            {
                parts.Add(entry.TypeCode!);
            }

            card.AddView(CreateLine(string.Join(" · ", parts), 14f, Color.Rgb(120, 190, 255)));

            int samples = flights.Sum((FlightInsight item) => item.Flight.Points.Count);
            card.AddView(CreateLine(Loc.F("Przelotów: {0} · próbek pozycji: {1}", flights.Count, samples), 13f, Color.White));

            if (entry != null)
            {
                card.AddView(CreateLine(Loc.F("Ostatnio: {0} · pierwszy raz: {1} · razy: {2}", FormatTime(entry.LastSeenUtc), FormatTime(entry.FirstSeenUtc), entry.Count), 12f, Color.Rgb(160, 160, 160)));
            }

            string callsigns = Summarize(flights.Select((FlightInsight item) => item.Flight.Callsign));
            string routes = Summarize(flights.Select((FlightInsight item) => item.Flight.OriginCode == null || item.Flight.DestinationCode == null ? null : item.Flight.OriginCode + "→" + item.Flight.DestinationCode));

            if (callsigns.Length > 0)
            {
                card.AddView(CreateLine(Loc.T("Linie: ") + callsigns, 13f, Color.White));
            }

            if (routes.Length > 0)
            {
                card.AddView(CreateLine(Loc.T("Trasy: ") + routes, 13f, Color.White));
            }

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(4), 0, Dp(4));
            card.LayoutParameters = parameters;

            return card;
        }

        /// <summary>
        /// Creates the row with the chips that switch between the flights and the sightings.
        /// </summary>
        /// <returns>The row.</returns>
        private View CreateChips()
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetPadding(Dp(4), Dp(8), 0, Dp(4));

            row.AddView(CreateChip(Loc.T("Przeloty"), !showSightings, false));
            row.AddView(CreateChip(Loc.T("Wszystkie obserwacje"), showSightings, true));

            return row;
        }

        /// <summary>
        /// Creates one chip.
        /// </summary>
        /// <param name="text">Caption.</param>
        /// <param name="selected">Whether the chip is selected.</param>
        /// <param name="sightings">Whether the chip shows the sightings.</param>
        /// <returns>The chip.</returns>
        private View CreateChip(string text, bool selected, bool sightings)
        {
            TextView chip = new TextView(this);
            chip.Text = text;
            chip.TextSize = 13f;
            chip.SetPadding(Dp(14), Dp(7), Dp(14), Dp(7));
            HistoryViews.StyleChip(this, chip, selected);
            chip.Click += (sender, args) =>
            {
                showSightings = sightings;
                Fill();
            };

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, 0, Dp(8), 0);
            chip.LayoutParameters = parameters;

            return chip;
        }

        /// <summary>
        /// Creates the card of a flight: the callsign with the marks, the route, the time, the figures and the first and the last place.
        /// </summary>
        /// <param name="insight">Flight with what is new in it.</param>
        /// <returns>The card.</returns>
        private View CreateFlightCard(FlightInsight insight)
        {
            FlightSegment flight = insight.Flight;
            AltitudeUnit altitudeUnit = AppSettings.AltitudeUnit;

            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(16), Dp(12), Dp(14), Dp(12));
            card.Background = HistoryViews.CardBackground(this);

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(5), 0, Dp(5));
            card.LayoutParameters = parameters;

            LinearLayout top = new LinearLayout(this);
            top.Orientation = Orientation.Horizontal;
            top.SetGravity(GravityFlags.CenterVertical);
            card.AddView(top);

            TextView title = new TextView(this);
            title.Text = flight.Callsign ?? Loc.T("Bez znaku wywoławczego");
            title.TextSize = 18f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            title.SetSingleLine(true);
            top.AddView(title);

            if (DateTime.UtcNow - flight.LastSeenUtc < ActiveWindow)
            {
                top.AddView(CreatePill(Loc.T("Aktywny"), Color.Rgb(120, 190, 255)));
            }

            if ((insight.Novelty & FlightNovelty.NewCallsign) != 0)
            {
                top.AddView(CreatePill(Loc.T("Nowa linia"), Color.Rgb(255, 170, 60)));
            }

            if ((insight.Novelty & FlightNovelty.NewRoute) != 0)
            {
                top.AddView(CreatePill(Loc.T("Nowa trasa"), Color.Rgb(90, 210, 120)));
            }

            card.AddView(CreateLine(DescribeRoute(flight), 14f, flight.RouteKey == null ? Color.Rgb(160, 160, 160) : Color.Rgb(120, 190, 255)));

            if (!string.IsNullOrWhiteSpace(flight.Airline))
            {
                card.AddView(CreateLine(flight.Airline!, 13f, Color.Rgb(190, 190, 190)));
            }

            card.AddView(CreateLine(FormatTime(flight.FirstSeenUtc) + " – " + (flight.LastSeenUtc.Date == flight.FirstSeenUtc.Date ? flight.LastSeenUtc.ToLocalTime().ToString("HH:mm") : FormatTime(flight.LastSeenUtc))
                + " · " + SightingFormat.Duration(flight.LastSeenUtc - flight.FirstSeenUtc), 13f, Color.White));

            List<string> figures = new List<string>();
            figures.Add(Loc.F("próbek: {0}", flight.Points.Count));

            int? maxAltitude = flight.Points.Where((Sighting point) => point.AltitudeFeet.HasValue).Select((Sighting point) => point.AltitudeFeet).Max();

            if (maxAltitude.HasValue)
            {
                figures.Add(Loc.T("maks. wysokość: ") + AltitudeFormatter.Format(maxAltitude.Value, altitudeUnit));
            }

            double? maxSpeed = flight.Points.Where((Sighting point) => point.SpeedKnots.HasValue).Select((Sighting point) => point.SpeedKnots).Max();

            if (maxSpeed.HasValue)
            {
                figures.Add(Loc.T("maks. prędkość: ") + UnitFormatter.FormatSpeed(maxSpeed.Value, AppSettings.SpeedUnit));
            }

            card.AddView(CreateLine(string.Join(" · ", figures), 12f, Color.Rgb(160, 160, 160)));

            Sighting first = flight.Points[0];
            Sighting last = flight.Points[flight.Points.Count - 1];

            card.AddView(CreateLine(Loc.T("Pierwsza pozycja: ") + DescribePlace(first.Latitude, first.Longitude), 12f, Color.Rgb(190, 190, 190)));

            if (flight.Points.Count > 1)
            {
                card.AddView(CreateLine(Loc.T("Ostatnia pozycja: ") + DescribePlace(last.Latitude, last.Longitude), 12f, Color.Rgb(190, 190, 190)));
            }

            return card;
        }

        /// <summary>
        /// Adds the list of all the sightings, the newest first.
        /// </summary>
        /// <param name="flights">Flights of the aircraft, the newest first.</param>
        private void AddSightings(List<FlightInsight> flights)
        {
            List<(FlightSegment Flight, Sighting Point)> rows = new List<(FlightSegment Flight, Sighting Point)>();

            foreach (FlightInsight insight in flights)
            {
                for (int index = insight.Flight.Points.Count - 1; index >= 0; index--)
                {
                    rows.Add((insight.Flight, insight.Flight.Points[index]));
                }
            }

            int total = rows.Count;

            if (total > MaxSightingRows)
            {
                rows = rows.GetRange(0, MaxSightingRows);
                contentLayout!.AddView(CreateLine(Loc.F("Pokazano {0} najnowszych z {1} obserwacji.", MaxSightingRows, total), 12f, Color.Rgb(160, 160, 160)));
            }

            for (int index = 0; index < rows.Count; index++)
            {
                contentLayout!.AddView(CreateSightingRow(rows[index].Flight, rows[index].Point, index));
            }
        }

        /// <summary>
        /// Creates the row of one sighting: the time with the flight, the place and the altitude with the speed.
        /// </summary>
        /// <param name="flight">Flight that the sighting belongs to.</param>
        /// <param name="point">The sighting.</param>
        /// <param name="index">Position on the list, used to alternate the background.</param>
        /// <returns>The row.</returns>
        private View CreateSightingRow(FlightSegment flight, Sighting point, int index)
        {
            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Vertical;
            row.SetPadding(Dp(12), Dp(7), Dp(12), Dp(7));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);

            string label = (flight.Callsign ?? string.Empty) + (flight.RouteKey == null ? string.Empty : " " + flight.OriginCode + "→" + flight.DestinationCode);
            TextView title = CreateLine(FormatTime(point.TimeUtc) + (label.Trim().Length == 0 ? string.Empty : "  " + label.Trim()), 14f, Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(title);

            row.AddView(CreateLine(DescribePlace(point.Latitude, point.Longitude), 12f, Color.Rgb(190, 190, 190)));

            List<string> figures = new List<string>();
            figures.Add(point.OnGround ? Loc.T("na ziemi") : point.AltitudeFeet.HasValue ? AltitudeFormatter.Format(point.AltitudeFeet.Value, AppSettings.AltitudeUnit) : "—");

            if (point.SpeedKnots.HasValue)
            {
                figures.Add(UnitFormatter.FormatSpeed(point.SpeedKnots.Value, AppSettings.SpeedUnit));
            }

            row.AddView(CreateLine(string.Join(" · ", figures), 12f, Color.Rgb(160, 160, 160)));

            return row;
        }

        /// <summary>
        /// Describes the route of a flight with the airport codes and cities.
        /// </summary>
        /// <param name="flight">Flight.</param>
        /// <returns>The text, or a note that the route is not known.</returns>
        private static string DescribeRoute(FlightSegment flight)
        {
            if (flight.RouteKey == null)
            {
                return Loc.T("Trasa nieznana");
            }

            string origin = flight.OriginCode + (string.IsNullOrWhiteSpace(flight.OriginCity) ? string.Empty : " " + flight.OriginCity);
            string destination = flight.DestinationCode + (string.IsNullOrWhiteSpace(flight.DestinationCity) ? string.Empty : " " + flight.DestinationCity);

            return origin + " → " + destination;
        }

        /// <summary>
        /// Describes a place with the coordinates and the nearest airport when it is not far.
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <returns>Text such as "52.17°N 21.01°E · 14 km od WAW".</returns>
        private static string DescribePlace(double latitude, double longitude)
        {
            string text = SightingFormat.Coordinates(latitude, longitude);
            List<Airport> nearest = AppServices.Airports.GetNearest(latitude, longitude, 1, AirportType.Medium);

            if (nearest.Count == 0)
            {
                return text;
            }

            double kilometers = GeoMath.DistanceKilometers(latitude, longitude, nearest[0].Latitude, nearest[0].Longitude);

            if (kilometers > NearAirportKilometers)
            {
                return text;
            }

            string code = string.IsNullOrEmpty(nearest[0].Iata) ? nearest[0].Icao : nearest[0].Iata;

            return text + " · " + UnitFormatter.FormatDistance(kilometers, AppSettings.DistanceUnit, 0) + Loc.T(" od ") + code;
        }

        /// <summary>
        /// Lists the distinct values with how many times each occurs, the most frequent first.
        /// </summary>
        /// <param name="values">Values; null and empty ones are ignored.</param>
        /// <returns>Text such as "LOT3 ×4 · LOT5 ×1", or an empty string when there are no values.</returns>
        private static string Summarize(IEnumerable<string?> values)
        {
            return string.Join(" · ", values
                .Where((string? value) => !string.IsNullOrEmpty(value))
                .GroupBy((string? value) => value!)
                .OrderByDescending((IGrouping<string, string?> group) => group.Count())
                .Select((IGrouping<string, string?> group) => group.Key + " ×" + group.Count()));
        }

        /// <summary>
        /// Formats a time in the local time zone.
        /// </summary>
        /// <param name="timeUtc">Time in UTC.</param>
        /// <returns>Text such as "04.11 08:15".</returns>
        private static string FormatTime(DateTime timeUtc)
        {
            return timeUtc.ToLocalTime().ToString("dd.MM HH:mm");
        }

        /// <summary>
        /// Creates a line of text.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="size">Size in sp.</param>
        /// <param name="color">Color.</param>
        /// <returns>The view.</returns>
        private TextView CreateLine(string text, float size, Color color)
        {
            TextView line = new TextView(this);
            line.Text = text;
            line.TextSize = size;
            line.SetTextColor(color);

            return line;
        }

        /// <summary>
        /// Creates a label of a flight, placed after the callsign.
        /// </summary>
        /// <param name="text">Caption.</param>
        /// <param name="color">Color of the label.</param>
        /// <returns>The label.</returns>
        private View CreatePill(string text, Color color)
        {
            TextView pill = HistoryViews.Pill(this, text, color);
            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(Dp(8), 0, 0, 0);
            pill.LayoutParameters = parameters;

            return pill;
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return HistoryViews.Dp(this, value);
        }
    }
}

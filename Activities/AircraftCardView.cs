using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities;

/// <summary>
/// Karta głównego zaznaczonego samolotu nakładana na mapę: lot, rejestracja, linia, typ, trasa z postępem lotu, parametry lotu oraz odległość i kierunek od użytkownika.
/// </summary>
public sealed class AircraftCardView : LinearLayout
{
    /// <summary>
    /// Kultura używana do formatowania liczb na karcie: przecinek dziesiętny i spacja jako separator tysięcy.
    /// </summary>
    private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

    /// <summary>
    /// Przelicznik stóp na metry.
    /// </summary>
    private const double MetersPerFoot = 0.3048;

    /// <summary>
    /// Przelicznik węzłów na kilometry na godzinę.
    /// </summary>
    private const double KilometersPerHourPerKnot = 1.852;

    /// <summary>
    /// Prędkość w węzłach, poniżej której nie szacuje się czasu do przylotu.
    /// </summary>
    private const double MinimumSpeedForEstimateKnots = 80.0;

    /// <summary>
    /// Wartość prędkości wznoszenia w stopach na minutę, poniżej której lot uznaje się za poziomy.
    /// </summary>
    private const int LevelFlightThresholdFeetPerMinute = 100;

    /// <summary>
    /// Współczynnik powiększenia całej karty: tekstów i odstępów.
    /// </summary>
    private readonly float sizeScale;

    /// <summary>
    /// Lot i liczba zaznaczonych samolotów.
    /// </summary>
    private readonly TextView nameText;

    /// <summary>
    /// Rejestracja samolotu.
    /// </summary>
    private readonly TextView registrationText;

    /// <summary>
    /// Linia lotnicza, typ i rodzaj samolotu.
    /// </summary>
    private readonly TextView airlineText;

    /// <summary>
    /// Lotniska wylotu i przylotu.
    /// </summary>
    private readonly TextView routeText;

    /// <summary>
    /// Przycisk wymuszający pobranie trasy, widoczny, gdy trasa nie jest znana.
    /// </summary>
    private readonly TextView routeActionText;

    /// <summary>
    /// Pasek postępu lotu.
    /// </summary>
    private readonly RouteProgressView progressView;

    /// <summary>
    /// Opis postępu: przebyta część, odległość do celu i szacowany czas.
    /// </summary>
    private readonly TextView progressText;

    /// <summary>
    /// Lewa kolumna parametrów lotu.
    /// </summary>
    private readonly TextView leftColumn;

    /// <summary>
    /// Prawa kolumna parametrów lotu.
    /// </summary>
    private readonly TextView rightColumn;

    /// <summary>
    /// Tworzy kartę i jej widoki.
    /// </summary>
    /// <param name="context">Kontekst Activity.</param>
    /// <param name="sizeScale">Współczynnik powiększenia tekstów i odstępów, 1 to rozmiar podstawowy.</param>
    public AircraftCardView(Context context, float sizeScale) : base(context)
    {
        this.sizeScale = sizeScale;
        Orientation = Orientation.Vertical;
        SetBackgroundResource(Resource.Drawable.bg_card);
        SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));

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
    /// Zdarzenie wywoływane po dotknięciu przycisku „Śledź”.
    /// </summary>
    public event System.Action? FollowClicked;

    /// <summary>
    /// Zdarzenie wywoływane po dotknięciu przycisku zamknięcia.
    /// </summary>
    public event System.Action? CloseClicked;

    /// <summary>
    /// Zdarzenie wywoływane po dotknięciu przycisku wymuszającego pobranie trasy.
    /// </summary>
    public event System.Action? RefreshRouteClicked;

    /// <summary>
    /// Wypełnia kartę danymi samolotu.
    /// </summary>
    /// <param name="info">Dane zaznaczonego samolotu i pozycja użytkownika.</param>
    /// <param name="route">Trasa lotu lub null, gdy nieznana albo jeszcze niepobrana.</param>
    /// <param name="routeState">Stan wyszukiwania trasy, który tłumaczy, dlaczego trasy nie ma.</param>
    public void Bind(SelectedAircraftInfo info, FlightRoute? route, RouteLookupState routeState)
    {
        Aircraft? aircraft = info.Aircraft;
        string extra = info.SelectedCount > 1 ? string.Format(DisplayCulture, "  (+{0})", info.SelectedCount - 1) : string.Empty;

        if (aircraft == null)
        {
            nameText.Text = info.Hex.ToUpperInvariant() + extra;
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

        nameText.Text = AircraftSearch.GetName(aircraft) + extra;
        registrationText.Text = aircraft.Registration ?? string.Empty;

        List<string> descriptors = new List<string>();
        string airline = AircraftSearch.GetAirline(aircraft, route);
        string kindName = AircraftClassifier.GetDisplayName(aircraft.Kind);

        if (!string.IsNullOrEmpty(airline))
        {
            descriptors.Add(airline);
        }

        if (!string.IsNullOrEmpty(aircraft.TypeCode))
        {
            descriptors.Add(aircraft.TypeCode);
        }

        if (!string.IsNullOrEmpty(kindName))
        {
            descriptors.Add(kindName);
        }

        airlineText.Text = string.Join(" · ", descriptors);
        airlineText.Visibility = descriptors.Count > 0 ? ViewStates.Visible : ViewStates.Gone;

        BindRoute(info, aircraft, route, routeState);

        leftColumn.Text = string.Join("\n", BuildLeftLines(info, aircraft));
        rightColumn.Text = string.Join("\n", BuildRightLines(info, aircraft));
    }

    /// <summary>
    /// Wypełnia część dotyczącą trasy: lotniska, pasek postępu i opis.
    /// </summary>
    /// <param name="info">Dane zaznaczonego samolotu.</param>
    /// <param name="aircraft">Samolot.</param>
    /// <param name="route">Trasa lub null.</param>
    /// <param name="routeState">Stan wyszukiwania trasy.</param>
    private void BindRoute(SelectedAircraftInfo info, Aircraft aircraft, FlightRoute? route, RouteLookupState routeState)
    {
        routeText.Visibility = ViewStates.Visible;

        if (route == null)
        {
            routeText.SetTypeface(null, TypefaceStyle.Normal);
            progressView.Visibility = ViewStates.Gone;
            progressText.Visibility = ViewStates.Gone;

            switch (routeState)
            {
                case RouteLookupState.NotApplicable:
                    routeText.Text = "Trasa: samolot nie nadaje znaku wywoławczego";
                    routeActionText.Visibility = ViewStates.Gone;
                    break;

                case RouteLookupState.NotFound:
                    routeText.Text = "Trasa: baza adsbdb nie zna tego lotu";
                    routeActionText.Text = "Sprawdź ponownie";
                    routeActionText.Visibility = ViewStates.Visible;
                    break;

                case RouteLookupState.Failed:
                    routeText.Text = "Trasa: nie udało się pobrać (błąd sieci lub limit zapytań)";
                    routeActionText.Text = "Spróbuj ponownie";
                    routeActionText.Visibility = ViewStates.Visible;
                    break;

                default:
                    routeText.Text = "Trasa: pobieranie…";
                    routeActionText.Text = "Pobierz teraz";
                    routeActionText.Visibility = ViewStates.Visible;
                    break;
            }

            return;
        }

        routeActionText.Visibility = ViewStates.Gone;
        routeText.SetTypeface(null, TypefaceStyle.Bold);
        routeText.Text = FormatAirport(route.OriginCode, route.OriginCity) + " → " + FormatAirport(route.DestinationCode, route.DestinationCity);

        if (!route.HasCoordinates)
        {
            progressView.Visibility = ViewStates.Gone;
            progressText.Visibility = ViewStates.Gone;

            return;
        }

        double flown = GeoMath.DistanceKilometers(route.OriginLatitude!.Value, route.OriginLongitude!.Value, info.Latitude, info.Longitude);
        double remaining = GeoMath.DistanceKilometers(info.Latitude, info.Longitude, route.DestinationLatitude!.Value, route.DestinationLongitude!.Value);
        double total = flown + remaining;
        float fraction = total > 0.0 ? (float)(flown / total) : 0.0f;

        progressView.Visibility = ViewStates.Visible;
        progressView.SetProgress(fraction);

        string text = string.Format(DisplayCulture, "przeleciano {0:0}%  ·  do celu {1:N0} km", fraction * 100.0, remaining);

        if (!aircraft.OnGround && aircraft.GroundSpeedKnots.HasValue && aircraft.GroundSpeedKnots.Value >= MinimumSpeedForEstimateKnots)
        {
            double hours = remaining / (aircraft.GroundSpeedKnots.Value * KilometersPerHourPerKnot);
            text += "  ·  ok. " + FormatDuration(hours);
        }

        progressText.Text = text;
        progressText.Visibility = ViewStates.Visible;
    }

    /// <summary>
    /// Buduje linie lewej kolumny: wysokość, prędkość, kurs i wiek danych.
    /// </summary>
    /// <param name="info">Dane zaznaczonego samolotu.</param>
    /// <param name="aircraft">Samolot.</param>
    /// <returns>Linie tekstu.</returns>
    private static List<string> BuildLeftLines(SelectedAircraftInfo info, Aircraft aircraft)
    {
        List<string> lines = new List<string>();

        if (aircraft.OnGround)
        {
            lines.Add("Wysokość: na ziemi");
        }
        else if (aircraft.AltitudeFeet.HasValue)
        {
            lines.Add(string.Format(DisplayCulture, "Wysokość: {0:N0} ft ({1:N0} m)", aircraft.AltitudeFeet.Value, aircraft.AltitudeFeet.Value * MetersPerFoot));
        }
        else
        {
            lines.Add("Wysokość: brak danych");
        }

        lines.Add(aircraft.GroundSpeedKnots.HasValue
            ? string.Format(DisplayCulture, "Prędkość: {0:N0} kt ({1:N0} km/h)", aircraft.GroundSpeedKnots.Value, aircraft.GroundSpeedKnots.Value * KilometersPerHourPerKnot)
            : "Prędkość: brak danych");

        lines.Add(aircraft.TrackDegrees.HasValue
            ? string.Format(DisplayCulture, "Kurs: {0:0}° {1}", aircraft.TrackDegrees.Value, GeoMath.CompassPoint(aircraft.TrackDegrees.Value))
            : "Kurs: brak danych");

        lines.Add(string.Format(DisplayCulture, "Dane sprzed {0:0} s", info.AgeSeconds + aircraft.SecondsSincePosition));

        return lines;
    }

    /// <summary>
    /// Buduje linie prawej kolumny: wznoszenie, odległość i kierunek od użytkownika oraz squawk.
    /// </summary>
    /// <param name="info">Dane zaznaczonego samolotu i pozycja użytkownika.</param>
    /// <param name="aircraft">Samolot.</param>
    /// <returns>Linie tekstu.</returns>
    private static List<string> BuildRightLines(SelectedAircraftInfo info, Aircraft aircraft)
    {
        List<string> lines = new List<string>();

        if (aircraft.OnGround)
        {
            lines.Add("Wznoszenie: –");
        }
        else if (aircraft.VerticalRateFeetPerMinute.HasValue)
        {
            int rate = aircraft.VerticalRateFeetPerMinute.Value;

            lines.Add(Math.Abs(rate) < LevelFlightThresholdFeetPerMinute
                ? "Wznoszenie: lot poziomy"
                : string.Format(DisplayCulture, "{0}: {1:N0} ft/min", rate > 0 ? "Wznoszenie" : "Opadanie", Math.Abs(rate)));
        }
        else
        {
            lines.Add("Wznoszenie: brak danych");
        }

        if (info.HasUser)
        {
            double distance = GeoMath.DistanceKilometers(info.UserLatitude, info.UserLongitude, info.Latitude, info.Longitude);
            double bearing = GeoMath.BearingDegrees(info.UserLatitude, info.UserLongitude, info.Latitude, info.Longitude);

            lines.Add(string.Format(DisplayCulture, "Od Ciebie: {0:N1} km, {1} ({2:0}°)", distance, GeoMath.CompassPoint(bearing), bearing));
        }
        else
        {
            lines.Add("Od Ciebie: brak pozycji GPS");
        }

        lines.Add(string.IsNullOrEmpty(aircraft.Squawk) ? "Squawk: brak danych" : "Squawk: " + aircraft.Squawk);

        return lines;
    }

    /// <summary>
    /// Formatuje lotnisko jako kod i miasto, np. "WAW Warszawa".
    /// </summary>
    /// <param name="code">Kod lotniska.</param>
    /// <param name="city">Miasto lub null.</param>
    /// <returns>Tekst do wyświetlenia.</returns>
    private static string FormatAirport(string code, string? city)
    {
        return string.IsNullOrEmpty(city) ? code : code + " " + city;
    }

    /// <summary>
    /// Formatuje czas trwania w godzinach jako "1 h 12 min" albo "35 min".
    /// </summary>
    /// <param name="hours">Czas w godzinach.</param>
    /// <returns>Tekst do wyświetlenia.</returns>
    private static string FormatDuration(double hours)
    {
        int totalMinutes = (int)Math.Round(hours * 60.0);

        if (totalMinutes < 60)
        {
            return string.Format(DisplayCulture, "{0} min", Math.Max(1, totalMinutes));
        }

        return string.Format(DisplayCulture, "{0} h {1:00} min", totalMinutes / 60, totalMinutes % 60);
    }

    /// <summary>
    /// Tworzy pole tekstowe karty.
    /// </summary>
    /// <param name="size">Rozmiar tekstu w sp.</param>
    /// <param name="color">Kolor tekstu.</param>
    /// <param name="bold">Informacja, czy tekst ma być pogrubiony.</param>
    /// <returns>Pole tekstowe.</returns>
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
    /// Przelicza dp na piksele ekranu, uwzględniając powiększenie karty.
    /// </summary>
    /// <param name="value">Wartość w dp.</param>
    /// <returns>Wartość w pikselach.</returns>
    private int Dp(int value)
    {
        return (int)(value * sizeScale * (Resources?.DisplayMetrics?.Density ?? 1.0f));
    }
}

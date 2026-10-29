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

namespace AdsbCarMap.Activities;

/// <summary>
/// Okno wyszukiwania samolotów po locie, rejestracji, typie i trasie. Wybór samolotu centruje na nim mapę.
/// </summary>
[Activity(Label = "Szukaj samolotu", Exported = false, Theme = "@android:style/Theme.Material")]
public class SearchActivity : Activity
{
    /// <summary>
    /// Tag używany w logach Androida.
    /// </summary>
    private const string LogTag = "AdsbCarMap";

    /// <summary>
    /// Maksymalna liczba wyników wyszukiwania na liście.
    /// </summary>
    private const int MaxResults = 100;

    /// <summary>
    /// Odstęp odświeżania listy wyników w milisekundach.
    /// </summary>
    private const int RefreshIntervalMilliseconds = 2000;

    /// <summary>
    /// Wiek danych w sekundach, po którym okno samo pobiera samoloty, gdy żadna mapa ich nie odświeża.
    /// </summary>
    private const double StaleDataSeconds = 10.0;

    /// <summary>
    /// Promień zapytania o samoloty wykonywanego przez to okno, w milach morskich.
    /// </summary>
    private const double FetchRadiusNauticalMiles = 100.0;

    /// <summary>
    /// Handler głównego wątku używany do okresowego odświeżania.
    /// </summary>
    private readonly Handler handler = new Handler(Looper.MainLooper!);

    /// <summary>
    /// Pole tekstowe wyszukiwarki.
    /// </summary>
    private EditText? searchInput;

    /// <summary>
    /// Informacja o liczbie i wieku danych.
    /// </summary>
    private TextView? statusText;

    /// <summary>
    /// Kontener z wynikami wyszukiwania.
    /// </summary>
    private LinearLayout? resultsLayout;

    /// <summary>
    /// Zadanie okresowego odświeżania, null gdy Activity nie jest widoczna.
    /// </summary>
    private Java.Lang.Runnable? refreshRunnable;

    /// <summary>
    /// Informacja, czy okno pobiera właśnie samoloty.
    /// </summary>
    private bool isFetching;

    /// <summary>
    /// Buduje ekran wyszukiwania i włącza strzałkę powrotu.
    /// </summary>
    /// <param name="savedInstanceState">Zapisany stan Activity.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        ActionBar?.SetDisplayHomeAsUpEnabled(true);
        SetContentView(BuildContent());
    }

    /// <summary>
    /// Zamyka okno po dotknięciu strzałki powrotu.
    /// </summary>
    /// <param name="item">Wybrana pozycja paska.</param>
    /// <returns>True, gdy obsłużono pozycję.</returns>
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
    /// Uruchamia okresowe odświeżanie wyników.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();

        refreshRunnable = new Java.Lang.Runnable(RefreshTick);
        handler.Post(refreshRunnable);
    }

    /// <summary>
    /// Zatrzymuje okresowe odświeżanie wyników.
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
    /// Tworzy cały widok ekranu.
    /// </summary>
    /// <returns>Widok z nieruchomą wyszukiwarką na górze i przewijaną listą wyników pod nią.</returns>
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
    /// Odświeża wyniki, w razie potrzeby pobiera świeże dane i planuje kolejne odświeżenie.
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
    /// Pobiera samoloty wokół środka ostatniego zapytania, gdy żadna mapa nie dostarcza świeżych danych.
    /// </summary>
    /// <returns>Zadanie reprezentujące pobieranie.</returns>
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
    /// Wyświetla samoloty pasujące do tekstu wyszukiwarki.
    /// </summary>
    private void RenderResults()
    {
        if (resultsLayout == null || statusText == null || searchInput == null)
        {
            return;
        }

        (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) snapshot = LiveTraffic.GetSnapshot();
        string query = searchInput.Text ?? string.Empty;
        bool isQueryEmpty = string.IsNullOrWhiteSpace(query);
        List<Aircraft> results = isQueryEmpty
            ? AircraftSearch.ListAll(snapshot.Aircraft, MaxResults)
            : AircraftSearch.Search(snapshot.Aircraft, AppServices.Routes, query, MaxResults);

        statusText.Text = snapshot.Aircraft.Count == 0
            ? (isFetching ? "Pobieranie samolotów…" : "Brak danych o samolotach")
            : string.Format(
                CultureInfo.InvariantCulture,
                "Samolotów w zasięgu: {0}, pokazano {1}, dane sprzed {2:0} s",
                snapshot.Aircraft.Count,
                results.Count,
                (DateTime.UtcNow - snapshot.TimestampUtc).TotalSeconds);

        resultsLayout.RemoveAllViews();

        for (int index = 0; index < results.Count; index++)
        {
            resultsLayout.AddView(CreateRow(results[index], index));
        }
    }

    /// <summary>
    /// Tworzy wiersz listy: w pierwszej linii lot i rejestracja, w drugiej linia lotnicza, w trzeciej typ, trasa i wysokość. Parzyste wiersze mają jaśniejsze tło.
    /// </summary>
    /// <param name="item">Samolot.</param>
    /// <param name="index">Numer wiersza na liście.</param>
    /// <returns>Klikalny wiersz listy.</returns>
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
    /// Prosi mapy o śledzenie wybranego samolotu i wraca do mapy.
    /// </summary>
    /// <param name="hex">Adres ICAO samolotu.</param>
    private void SelectAircraft(string hex)
    {
        LiveTraffic.RequestFocus(hex);
        Finish();
    }

    /// <summary>
    /// Przelicza dp na piksele ekranu telefonu.
    /// </summary>
    /// <param name="value">Wartość w dp.</param>
    /// <returns>Wartość w pikselach.</returns>
    private int Dp(int value)
    {
        return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
    }
}

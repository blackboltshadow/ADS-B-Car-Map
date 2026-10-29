using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities;

/// <summary>
/// Activity startowa na telefonie z mapą samolotów, przyciskami zoomu i centrowania oraz menu prowadzącym do wyszukiwarki i ustawień. Prosi też o uprawnienia lokalizacji i powiadomień.
/// </summary>
[Activity(
    Label = "ADS-B Car Map",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout)]
public class MainActivity : Activity
{
    /// <summary>
    /// Tag używany w logach Androida.
    /// </summary>
    private const string LogTag = "AdsbCarMap";

    /// <summary>
    /// Kod żądania uprawnień lokalizacji i powiadomień.
    /// </summary>
    private const int PermissionRequestCode = 1;

    /// <summary>
    /// Identyfikator pozycji menu otwierającej wyszukiwarkę.
    /// </summary>
    private const int MenuSearchId = 1;

    /// <summary>
    /// Identyfikator pozycji menu otwierającej ustawienia.
    /// </summary>
    private const int MenuSettingsId = 2;

    /// <summary>
    /// Kontroler mapy rysowanej na ekranie telefonu.
    /// </summary>
    private readonly MapController controller = new MapController(AppServices.Adsb, AppServices.Tiles, AppServices.Routes);

    /// <summary>
    /// Przycisk izolacji zaznaczonych samolotów.
    /// </summary>
    private TextView? isolateButton;

    /// <summary>
    /// Przycisk trybu zaznaczania wielu samolotów.
    /// </summary>
    private TextView? multiSelectButton;

    /// <summary>
    /// Przycisk pokazujący i ukrywający etykiety z opisem samolotów.
    /// </summary>
    private ImageButton? labelsButton;

    /// <summary>
    /// Przycisk filtra pokazującego wyłącznie samoloty wojskowe.
    /// </summary>
    private ImageButton? militaryButton;

    /// <summary>
    /// Karta głównego zaznaczonego samolotu.
    /// </summary>
    private AircraftCardView? aircraftCard;

    /// <summary>
    /// Handler głównego wątku używany do okresowego odświeżania karty.
    /// </summary>
    private readonly Handler handler = new Handler(Looper.MainLooper!);

    /// <summary>
    /// Zadanie okresowego odświeżania karty, null gdy Activity nie jest widoczna.
    /// </summary>
    private Java.Lang.Runnable? cardRefreshRunnable;

    /// <summary>
    /// Odstęp odświeżania karty samolotu w milisekundach.
    /// </summary>
    private const int CardRefreshIntervalMilliseconds = 1000;

    /// <summary>
    /// Część szerokości ekranu zajmowana przez kartę samolotu w poziomie, aby nie zasłaniała przycisków po prawej.
    /// </summary>
    private const float LandscapeCardWidthFraction = 0.55f;

    /// <summary>
    /// Buduje ekran z mapą i przyciskami oraz prosi o uprawnienia lokalizacji i powiadomień, jeśli ich brakuje.
    /// </summary>
    /// <param name="savedInstanceState">Zapisany stan Activity.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Log.Info(LogTag, "MainActivity.OnCreate");

        controller.SelectionChanged += () => RunOnUiThread(OnSelectionChanged);
        SetContentView(BuildContent());

        List<string> missingPermissions = new List<string>();

        if (!LocationTracker.HasPermission())
        {
            missingPermissions.Add(Android.Manifest.Permission.AccessFineLocation);
            missingPermissions.Add(Android.Manifest.Permission.AccessCoarseLocation);
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            missingPermissions.Add(Android.Manifest.Permission.PostNotifications);
        }

        if (missingPermissions.Count > 0)
        {
            RequestPermissions(missingPermissions.ToArray(), PermissionRequestCode);
        }
    }

    /// <summary>
    /// Przebudowuje układ po obrocie ekranu bez odtwarzania Activity, dzięki czemu mapa zachowuje przybliżenie, środek i zaznaczenie.
    /// </summary>
    /// <param name="newConfig">Nowa konfiguracja ekranu.</param>
    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        SetContentView(BuildContent());
        UpdateCard();
    }

    /// <summary>
    /// Uruchamia okresowe odświeżanie karty samolotu.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();

        UpdateModeButtons();
        cardRefreshRunnable = new Java.Lang.Runnable(CardRefreshTick);
        handler.Post(cardRefreshRunnable);
    }

    /// <summary>
    /// Zatrzymuje okresowe odświeżanie karty samolotu.
    /// </summary>
    protected override void OnPause()
    {
        if (cardRefreshRunnable != null)
        {
            handler.RemoveCallbacks(cardRefreshRunnable);
            cardRefreshRunnable = null;
        }

        base.OnPause();
    }

    /// <summary>
    /// Odświeża kartę i planuje kolejne odświeżenie, dopóki Activity jest widoczna.
    /// </summary>
    private void CardRefreshTick()
    {
        UpdateCard();

        if (cardRefreshRunnable != null)
        {
            handler.PostDelayed(cardRefreshRunnable, CardRefreshIntervalMilliseconds);
        }
    }

    /// <summary>
    /// Pokazuje kartę głównego zaznaczonego samolotu z aktualnymi danymi albo ją ukrywa, gdy nic nie jest zaznaczone.
    /// </summary>
    private void UpdateCard()
    {
        if (aircraftCard == null)
        {
            return;
        }

        SelectedAircraftInfo? info = controller.GetPrimaryInfo();

        if (info == null)
        {
            aircraftCard.Visibility = ViewStates.Gone;

            return;
        }

        string? callsign = info.Aircraft?.Callsign;
        FlightRoute? route = AppServices.Routes.Get(callsign);

        AppServices.Routes.Prioritize(callsign);

        aircraftCard.Bind(info, route, AppServices.Routes.GetState(callsign));
        aircraftCard.Visibility = ViewStates.Visible;
    }

    /// <summary>
    /// Wymusza pobranie trasy głównego zaznaczonego samolotu i od razu odświeża kartę, żeby pokazała stan „pobieranie”.
    /// </summary>
    private void OnRefreshRouteClicked()
    {
        AppServices.Routes.Refresh(controller.GetPrimaryInfo()?.Aircraft?.Callsign);
        UpdateCard();
    }

    /// <summary>
    /// Odświeża przyciski trybów i kartę po zmianie zaznaczenia.
    /// </summary>
    private void OnSelectionChanged()
    {
        UpdateModeButtons();
        UpdateCard();
    }

    /// <summary>
    /// Uruchamia aktualizacje pozycji po przyznaniu uprawnienia lokalizacji.
    /// </summary>
    /// <param name="requestCode">Kod żądania.</param>
    /// <param name="permissions">Zażądane uprawnienia.</param>
    /// <param name="grantResults">Wyniki żądania.</param>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == PermissionRequestCode && LocationTracker.HasPermission())
        {
            controller.StartLocationUpdates();
        }
    }

    /// <summary>
    /// Tworzy widok ekranu: mapę na całej powierzchni, menu w prawym górnym rogu oraz przyciski zoomu i centrowania w prawym dolnym.
    /// </summary>
    /// <returns>Główny widok Activity.</returns>
    private View BuildContent()
    {
        FrameLayout root = new FrameLayout(this);
        root.SetFitsSystemWindows(true);
        root.AddView(new PhoneMapView(this, controller));

        ImageButton menuButton = CreateRoundButton(Resource.Drawable.ic_menu, "Menu");
        menuButton.Click += (sender, args) => ShowMenu(menuButton);

        FrameLayout.LayoutParams menuParams = new FrameLayout.LayoutParams(Dp(48), Dp(48), GravityFlags.Top | GravityFlags.End);
        menuParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(12));
        root.AddView(menuButton, menuParams);

        isolateButton = CreateTextButton("I", "Izoluj zaznaczone samoloty");
        isolateButton.Click += (sender, args) => OnIsolateClicked();

        multiSelectButton = CreateTextButton("M", "Zaznaczanie wielu samolotów");
        multiSelectButton.Click += (sender, args) => OnMultiSelectClicked();

        bool landscape = Resources?.Configuration?.Orientation == Android.Content.Res.Orientation.Landscape;

        LinearLayout modeColumn = new LinearLayout(this);
        modeColumn.Orientation = landscape ? Orientation.Horizontal : Orientation.Vertical;

        labelsButton = CreateRoundButton(Resource.Drawable.ic_labels, "Pokaż lub ukryj etykiety samolotów");
        labelsButton.Click += (sender, args) => OnLabelsClicked();

        militaryButton = CreateRoundButton(Resource.Drawable.ic_fighter, "Tylko samoloty wojskowe");
        militaryButton.Click += (sender, args) => OnMilitaryClicked();

        foreach (View button in new View[] { isolateButton, multiSelectButton, labelsButton, militaryButton })
        {
            LinearLayout.LayoutParams buttonParams = new LinearLayout.LayoutParams(Dp(48), Dp(48));

            if (landscape)
            {
                buttonParams.SetMargins(Dp(6), 0, Dp(6), 0);
            }
            else
            {
                buttonParams.SetMargins(0, Dp(6), 0, Dp(6));
            }

            modeColumn.AddView(button, buttonParams);
        }

        FrameLayout.LayoutParams modeParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.End);

        if (landscape)
        {
            modeParams.SetMargins(Dp(12), Dp(12), Dp(72), Dp(12));
        }
        else
        {
            modeParams.SetMargins(Dp(12), Dp(66), Dp(12), Dp(12));
        }

        root.AddView(modeColumn, modeParams);

        aircraftCard = new AircraftCardView(this, landscape ? 1.0f : 1.25f);
        aircraftCard.Visibility = ViewStates.Gone;
        aircraftCard.FollowClicked += () => controller.FollowPrimary();
        aircraftCard.CloseClicked += () => controller.ClearSelection();
        aircraftCard.RefreshRouteClicked += OnRefreshRouteClicked;

        int cardWidth = landscape
            ? (int)((Resources?.DisplayMetrics?.WidthPixels ?? 0) * LandscapeCardWidthFraction)
            : ViewGroup.LayoutParams.MatchParent;

        FrameLayout.LayoutParams cardParams = new FrameLayout.LayoutParams(cardWidth, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.Start);
        cardParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
        root.AddView(aircraftCard, cardParams);

        UpdateModeButtons();

        ImageButton zoomInButton = CreateRoundButton(Resource.Drawable.ic_zoom_in, "Przybliż");
        zoomInButton.Click += (sender, args) => controller.ZoomIn();

        ImageButton zoomOutButton = CreateRoundButton(Resource.Drawable.ic_zoom_out, "Oddal");
        zoomOutButton.Click += (sender, args) => controller.ZoomOut();

        ImageButton centerButton = CreateRoundButton(Resource.Drawable.ic_my_location, "Centruj na mojej pozycji");
        centerButton.Click += (sender, args) => controller.FollowUser();

        LinearLayout buttonColumn = new LinearLayout(this);
        buttonColumn.Orientation = Orientation.Vertical;

        foreach (ImageButton button in new ImageButton[] { zoomInButton, zoomOutButton, centerButton })
        {
            LinearLayout.LayoutParams buttonParams = new LinearLayout.LayoutParams(Dp(48), Dp(48));
            buttonParams.SetMargins(0, Dp(6), 0, Dp(6));
            buttonColumn.AddView(button, buttonParams);
        }

        FrameLayout.LayoutParams columnParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.End);
        columnParams.SetMargins(Dp(12), Dp(12), Dp(12), Dp(32));
        root.AddView(buttonColumn, columnParams);

        aircraftCard.BringToFront();

        return root;
    }

    /// <summary>
    /// Tworzy okrągły przycisk z ikoną nakładany na mapę.
    /// </summary>
    /// <param name="iconResourceId">Identyfikator zasobu drawable z ikoną.</param>
    /// <param name="description">Opis przycisku dla czytników ekranu.</param>
    /// <returns>Przycisk gotowy do dodania do układu.</returns>
    private ImageButton CreateRoundButton(int iconResourceId, string description)
    {
        ImageButton button = new ImageButton(this);
        button.SetBackgroundResource(Resource.Drawable.bg_map_button);
        button.SetImageResource(iconResourceId);
        button.SetScaleType(ImageView.ScaleType.Center);
        button.ContentDescription = description;

        return button;
    }

    /// <summary>
    /// Tworzy okrągły przycisk z literą nakładany na mapę.
    /// </summary>
    /// <param name="text">Litera na przycisku.</param>
    /// <param name="description">Opis przycisku dla czytników ekranu.</param>
    /// <returns>Przycisk gotowy do dodania do układu.</returns>
    private TextView CreateTextButton(string text, string description)
    {
        TextView button = new TextView(this);
        button.Text = text;
        button.TextSize = 18f;
        button.SetTypeface(null, TypefaceStyle.Bold);
        button.SetTextColor(Color.White);
        button.Gravity = GravityFlags.Center;
        button.SetBackgroundResource(Resource.Drawable.bg_map_button);
        button.Clickable = true;
        button.ContentDescription = description;

        return button;
    }

    /// <summary>
    /// Włącza lub wyłącza izolację zaznaczonych samolotów i podpowiada, gdy nic nie jest zaznaczone.
    /// </summary>
    private void OnIsolateClicked()
    {
        if (!controller.TryToggleIsolation(out bool isolated))
        {
            Toast.MakeText(this, "Najpierw zaznacz samolot, dotykając go na mapie", ToastLength.Short)?.Show();
        }
    }

    /// <summary>
    /// Włącza lub wyłącza zaznaczanie wielu samolotów.
    /// </summary>
    private void OnMultiSelectClicked()
    {
        bool enabled = controller.ToggleMultiSelect();
        string message = enabled
            ? "Zaznaczanie wielu samolotów: dotykaj samolotów, aby je dodać lub odjąć"
            : "Zaznaczanie wielu samolotów wyłączone";

        Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Przełącza widoczność etykiet z opisem samolotów i zapisuje wybór w ustawieniach.
    /// </summary>
    private void OnLabelsClicked()
    {
        bool show = !AppSettings.ShowLabels;
        Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>
        {
            { AppSettings.ShowLabelsSetting, show ? 1.0 : 0.0 },
        };

        AppSettings.Save(values);
        UpdateModeButtons();
        Toast.MakeText(this, show ? "Etykiety samolotów: widoczne" : "Etykiety samolotów: ukryte", ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Włącza lub wyłącza filtr samolotów wojskowych i podaje, ile ich jest w ostatnich danych.
    /// </summary>
    private void OnMilitaryClicked()
    {
        bool enabled = controller.ToggleMilitaryOnly(out int militaryCount);
        string message = !enabled
            ? "Wszystkie samoloty"
            : militaryCount > 0
                ? string.Format(CultureInfo.InvariantCulture, "Tylko wojskowe: {0} w zasięgu", militaryCount)
                : "Tylko wojskowe: żadnego w zasięgu";

        Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Podświetla przyciski „I”, „M”, etykiet i filtra wojskowych, gdy odpowiadające im tryby są włączone.
    /// </summary>
    private void UpdateModeButtons()
    {
        militaryButton?.SetBackgroundResource(controller.IsMilitaryOnly ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
        isolateButton?.SetBackgroundResource(controller.IsIsolated ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
        multiSelectButton?.SetBackgroundResource(controller.IsMultiSelect ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
        labelsButton?.SetBackgroundResource(AppSettings.ShowLabels ? Resource.Drawable.bg_map_button_active : Resource.Drawable.bg_map_button);
    }

    /// <summary>
    /// Pokazuje menu z wyszukiwarką i ustawieniami pod przyciskiem menu.
    /// </summary>
    /// <param name="anchor">Przycisk, pod którym pojawia się menu.</param>
    private void ShowMenu(View anchor)
    {
        PopupMenu popup = new PopupMenu(this, anchor);
        popup.Menu?.Add(0, MenuSearchId, 0, "Szukaj samolotu");
        popup.Menu?.Add(0, MenuSettingsId, 1, "Ustawienia");
        popup.MenuItemClick += (sender, args) =>
        {
            args.Handled = true;

            if (args.Item?.ItemId == MenuSearchId)
            {
                StartActivity(new Intent(this, typeof(SearchActivity)));
            }
            else if (args.Item?.ItemId == MenuSettingsId)
            {
                StartActivity(new Intent(this, typeof(SettingsActivity)));
            }
        };
        popup.Show();
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

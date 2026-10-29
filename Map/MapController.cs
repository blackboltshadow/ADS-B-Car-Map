using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map;

/// <summary>
/// Przechowuje stan mapy oraz uruchamia pętle pobierania samolotów i rysowania na powierzchni Android Auto.
/// </summary>
public sealed class MapController
{
    /// <summary>
    /// Tag używany w logach Androida.
    /// </summary>
    private const string LogTag = "AdsbCarMap";

    /// <summary>
    /// Maksymalny czas w milisekundach, przez jaki główny wątek czeka na zakończenie rysowania klatki przy zatrzymaniu.
    /// </summary>
    private const int StopWaitMilliseconds = 500;

    /// <summary>
    /// Największy poziom przybliżenia kafelków OpenStreetMap.
    /// </summary>
    private const int MaxTileZoom = 19;

    /// <summary>
    /// O ile poziomów niższy jest zawsze wczytywany kafelek bazowy, który służy za tło, gdy dokładniejszy kafelek jeszcze nie dotarł.
    /// </summary>
    private const int BaseLayerLevels = 2;

    /// <summary>
    /// Na ilu poziomach w dół szukany jest kafelek zastępczy.
    /// </summary>
    private const int MaxFallbackLevels = 3;

    /// <summary>
    /// Szerokość pasa wokół ekranu w pikselach kafelków, z którego kafelki są wczytywane z wyprzedzeniem.
    /// </summary>
    private const int TilePrefetchPixels = 256;

    /// <summary>
    /// Największa liczba jednocześnie pobieranych kafelków.
    /// </summary>
    private const int MaxConcurrentTileDownloads = 6;

    /// <summary>
    /// Stała czasowa płynnego dochodzenia zoomu do celu, w sekundach.
    /// </summary>
    private const double ZoomEasingSeconds = 0.09;

    /// <summary>
    /// Różnica zoomu, poniżej której zoom jest od razu ustawiany na cel.
    /// </summary>
    private const double ZoomSnapThreshold = 0.004;

    /// <summary>
    /// Stała czasowa zanikania bezwładnego przesuwania, w sekundach.
    /// </summary>
    private const double FlingDecaySeconds = 0.35;

    /// <summary>
    /// Prędkość w pikselach na sekundę, poniżej której bezwładne przesuwanie się kończy.
    /// </summary>
    private const double FlingStopVelocity = 25.0;

    /// <summary>
    /// Największy czas jednej klatki liczony do ruchu, w sekundach, aby po zatrzymaniu pętli mapa nie skakała.
    /// </summary>
    private const double MaxFrameSeconds = 0.1;

    /// <summary>
    /// Zapas miejsc w pamięci kafelków ponad liczbę widocznych kafelków, aby limit z ustawień nie powodował ciągłego pobierania.
    /// </summary>
    private const int TileCacheMargin = 16;

    /// <summary>
    /// Odstęp między zapisami statystyk kafelków w logu.
    /// </summary>
    private static readonly TimeSpan TileStatisticsInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maksymalny wiek współdzielonej listy samolotów, przy którym nowa mapa pokazuje ją od razu po uruchomieniu.
    /// </summary>
    private static readonly TimeSpan SharedSnapshotMaxAge = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Kolor tła widoczny, zanim kafelki zostaną pobrane.
    /// </summary>
    private static readonly Color BackgroundColor = Color.Rgb(30, 34, 40);

    /// <summary>
    /// Przelicznik stóp na metry.
    /// </summary>
    private const double MetersPerFoot = 0.3048;

    /// <summary>
    /// Kolor samolotu na ziemi.
    /// </summary>
    private static readonly Color GroundColor = Color.Rgb(150, 150, 150);

    /// <summary>
    /// Kolory samolotu w powietrzu odpowiadające progom wysokości z <see cref="AppSettings.AltitudeStopsMeters"/>: pomarańczowy, żółty, zielony, niebieski, fioletowy.
    /// </summary>
    private static readonly Color[] AltitudeStopColors = new Color[]
    {
        Color.Rgb(255, 140, 0),
        Color.Rgb(255, 220, 0),
        Color.Rgb(60, 200, 60),
        Color.Rgb(40, 130, 255),
        Color.Rgb(170, 70, 230),
    };

    /// <summary>
    /// Kolor znacznika pozycji użytkownika.
    /// </summary>
    private static readonly Color UserColor = Color.Rgb(66, 133, 244);

    /// <summary>
    /// Kolor obwódki znacznika pozycji użytkownika.
    /// </summary>
    private static readonly Color UserOutlineColor = Color.White;

    /// <summary>
    /// Kolor pierścienia wokół zaznaczonego samolotu.
    /// </summary>
    private static readonly Color SelectedAircraftColor = Color.Rgb(255, 214, 0);

    /// <summary>
    /// Promień w dp, w jakim dotknięcie jest przypisywane do samolotu.
    /// </summary>
    private const float TapRadius = 30.0f;

    /// <summary>
    /// Kolor obwódki wokół śledzonego samolotu.
    /// </summary>
    private static readonly Color FollowedAircraftColor = Color.White;

    /// <summary>
    /// Klient API ADS-B.
    /// </summary>
    private readonly AdsbClient adsbClient;

    /// <summary>
    /// Dostawca kafelków mapy.
    /// </summary>
    private readonly TileProvider tileProvider;

    /// <summary>
    /// Pamięć tras lotów.
    /// </summary>
    private readonly RouteCache routeCache;

    /// <summary>
    /// Blokada chroniąca stan widoku i listę samolotów.
    /// </summary>
    private readonly object stateLock = new object();

    /// <summary>
    /// Blokada gwarantująca, że powierzchnia nie jest używana po jej zniszczeniu.
    /// </summary>
    private readonly object renderLock = new object();

    /// <summary>
    /// Zdekodowane kafelki gotowe do narysowania.
    /// </summary>
    private readonly ConcurrentDictionary<TileKey, Bitmap> bitmaps = new ConcurrentDictionary<TileKey, Bitmap>();

    /// <summary>
    /// Kafelki, które są aktualnie pobierane lub dekodowane.
    /// </summary>
    private readonly ConcurrentDictionary<TileKey, byte> loadingTiles = new ConcurrentDictionary<TileKey, byte>();

    /// <summary>
    /// Numer klatki, w której kafelek był ostatnio narysowany lub zdekodowany. Służy do usuwania najdawniej użytych kafelków.
    /// </summary>
    private readonly ConcurrentDictionary<TileKey, long> tileLastUsedFrame = new ConcurrentDictionary<TileKey, long>();

    /// <summary>
    /// Kafelki potrzebne w ostatniej klatce: widoczne, sąsiednie do wczytania z wyprzedzeniem oraz warstwa bazowa o niższym poziomie. Zbiór jest podmieniany co klatkę i nie jest modyfikowany po publikacji.
    /// </summary>
    private HashSet<TileKey> wantedTiles = new HashSet<TileKey>();

    /// <summary>
    /// Ogranicza liczbę jednoczesnych pobrań kafelków.
    /// </summary>
    private readonly SemaphoreSlim tileGate = new SemaphoreSlim(MaxConcurrentTileDownloads);

    /// <summary>
    /// Źródło fragmentu kafelka używanego jako zastępczy.
    /// </summary>
    private readonly Rect fallbackSource = new Rect();

    /// <summary>
    /// Obszar, w którym rysowany jest kafelek zastępczy.
    /// </summary>
    private readonly RectF fallbackDestination = new RectF();

    /// <summary>
    /// Numer ostatnio rozpoczętej klatki rysowania.
    /// </summary>
    private long frameCounter;

    /// <summary>
    /// Liczba kafelków pokrywających ekran w ostatniej klatce.
    /// </summary>
    private int lastVisibleTileCount;

    /// <summary>
    /// Czas, po którym można ponowić pobranie kafelka, który się nie udał.
    /// </summary>
    private readonly ConcurrentDictionary<TileKey, DateTime> tileRetryAfterUtc = new ConcurrentDictionary<TileKey, DateTime>();

    /// <summary>
    /// Liczba kafelków zdekodowanych od uruchomienia kontrolera.
    /// </summary>
    private long tilesDecodedCount;

    /// <summary>
    /// Liczba nieudanych prób pobrania lub zdekodowania kafelka od uruchomienia kontrolera.
    /// </summary>
    private long tilesFailedCount;

    /// <summary>
    /// Rozmiar i format ostatnio zdekodowanego kafelka.
    /// </summary>
    private string lastDecodedTileInfo = "brak";

    /// <summary>
    /// Moment ostatniego zapisu statystyk kafelków w logu, w UTC.
    /// </summary>
    private DateTime lastTileStatisticsUtc = DateTime.MinValue;

    /// <summary>
    /// Pędzel używany do rysowania ciemnej obwódki ikon samolotów.
    /// </summary>
    private readonly Paint aircraftOutlinePaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania pierścienia wokół zaznaczonych samolotów.
    /// </summary>
    private readonly Paint selectedPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania kafelków.
    /// </summary>
    private readonly Paint tilePaint = new Paint(PaintFlags.FilterBitmap);

    /// <summary>
    /// Pędzel używany do rysowania sylwetek samolotów.
    /// </summary>
    private readonly Paint aircraftPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania śladów samolotów.
    /// </summary>
    private readonly Paint trailPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania ciemnej obwódki pod śladami samolotów.
    /// </summary>
    private readonly Paint trailOutlinePaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Ścieżka obwódki śladu, wielokrotnie używana dla kolejnych samolotów.
    /// </summary>
    private readonly AndroidPath trailOutlinePath = new AndroidPath();

    /// <summary>
    /// Pędzel używany do rysowania etykiet samolotów.
    /// </summary>
    private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania atrybucji OpenStreetMap.
    /// </summary>
    private readonly Paint attributionPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania numeru wersji.
    /// </summary>
    private readonly Paint versionPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania znacznika pozycji użytkownika.
    /// </summary>
    private readonly Paint userPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Pędzel używany do rysowania obwódki wokół śledzonego samolotu.
    /// </summary>
    private readonly Paint followedPaint = new Paint(PaintFlags.AntiAlias);

    /// <summary>
    /// Kształty ikon samolotów według rodzaju.
    /// </summary>
    private readonly AircraftShapes aircraftShapes = new AircraftShapes();

    /// <summary>
    /// Szerokość geograficzna środka mapy.
    /// </summary>
    private double centerLatitude = 52.2297;

    /// <summary>
    /// Długość geograficzna środka mapy.
    /// </summary>
    private double centerLongitude = 21.0122;

    /// <summary>
    /// Informacja, czy środek mapy podąża za pozycją użytkownika.
    /// </summary>
    private bool followLocation = true;

    /// <summary>
    /// Adres ICAO samolotu, za którym podąża środek mapy, lub null, gdy żaden nie jest śledzony.
    /// </summary>
    private string? followedHex;

    /// <summary>
    /// Adresy ICAO zaznaczonych samolotów. Zbiór jest podmieniany przy każdej zmianie i nie jest modyfikowany po przypisaniu.
    /// </summary>
    private IReadOnlySet<string> selectedHexes = new HashSet<string>();

    /// <summary>
    /// Adres ICAO głównego zaznaczonego samolotu, którego dotyczy karta, albo null, gdy nic nie jest zaznaczone.
    /// </summary>
    private string? primaryHex;

    /// <summary>
    /// Informacja, czy dotknięcia samolotów dodają je do zaznaczenia.
    /// </summary>
    private bool multiSelect;

    /// <summary>
    /// Informacja, czy rysowane są wyłącznie zaznaczone samoloty.
    /// </summary>
    private bool isolateSelected;

    /// <summary>
    /// Pozycje samolotów narysowanych w ostatniej klatce, używane do rozpoznawania dotknięć. Lista jest podmieniana co klatkę.
    /// </summary>
    private IReadOnlyList<(string Hex, float X, float Y)> screenHits = Array.Empty<(string Hex, float X, float Y)>();

    /// <summary>
    /// Progi wysokości w metrach obowiązujące w bieżącej klatce, odczytywane z ustawień na początku rysowania.
    /// </summary>
    private double[] frameAltitudeStops = AppSettings.AltitudeStopsMeters;

    /// <summary>
    /// Informacja, czy znana jest już pozycja użytkownika.
    /// </summary>
    private bool hasUserLocation;

    /// <summary>
    /// Ostatnia znana szerokość geograficzna użytkownika.
    /// </summary>
    private double userLatitude;

    /// <summary>
    /// Ostatnia znana długość geograficzna użytkownika.
    /// </summary>
    private double userLongitude;

    /// <summary>
    /// Aktualny, ułamkowy poziom przybliżenia widoczny na ekranie.
    /// </summary>
    private double zoom = 8.0;

    /// <summary>
    /// Poziom przybliżenia, do którego płynnie zmierza <see cref="zoom"/>.
    /// </summary>
    private double zoomTarget = 8.0;

    /// <summary>
    /// Prędkość bezwładnego przesuwania w poziomie w pikselach na sekundę, według konwencji GestureDetector.
    /// </summary>
    private double flingVelocityX;

    /// <summary>
    /// Prędkość bezwładnego przesuwania w pionie w pikselach na sekundę, według konwencji GestureDetector.
    /// </summary>
    private double flingVelocityY;

    /// <summary>
    /// Moment rozpoczęcia poprzedniej klatki, w UTC. Służy do ruchu niezależnego od tempa klatek.
    /// </summary>
    private DateTime lastFrameUtc = DateTime.MinValue;

    /// <summary>
    /// Szerokość powierzchni w pikselach.
    /// </summary>
    private int surfaceWidth;

    /// <summary>
    /// Wysokość powierzchni w pikselach.
    /// </summary>
    private int surfaceHeight;

    /// <summary>
    /// Współczynnik gęstości ekranu względem 160 dpi.
    /// </summary>
    private float density = 1.0f;

    /// <summary>
    /// Ostatnio pobrana lista samolotów.
    /// </summary>
    private IReadOnlyList<Aircraft> latestAircraft = Array.Empty<Aircraft>();

    /// <summary>
    /// Moment pobrania ostatniej listy samolotów, w UTC.
    /// </summary>
    private DateTime latestAircraftTimestampUtc = DateTime.UtcNow;

    /// <summary>
    /// Ślady samolotów według adresu ICAO, od najstarszego punktu. Słownik i tablice nie są modyfikowane po przypisaniu, każda aktualizacja tworzy nowe.
    /// </summary>
    private IReadOnlyDictionary<string, TrailPoint[]> latestTrails = new Dictionary<string, TrailPoint[]>();

    /// <summary>
    /// Powierzchnia, na której rysowana jest mapa. Null, gdy nie jest dostępna.
    /// </summary>
    private Surface? surface;

    /// <summary>
    /// Źródło tokenów anulowania dla pętli pobierania i rysowania.
    /// </summary>
    private CancellationTokenSource? cancellation;

    /// <summary>
    /// Odbiornik aktualizacji pozycji użytkownika.
    /// </summary>
    private readonly LocationTracker locationTracker;

    /// <summary>
    /// Zdarzenie wywoływane po zmianie zaznaczenia, trybu zaznaczania lub izolacji.
    /// </summary>
    public event System.Action? SelectionChanged;

    /// <summary>
    /// Tworzy kontroler i przygotowuje pędzle oraz kształt samolotu.
    /// </summary>
    /// <param name="adsbClient">Klient API ADS-B.</param>
    /// <param name="tileProvider">Dostawca kafelków mapy.</param>
    /// <param name="routeCache">Pamięć tras lotów.</param>
    public MapController(AdsbClient adsbClient, TileProvider tileProvider, RouteCache routeCache)
    {
        this.adsbClient = adsbClient;
        this.tileProvider = tileProvider;
        this.routeCache = routeCache;
        locationTracker = new LocationTracker(SetUserLocation);

        trailPaint.SetStyle(Paint.Style.Stroke);
        trailPaint.StrokeCap = Paint.Cap.Round;

        trailOutlinePaint.SetStyle(Paint.Style.Stroke);
        trailOutlinePaint.StrokeCap = Paint.Cap.Round;
        trailOutlinePaint.StrokeJoin = Paint.Join.Round;

        followedPaint.SetStyle(Paint.Style.Stroke);
        followedPaint.Color = FollowedAircraftColor;

        selectedPaint.SetStyle(Paint.Style.Stroke);
        selectedPaint.Color = SelectedAircraftColor;

        aircraftOutlinePaint.SetStyle(Paint.Style.Stroke);
        aircraftOutlinePaint.StrokeJoin = Paint.Join.Round;
        aircraftOutlinePaint.Color = Color.Argb(235, 0, 0, 0);

        labelPaint.Color = Color.White;
        labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

        attributionPaint.Color = Color.White;
        attributionPaint.TextAlign = Paint.Align.Right;
        attributionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

        versionPaint.Color = Color.White;
        versionPaint.TextAlign = Paint.Align.Left;
        versionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
    }

    /// <summary>
    /// Zapamiętuje powierzchnię i uruchamia pętle pobierania danych oraz rysowania, a także aktualizacje pozycji.
    /// </summary>
    /// <param name="newSurface">Powierzchnia udostępniona przez Android Auto.</param>
    /// <param name="width">Szerokość powierzchni w pikselach.</param>
    /// <param name="height">Wysokość powierzchni w pikselach.</param>
    /// <param name="dpi">Gęstość ekranu w dpi.</param>
    public void Start(Surface newSurface, int width, int height, int dpi)
    {
        Stop();

        (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) shared = LiveTraffic.GetSnapshot();

        lock (stateLock)
        {
            surfaceWidth = width;
            surfaceHeight = height;
            density = Math.Max(1.0f, dpi / 160.0f);

            if (latestAircraft.Count == 0 && DateTime.UtcNow - shared.TimestampUtc < SharedSnapshotMaxAge)
            {
                latestAircraft = shared.Aircraft;
                latestAircraftTimestampUtc = shared.TimestampUtc;
            }
        }

        lock (renderLock)
        {
            surface = newSurface;
        }

        CancellationTokenSource source = new CancellationTokenSource();
        cancellation = source;
        locationTracker.Start();
        LiveTraffic.FocusRequested += FollowAircraft;

        string? pendingFocus = LiveTraffic.GetPendingFocus();

        if (pendingFocus != null)
        {
            FollowAircraft(pendingFocus);
        }

        _ = Task.Run(() => RenderLoopAsync(source.Token));
        _ = Task.Run(() => DataLoopAsync(source.Token));
    }

    /// <summary>
    /// Zatrzymuje pętle i aktualizacje pozycji oraz zwalnia powierzchnię. Wraca dopiero po zakończeniu ewentualnego rysowania klatki.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? source = cancellation;
        cancellation = null;
        source?.Cancel();
        locationTracker.Stop();
        LiveTraffic.FocusRequested -= FollowAircraft;
        Volatile.Write(ref wantedTiles, new HashSet<TileKey>());

        bool lockTaken = false;

        try
        {
            Monitor.TryEnter(renderLock, StopWaitMilliseconds, ref lockTaken);
            surface = null;

            if (!lockTaken)
            {
                Log.Warn(LogTag, "Rysowanie klatki nie zakończyło się w czasie, powierzchnia zwolniona bez czekania.");
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(renderLock);
            }
        }
    }

    /// <summary>
    /// Płynnie przybliża mapę do następnego pełnego poziomu.
    /// </summary>
    public void ZoomIn()
    {
        lock (stateLock)
        {
            zoomTarget = Math.Min(AppSettings.MaxZoom, Math.Floor(zoomTarget + 0.01) + 1.0);
        }
    }

    /// <summary>
    /// Płynnie oddala mapę do poprzedniego pełnego poziomu.
    /// </summary>
    public void ZoomOut()
    {
        lock (stateLock)
        {
            zoomTarget = Math.Max(AppSettings.MinZoom, Math.Ceiling(zoomTarget - 0.01) - 1.0);
        }
    }

    /// <summary>
    /// Zmienia zoom ciągle zgodnie ze skalą gestu przybliżania.
    /// </summary>
    /// <param name="scaleFactor">Współczynnik skali względem poprzedniego zdarzenia.</param>
    public void Scale(float scaleFactor)
    {
        if (scaleFactor <= 0.0f)
        {
            return;
        }

        lock (stateLock)
        {
            double next = Math.Clamp(zoom + Math.Log2(scaleFactor), AppSettings.MinZoom, AppSettings.MaxZoom);
            zoom = next;
            zoomTarget = next;
        }
    }

    /// <summary>
    /// Przesuwa środek mapy o podaną liczbę pikseli, zatrzymuje bezwładność i wyłącza śledzenie pozycji użytkownika oraz samolotu.
    /// </summary>
    /// <param name="distanceX">Przesunięcie w poziomie w pikselach, według konwencji GestureDetector.</param>
    /// <param name="distanceY">Przesunięcie w pionie w pikselach, według konwencji GestureDetector.</param>
    public void Pan(float distanceX, float distanceY)
    {
        lock (stateLock)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
            MoveCenter(distanceX, distanceY);
        }
    }

    /// <summary>
    /// Uruchamia bezwładne przesuwanie mapy po geście przeciągnięcia z rozpędem.
    /// </summary>
    /// <param name="velocityX">Prędkość palca w poziomie w pikselach na sekundę.</param>
    /// <param name="velocityY">Prędkość palca w pionie w pikselach na sekundę.</param>
    public void Fling(float velocityX, float velocityY)
    {
        lock (stateLock)
        {
            flingVelocityX = velocityX;
            flingVelocityY = velocityY;
            followLocation = false;
            followedHex = null;
        }
    }

    /// <summary>
    /// Zatrzymuje bezwładne przesuwanie, np. po dotknięciu ekranu.
    /// </summary>
    public void StopFling()
    {
        lock (stateLock)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
        }
    }

    /// <summary>
    /// Przesuwa środek mapy o podaną liczbę pikseli i wyłącza śledzenie. Wywoływać pod blokadą stanu.
    /// </summary>
    /// <param name="distanceX">Przesunięcie w poziomie w pikselach.</param>
    /// <param name="distanceY">Przesunięcie w pionie w pikselach.</param>
    private void MoveCenter(double distanceX, double distanceY)
    {
        (double X, double Y) center = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
        (double Latitude, double Longitude) moved = WebMercator.WorldPixelToLatLon(center.X + distanceX, center.Y + distanceY, zoom);

        centerLatitude = Math.Clamp(moved.Latitude, -WebMercator.MaxLatitude, WebMercator.MaxLatitude);
        centerLongitude = ((moved.Longitude + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        followLocation = false;
        followedHex = null;
    }

    /// <summary>
    /// Przesuwa stan ruchu o czas jednej klatki: zbliża zoom do celu i przesuwa mapę z bezwładnością. Wywoływać pod blokadą stanu.
    /// </summary>
    /// <param name="deltaSeconds">Czas od poprzedniej klatki w sekundach.</param>
    private void AdvanceMotion(double deltaSeconds)
    {
        double minimumZoom = AppSettings.MinZoom;
        double maximumZoom = AppSettings.MaxZoom;

        zoomTarget = Math.Clamp(zoomTarget, minimumZoom, maximumZoom);

        double difference = zoomTarget - zoom;

        zoom = Math.Abs(difference) < ZoomSnapThreshold
            ? zoomTarget
            : zoom + difference * (1.0 - Math.Exp(-deltaSeconds / ZoomEasingSeconds));
        zoom = Math.Clamp(zoom, minimumZoom, maximumZoom);

        if (flingVelocityX == 0.0 && flingVelocityY == 0.0)
        {
            return;
        }

        MoveCenter(-flingVelocityX * deltaSeconds, -flingVelocityY * deltaSeconds);

        double decay = Math.Exp(-deltaSeconds / FlingDecaySeconds);
        flingVelocityX *= decay;
        flingVelocityY *= decay;

        if (Math.Abs(flingVelocityX) < FlingStopVelocity && Math.Abs(flingVelocityY) < FlingStopVelocity)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
        }
    }

    /// <summary>
    /// Włącza podążanie środka mapy za samolotem i wyłącza śledzenie pozycji użytkownika.
    /// </summary>
    /// <param name="hex">Adres ICAO samolotu.</param>
    public void FollowAircraft(string hex)
    {
        lock (stateLock)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
            followedHex = hex;
            followLocation = false;
            selectedHexes = new HashSet<string> { hex };
            primaryHex = hex;
        }

        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Włącza podążanie środka mapy za głównym zaznaczonym samolotem, nie zmieniając zaznaczenia.
    /// </summary>
    public void FollowPrimary()
    {
        lock (stateLock)
        {
            if (primaryHex == null)
            {
                return;
            }

            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
            followedHex = primaryHex;
            followLocation = false;
        }
    }

    /// <summary>
    /// Czyści zaznaczenie samolotów i wyłącza izolację.
    /// </summary>
    public void ClearSelection()
    {
        lock (stateLock)
        {
            selectedHexes = new HashSet<string>();
            primaryHex = null;
            isolateSelected = false;
        }

        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Zwraca dane głównego zaznaczonego samolotu z bieżącą, ekstrapolowaną pozycją oraz pozycję użytkownika, albo null, gdy nic nie jest zaznaczone.
    /// </summary>
    /// <returns>Dane do pokazania na karcie samolotu lub null.</returns>
    public SelectedAircraftInfo? GetPrimaryInfo()
    {
        lock (stateLock)
        {
            string? hex = primaryHex;

            if (hex == null)
            {
                return null;
            }

            Aircraft? aircraft = latestAircraft.FirstOrDefault(item => item.Hex == hex);
            double ageSeconds = (DateTime.UtcNow - latestAircraftTimestampUtc).TotalSeconds;

            if (aircraft == null)
            {
                return new SelectedAircraftInfo(hex, null, 0.0, 0.0, ageSeconds, hasUserLocation, userLatitude, userLongitude, selectedHexes.Count);
            }

            (double Latitude, double Longitude) position = Extrapolate(aircraft, ageSeconds);

            return new SelectedAircraftInfo(hex, aircraft, position.Latitude, position.Longitude, ageSeconds, hasUserLocation, userLatitude, userLongitude, selectedHexes.Count);
        }
    }

    /// <summary>
    /// Informacja, czy dotknięcia samolotów dodają je do zaznaczenia zamiast zastępować zaznaczenie.
    /// </summary>
    public bool IsMultiSelect
    {
        get
        {
            lock (stateLock)
            {
                return multiSelect;
            }
        }
    }

    /// <summary>
    /// Informacja, czy na mapie widać wyłącznie zaznaczone samoloty.
    /// </summary>
    public bool IsIsolated
    {
        get
        {
            lock (stateLock)
            {
                return isolateSelected;
            }
        }
    }

    /// <summary>
    /// Włącza lub wyłącza tryb zaznaczania wielu samolotów.
    /// </summary>
    /// <returns>True, gdy tryb jest teraz włączony.</returns>
    public bool ToggleMultiSelect()
    {
        bool enabled;

        lock (stateLock)
        {
            multiSelect = !multiSelect;
            enabled = multiSelect;
        }

        SelectionChanged?.Invoke();

        return enabled;
    }

    /// <summary>
    /// Włącza izolację zaznaczonych samolotów albo ją wyłącza, jeśli jest włączona.
    /// </summary>
    /// <param name="isolated">Informacja, czy izolacja jest włączona po wywołaniu.</param>
    /// <returns>False, gdy izolacji nie da się włączyć, bo nic nie jest zaznaczone.</returns>
    public bool TryToggleIsolation(out bool isolated)
    {
        lock (stateLock)
        {
            if (isolateSelected)
            {
                isolateSelected = false;
            }
            else if (selectedHexes.Count > 0)
            {
                isolateSelected = true;
            }
            else
            {
                isolated = false;

                return false;
            }

            isolated = isolateSelected;
        }

        SelectionChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Obsługuje dotknięcie mapy: zaznacza najbliższy samolot w pobliżu palca. W trybie wielokrotnym dodaje go do zaznaczenia lub usuwa z niego, a w zwykłym zastępuje zaznaczenie. Dotknięcie pustego miejsca poza trybem wielokrotnym czyści zaznaczenie.
    /// </summary>
    /// <param name="x">Pozycja dotknięcia w poziomie w pikselach.</param>
    /// <param name="y">Pozycja dotknięcia w pionie w pikselach.</param>
    public void Tap(float x, float y)
    {
        IReadOnlyList<(string Hex, float X, float Y)> hits = Volatile.Read(ref screenHits);
        float maximumDistance = TapRadius * density;
        float bestDistance = maximumDistance * maximumDistance;
        string? tapped = null;

        foreach ((string Hex, float X, float Y) hit in hits)
        {
            float distance = (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                tapped = hit.Hex;
            }
        }

        lock (stateLock)
        {
            if (tapped == null)
            {
                if (multiSelect || selectedHexes.Count == 0)
                {
                    return;
                }

                selectedHexes = new HashSet<string>();
            }
            else if (multiSelect)
            {
                HashSet<string> next = new HashSet<string>(selectedHexes);

                if (next.Remove(tapped))
                {
                    if (primaryHex == tapped)
                    {
                        primaryHex = next.FirstOrDefault();
                    }
                }
                else
                {
                    next.Add(tapped);
                    primaryHex = tapped;
                }

                selectedHexes = next;
            }
            else
            {
                selectedHexes = new HashSet<string> { tapped };
                primaryHex = tapped;
            }

            if (selectedHexes.Count == 0)
            {
                isolateSelected = false;
                primaryHex = null;
            }
        }

        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Zapamiętuje pozycję użytkownika i, w trybie śledzenia, przesuwa na nią środek mapy.
    /// </summary>
    /// <param name="latitude">Szerokość geograficzna użytkownika.</param>
    /// <param name="longitude">Długość geograficzna użytkownika.</param>
    public void SetUserLocation(double latitude, double longitude)
    {
        lock (stateLock)
        {
            userLatitude = latitude;
            userLongitude = longitude;
            hasUserLocation = true;

            if (followLocation)
            {
                centerLatitude = latitude;
                centerLongitude = longitude;
            }
        }
    }

    /// <summary>
    /// Włącza tryb śledzenia użytkownika zamiast samolotu i, jeśli pozycja jest znana, centruje na niej mapę.
    /// </summary>
    public void FollowUser()
    {
        lock (stateLock)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
            followLocation = true;
            followedHex = null;

            if (hasUserLocation)
            {
                centerLatitude = userLatitude;
                centerLongitude = userLongitude;
            }
        }
    }

    /// <summary>
    /// Uruchamia aktualizacje pozycji, jeśli powierzchnia jest aktywna. Wywoływane po przyznaniu uprawnienia.
    /// </summary>
    public void StartLocationUpdates()
    {
        if (cancellation != null)
        {
            locationTracker.Start();
        }
    }

    /// <summary>
    /// Pętla rysowania, która odświeża klatkę co odstęp z ustawień.
    /// </summary>
    /// <param name="cancellationToken">Token zatrzymujący pętlę.</param>
    /// <returns>Zadanie reprezentujące pętlę.</returns>
    private async Task RenderLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long frameStart = Stopwatch.GetTimestamp();

                DrawFrame();

                double remainingMilliseconds = AppSettings.RenderIntervalMilliseconds - Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;

                await Task.Delay(Math.Max(1, (int)remainingMilliseconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    /// <summary>
    /// Pętla pobierająca samoloty w zasięgu widocznego obszaru.
    /// </summary>
    /// <param name="cancellationToken">Token zatrzymujący pętlę.</param>
    /// <returns>Zadanie reprezentujące pętlę.</returns>
    private async Task DataLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            double latitude;
            double longitude;
            double radiusNauticalMiles;
            TimeSpan delay = AppSettings.FetchInterval;

            lock (stateLock)
            {
                latitude = centerLatitude;
                longitude = centerLongitude;
                radiusNauticalMiles = CalculateRadiusNauticalMiles(centerLatitude, zoom, surfaceWidth, surfaceHeight);
            }

            try
            {
                IReadOnlyList<Aircraft> fetched = await adsbClient.GetAircraftAsync(latitude, longitude, radiusNauticalMiles, cancellationToken);
                DateTime fetchedAtUtc = DateTime.UtcNow;
                IReadOnlyDictionary<string, TrailPoint[]> previousTrails;

                lock (stateLock)
                {
                    previousTrails = latestTrails;
                }

                IReadOnlyDictionary<string, TrailPoint[]> trails = UpdateTrails(previousTrails, fetched, fetchedAtUtc);

                lock (stateLock)
                {
                    latestAircraft = fetched;
                    latestAircraftTimestampUtc = fetchedAtUtc;
                    latestTrails = trails;
                }

                LiveTraffic.Update(fetched, fetchedAtUtc, latitude, longitude);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (RateLimitedException exception)
            {
                Log.Warn(LogTag, "Pobieranie samolotów wstrzymane: " + exception.Message);
                delay = exception.RetryAfter > delay ? exception.RetryAfter : delay;
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is OperationCanceledException || exception is JsonException)
            {
                Log.Warn(LogTag, "Pobieranie samolotów nie powiodło się: " + exception.Message);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Tworzy nowe ślady, dopisując pozycje z odpowiedzi API i usuwając punkty starsze niż <see cref="AppSettings.TrailDuration"/>.
    /// </summary>
    /// <param name="previous">Dotychczasowe ślady.</param>
    /// <param name="fetched">Samoloty z najnowszej odpowiedzi.</param>
    /// <param name="nowUtc">Moment otrzymania odpowiedzi, w UTC.</param>
    /// <returns>Nowy słownik śladów.</returns>
    private static IReadOnlyDictionary<string, TrailPoint[]> UpdateTrails(IReadOnlyDictionary<string, TrailPoint[]> previous, IReadOnlyList<Aircraft> fetched, DateTime nowUtc)
    {
        DateTime cutoffUtc = nowUtc - AppSettings.TrailDuration;
        int maxTrailPoints = AppSettings.MaxTrailPoints;
        Dictionary<string, TrailPoint[]> result = new Dictionary<string, TrailPoint[]>();

        foreach (Aircraft item in fetched)
        {
            List<TrailPoint> points = new List<TrailPoint>();

            if (previous.TryGetValue(item.Hex, out TrailPoint[]? existing))
            {
                points.AddRange(existing.Where(point => point.TimestampUtc >= cutoffUtc));
            }

            bool moved = points.Count == 0
                || points[points.Count - 1].Latitude != item.Latitude
                || points[points.Count - 1].Longitude != item.Longitude;

            if (moved)
            {
                DateTime measuredAtUtc = nowUtc - TimeSpan.FromSeconds(item.SecondsSincePosition);
                points.Add(new TrailPoint(item.Latitude, item.Longitude, item.AltitudeFeet, item.OnGround, measuredAtUtc));
            }

            if (points.Count > maxTrailPoints)
            {
                points.RemoveRange(0, points.Count - maxTrailPoints);
            }

            result[item.Hex] = points.ToArray();
        }

        foreach (KeyValuePair<string, TrailPoint[]> entry in previous)
        {
            if (result.ContainsKey(entry.Key))
            {
                continue;
            }

            TrailPoint[] remaining = entry.Value.Where(point => point.TimestampUtc >= cutoffUtc).ToArray();

            if (remaining.Length > 0)
            {
                result[entry.Key] = remaining;
            }
        }

        return result;
    }

    /// <summary>
    /// Wylicza promień zapytania tak, aby pokrywał przekątną widocznego obszaru.
    /// </summary>
    /// <param name="latitude">Szerokość geograficzna środka mapy.</param>
    /// <param name="zoomLevel">Poziom przybliżenia.</param>
    /// <param name="width">Szerokość widoku w pikselach.</param>
    /// <param name="height">Wysokość widoku w pikselach.</param>
    /// <returns>Promień w milach morskich.</returns>
    private static double CalculateRadiusNauticalMiles(double latitude, double zoomLevel, int width, int height)
    {
        double metersPerPixel = 156543.03392 * Math.Cos(latitude * Math.PI / 180.0) / Math.Pow(2.0, zoomLevel);
        double halfDiagonalPixels = Math.Sqrt((double)width * width + (double)height * height) / 2.0;

        return Math.Max(10.0, halfDiagonalPixels * metersPerPixel / 1852.0);
    }

    /// <summary>
    /// Rysuje jedną klatkę na powierzchni, jeśli jest dostępna.
    /// </summary>
    private void DrawFrame()
    {
        lock (renderLock)
        {
            Surface? current = surface;

            if (current == null || !current.IsValid)
            {
                return;
            }

            try
            {
                Canvas? canvas = current.LockCanvas(null);

                if (canvas == null)
                {
                    return;
                }

                try
                {
                    DrawScene(canvas);
                }
                finally
                {
                    current.UnlockCanvasAndPost(canvas);
                }
            }
            catch (Java.Lang.Exception exception)
            {
                Log.Warn(LogTag, "Rysowanie klatki nie powiodło się: " + exception.Message);
            }
        }
    }

    /// <summary>
    /// Rysuje tło, kafelki, ślady, samoloty, atrybucję i numer wersji.
    /// </summary>
    /// <param name="canvas">Płótno powierzchni.</param>
    private void DrawScene(Canvas canvas)
    {
        double latitude;
        double longitude;
        double zoomLevel;
        int width;
        int height;
        float screenDensity;
        IReadOnlyList<Aircraft> snapshot;
        DateTime snapshotTimestampUtc;
        IReadOnlyDictionary<string, TrailPoint[]> trails;
        bool userKnown;
        double currentUserLatitude;
        double currentUserLongitude;
        string? followed;
        IReadOnlySet<string> selection;
        bool isolated;

        DateTime frameStartUtc = DateTime.UtcNow;
        double deltaSeconds = Math.Min(MaxFrameSeconds, (frameStartUtc - lastFrameUtc).TotalSeconds);
        lastFrameUtc = frameStartUtc;

        lock (stateLock)
        {
            AdvanceMotion(deltaSeconds);
            latitude = centerLatitude;
            longitude = centerLongitude;
            zoomLevel = zoom;
            width = surfaceWidth;
            height = surfaceHeight;
            screenDensity = density;
            snapshot = latestAircraft;
            snapshotTimestampUtc = latestAircraftTimestampUtc;
            trails = latestTrails;
            userKnown = hasUserLocation;
            currentUserLatitude = userLatitude;
            currentUserLongitude = userLongitude;
            followed = followedHex;
            selection = selectedHexes;
            isolated = isolateSelected;
        }

        double elapsedSeconds = (DateTime.UtcNow - snapshotTimestampUtc).TotalSeconds;
        Aircraft? followedAircraft = followed == null ? null : snapshot.FirstOrDefault(item => item.Hex == followed);

        if (followedAircraft != null)
        {
            (double Latitude, double Longitude) followedPosition = Extrapolate(followedAircraft, elapsedSeconds);
            latitude = followedPosition.Latitude;
            longitude = followedPosition.Longitude;

            lock (stateLock)
            {
                if (followedHex == followed)
                {
                    centerLatitude = latitude;
                    centerLongitude = longitude;
                }
            }
        }

        frameAltitudeStops = AppSettings.AltitudeStopsMeters;

        canvas.DrawColor(BackgroundColor);

        long frame = Interlocked.Increment(ref frameCounter);
        int tileZoom = Math.Clamp((int)Math.Round(zoomLevel), 0, MaxTileZoom);
        float tileScale = (float)Math.Pow(2.0, zoomLevel - tileZoom);
        int virtualWidth = (int)Math.Ceiling(width / tileScale);
        int virtualHeight = (int)Math.Ceiling(height / tileScale);

        List<VisibleTile> tiles = WebMercator.GetVisibleTiles(latitude, longitude, tileZoom, virtualWidth, virtualHeight);
        List<VisibleTile> prefetchTiles = WebMercator.GetVisibleTiles(latitude, longitude, tileZoom, virtualWidth + 2 * TilePrefetchPixels, virtualHeight + 2 * TilePrefetchPixels);
        List<TileKey> baseKeys = new List<TileKey>();
        HashSet<TileKey> wanted = new HashSet<TileKey>();

        foreach (VisibleTile tile in tiles)
        {
            if (tile.Key.Zoom >= BaseLayerLevels)
            {
                TileKey baseKey = new TileKey(tile.Key.Zoom - BaseLayerLevels, tile.Key.X >> BaseLayerLevels, tile.Key.Y >> BaseLayerLevels);

                if (wanted.Add(baseKey))
                {
                    baseKeys.Add(baseKey);
                }
            }
        }

        foreach (VisibleTile tile in tiles)
        {
            wanted.Add(tile.Key);
        }

        foreach (VisibleTile tile in prefetchTiles)
        {
            wanted.Add(tile.Key);
        }

        Volatile.Write(ref wantedTiles, wanted);
        Volatile.Write(ref lastVisibleTileCount, wanted.Count);

        foreach (TileKey key in wanted)
        {
            if (bitmaps.ContainsKey(key))
            {
                tileLastUsedFrame[key] = frame;
            }
        }

        canvas.Save();
        canvas.Translate(width / 2f, height / 2f);
        canvas.Scale(tileScale, tileScale);
        canvas.Translate(-virtualWidth / 2f, -virtualHeight / 2f);

        int missingTiles = 0;

        foreach (VisibleTile tile in tiles)
        {
            if (bitmaps.TryGetValue(tile.Key, out Bitmap? bitmap))
            {
                canvas.DrawBitmap(bitmap, tile.ScreenX, tile.ScreenY, tilePaint);
            }
            else
            {
                missingTiles++;
                DrawFallbackTile(canvas, tile, frame);
            }
        }

        canvas.Restore();

        int mapDim = AppSettings.MapDim;

        if (mapDim > 0)
        {
            canvas.DrawColor(Color.Argb(mapDim, 0, 0, 0));
        }

        foreach (TileKey key in baseKeys)
        {
            RequestTile(key);
        }

        foreach (VisibleTile tile in tiles)
        {
            RequestTile(tile.Key);
        }

        foreach (VisibleTile tile in prefetchTiles)
        {
            RequestTile(tile.Key);
        }

        LogTileStatistics(tiles.Count, missingTiles, zoomLevel);

        labelPaint.TextSize = AppSettings.LabelTextSize * screenDensity;
        trailPaint.StrokeWidth = AppSettings.TrailWidth * screenDensity;
        trailOutlinePaint.StrokeWidth = (AppSettings.TrailWidth + 2.0f * AppSettings.TrailOutlineWidth) * screenDensity;
        trailOutlinePaint.Color = Color.Argb(AppSettings.TrailOutlineAlpha, 0, 0, 0);
        followedPaint.StrokeWidth = 2.0f * screenDensity;
        selectedPaint.StrokeWidth = 2.5f * screenDensity;

        List<(Aircraft Item, float X, float Y)> projected = new List<(Aircraft Item, float X, float Y)>();
        List<(string Hex, float X, float Y)> hits = new List<(string Hex, float X, float Y)>();

        foreach (Aircraft item in snapshot)
        {
            if (isolated && !selection.Contains(item.Hex))
            {
                continue;
            }

            (double Latitude, double Longitude) position = Extrapolate(item, elapsedSeconds);
            (float X, float Y) screen = WebMercator.ProjectToScreen(position.Latitude, position.Longitude, latitude, longitude, zoomLevel, width, height);

            if (trails.TryGetValue(item.Hex, out TrailPoint[]? trail))
            {
                DrawTrail(canvas, trail, item, screen.X, screen.Y, latitude, longitude, zoomLevel, width, height);
            }

            projected.Add((item, screen.X, screen.Y));
        }

        foreach ((Aircraft Item, float X, float Y) entry in projected)
        {
            if (entry.X < -40f || entry.Y < -40f || entry.X > width + 40f || entry.Y > height + 40f)
            {
                continue;
            }

            DrawAircraft(canvas, entry.Item, entry.X, entry.Y, screenDensity);
            hits.Add((entry.Item.Hex, entry.X, entry.Y));

            if (entry.Item.Hex == followed)
            {
                canvas.DrawCircle(entry.X, entry.Y, 20.0f * screenDensity, followedPaint);
            }

            if (selection.Contains(entry.Item.Hex))
            {
                canvas.DrawCircle(entry.X, entry.Y, 25.0f * screenDensity, selectedPaint);
            }
        }

        Volatile.Write(ref screenHits, hits);

        if (userKnown)
        {
            (float X, float Y) userScreen = WebMercator.ProjectToScreen(currentUserLatitude, currentUserLongitude, latitude, longitude, zoomLevel, width, height);

            userPaint.Color = UserOutlineColor;
            canvas.DrawCircle(userScreen.X, userScreen.Y, 11.0f * screenDensity, userPaint);
            userPaint.Color = UserColor;
            canvas.DrawCircle(userScreen.X, userScreen.Y, 8.0f * screenDensity, userPaint);
        }

        attributionPaint.TextSize = 11.0f * screenDensity;
        canvas.DrawText(TileProvider.OpenStreetMapAttribution, width - 8f * screenDensity, height - 8f * screenDensity, attributionPaint);

        versionPaint.TextSize = 11.0f * screenDensity;
        canvas.DrawText("v" + BuildInfo.Version, 8f * screenDensity, height - 8f * screenDensity, versionPaint);
    }

    /// <summary>
    /// Rysuje sylwetkę samolotu obróconą o kurs wraz z etykietą: w pierwszej linii lot i wysokość, w drugiej typ i trasa.
    /// </summary>
    /// <param name="canvas">Płótno powierzchni.</param>
    /// <param name="item">Samolot do narysowania.</param>
    /// <param name="x">Pozycja samolotu na ekranie w poziomie.</param>
    /// <param name="y">Pozycja samolotu na ekranie w pionie.</param>
    /// <param name="screenDensity">Współczynnik gęstości ekranu.</param>
    private void DrawAircraft(Canvas canvas, Aircraft item, float x, float y, float screenDensity)
    {
        aircraftPaint.Color = GetAltitudeColor(item.OnGround, item.AltitudeFeet);

        AircraftKind kind = item.Kind;
        (AndroidPath Path, float Scale) shape = aircraftShapes.Get(kind);
        float rotation = kind == AircraftKind.Ground || kind == AircraftKind.Balloon ? 0f : (float)(item.TrackDegrees ?? 0.0);

        canvas.Save();
        canvas.Translate(x, y);
        canvas.Rotate(rotation);
        canvas.Scale(screenDensity * shape.Scale, screenDensity * shape.Scale);

        float outlineWidth = AppSettings.AircraftOutlineWidth;

        if (outlineWidth > 0.0f)
        {
            aircraftOutlinePaint.StrokeWidth = 2.0f * outlineWidth / shape.Scale;
            canvas.DrawPath(shape.Path, aircraftOutlinePaint);
        }

        canvas.DrawPath(shape.Path, aircraftPaint);
        canvas.Restore();

        if (kind == AircraftKind.Ground)
        {
            return;
        }

        string name = AircraftSearch.GetName(item);
        string label = item.AltitudeFeet.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "{0} FL{1:000}", name, Math.Round(item.AltitudeFeet.Value / 100.0))
            : name;

        float labelX = x + 14f * screenDensity;
        float labelY = y + 4f * screenDensity;

        canvas.DrawText(label, labelX, labelY, labelPaint);

        FlightRoute? route = routeCache.Get(item.Callsign);
        List<string> details = new List<string>();

        if (!string.IsNullOrEmpty(item.TypeCode))
        {
            details.Add(item.TypeCode);
        }

        if (route != null)
        {
            details.Add(route.ToShortString());
        }

        if (details.Count > 0)
        {
            canvas.DrawText(string.Join(" ", details), labelX, labelY + labelPaint.TextSize * 1.15f, labelPaint);
        }
    }

    /// <summary>
    /// Rysuje ślad samolotu jako odcinki w kolorze wysokości na ciemnej obwódce, coraz bardziej przezroczyste w stronę najstarszego punktu. Ostatni odcinek łączy ślad z bieżącą pozycją ikony.
    /// </summary>
    /// <param name="canvas">Płótno powierzchni.</param>
    /// <param name="trail">Punkty śladu od najstarszego.</param>
    /// <param name="item">Samolot, do którego należy ślad.</param>
    /// <param name="aircraftX">Bieżąca pozycja ikony na ekranie w poziomie.</param>
    /// <param name="aircraftY">Bieżąca pozycja ikony na ekranie w pionie.</param>
    /// <param name="centerLatitude">Szerokość geograficzna środka ekranu.</param>
    /// <param name="centerLongitude">Długość geograficzna środka ekranu.</param>
    /// <param name="zoomLevel">Poziom przybliżenia.</param>
    /// <param name="width">Szerokość ekranu w pikselach.</param>
    /// <param name="height">Wysokość ekranu w pikselach.</param>
    private void DrawTrail(Canvas canvas, TrailPoint[] trail, Aircraft item, float aircraftX, float aircraftY, double centerLatitude, double centerLongitude, double zoomLevel, int width, int height)
    {
        if (trail.Length == 0)
        {
            return;
        }

        int segmentCount = trail.Length;
        int minAlpha = AppSettings.TrailMinAlpha;
        int maxAlpha = AppSettings.TrailMaxAlpha;
        float[] xs = new float[segmentCount + 1];
        float[] ys = new float[segmentCount + 1];

        for (int index = 0; index < segmentCount; index++)
        {
            (float X, float Y) point = WebMercator.ProjectToScreen(trail[index].Latitude, trail[index].Longitude, centerLatitude, centerLongitude, zoomLevel, width, height);
            xs[index] = point.X;
            ys[index] = point.Y;
        }

        xs[segmentCount] = aircraftX;
        ys[segmentCount] = aircraftY;

        trailOutlinePath.Reset();
        trailOutlinePath.MoveTo(xs[0], ys[0]);

        for (int index = 1; index <= segmentCount; index++)
        {
            trailOutlinePath.LineTo(xs[index], ys[index]);
        }

        canvas.DrawPath(trailOutlinePath, trailOutlinePaint);

        for (int index = 1; index <= segmentCount; index++)
        {
            Color color = index < segmentCount
                ? GetAltitudeColor(trail[index].OnGround, trail[index].AltitudeFeet)
                : GetAltitudeColor(item.OnGround, item.AltitudeFeet);

            int alpha = minAlpha + (maxAlpha - minAlpha) * index / segmentCount;

            trailPaint.Color = Color.Argb(alpha, color.R, color.G, color.B);
            canvas.DrawLine(xs[index - 1], ys[index - 1], xs[index], ys[index], trailPaint);
        }
    }

    /// <summary>
    /// Wyznacza kolor samolotu według wysokości: szary na ziemi, a w powietrzu przejście od pomarańczowego przez żółty, zielony i niebieski do fioletowego, według progów z ustawień.
    /// </summary>
    /// <param name="onGround">Informacja, czy samolot jest na ziemi.</param>
    /// <param name="altitudeFeet">Wysokość barometryczna w stopach lub null, gdy nieznana.</param>
    /// <returns>Kolor ikony i śladu.</returns>
    private Color GetAltitudeColor(bool onGround, int? altitudeFeet)
    {
        if (onGround)
        {
            return GroundColor;
        }

        if (!altitudeFeet.HasValue)
        {
            return AltitudeStopColors[0];
        }

        double meters = altitudeFeet.Value * MetersPerFoot;
        double[] stops = frameAltitudeStops;

        if (meters <= stops[0])
        {
            return AltitudeStopColors[0];
        }

        for (int index = 1; index < stops.Length; index++)
        {
            if (meters <= stops[index])
            {
                double span = stops[index] - stops[index - 1];

                if (span <= 0.0)
                {
                    return AltitudeStopColors[index];
                }

                double fraction = (meters - stops[index - 1]) / span;
                Color from = AltitudeStopColors[index - 1];
                Color to = AltitudeStopColors[index];

                return Color.Rgb(
                    (int)Math.Round(from.R + (to.R - from.R) * fraction),
                    (int)Math.Round(from.G + (to.G - from.G) * fraction),
                    (int)Math.Round(from.B + (to.B - from.B) * fraction));
            }
        }

        return AltitudeStopColors[AltitudeStopColors.Length - 1];
    }

    /// <summary>
    /// Szacuje aktualną pozycję samolotu na podstawie kursu i prędkości od momentu ostatniego pomiaru.
    /// </summary>
    /// <param name="item">Samolot z ostatnim znanym położeniem.</param>
    /// <param name="elapsedSeconds">Czas od pobrania danych w sekundach.</param>
    /// <returns>Szacowana szerokość i długość geograficzna.</returns>
    private static (double Latitude, double Longitude) Extrapolate(Aircraft item, double elapsedSeconds)
    {
        if (item.OnGround || !item.GroundSpeedKnots.HasValue || !item.TrackDegrees.HasValue)
        {
            return (item.Latitude, item.Longitude);
        }

        double seconds = elapsedSeconds + item.SecondsSincePosition;
        double distanceNauticalMiles = item.GroundSpeedKnots.Value * seconds / 3600.0;
        double trackRadians = item.TrackDegrees.Value * Math.PI / 180.0;
        double cosineLatitude = Math.Max(0.1, Math.Cos(item.Latitude * Math.PI / 180.0));

        double latitudeDelta = distanceNauticalMiles * Math.Cos(trackRadians) / 60.0;
        double longitudeDelta = distanceNauticalMiles * Math.Sin(trackRadians) / (60.0 * cosineLatitude);

        return (item.Latitude + latitudeDelta, item.Longitude + longitudeDelta);
    }

    /// <summary>
    /// Zapisuje w logu podsumowanie stanu kafelków co kilka sekund.
    /// </summary>
    /// <param name="visibleTiles">Liczba kafelków pokrywających ekran.</param>
    /// <param name="missingTiles">Liczba kafelków, których jeszcze nie ma w pamięci.</param>
    /// <param name="zoomLevel">Bieżący poziom przybliżenia.</param>
    private void LogTileStatistics(int visibleTiles, int missingTiles, double zoomLevel)
    {
        DateTime now = DateTime.UtcNow;

        if (now - lastTileStatisticsUtc < TileStatisticsInterval)
        {
            return;
        }

        lastTileStatisticsUtc = now;

        Log.Info(LogTag, string.Format(
            CultureInfo.InvariantCulture,
            "Kafelki: zoom {0:0.00}, widoczne {1}, brakujące {2}, w pamięci {3}, w toku {4}, zdekodowane {5}, błędy {6}, ostatni {7}",
            zoomLevel,
            visibleTiles,
            missingTiles,
            bitmaps.Count,
            loadingTiles.Count,
            Interlocked.Read(ref tilesDecodedCount),
            Interlocked.Read(ref tilesFailedCount),
            lastDecodedTileInfo));
    }

    /// <summary>
    /// Rysuje w miejscu brakującego kafelka jego powiększony fragment z kafelka o niższym poziomie przybliżenia albo, gdy go nie ma, zmniejszone kafelki o wyższym poziomie. Pozostawia tło, gdy nic nie jest dostępne.
    /// </summary>
    /// <param name="canvas">Płótno powierzchni z ustawioną skalą kafelków.</param>
    /// <param name="tile">Brakujący kafelek z pozycją na ekranie.</param>
    /// <param name="frame">Numer bieżącej klatki.</param>
    private void DrawFallbackTile(Canvas canvas, VisibleTile tile, long frame)
    {
        TileKey key = tile.Key;
        int tileSize = WebMercator.TileSize;

        for (int levels = 1; levels <= MaxFallbackLevels && key.Zoom - levels >= 0; levels++)
        {
            TileKey parentKey = new TileKey(key.Zoom - levels, key.X >> levels, key.Y >> levels);

            if (!bitmaps.TryGetValue(parentKey, out Bitmap? parent))
            {
                continue;
            }

            int sourceSize = Math.Max(1, parent.Width >> levels);
            int sourceLeft = (key.X & ((1 << levels) - 1)) * sourceSize;
            int sourceTop = (key.Y & ((1 << levels) - 1)) * sourceSize;

            fallbackSource.Set(sourceLeft, sourceTop, sourceLeft + sourceSize, sourceTop + sourceSize);
            fallbackDestination.Set(tile.ScreenX, tile.ScreenY, tile.ScreenX + tileSize, tile.ScreenY + tileSize);
            canvas.DrawBitmap(parent, fallbackSource, fallbackDestination, tilePaint);
            tileLastUsedFrame[parentKey] = frame;

            return;
        }

        if (key.Zoom >= MaxTileZoom)
        {
            return;
        }

        int half = tileSize / 2;

        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                TileKey childKey = new TileKey(key.Zoom + 1, key.X * 2 + column, key.Y * 2 + row);

                if (!bitmaps.TryGetValue(childKey, out Bitmap? child))
                {
                    continue;
                }

                float left = tile.ScreenX + column * half;
                float top = tile.ScreenY + row * half;

                fallbackDestination.Set(left, top, left + half, top + half);
                canvas.DrawBitmap(child, null, fallbackDestination, tilePaint);
                tileLastUsedFrame[childKey] = frame;
            }
        }
    }

    /// <summary>
    /// Zleca pobranie i zdekodowanie kafelka, jeśli nie jest już w pamięci, w toku ani nie czeka na ponowną próbę.
    /// </summary>
    /// <param name="key">Identyfikator kafelka.</param>
    private void RequestTile(TileKey key)
    {
        if (bitmaps.ContainsKey(key))
        {
            return;
        }

        DateTime retryAfter;

        if (tileRetryAfterUtc.TryGetValue(key, out retryAfter) && retryAfter > DateTime.UtcNow)
        {
            return;
        }

        if (!loadingTiles.TryAdd(key, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await tileGate.WaitAsync();

            try
            {
                if (!Volatile.Read(ref wantedTiles).Contains(key))
                {
                    return;
                }

                byte[]? data = await tileProvider.GetTileAsync(key, CancellationToken.None);
                Bitmap? bitmap = data == null ? null : BitmapFactory.DecodeByteArray(data, 0, data.Length);

                if (bitmap != null)
                {
                    tileLastUsedFrame[key] = Interlocked.Read(ref frameCounter);
                    bitmaps[key] = bitmap;
                    tileRetryAfterUtc.TryRemove(key, out DateTime removedRetry);
                    Interlocked.Increment(ref tilesDecodedCount);
                    lastDecodedTileInfo = string.Format(CultureInfo.InvariantCulture, "{0}x{1} {2}", bitmap.Width, bitmap.Height, bitmap.GetConfig());
                    TrimBitmapCache();
                }
                else
                {
                    if (data != null)
                    {
                        Log.Warn(LogTag, string.Format(CultureInfo.InvariantCulture, "Dekodowanie kafelka {0}/{1}/{2} nie powiodło się, bajtów: {3}", key.Zoom, key.X, key.Y, data.Length));
                    }

                    Interlocked.Increment(ref tilesFailedCount);
                    tileRetryAfterUtc[key] = DateTime.UtcNow + AppSettings.TileRetry;
                }
            }
            catch (Exception exception)
            {
                Log.Warn(LogTag, string.Format(CultureInfo.InvariantCulture, "Kafelek {0}/{1}/{2}: nieoczekiwany wyjątek: {3}", key.Zoom, key.X, key.Y, exception));
                Interlocked.Increment(ref tilesFailedCount);
                tileRetryAfterUtc[key] = DateTime.UtcNow + AppSettings.TileRetry;
            }
            finally
            {
                tileGate.Release();
                loadingTiles.TryRemove(key, out byte removedMarker);
            }
        });
    }

    /// <summary>
    /// Usuwa nadmiarowe zdekodowane kafelki, gdy cache przekracza dozwolony rozmiar.
    /// </summary>
    private void TrimBitmapCache()
    {
        int limit = Math.Max(AppSettings.MaxCachedTiles, Volatile.Read(ref lastVisibleTileCount) + TileCacheMargin);
        int excess = bitmaps.Count - limit;

        if (excess <= 0)
        {
            return;
        }

        long currentFrame = Interlocked.Read(ref frameCounter);

        List<TileKey> keysToRemove = bitmaps.Keys
            .Select(key => (Key: key, LastUsed: tileLastUsedFrame.TryGetValue(key, out long usedFrame) ? usedFrame : 0L))
            .Where(entry => entry.LastUsed < currentFrame)
            .OrderBy(entry => entry.LastUsed)
            .Take(excess)
            .Select(entry => entry.Key)
            .ToList();

        foreach (TileKey key in keysToRemove)
        {
            bitmaps.TryRemove(key, out Bitmap? removedBitmap);
            tileLastUsedFrame.TryRemove(key, out long removedFrame);
        }
    }
}

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

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Holds the map state and runs the aircraft download and drawing loops on the Android Auto surface.
    /// </summary>
    public sealed class MapController
    {
        /// <summary>
        /// Tag used in Android logs.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Maximum time in milliseconds the main thread waits for the current frame to finish drawing when stopping.
        /// </summary>
        private const int StopWaitMilliseconds = 500;

        /// <summary>
        /// Highest zoom level of the OpenStreetMap tiles.
        /// </summary>
        private const int MaxTileZoom = 19;

        /// <summary>
        /// How many levels lower the base tile is always loaded; it serves as a background while the more detailed tile has not arrived yet.
        /// </summary>
        private const int BaseLayerLevels = 2;

        /// <summary>
        /// How many levels down a fallback tile is searched for.
        /// </summary>
        private const int MaxFallbackLevels = 3;

        /// <summary>
        /// Width of the band around the screen, in tile pixels, from which tiles are loaded ahead of time.
        /// </summary>
        private const int TilePrefetchPixels = 256;

        /// <summary>
        /// Maximum number of tiles downloaded at the same time.
        /// </summary>
        private const int MaxConcurrentTileDownloads = 6;

        /// <summary>
        /// Time constant of the smooth zoom approach to its target, in seconds.
        /// </summary>
        private const double ZoomEasingSeconds = 0.09;

        /// <summary>
        /// Zoom difference below which the zoom is set to the target immediately.
        /// </summary>
        private const double ZoomSnapThreshold = 0.004;

        /// <summary>
        /// Time constant of the fling movement decay, in seconds.
        /// </summary>
        private const double FlingDecaySeconds = 0.35;

        /// <summary>
        /// Velocity in pixels per second below which the fling movement ends.
        /// </summary>
        private const double FlingStopVelocity = 25.0;

        /// <summary>
        /// Longest frame time counted for movement, in seconds, so that the map does not jump after the loop was paused.
        /// </summary>
        private const double MaxFrameSeconds = 0.1;

        /// <summary>
        /// Number of spare tile slots above the visible tile count, so that the settings limit does not cause constant downloading.
        /// </summary>
        private const int TileCacheMargin = 16;

        /// <summary>
        /// Interval between tile statistics log entries.
        /// </summary>
        private static readonly TimeSpan TileStatisticsInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum age of the shared aircraft list at which a new map shows it immediately after start.
        /// </summary>
        private static readonly TimeSpan SharedSnapshotMaxAge = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Background color visible before the tiles are downloaded.
        /// </summary>
        private static readonly Color BackgroundColor = Color.Rgb(30, 34, 40);

        /// <summary>
        /// Feet to meters conversion factor.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Color of the user position marker.
        /// </summary>
        private static readonly Color UserColor = Color.Rgb(66, 133, 244);

        /// <summary>
        /// Color of the outline of the user position marker.
        /// </summary>
        private static readonly Color UserOutlineColor = Color.White;

        /// <summary>
        /// Color of the ring around a selected aircraft.
        /// </summary>
        private static readonly Color SelectedAircraftColor = Color.Rgb(255, 214, 0);

        /// <summary>
        /// Radius in dp within which a tap is assigned to an aircraft.
        /// </summary>
        private const float TapRadius = 30.0f;

        /// <summary>
        /// Time the loading indicator stays visible after the download ends, so that a short download is still noticeable.
        /// </summary>
        private static readonly TimeSpan LoadingIndicatorMinimumVisible = TimeSpan.FromMilliseconds(800);

        /// <summary>
        /// Duration of one full turn of the loading indicator spinner, in milliseconds.
        /// </summary>
        private const int SpinnerTurnMilliseconds = 900;

        /// <summary>
        /// Minimum time between rebuilds of the popup lines.
        /// </summary>
        private static readonly TimeSpan PopupRefreshInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Background color of the selected aircraft popup.
        /// </summary>
        private static readonly Color PopupBackgroundColor = Color.Argb(225, 24, 28, 34);

        /// <summary>
        /// Color of the accent lines and the progress bar of the popup.
        /// </summary>
        private static readonly Color PopupAccentColor = Color.Rgb(120, 190, 255);

        /// <summary>
        /// Text size of the popup lines in dp.
        /// </summary>
        private const float PopupTextSize = 15.0f;

        /// <summary>
        /// Popup margin from the screen edge and padding inside the popup, in dp.
        /// </summary>
        private const float PopupMargin = 12.0f;

        /// <summary>
        /// Popup distance from the bottom edge of the screen, in dp. Leaves room for the version number.
        /// </summary>
        private const float PopupBottomOffset = 30.0f;

        /// <summary>
        /// Maximum popup width in dp.
        /// </summary>
        private const float PopupMaxWidth = 380.0f;

        /// <summary>
        /// Maximum popup width as a part of the screen width.
        /// </summary>
        private const float PopupMaxWidthFraction = 0.5f;

        /// <summary>
        /// Popup line height as a multiple of the text size.
        /// </summary>
        private const float PopupLineSpacing = 1.35f;

        /// <summary>
        /// Height of the popup progress bar in dp.
        /// </summary>
        private const float PopupBarHeight = 8.0f;

        /// <summary>
        /// Color of the outline around the followed aircraft.
        /// </summary>
        private static readonly Color FollowedAircraftColor = Color.White;

        /// <summary>
        /// ADS-B API client.
        /// </summary>
        private readonly AdsbClient adsbClient;

        /// <summary>
        /// Map tile provider.
        /// </summary>
        private readonly TileProvider tileProvider;

        /// <summary>
        /// Flight route cache.
        /// </summary>
        private readonly RouteCache routeCache;

        /// <summary>
        /// Selection and filters shared with the other map.
        /// </summary>
        private readonly SelectionState selectionState;

        /// <summary>
        /// Version of the shared follow target this map published or applied last.
        /// </summary>
        private int appliedFollowVersion;

        /// <summary>
        /// Area of the screen covered by a view drawn over the map, e.g. the aircraft card on the phone, or null when nothing covers the map. Aircraft in this area cannot be tapped. The object is replaced on every change and is not modified after assignment.
        /// </summary>
        private RectF? coveredArea;

        /// <summary>
        /// Lock protecting the view state and the aircraft list.
        /// </summary>
        private readonly object stateLock = new object();

        /// <summary>
        /// Lock guaranteeing that the surface is not used after it has been destroyed.
        /// </summary>
        private readonly object renderLock = new object();

        /// <summary>
        /// Decoded tiles ready to be drawn.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, Bitmap> bitmaps = new ConcurrentDictionary<TileKey, Bitmap>();

        /// <summary>
        /// Tiles that are currently being downloaded or decoded.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, byte> loadingTiles = new ConcurrentDictionary<TileKey, byte>();

        /// <summary>
        /// Number of the frame in which a tile was last drawn or decoded. Used to remove the least recently used tiles.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, long> tileLastUsedFrame = new ConcurrentDictionary<TileKey, long>();

        /// <summary>
        /// Tiles needed in the last frame: visible ones, neighbors to load ahead of time, and the lower-level base layer. The set is replaced every frame and is not modified after publication.
        /// </summary>
        private HashSet<TileKey> wantedTiles = new HashSet<TileKey>();

        /// <summary>
        /// Limits the number of simultaneous tile downloads.
        /// </summary>
        private readonly SemaphoreSlim tileGate = new SemaphoreSlim(MaxConcurrentTileDownloads);

        /// <summary>
        /// Source region of the tile used as a fallback.
        /// </summary>
        private readonly Rect fallbackSource = new Rect();

        /// <summary>
        /// Area in which the fallback tile is drawn.
        /// </summary>
        private readonly RectF fallbackDestination = new RectF();

        /// <summary>
        /// Number of the most recently started drawing frame.
        /// </summary>
        private long frameCounter;

        /// <summary>
        /// Number of tiles covering the screen in the last frame.
        /// </summary>
        private int lastVisibleTileCount;

        /// <summary>
        /// Time after which a failed tile download can be retried.
        /// </summary>
        private readonly ConcurrentDictionary<TileKey, DateTime> tileRetryAfterUtc = new ConcurrentDictionary<TileKey, DateTime>();

        /// <summary>
        /// Number of tiles decoded since the controller was started.
        /// </summary>
        private long tilesDecodedCount;

        /// <summary>
        /// Number of failed attempts to download or decode a tile since the controller was started.
        /// </summary>
        private long tilesFailedCount;

        /// <summary>
        /// Size and format of the most recently decoded tile.
        /// </summary>
        private string lastDecodedTileInfo = "brak";

        /// <summary>
        /// Moment of the last tile statistics log entry, in UTC.
        /// </summary>
        private DateTime lastTileStatisticsUtc = DateTime.MinValue;

        /// <summary>
        /// Paint used to draw the dark outline of the aircraft icons.
        /// </summary>
        private readonly Paint aircraftOutlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the ring around the selected aircraft.
        /// </summary>
        private readonly Paint selectedPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the tiles.
        /// </summary>
        private readonly Paint tilePaint = new Paint(PaintFlags.FilterBitmap);

        /// <summary>
        /// Paint used to draw the aircraft silhouettes.
        /// </summary>
        private readonly Paint aircraftPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Draws the aircraft trails, the same way as the trail preview on the settings screen.
        /// </summary>
        private readonly TrailPainter trailPainter = new TrailPainter();

        /// <summary>
        /// Paint used to draw the aircraft labels.
        /// </summary>
        private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the OpenStreetMap attribution.
        /// </summary>
        private readonly Paint attributionPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the version number.
        /// </summary>
        private readonly Paint versionPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the user position marker.
        /// </summary>
        private readonly Paint userPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the outline around the followed aircraft.
        /// </summary>
        private readonly Paint followedPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Aircraft icon shapes by kind.
        /// </summary>
        private readonly AircraftShapes aircraftShapes = new AircraftShapes();

        /// <summary>
        /// Latitude of the map center.
        /// </summary>
        private double centerLatitude = 52.2297;

        /// <summary>
        /// Longitude of the map center.
        /// </summary>
        private double centerLongitude = 21.0122;

        /// <summary>
        /// Whether the map center follows the user position.
        /// </summary>
        private bool followLocation = true;

        /// <summary>
        /// ICAO address of the aircraft the map center follows, or null when none is followed.
        /// </summary>
        private string? followedHex;

        /// <summary>
        /// ICAO addresses of the selected aircraft. The set is replaced on every change and is not modified after assignment.
        /// </summary>
        private IReadOnlySet<string> selectedHexes = new HashSet<string>();

        /// <summary>
        /// ICAO address of the primary selected aircraft the card refers to, or null when nothing is selected.
        /// </summary>
        private string? primaryHex;

        /// <summary>
        /// Whether taps on aircraft add them to the selection.
        /// </summary>
        private bool multiSelect;

        /// <summary>
        /// Whether only the selected aircraft are drawn.
        /// </summary>
        private bool isolateSelected;

        /// <summary>
        /// Whether only the aircraft marked as military in the database are drawn.
        /// </summary>
        private bool militaryOnly;

        /// <summary>
        /// Positions of the aircraft drawn in the last frame, used to recognize taps. The list is replaced every frame.
        /// </summary>
        private IReadOnlyList<(string Hex, float X, float Y)> screenHits = Array.Empty<(string Hex, float X, float Y)>();

        /// <summary>
        /// Altitude thresholds in meters used in the current frame, read from the settings at the start of drawing.
        /// </summary>
        private double[] frameAltitudeStops = AppSettings.AltitudeStopsMeters;

        /// <summary>
        /// Whether the user position is already known.
        /// </summary>
        private bool hasUserLocation;

        /// <summary>
        /// Last known latitude of the user.
        /// </summary>
        private double userLatitude;

        /// <summary>
        /// Last known longitude of the user.
        /// </summary>
        private double userLongitude;

        /// <summary>
        /// Current, fractional zoom level visible on the screen.
        /// </summary>
        private double zoom = 8.0;

        /// <summary>
        /// Zoom level that <see cref="zoom"/> smoothly approaches.
        /// </summary>
        private double zoomTarget = 8.0;

        /// <summary>
        /// Horizontal fling velocity in pixels per second, following the GestureDetector convention.
        /// </summary>
        private double flingVelocityX;

        /// <summary>
        /// Vertical fling velocity in pixels per second, following the GestureDetector convention.
        /// </summary>
        private double flingVelocityY;

        /// <summary>
        /// Start of the previous frame, in UTC. Used for movement independent of the frame rate.
        /// </summary>
        private DateTime lastFrameUtc = DateTime.MinValue;

        /// <summary>
        /// Surface width in pixels.
        /// </summary>
        private int surfaceWidth;

        /// <summary>
        /// Surface height in pixels.
        /// </summary>
        private int surfaceHeight;

        /// <summary>
        /// Screen density factor relative to 160 dpi.
        /// </summary>
        private float density = 1.0f;

        /// <summary>
        /// Most recently downloaded aircraft list.
        /// </summary>
        private IReadOnlyList<Aircraft> latestAircraft = Array.Empty<Aircraft>();

        /// <summary>
        /// Moment the latest aircraft list was downloaded, in UTC.
        /// </summary>
        private DateTime latestAircraftTimestampUtc = DateTime.UtcNow;

        /// <summary>
        /// Aircraft trails by ICAO address, from the oldest point. The dictionary and the arrays are not modified after assignment; every update creates new ones.
        /// </summary>
        private IReadOnlyDictionary<string, TrailPoint[]> latestTrails = new Dictionary<string, TrailPoint[]>();

        /// <summary>
        /// Surface the map is drawn on. Null when it is not available.
        /// </summary>
        private Surface? surface;

        /// <summary>
        /// Cancellation token source for the download and drawing loops.
        /// </summary>
        private CancellationTokenSource? cancellation;

        /// <summary>
        /// Receiver of the user position updates.
        /// </summary>
        private readonly LocationTracker locationTracker;

        /// <summary>
        /// Paint used to draw the background of the selected aircraft popup.
        /// </summary>
        private readonly Paint popupBackgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the regular lines of the popup.
        /// </summary>
        private readonly Paint popupPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the bold lines of the popup.
        /// </summary>
        private readonly Paint popupBoldPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the accent lines of the popup and the filled part of its progress bar.
        /// </summary>
        private readonly Paint popupAccentPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Lines of the selected aircraft popup, rebuilt at most every <see cref="PopupRefreshInterval"/>. Null when no popup is shown.
        /// </summary>
        private IReadOnlyList<PopupLine>? popupLines;

        /// <summary>
        /// ICAO address of the aircraft <see cref="popupLines"/> were built for.
        /// </summary>
        private string? popupHex;

        /// <summary>
        /// Moment <see cref="popupLines"/> were built, in UTC.
        /// </summary>
        private DateTime popupBuiltUtc = DateTime.MinValue;

        /// <summary>
        /// Callsign whose route download was last prioritized for the popup.
        /// </summary>
        private string? popupPrioritizedCallsign;

        /// <summary>
        /// Paint used to draw the spinner of the loading indicator.
        /// </summary>
        private readonly Paint spinnerPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle of the loading indicator spinner, reused every frame.
        /// </summary>
        private readonly RectF spinnerBounds = new RectF();

        /// <summary>
        /// Released to start the next aircraft download before the regular interval ends.
        /// </summary>
        private readonly SemaphoreSlim fetchWake = new SemaphoreSlim(0, 1);

        /// <summary>
        /// Non-zero while the aircraft download is in progress.
        /// </summary>
        private int aircraftLoading;

        /// <summary>
        /// Moment the last aircraft download ended, in UTC ticks.
        /// </summary>
        private long aircraftLoadEndedTicks;

        /// <summary>
        /// Non-zero when the last aircraft download failed, for example because the network is unavailable.
        /// </summary>
        private int aircraftFailed;

        /// <summary>
        /// Paint used to draw the gradient bar of the altitude legend.
        /// </summary>
        private readonly Paint legendPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the dark outline of the altitude legend bar.
        /// </summary>
        private readonly Paint legendOutlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the altitude legend labels.
        /// </summary>
        private readonly Paint legendTextPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle of the altitude legend bar, reused every frame.
        /// </summary>
        private readonly RectF legendBounds = new RectF();

        /// <summary>
        /// Paint used to fill the background circle of the status icon.
        /// </summary>
        private readonly Paint statusFillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// State shown by the status icon in the bottom left corner.
        /// </summary>
        private enum StatusKind
        {
            /// <summary>
            /// Everything works.
            /// </summary>
            Ok,

            /// <summary>
            /// The last aircraft download failed.
            /// </summary>
            Offline,

            /// <summary>
            /// The server asked to wait before the next request.
            /// </summary>
            RateLimited,

            /// <summary>
            /// The aircraft list is being downloaded.
            /// </summary>
            SearchingAircraft,

            /// <summary>
            /// Details (route) of the selected aircraft are being downloaded.
            /// </summary>
            FetchingDetails,

            /// <summary>
            /// Map tiles are being downloaded.
            /// </summary>
            LoadingMap,
        }

        /// <summary>
        /// Area covered by the popup in the last frame, or null when no popup was drawn. The object is replaced every frame and is not modified after publication.
        /// </summary>
        private RectF? popupBounds;

        /// <summary>
        /// Whether the selected aircraft always get a label, even when the labels of all aircraft are hidden. Used on the car screen, which has no aircraft card.
        /// </summary>
        public bool LabelSelectedAircraft { get; set; }

        /// <summary>
        /// Whether a popup with the data of the primary selected aircraft is drawn on the map. Used on the car screen, which has no aircraft card.
        /// </summary>
        public bool ShowSelectionPopup { get; set; }

        /// <summary>
        /// Event raised after the selection, the selection mode or the isolation changes.
        /// </summary>
        public event System.Action? SelectionChanged;

        /// <summary>
        /// Creates the controller and prepares the paints and the aircraft shapes.
        /// </summary>
        /// <param name="adsbClient">ADS-B API client.</param>
        /// <param name="tileProvider">Map tile provider.</param>
        /// <param name="routeCache">Flight route cache.</param>
        /// <param name="selectionState">Selection and filters shared with the other map.</param>
        public MapController(AdsbClient adsbClient, TileProvider tileProvider, RouteCache routeCache, SelectionState selectionState)
        {
            this.adsbClient = adsbClient;
            this.tileProvider = tileProvider;
            this.routeCache = routeCache;
            this.selectionState = selectionState;
            locationTracker = new LocationTracker(SetUserLocation);

            followedPaint.SetStyle(Paint.Style.Stroke);
            followedPaint.Color = FollowedAircraftColor;

            selectedPaint.SetStyle(Paint.Style.Stroke);
            selectedPaint.Color = SelectedAircraftColor;

            aircraftOutlinePaint.SetStyle(Paint.Style.Stroke);
            aircraftOutlinePaint.StrokeJoin = Paint.Join.Round;
            aircraftOutlinePaint.Color = Color.Argb(235, 0, 0, 0);

            labelPaint.Color = Color.White;
            labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            legendTextPaint.Color = Color.White;
            legendTextPaint.TextAlign = Paint.Align.Center;
            legendTextPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            attributionPaint.Color = Color.White;
            attributionPaint.TextAlign = Paint.Align.Right;
            attributionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            versionPaint.Color = Color.White;
            versionPaint.TextAlign = Paint.Align.Left;
            versionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            popupBackgroundPaint.Color = PopupBackgroundColor;

            popupPaint.Color = Color.White;

            popupBoldPaint.Color = Color.White;
            popupBoldPaint.SetTypeface(Typeface.DefaultBold);

            popupAccentPaint.Color = PopupAccentColor;

            spinnerPaint.Color = Color.White;
            spinnerPaint.SetStyle(Paint.Style.Stroke);
            spinnerPaint.StrokeCap = Paint.Cap.Round;
            spinnerPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Raised on the drawing thread when the surface got stuck in the "already locked" state, which happens when it is destroyed while a frame is being drawn. Such a surface cannot be drawn on again, so the owner must create a new one. Drawing on the broken surface is stopped before the event is raised.
        /// </summary>
        public event System.Action? SurfaceBroken;

        /// <summary>
        /// Stores the surface and starts the data download and drawing loops as well as the position updates.
        /// </summary>
        /// <param name="newSurface">Surface provided by Android Auto.</param>
        /// <param name="width">Surface width in pixels.</param>
        /// <param name="height">Surface height in pixels.</param>
        /// <param name="dpi">Screen density in dpi.</param>
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
            LiveTraffic.FocusRequested += FocusAircraft;
            selectionState.Changed += OnSharedSelectionChanged;
            selectionState.FollowChanged += OnSharedFollowChanged;
            ApplySelection(selectionState.Current);

            FollowSnapshot sharedFollow = selectionState.CurrentFollow;

            if (sharedFollow.Version != appliedFollowVersion)
            {
                ApplyFollow(sharedFollow);
            }

            SelectionChanged?.Invoke();

            string? pendingFocus = LiveTraffic.GetPendingFocus();

            if (pendingFocus != null)
            {
                FocusAircraft(pendingFocus);
            }

            _ = Task.Run(() => RenderLoopAsync(source.Token));
            _ = Task.Run(() => DataLoopAsync(source.Token));
        }

        /// <summary>
        /// Stops the loops and the position updates and releases the surface. Returns only after a possible frame drawing has finished.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource? source = cancellation;
            cancellation = null;
            source?.Cancel();
            locationTracker.Stop();
            LiveTraffic.FocusRequested -= FocusAircraft;
            selectionState.Changed -= OnSharedSelectionChanged;
            selectionState.FollowChanged -= OnSharedFollowChanged;
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
        /// Stops drawing only if the destroyed surface is the one the map is currently drawing on. Protects against stopping the drawing on a new surface when the old one is destroyed after it was created, e.g. on screen rotation.
        /// </summary>
        /// <param name="destroyed">Surface being destroyed.</param>
        public void StopIfCurrent(Surface destroyed)
        {
            Surface? current = surface;

            if (current != null && current.Equals(destroyed))
            {
                Stop();
            }
        }

        /// <summary>
        /// Smoothly zooms in to the next whole level.
        /// </summary>
        public void ZoomIn()
        {
            lock (stateLock)
            {
                zoomTarget = Math.Min(AppSettings.MaxZoom, Math.Floor(zoomTarget + 0.01) + 1.0);
            }
        }

        /// <summary>
        /// Smoothly zooms out to the previous whole level.
        /// </summary>
        public void ZoomOut()
        {
            lock (stateLock)
            {
                zoomTarget = Math.Max(AppSettings.MinZoom, Math.Ceiling(zoomTarget - 0.01) - 1.0);
            }
        }

        /// <summary>
        /// Changes the zoom continuously according to the pinch gesture scale.
        /// </summary>
        /// <param name="scaleFactor">Scale factor relative to the previous event.</param>
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
        /// Moves the map center by the given number of pixels, stops the fling and turns off following of the user position and of the aircraft.
        /// </summary>
        /// <param name="distanceX">Horizontal movement in pixels, following the GestureDetector convention.</param>
        /// <param name="distanceY">Vertical movement in pixels, following the GestureDetector convention.</param>
        public void Pan(float distanceX, float distanceY)
        {
            bool wasFollowing;

            lock (stateLock)
            {
                wasFollowing = followLocation || followedHex != null;
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                MoveCenter(distanceX, distanceY);
            }

            if (wasFollowing)
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Whether the map center follows the user position or an aircraft.
        /// </summary>
        public bool IsFollowing
        {
            get
            {
                lock (stateLock)
                {
                    return followLocation || followedHex != null;
                }
            }
        }

        /// <summary>
        /// Starts the fling movement of the map after a fling gesture.
        /// </summary>
        /// <param name="velocityX">Horizontal finger velocity in pixels per second.</param>
        /// <param name="velocityY">Vertical finger velocity in pixels per second.</param>
        public void Fling(float velocityX, float velocityY)
        {
            bool wasFollowing;

            lock (stateLock)
            {
                wasFollowing = followLocation || followedHex != null;
                flingVelocityX = velocityX;
                flingVelocityY = velocityY;
                followLocation = false;
                followedHex = null;
            }

            if (wasFollowing)
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Stops the fling movement, e.g. after the screen is touched.
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
        /// Moves the map center by the given number of pixels and turns off following. Call under the state lock.
        /// </summary>
        /// <param name="distanceX">Horizontal movement in pixels.</param>
        /// <param name="distanceY">Vertical movement in pixels.</param>
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
        /// Advances the motion state by one frame time: brings the zoom closer to its target and moves the map with inertia. Call under the state lock.
        /// </summary>
        /// <param name="deltaSeconds">Time since the previous frame in seconds.</param>
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
        /// Makes the map center follow an aircraft and turns off following of the user position.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public void FollowAircraft(string hex)
        {
            FollowAircraft(hex, false);
        }

        /// <summary>
        /// When the aircraft is not in this map's data, e.g. because it was selected on the other screen far from this map's area, copies it from the data shared with the other map, moves the map to it and requests an immediate download around it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft, or null.</param>
        private void RevealAircraftIfMissing(string? hex)
        {
            if (hex == null)
            {
                return;
            }

            lock (stateLock)
            {
                if (latestAircraft.Any(item => item.Hex == hex))
                {
                    return;
                }
            }

            SelectionSnapshot published = selectionState.Current;
            Aircraft? found = published.PrimaryHex == hex ? published.PrimaryAircraft : null;
            DateTime foundTimestampUtc = published.PrimaryAircraftTimestampUtc;

            if (found == null)
            {
                (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) shared = LiveTraffic.GetSnapshot();

                found = shared.Aircraft.FirstOrDefault(item => item.Hex == hex);
                foundTimestampUtc = shared.TimestampUtc;
            }

            if (found == null)
            {
                return;
            }

            (double Latitude, double Longitude) position = Extrapolate(found, (DateTime.UtcNow - foundTimestampUtc).TotalSeconds);

            lock (stateLock)
            {
                if (!latestAircraft.Any(item => item.Hex == hex))
                {
                    List<Aircraft> merged = new List<Aircraft>(latestAircraft);
                    merged.Add(found);
                    latestAircraft = merged;
                }

                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                centerLatitude = position.Latitude;
                centerLongitude = position.Longitude;
                followedHex = hex;
                followLocation = false;
            }

            if (fetchWake.CurrentCount == 0)
            {
                fetchWake.Release();
            }
        }

        /// <summary>
        /// Handles the request to show an aircraft found in the search: follows it, selects it and isolates it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        private void FocusAircraft(string hex)
        {
            FollowAircraft(hex, true);
        }

        /// <summary>
        /// Publishes the selection and the filters to the other map and raises <see cref="SelectionChanged"/>.
        /// </summary>
        private void NotifySelectionChanged()
        {
            SelectionSnapshot snapshot;

            lock (stateLock)
            {
                Aircraft? primaryAircraft = primaryHex == null ? null : latestAircraft.FirstOrDefault(item => item.Hex == primaryHex);

                snapshot = new SelectionSnapshot(selectedHexes, primaryHex, isolateSelected, militaryOnly, primaryAircraft, latestAircraftTimestampUtc);
            }

            selectionState.Publish(this, snapshot);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Takes over the selection and the filters changed by the other map.
        /// </summary>
        /// <param name="source">Map that changed the state.</param>
        /// <param name="snapshot">New state.</param>
        private void OnSharedSelectionChanged(object source, SelectionSnapshot snapshot)
        {
            if (ReferenceEquals(source, this))
            {
                return;
            }

            string? previousPrimary;

            lock (stateLock)
            {
                previousPrimary = primaryHex;
            }

            ApplySelection(snapshot);

            if (snapshot.PrimaryHex != null && snapshot.PrimaryHex != previousPrimary)
            {
                RevealAircraftIfMissing(snapshot.PrimaryHex);

                lock (stateLock)
                {
                    if (latestAircraft.Any(item => item.Hex == snapshot.PrimaryHex))
                    {
                        flingVelocityX = 0.0;
                        flingVelocityY = 0.0;
                        followedHex = snapshot.PrimaryHex;
                        followLocation = false;
                    }
                }
            }

            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Replaces the selection and the filters with the given state.
        /// </summary>
        /// <param name="snapshot">State to apply.</param>
        private void ApplySelection(SelectionSnapshot snapshot)
        {
            lock (stateLock)
            {
                selectedHexes = snapshot.Hexes;
                primaryHex = snapshot.PrimaryHex;
                isolateSelected = snapshot.Isolated;
                militaryOnly = snapshot.MilitaryOnly;
            }
        }

        /// <summary>
        /// Sets the area of the screen covered by a view drawn over the map. Aircraft in this area are not selectable.
        /// </summary>
        /// <param name="area">Covered area in map pixels, or null when nothing covers the map.</param>
        public void SetCoveredArea(RectF? area)
        {
            Volatile.Write(ref coveredArea, area);
        }

        /// <summary>
        /// Checks whether a point of the map is covered by the popup or by a view drawn over the map.
        /// </summary>
        /// <param name="popup">Area of the popup, or null.</param>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <param name="y">Vertical position in pixels.</param>
        /// <returns>True when the point is covered.</returns>
        private bool IsCovered(RectF? popup, float x, float y)
        {
            RectF? covered = Volatile.Read(ref coveredArea);

            return (popup != null && popup.Contains(x, y)) || (covered != null && covered.Contains(x, y));
        }

        /// <summary>
        /// Makes the map center follow an aircraft, selects it and optionally isolates it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="isolate">Whether the aircraft is also isolated, so that only it stays visible.</param>
        public void FollowAircraft(string hex, bool isolate)
        {
            lock (stateLock)
            {
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = hex;
                followLocation = false;
                selectedHexes = new HashSet<string> { hex };
                primaryHex = hex;

                if (isolate)
                {
                    isolateSelected = true;
                }
            }

            RevealAircraftIfMissing(hex);

            NotifySelectionChanged();
            PublishFollow();
        }

        /// <summary>
        /// Makes the map center follow the primary selected aircraft without changing the selection.
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

            PublishFollow();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Publishes what this map centers on, so that the other map centers on it too.
        /// </summary>
        private void PublishFollow()
        {
            string? hex;
            bool followUser;

            lock (stateLock)
            {
                hex = followedHex;
                followUser = followLocation;
            }

            appliedFollowVersion = selectionState.PublishFollow(this, hex, followUser).Version;
        }

        /// <summary>
        /// Takes over the follow target published by the other map.
        /// </summary>
        /// <param name="source">Map that changed the follow target.</param>
        /// <param name="snapshot">New follow target.</param>
        private void OnSharedFollowChanged(object source, FollowSnapshot snapshot)
        {
            if (ReferenceEquals(source, this))
            {
                return;
            }

            ApplyFollow(snapshot);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Starts following the given target.
        /// </summary>
        /// <param name="snapshot">Follow target to apply.</param>
        private void ApplyFollow(FollowSnapshot snapshot)
        {
            lock (stateLock)
            {
                appliedFollowVersion = snapshot.Version;
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = snapshot.Hex;
                followLocation = snapshot.FollowUser;

                if (snapshot.FollowUser && hasUserLocation)
                {
                    centerLatitude = userLatitude;
                    centerLongitude = userLongitude;
                }
            }

            RevealAircraftIfMissing(snapshot.Hex);
        }

        /// <summary>
        /// Clears the aircraft selection and turns off the isolation.
        /// </summary>
        public void ClearSelection()
        {
            lock (stateLock)
            {
                selectedHexes = new HashSet<string>();
                primaryHex = null;
                isolateSelected = false;
            }

            NotifySelectionChanged();
        }

        /// <summary>
        /// Returns the data of the primary selected aircraft with its current, extrapolated position and the user position, or null when nothing is selected.
        /// </summary>
        /// <returns>Data shown on the aircraft card, or null.</returns>
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
        /// Whether taps on aircraft add them to the selection instead of replacing it.
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
        /// Whether only the selected aircraft are visible on the map.
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
        /// Number of currently selected aircraft.
        /// </summary>
        public int SelectedCount
        {
            get
            {
                lock (stateLock)
                {
                    return selectedHexes.Count;
                }
            }
        }

        /// <summary>
        /// Whether only the aircraft marked as military are visible on the map.
        /// </summary>
        public bool IsMilitaryOnly
        {
            get
            {
                lock (stateLock)
                {
                    return militaryOnly;
                }
            }
        }

        /// <summary>
        /// Turns the filter that shows only military aircraft on or off.
        /// </summary>
        /// <param name="militaryCount">Number of military aircraft in the latest data.</param>
        /// <returns>True when the filter is now on.</returns>
        public bool ToggleMilitaryOnly(out int militaryCount)
        {
            bool enabled;

            lock (stateLock)
            {
                militaryOnly = !militaryOnly;
                enabled = militaryOnly;
                militaryCount = latestAircraft.Count(item => item.IsMilitary);
            }

            NotifySelectionChanged();

            return enabled;
        }

        /// <summary>
        /// Turns the multi-select mode on or off.
        /// </summary>
        /// <returns>True when the mode is now on.</returns>
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
        /// Turns the isolation of the selected aircraft on, or off if it is on.
        /// </summary>
        /// <param name="isolated">Whether the isolation is on after the call.</param>
        /// <returns>False when the isolation cannot be turned on because nothing is selected.</returns>
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

            NotifySelectionChanged();

            return true;
        }

        /// <summary>
        /// Handles a tap on the map: selects the aircraft nearest to the finger. In the multi-select mode it adds the aircraft to the selection or removes it, and in the normal mode it replaces the selection. A tap on an empty spot clears the selection outside the multi-select mode.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        public void Tap(float x, float y)
        {
            if (IsCovered(Volatile.Read(ref popupBounds), x, y))
            {
                Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Dotknięcie {0:0},{1:0} pominięte: obszar zasłonięty", x, y));

                return;
            }

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

            Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Dotknięcie {0:0},{1:0}: samolotów do wyboru {2}, promień {3:0} px, wybrany {4}", x, y, hits.Count, maximumDistance, tapped ?? "brak"));

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

            NotifySelectionChanged();
        }

        /// <summary>
        /// Stores the user position and, in the following mode, moves the map center to it.
        /// </summary>
        /// <param name="latitude">Latitude of the user.</param>
        /// <param name="longitude">Longitude of the user.</param>
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
        /// Turns on following of the user instead of an aircraft and, if the position is known, centers the map on it.
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

            PublishFollow();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Starts the position updates if the surface is active. Called after the permission is granted.
        /// </summary>
        public void StartLocationUpdates()
        {
            if (cancellation != null)
            {
                locationTracker.Start();
            }
        }

        /// <summary>
        /// Drawing loop that refreshes the frame at the interval from the settings.
        /// </summary>
        /// <param name="cancellationToken">Token that stops the loop.</param>
        /// <returns>Task representing the loop.</returns>
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
        /// Loop that downloads the aircraft within the range of the visible area.
        /// </summary>
        /// <param name="cancellationToken">Token that stops the loop.</param>
        /// <returns>Task representing the loop.</returns>
        private async Task DataLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan rateLimitRemaining = LiveTraffic.GetRateLimitRemaining();

                if (rateLimitRemaining > TimeSpan.Zero)
                {
                    try
                    {
                        await fetchWake.WaitAsync(rateLimitRemaining, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

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

                Volatile.Write(ref aircraftLoading, 1);

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
                    Volatile.Write(ref aircraftFailed, 0);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (RateLimitedException exception)
                {
                    Log.Warn(LogTag, "Pobieranie samolotów wstrzymane: " + exception.Message);
                    delay = exception.RetryAfter > delay ? exception.RetryAfter : delay;
                    LiveTraffic.ReportRateLimited(delay);
                }
                catch (Exception exception)
                {
                    Log.Warn(LogTag, "Pobieranie samolotów nie powiodło się: " + exception.Message);
                    Volatile.Write(ref aircraftFailed, 1);
                }

                Volatile.Write(ref aircraftLoadEndedTicks, DateTime.UtcNow.Ticks);
                Volatile.Write(ref aircraftLoading, 0);

                try
                {
                    await fetchWake.WaitAsync(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Creates new trails by appending the positions from the API response and removing the points older than <see cref="AppSettings.TrailDuration"/>.
        /// </summary>
        /// <param name="previous">Existing trails.</param>
        /// <param name="fetched">Aircraft from the latest response.</param>
        /// <param name="nowUtc">Moment the response was received, in UTC.</param>
        /// <returns>New trail dictionary.</returns>
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
        /// Calculates the query radius so that it covers the diagonal of the visible area.
        /// </summary>
        /// <param name="latitude">Latitude of the map center.</param>
        /// <param name="zoomLevel">Zoom level.</param>
        /// <param name="width">View width in pixels.</param>
        /// <param name="height">View height in pixels.</param>
        /// <returns>Radius in nautical miles.</returns>
        private static double CalculateRadiusNauticalMiles(double latitude, double zoomLevel, int width, int height)
        {
            double metersPerPixel = 156543.03392 * Math.Cos(latitude * Math.PI / 180.0) / Math.Pow(2.0, zoomLevel);
            double halfDiagonalPixels = Math.Sqrt((double)width * width + (double)height * height) / 2.0;

            return Math.Max(10.0, halfDiagonalPixels * metersPerPixel / 1852.0);
        }

        /// <summary>
        /// Draws one frame on the surface if it is available.
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

                    if ((exception.Message ?? string.Empty).Contains("already locked", StringComparison.OrdinalIgnoreCase))
                    {
                        surface = null;
                        SurfaceBroken?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Draws the background, tiles, trails, aircraft, attribution and the version number.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
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
            bool onlyMilitary;

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
                onlyMilitary = militaryOnly;
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
            followedPaint.StrokeWidth = 2.0f * screenDensity;
            selectedPaint.StrokeWidth = 2.5f * screenDensity;

            List<(Aircraft Item, float X, float Y)> projected = new List<(Aircraft Item, float X, float Y)>();
            List<(string Hex, float X, float Y)> hits = new List<(string Hex, float X, float Y)>();

            IReadOnlyList<PopupLine>? popup = ShowSelectionPopup ? GetPopupLines(frameStartUtc) : null;
            RectF? popupArea = popup == null ? null : MeasurePopup(popup, width, height, screenDensity);

            foreach (Aircraft item in snapshot)
            {
                if ((isolated && !selection.Contains(item.Hex)) || (onlyMilitary && !item.IsMilitary))
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

            float iconScale = AppSettings.IconScale;

            foreach ((Aircraft Item, float X, float Y) entry in projected)
            {
                if (entry.X < -40f || entry.Y < -40f || entry.X > width + 40f || entry.Y > height + 40f)
                {
                    continue;
                }

                bool forceLabel = LabelSelectedAircraft && selection.Contains(entry.Item.Hex);

                DrawAircraft(canvas, entry.Item, entry.X, entry.Y, screenDensity, forceLabel);

                if (!IsCovered(popupArea, entry.X, entry.Y))
                {
                    hits.Add((entry.Item.Hex, entry.X, entry.Y));
                }

                if (entry.Item.Hex == followed)
                {
                    canvas.DrawCircle(entry.X, entry.Y, 20.0f * screenDensity * iconScale, followedPaint);
                }

                if (selection.Contains(entry.Item.Hex))
                {
                    canvas.DrawCircle(entry.X, entry.Y, 25.0f * screenDensity * iconScale, selectedPaint);
                }
            }

            Volatile.Write(ref screenHits, hits);
            Volatile.Write(ref popupBounds, popupArea);

            if (userKnown)
            {
                (float X, float Y) userScreen = WebMercator.ProjectToScreen(currentUserLatitude, currentUserLongitude, latitude, longitude, zoomLevel, width, height);

                userPaint.Color = UserOutlineColor;
                canvas.DrawCircle(userScreen.X, userScreen.Y, 11.0f * screenDensity, userPaint);
                userPaint.Color = UserColor;
                canvas.DrawCircle(userScreen.X, userScreen.Y, 8.0f * screenDensity, userPaint);
            }

            if (popup != null && popupArea != null)
            {
                DrawPopup(canvas, popup, popupArea, screenDensity);
            }

            if (AppSettings.ShowAltitudeLegend)
            {
                DrawAltitudeLegend(canvas, width, height, screenDensity);
            }

            attributionPaint.TextSize = 11.0f * screenDensity;
            canvas.DrawText(TileProvider.OpenStreetMapAttribution, width - 8f * screenDensity, height - 8f * screenDensity, attributionPaint);

            versionPaint.TextSize = 11.0f * screenDensity;
            string versionText = "v" + BuildInfo.Version;
            float statusDiameter = 18f * screenDensity;
            float statusLeft = 8f * screenDensity;
            float baseline = height - 8f * screenDensity;
            StatusKind status = GetStatusKind();

            DrawStatusIcon(canvas, status, statusLeft + statusDiameter / 2f, baseline - 4f * screenDensity, statusDiameter / 2f, screenDensity);

            float versionLeft = statusLeft + statusDiameter + 6f * screenDensity;
            canvas.DrawText(versionText, versionLeft, baseline, versionPaint);

            string? statusText = GetStatusText(status);

            if (statusText != null)
            {
                canvas.DrawText(statusText, versionLeft + versionPaint.MeasureText(versionText) + 12f * screenDensity, baseline, versionPaint);
            }
        }

        /// <summary>
        /// Draws the altitude legend above the bottom edge: a gradient bar with the same colors and thresholds as the aircraft icons and trails. The thresholds are spaced evenly, because the colors blend linearly between them.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        private void DrawAltitudeLegend(Canvas canvas, int width, int height, float screenDensity)
        {
            double[] stops = frameAltitudeStops;
            int stepCount = AltitudePalette.StopColors.Length;
            float barWidth = Math.Min(width - 32f * screenDensity, Math.Clamp(width * 0.4f, 240f * screenDensity, 400f * screenDensity));
            float barHeight = 7f * screenDensity;
            float left = (width - barWidth) / 2f;
            float bottom = height - 8f * screenDensity - 16f * screenDensity;
            float top = bottom - barHeight;
            float outline = screenDensity;

            int[] colors = new int[stepCount + 1];
            float[] positions = new float[stepCount + 1];

            colors[0] = AltitudePalette.StopColors[0].ToArgb();
            positions[0] = 0f;

            for (int index = 0; index < stepCount; index++)
            {
                colors[index + 1] = AltitudePalette.StopColors[index].ToArgb();
                positions[index + 1] = (index + 1f) / stepCount;
            }

            legendBounds.Set(left - outline, top - outline, left + barWidth + outline, bottom + outline);
            legendOutlinePaint.Color = Color.Argb(170, 0, 0, 0);
            canvas.DrawRoundRect(legendBounds, barHeight / 2f + outline, barHeight / 2f + outline, legendOutlinePaint);

            using LinearGradient gradient = new LinearGradient(left, 0f, left + barWidth, 0f, colors, positions, Shader.TileMode.Clamp!);

            legendPaint.SetShader(gradient);
            legendBounds.Set(left, top, left + barWidth, bottom);
            canvas.DrawRoundRect(legendBounds, barHeight / 2f, barHeight / 2f, legendPaint);
            legendPaint.SetShader(null);

            legendTextPaint.TextSize = 9f * screenDensity;

            for (int index = 0; index <= stepCount; index++)
            {
                double meters = index == 0 ? 0.0 : stops[Math.Min(index - 1, stops.Length - 1)];
                string text = AltitudeFormatter.FormatLegend(meters, index == stepCount);

                canvas.DrawText(text, left + barWidth * index / stepCount, top - 3f * screenDensity, legendTextPaint);
            }
        }

        /// <summary>
        /// Determines the state shown by the status icon. The most important problem wins: no network, request limit, then the running downloads.
        /// </summary>
        /// <returns>State of the application.</returns>
        private StatusKind GetStatusKind()
        {
            bool aircraftBusy = Volatile.Read(ref aircraftLoading) != 0
                || DateTime.UtcNow - new DateTime(Volatile.Read(ref aircraftLoadEndedTicks), DateTimeKind.Utc) < LoadingIndicatorMinimumVisible;

            if (LiveTraffic.GetRateLimitRemaining() > TimeSpan.Zero)
            {
                return StatusKind.RateLimited;
            }

            if (Volatile.Read(ref aircraftFailed) != 0)
            {
                return StatusKind.Offline;
            }

            string? callsign = null;

            lock (stateLock)
            {
                if (primaryHex != null)
                {
                    callsign = latestAircraft.FirstOrDefault(item => item.Hex == primaryHex)?.Callsign;
                }
            }

            if (callsign != null && routeCache.GetState(callsign) == RouteLookupState.Pending)
            {
                return StatusKind.FetchingDetails;
            }

            if (aircraftBusy)
            {
                return StatusKind.SearchingAircraft;
            }

            return loadingTiles.IsEmpty ? StatusKind.Ok : StatusKind.LoadingMap;
        }

        /// <summary>
        /// Returns the description shown next to the version number.
        /// </summary>
        /// <param name="status">State of the application.</param>
        /// <returns>Text, or null when the state needs no description.</returns>
        private static string? GetStatusText(StatusKind status)
        {
            switch (status)
            {
                case StatusKind.Offline:
                    return "Brak połączenia z siecią – samoloty niedostępne";
                case StatusKind.RateLimited:
                    return "Limit zapytań – ponowienie za " + Math.Ceiling(LiveTraffic.GetRateLimitRemaining().TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
                case StatusKind.SearchingAircraft:
                    return "Wyszukiwanie samolotów…";
                case StatusKind.FetchingDetails:
                    return "Pobieranie danych samolotu…";
                case StatusKind.LoadingMap:
                    return "Wczytywanie mapy…";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Draws the round status icon: a green check, a red exclamation mark, an orange pause, a radar, a magnifier or a map spinner.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="status">State to show.</param>
        /// <param name="centerX">Horizontal center of the icon in pixels.</param>
        /// <param name="centerY">Vertical center of the icon in pixels.</param>
        /// <param name="radius">Radius of the icon background in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        private void DrawStatusIcon(Canvas canvas, StatusKind status, float centerX, float centerY, float radius, float screenDensity)
        {
            switch (status)
            {
                case StatusKind.Ok:
                    statusFillPaint.Color = Color.Rgb(46, 160, 67);
                    break;
                case StatusKind.Offline:
                    statusFillPaint.Color = Color.Rgb(214, 48, 49);
                    break;
                case StatusKind.RateLimited:
                    statusFillPaint.Color = Color.Rgb(230, 126, 34);
                    break;
                default:
                    statusFillPaint.Color = Color.Rgb(41, 121, 255);
                    break;
            }

            canvas.DrawCircle(centerX, centerY, radius, statusFillPaint);

            spinnerPaint.StrokeWidth = 1.8f * screenDensity;

            float phase = (Environment.TickCount64 % SpinnerTurnMilliseconds) * 360f / SpinnerTurnMilliseconds;

            switch (status)
            {
                case StatusKind.Ok:
                    canvas.DrawLine(centerX - 0.45f * radius, centerY, centerX - 0.12f * radius, centerY + 0.38f * radius, spinnerPaint);
                    canvas.DrawLine(centerX - 0.12f * radius, centerY + 0.38f * radius, centerX + 0.5f * radius, centerY - 0.38f * radius, spinnerPaint);
                    break;
                case StatusKind.Offline:
                    canvas.DrawLine(centerX, centerY - 0.5f * radius, centerX, centerY + 0.1f * radius, spinnerPaint);
                    canvas.DrawPoint(centerX, centerY + 0.5f * radius, spinnerPaint);
                    break;
                case StatusKind.RateLimited:
                    canvas.DrawLine(centerX - 0.27f * radius, centerY - 0.4f * radius, centerX - 0.27f * radius, centerY + 0.4f * radius, spinnerPaint);
                    canvas.DrawLine(centerX + 0.27f * radius, centerY - 0.4f * radius, centerX + 0.27f * radius, centerY + 0.4f * radius, spinnerPaint);
                    break;
                case StatusKind.SearchingAircraft:
                    {
                        float sweepRadius = 0.6f * radius;
                        double angle = phase * Math.PI / 180.0;

                        canvas.DrawCircle(centerX, centerY, sweepRadius, spinnerPaint);
                        canvas.DrawLine(centerX, centerY, centerX + sweepRadius * (float)Math.Cos(angle), centerY + sweepRadius * (float)Math.Sin(angle), spinnerPaint);
                        break;
                    }

                case StatusKind.FetchingDetails:
                    canvas.DrawCircle(centerX - 0.1f * radius, centerY - 0.1f * radius, 0.36f * radius, spinnerPaint);
                    canvas.DrawLine(centerX + 0.16f * radius, centerY + 0.16f * radius, centerX + 0.55f * radius, centerY + 0.55f * radius, spinnerPaint);
                    break;
                case StatusKind.LoadingMap:
                    spinnerBounds.Set(centerX - 0.55f * radius, centerY - 0.55f * radius, centerX + 0.55f * radius, centerY + 0.55f * radius);
                    canvas.DrawArc(spinnerBounds, phase, 270f, false, spinnerPaint);
                    break;
            }
        }

        /// <summary>
        /// Style of a popup line.
        /// </summary>
        private enum PopupLineStyle
        {
            /// <summary>
            /// Regular white text.
            /// </summary>
            Normal,

            /// <summary>
            /// Bold white text.
            /// </summary>
            Bold,

            /// <summary>
            /// Accent colored text.
            /// </summary>
            Accent,

            /// <summary>
            /// Progress bar with a description below it.
            /// </summary>
            Progress,
        }

        /// <summary>
        /// One line of the selected aircraft popup.
        /// </summary>
        /// <param name="Text">Text of the line.</param>
        /// <param name="Style">Style of the line.</param>
        /// <param name="Fraction">Filled part of the progress bar from 0 to 1, used by the progress style only.</param>
        private readonly record struct PopupLine(string Text, PopupLineStyle Style, float Fraction);

        /// <summary>
        /// Returns the lines of the selected aircraft popup, rebuilding them when the selection changed or they are older than <see cref="PopupRefreshInterval"/>. Call from the drawing thread only.
        /// </summary>
        /// <param name="nowUtc">Start of the current frame, in UTC.</param>
        /// <returns>Popup lines, or null when no aircraft is selected.</returns>
        private IReadOnlyList<PopupLine>? GetPopupLines(DateTime nowUtc)
        {
            string? hex;

            lock (stateLock)
            {
                hex = primaryHex;
            }

            if (hex == null)
            {
                popupLines = null;
                popupHex = null;

                return null;
            }

            IReadOnlyList<PopupLine>? current = popupLines;

            if (current != null && popupHex == hex && nowUtc - popupBuiltUtc < PopupRefreshInterval)
            {
                return current;
            }

            SelectedAircraftInfo? info = GetPrimaryInfo();

            if (info == null)
            {
                popupLines = null;
                popupHex = null;

                return null;
            }

            popupLines = BuildPopupLines(info);
            popupHex = hex;
            popupBuiltUtc = nowUtc;

            return popupLines;
        }

        /// <summary>
        /// Builds the popup lines from the data of the primary selected aircraft.
        /// </summary>
        /// <param name="info">Data of the selected aircraft and the user position.</param>
        /// <returns>Popup lines.</returns>
        private List<PopupLine> BuildPopupLines(SelectedAircraftInfo info)
        {
            List<PopupLine> lines = new List<PopupLine>();
            Aircraft? aircraft = info.Aircraft;
            string title = AircraftCardText.BuildTitle(info);

            if (aircraft == null)
            {
                lines.Add(new PopupLine(title, PopupLineStyle.Bold, 0.0f));
                lines.Add(new PopupLine("Samolot jest poza zasięgiem danych.", PopupLineStyle.Normal, 0.0f));

                return lines;
            }

            if (!string.IsNullOrEmpty(aircraft.Registration))
            {
                title += "  ·  " + aircraft.Registration;
            }

            if (aircraft.Callsign != popupPrioritizedCallsign)
            {
                routeCache.Prioritize(aircraft.Callsign);
                popupPrioritizedCallsign = aircraft.Callsign;
            }

            FlightRoute? route = routeCache.Get(aircraft.Callsign);
            RouteLookupState routeState = routeCache.GetState(aircraft.Callsign);
            string descriptors = AircraftCardText.BuildDescriptors(aircraft, route);

            lines.Add(new PopupLine(title, PopupLineStyle.Bold, 0.0f));

            if (descriptors.Length > 0)
            {
                lines.Add(new PopupLine(descriptors, PopupLineStyle.Accent, 0.0f));
            }

            lines.Add(new PopupLine(AircraftCardText.BuildRouteText(route, routeState), route == null ? PopupLineStyle.Normal : PopupLineStyle.Bold, 0.0f));

            if (AircraftCardText.TryBuildProgress(info, aircraft, route, out float fraction, out string progress))
            {
                lines.Add(new PopupLine(progress, PopupLineStyle.Progress, fraction));
            }

            List<string> leftLines = AircraftCardText.BuildLeftLines(info, aircraft);

            for (int index = 0; index < leftLines.Count - 1; index++)
            {
                lines.Add(new PopupLine(leftLines[index], PopupLineStyle.Normal, 0.0f));
            }

            foreach (string rightLine in AircraftCardText.BuildRightLines(info, aircraft))
            {
                lines.Add(new PopupLine(rightLine, PopupLineStyle.Normal, 0.0f));
            }

            return lines;
        }

        /// <summary>
        /// Returns the height of one popup line in pixels.
        /// </summary>
        /// <param name="line">Popup line.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Line height in pixels.</returns>
        private static float GetPopupLineHeight(PopupLine line, float screenDensity)
        {
            float lineHeight = PopupTextSize * PopupLineSpacing * screenDensity;

            return line.Style == PopupLineStyle.Progress ? lineHeight + (PopupBarHeight + 4.0f) * screenDensity : lineHeight;
        }

        /// <summary>
        /// Computes the area the popup covers: the bottom left corner of the screen above the version number.
        /// </summary>
        /// <param name="lines">Popup lines.</param>
        /// <param name="width">Surface width in pixels.</param>
        /// <param name="height">Surface height in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Area of the popup in screen pixels.</returns>
        private static RectF MeasurePopup(IReadOnlyList<PopupLine> lines, int width, int height, float screenDensity)
        {
            float margin = PopupMargin * screenDensity;
            float popupWidth = Math.Min(PopupMaxWidth * screenDensity, width * PopupMaxWidthFraction);
            float popupHeight = 2.0f * margin;

            foreach (PopupLine line in lines)
            {
                popupHeight += GetPopupLineHeight(line, screenDensity);
            }

            float bottom = height - PopupBottomOffset * screenDensity;
            float top = Math.Max(margin, bottom - popupHeight);

            return new RectF(margin, top, margin + popupWidth, bottom);
        }

        /// <summary>
        /// Draws the popup with the data of the selected aircraft.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="lines">Popup lines.</param>
        /// <param name="area">Area of the popup.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        private void DrawPopup(Canvas canvas, IReadOnlyList<PopupLine> lines, RectF area, float screenDensity)
        {
            float margin = PopupMargin * screenDensity;
            float radius = 8.0f * screenDensity;
            float textWidth = area.Width() - 2.0f * margin;
            float textSize = PopupTextSize * screenDensity;
            float y = area.Top + margin;

            popupPaint.TextSize = textSize;
            popupBoldPaint.TextSize = textSize;
            popupAccentPaint.TextSize = textSize;

            canvas.DrawRoundRect(area, radius, radius, popupBackgroundPaint);

            foreach (PopupLine line in lines)
            {
                float lineHeight = GetPopupLineHeight(line, screenDensity);

                if (y + lineHeight > area.Bottom)
                {
                    break;
                }

                Paint paint = line.Style switch
                {
                    PopupLineStyle.Bold => popupBoldPaint,
                    PopupLineStyle.Accent => popupAccentPaint,
                    _ => popupPaint,
                };

                if (line.Style == PopupLineStyle.Progress)
                {
                    float barHeight = PopupBarHeight * screenDensity;
                    float barLeft = area.Left + margin;
                    float barTop = y + 2.0f * screenDensity;
                    float barRadius = barHeight / 2.0f;

                    popupPaint.Alpha = 70;
                    canvas.DrawRoundRect(barLeft, barTop, barLeft + textWidth, barTop + barHeight, barRadius, barRadius, popupPaint);
                    popupPaint.Alpha = 255;
                    canvas.DrawRoundRect(barLeft, barTop, barLeft + Math.Max(barHeight, textWidth * Math.Clamp(line.Fraction, 0.0f, 1.0f)), barTop + barHeight, barRadius, barRadius, popupAccentPaint);
                    y += (PopupBarHeight + 4.0f) * screenDensity;
                }

                canvas.DrawText(ClipText(paint, line.Text, textWidth), area.Left + margin, y + textSize, paint);
                y += PopupTextSize * PopupLineSpacing * screenDensity;
            }
        }

        /// <summary>
        /// Shortens a text with an ellipsis so that it fits in the given width.
        /// </summary>
        /// <param name="paint">Paint the text is drawn with.</param>
        /// <param name="text">Text to fit.</param>
        /// <param name="maxWidth">Available width in pixels.</param>
        /// <returns>The text, or its beginning followed by an ellipsis.</returns>
        private static string ClipText(Paint paint, string text, float maxWidth)
        {
            if (paint.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            int count = paint.BreakText(text, true, maxWidth - paint.MeasureText("…"), null);

            return text.Substring(0, Math.Max(0, count)) + "…";
        }

        /// <summary>
        /// Draws the aircraft silhouette rotated by the track together with the label: flight and altitude in the first line, type and route in the second.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="item">Aircraft to draw.</param>
        /// <param name="x">Horizontal position of the aircraft on the screen.</param>
        /// <param name="y">Vertical position of the aircraft on the screen.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <param name="forceLabel">Whether the label is drawn even when the labels are hidden in the settings.</param>
        private void DrawAircraft(Canvas canvas, Aircraft item, float x, float y, float screenDensity, bool forceLabel)
        {
            aircraftPaint.Color = GetAltitudeColor(item.OnGround, item.AltitudeFeet);

            AircraftKind kind = item.Kind;
            (AndroidPath Path, float Scale) shape = aircraftShapes.Get(kind);
            float rotation = kind == AircraftKind.Ground || kind == AircraftKind.Balloon ? 0f : (float)(item.TrackDegrees ?? 0.0);

            canvas.Save();
            canvas.Translate(x, y);
            canvas.Rotate(rotation);
            float iconScale = AppSettings.IconScale;
            canvas.Scale(screenDensity * shape.Scale * iconScale, screenDensity * shape.Scale * iconScale);

            float outlineWidth = AppSettings.AircraftOutlineWidth;

            if (outlineWidth > 0.0f)
            {
                aircraftOutlinePaint.StrokeWidth = 2.0f * outlineWidth / (shape.Scale * iconScale);
                canvas.DrawPath(shape.Path, aircraftOutlinePaint);
            }

            canvas.DrawPath(shape.Path, aircraftPaint);
            canvas.Restore();

            if (kind == AircraftKind.Ground || !(AppSettings.ShowLabels || forceLabel))
            {
                return;
            }

            string name = AircraftSearch.GetName(item);
            string label = item.AltitudeFeet.HasValue
                ? name + " " + AltitudeFormatter.Format(item.AltitudeFeet.Value)
                : name;

            float labelX = x + 14f * screenDensity * iconScale;
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
        /// Draws the aircraft trail as segments in the altitude color on a dark outline, increasingly transparent toward the oldest point. The last segment connects the trail with the current icon position.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="trail">Trail points from the oldest.</param>
        /// <param name="item">Aircraft the trail belongs to.</param>
        /// <param name="aircraftX">Current horizontal position of the icon on the screen.</param>
        /// <param name="aircraftY">Current vertical position of the icon on the screen.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoomLevel">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        private void DrawTrail(Canvas canvas, TrailPoint[] trail, Aircraft item, float aircraftX, float aircraftY, double centerLatitude, double centerLongitude, double zoomLevel, int width, int height)
        {
            if (trail.Length == 0)
            {
                return;
            }

            int segmentCount = trail.Length;
            float[] xs = new float[segmentCount + 1];
            float[] ys = new float[segmentCount + 1];
            Color[] colors = new Color[segmentCount + 1];

            for (int index = 0; index < segmentCount; index++)
            {
                (float X, float Y) point = WebMercator.ProjectToScreen(trail[index].Latitude, trail[index].Longitude, centerLatitude, centerLongitude, zoomLevel, width, height);
                xs[index] = point.X;
                ys[index] = point.Y;
                colors[index] = GetAltitudeColor(trail[index].OnGround, trail[index].AltitudeFeet);
            }

            xs[segmentCount] = aircraftX;
            ys[segmentCount] = aircraftY;
            colors[segmentCount] = GetAltitudeColor(item.OnGround, item.AltitudeFeet);

            trailPainter.Draw(
                canvas,
                xs,
                ys,
                colors,
                AppSettings.TrailWidth * density,
                AppSettings.TrailOutlineWidth * density,
                AppSettings.TrailOutlineAlpha,
                AppSettings.TrailMinAlpha,
                AppSettings.TrailMaxAlpha);
        }

        /// <summary>
        /// Determines the aircraft color by altitude: gray on the ground, and in the air a gradient from orange through yellow, green and blue to purple, according to the thresholds from the settings.
        /// </summary>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="altitudeFeet">Barometric altitude in feet, or null when unknown.</param>
        /// <returns>Color of the icon and the trail.</returns>
        private Color GetAltitudeColor(bool onGround, int? altitudeFeet)
        {
            if (onGround)
            {
                return AltitudePalette.GroundColor;
            }

            if (!altitudeFeet.HasValue)
            {
                return AltitudePalette.StopColors[0];
            }

            return AltitudePalette.GetColor(altitudeFeet.Value * MetersPerFoot, frameAltitudeStops);
        }

        /// <summary>
        /// Estimates the current aircraft position from its track and speed since the last measurement.
        /// </summary>
        /// <param name="item">Aircraft with the last known position.</param>
        /// <param name="elapsedSeconds">Time since the data was downloaded, in seconds.</param>
        /// <returns>Estimated latitude and longitude.</returns>
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
        /// Writes a tile status summary to the log every few seconds.
        /// </summary>
        /// <param name="visibleTiles">Number of tiles covering the screen.</param>
        /// <param name="missingTiles">Number of tiles not in memory yet.</param>
        /// <param name="zoomLevel">Current zoom level.</param>
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
        /// Draws in place of a missing tile its enlarged part from a tile of a lower zoom level or, when there is none, reduced tiles of a higher level. Leaves the background when nothing is available.
        /// </summary>
        /// <param name="canvas">Surface canvas with the tile scale set.</param>
        /// <param name="tile">Missing tile with its position on the screen.</param>
        /// <param name="frame">Number of the current frame.</param>
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
        /// Requests the download and decoding of a tile if it is not already in memory, in progress, or waiting for a retry.
        /// </summary>
        /// <param name="key">Tile identifier.</param>
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
        /// Removes the excess decoded tiles when the cache exceeds the allowed size.
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
}

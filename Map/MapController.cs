using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
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
    public sealed partial class MapController
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
        /// Zoom level the map moves to at least when an airport is picked in the search window, so that the airport and its runways are visible.
        /// </summary>
        private const double AirportFocusZoom = 11.0;

        /// <summary>
        /// Time the loading indicator stays visible after the download ends, so that a short download is still noticeable.
        /// </summary>
        private static readonly TimeSpan LoadingIndicatorMinimumVisible = TimeSpan.FromMilliseconds(800);

        /// <summary>
        /// Minimum time between rebuilds of the popup lines.
        /// </summary>
        private static readonly TimeSpan PopupRefreshInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Color of the outline around the followed aircraft.
        /// </summary>
        private static readonly Color FollowedAircraftColor = Color.White;

        /// <summary>
        /// Loop that downloads the aircraft and completes the data of the selected aircraft.
        /// </summary>
        private readonly AircraftDataLoop dataLoop;

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
        /// Paint used to draw the ring around the selected aircraft.
        /// </summary>
        private readonly Paint selectedPaint = new Paint(PaintFlags.AntiAlias);

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
        /// Paint used to draw the message in the middle of the map before the first data arrives.
        /// </summary>
        private readonly Paint startupPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the plate under the message in the middle of the map.
        /// </summary>
        private readonly Paint startupPlatePaint = new Paint(PaintFlags.AntiAlias);

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
        private readonly AircraftShapes aircraftShapes;

        /// <summary>
        /// Draws the aircraft icons with the shadow, outline and cockpit accent.
        /// </summary>
        private readonly AircraftIconPainter aircraftIconPainter;

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
        /// Selected airport, or null. An airport and aircraft are never selected at the same time.
        /// </summary>
        private Airport? selectedAirport;

        /// <summary>
        /// Draws the airports and their runways.
        /// </summary>
        private readonly AirportLayer airportLayer = new AirportLayer();

        /// <summary>
        /// Airports drawn in the last frame with the positions of their markers, used to recognize taps.
        /// </summary>
        private IReadOnlyList<(Airport Airport, float X, float Y)> airportHits = Array.Empty<(Airport Airport, float X, float Y)>();

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
        /// Draws the map tiles and loads the missing ones.
        /// </summary>
        private readonly TileLayer tileLayer;

        /// <summary>
        /// Measures and draws the selection popup.
        /// </summary>
        private readonly PopupRenderer popupRenderer = new PopupRenderer();

        /// <summary>
        /// Draws the altitude legend.
        /// </summary>
        private readonly AltitudeLegendRenderer altitudeLegendRenderer = new AltitudeLegendRenderer();

        /// <summary>
        /// Draws the status icon.
        /// </summary>
        private readonly StatusIconRenderer statusIconRenderer = new StatusIconRenderer();

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
        /// <param name="enricher">Completes the data of the selected aircraft from the other sources, or null when it is not used.</param>
        /// <param name="keeper">Marks the visible flights as seen and closes the landed ones in the memory of the flight information, or null when it is not used.</param>
        public MapController(IAircraftSource adsbClient, TileProvider tileProvider, RouteCache routeCache, SelectionState selectionState, AircraftEnricher? enricher = null, FlightCacheKeeper? keeper = null)
        {
            dataLoop = new AircraftDataLoop(adsbClient, enricher, keeper, GetQueryArea, GetPrimaryHex, PublishAircraft, MergeAircraftDetails);
            tileLayer = new TileLayer(tileProvider);
            this.routeCache = routeCache;
            this.selectionState = selectionState;
            locationTracker = new LocationTracker(SetUserLocation);
            aircraftShapes = new AircraftShapes();
            aircraftIconPainter = new AircraftIconPainter(aircraftShapes);

            followedPaint.SetStyle(Paint.Style.Stroke);
            followedPaint.Color = FollowedAircraftColor;

            selectedPaint.SetStyle(Paint.Style.Stroke);
            selectedPaint.Color = SelectedAircraftColor;

            labelPaint.Color = Color.White;
            labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            attributionPaint.Color = Color.White;
            attributionPaint.TextAlign = Paint.Align.Right;
            attributionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            versionPaint.Color = Color.White;
            versionPaint.TextAlign = Paint.Align.Left;
            versionPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            startupPaint.Color = Color.White;
            startupPaint.TextAlign = Paint.Align.Center;
            startupPaint.FakeBoldText = true;

            startupPlatePaint.Color = Color.Argb(220, 24, 28, 34);
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
            LiveTraffic.AirportFocusRequested += FocusAirport;
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

            string? pendingAirport = LiveTraffic.GetPendingAirportFocus();

            if (pendingAirport != null && pendingFocus == null)
            {
                FocusAirport(pendingAirport);
            }

            _ = Task.Run(() => RenderLoopAsync(source.Token));
            _ = Task.Run(() => dataLoop.RunAsync(source.Token));
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
            LiveTraffic.AirportFocusRequested -= FocusAirport;
            selectionState.Changed -= OnSharedSelectionChanged;
            selectionState.FollowChanged -= OnSharedFollowChanged;
            tileLayer.ResetWanted();

            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(renderLock, StopWaitMilliseconds, ref lockTaken);
                surface = null;

                if (!lockTaken)
                {
                    DevLogger.Warn(LogTag, "Rysowanie klatki nie zakończyło się w czasie, powierzchnia zwolniona bez czekania.");
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
    }
}

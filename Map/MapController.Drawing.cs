using AdsbCarMap.Localization;
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
    /// Render loop and scene drawing of the map controller.
    /// </summary>
    public sealed partial class MapController
    {
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
                    Canvas? canvas = UseHardwareCanvas && OperatingSystem.IsAndroidVersionAtLeast(26) ? current.LockHardwareCanvas() : current.LockCanvas(null);

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
                    DevLogger.Warn(LogTag, "Rysowanie klatki nie powiodło się: " + exception.Message);

                    if ((exception.Message ?? string.Empty).Contains("already locked", StringComparison.OrdinalIgnoreCase))
                    {
                        surface = null;
                        SurfaceBroken?.Invoke();
                    }
                }
            }

        }

        /// <summary>
        /// Finds the vertical position at which the followed aircraft or the centered airport is kept. While the framing is frozen the position found last is returned, so that a view that fills the screen does not move the map.
        /// </summary>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="height">Height of the map in pixels.</param>
        /// <param name="popupArea">Area of the popup, or null.</param>
        /// <returns>Position in pixels from the top edge.</returns>
        private double GetFramingTargetY(int width, int height, RectF? popupArea)
        {
            if (Volatile.Read(ref framingFrozen) != 0 && !double.IsNaN(lastFramingTargetY))
            {
                return lastFramingTargetY;
            }

            lastFramingTargetY = FollowFraming.GetTargetY(width, height, GetCoveringRectangles(popupArea));

            return lastFramingTargetY;
        }

        /// <summary>
        /// Lists the areas of the map that are covered by the popup drawn on the map or by a view of the phone placed over it.
        /// </summary>
        /// <param name="popupArea">Area of the popup, or null.</param>
        /// <returns>The areas as left, top, right and bottom.</returns>
        private List<(double Left, double Top, double Right, double Bottom)> GetCoveringRectangles(RectF? popupArea)
        {
            List<(double Left, double Top, double Right, double Bottom)> rectangles = new List<(double Left, double Top, double Right, double Bottom)>();
            RectF? covered = Volatile.Read(ref coveredArea);

            if (covered != null)
            {
                rectangles.Add((covered.Left, covered.Top, covered.Right, covered.Bottom));
            }

            if (popupArea != null)
            {
                rectangles.Add((popupArea.Left, popupArea.Top, popupArea.Right, popupArea.Bottom));
            }

            return rectangles;
        }

        /// <summary>
        /// Checks whether the car host or the system is in night mode.
        /// </summary>
        /// <returns>True in night mode.</returns>
        private bool IsSystemDark()
        {
            Func<bool>? provider = SystemDarkProvider;

            if (provider != null)
            {
                return provider();
            }

            Android.Content.Res.UiMode? uiMode = Android.App.Application.Context.Resources?.Configuration?.UiMode;

            return uiMode != null && (uiMode.Value & Android.Content.Res.UiMode.NightMask) == Android.Content.Res.UiMode.NightYes;
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
            string? primary;
            IReadOnlySet<string> selection;
            bool isolated;
            bool onlyMilitary;
            Airport? drawnAirport;
            Airport? centeredLocal;

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
                primary = primaryHex;
                selection = selectedHexes;
                isolated = isolateSelected;
                onlyMilitary = militaryOnly;
                drawnAirport = selectedAirport;
                centeredLocal = centeredAirport;
            }

            WakeWhenUncovered(frameStartUtc, latitude, longitude, zoomLevel, width, height);

            IReadOnlyList<PopupLine>? popup = ShowSelectionPopup ? GetPopupLines(frameStartUtc) : null;
            RectF? popupArea = popup == null ? null : popupRenderer.Measure(popup, width, height, screenDensity);

            double elapsedSeconds = (DateTime.UtcNow - snapshotTimestampUtc).TotalSeconds;
            Aircraft? followedAircraft = followed == null ? null : snapshot.FirstOrDefault(item => item.Hex == followed);

            if (followedAircraft != null)
            {
                (double Latitude, double Longitude) followedPosition = MapMath.Extrapolate(followedAircraft, elapsedSeconds);
                double targetY = GetFramingTargetY(width, height, popupArea);

                followOffsetPixels = FollowFraming.Ease(followOffsetPixels, (height / 2.0) - targetY, deltaSeconds);

                (double worldX, double worldY) = WebMercator.LatLonToWorldPixel(followedPosition.Latitude, followedPosition.Longitude, zoomLevel);
                (latitude, longitude) = WebMercator.WorldPixelToLatLon(worldX, worldY + followOffsetPixels, zoomLevel);

                lock (stateLock)
                {
                    if (followedHex == followed)
                    {
                        centerLatitude = latitude;
                        centerLongitude = longitude;
                    }
                }
            }

            if (followedAircraft == null && followed == null && centeredLocal != null && centeredLocal == drawnAirport)
            {
                double targetY = GetFramingTargetY(width, height, popupArea);
                (double airportX, double airportY) = WebMercator.LatLonToWorldPixel(centeredLocal.Latitude, centeredLocal.Longitude, zoomLevel);
                (double centerX, double centerY) = WebMercator.LatLonToWorldPixel(latitude, longitude, zoomLevel);

                (latitude, longitude) = WebMercator.WorldPixelToLatLon(
                    FollowFraming.Ease(centerX, airportX, deltaSeconds),
                    FollowFraming.Ease(centerY, airportY + (height / 2.0) - targetY, deltaSeconds),
                    zoomLevel);

                lock (stateLock)
                {
                    if (centeredAirport == centeredLocal)
                    {
                        centerLatitude = latitude;
                        centerLongitude = longitude;
                    }
                }
            }

            frameAltitudeStops = AppSettings.AltitudeStopsMeters;

            canvas.DrawColor(BackgroundColor);

            tileLayer.SetDark(MapTheme.IsDark(AppSettings.MapThemeIndex, IsSystemDark()));
            tileLayer.Draw(canvas, latitude, longitude, zoomLevel, width, height);
            int mapDim = AppSettings.MapDim;

            if (mapDim > 0)
            {
                canvas.DrawColor(Color.Argb(mapDim, 0, 0, 0));
            }

            if (AirspacesAllowed && AppSettings.ShowAirspace)
            {
                airspaceLayer.Draw(canvas, GetAirspaceViews(), latitude, longitude, zoomLevel, width, height, screenDensity);
            }
            else
            {
                ClearAirspaceViews();
            }

            Volatile.Write(ref airportHits, airportLayer.Draw(canvas, AppServices.Airports, drawnAirport, latitude, longitude, zoomLevel, width, height, screenDensity));

            labelPaint.TextSize = AppSettings.LabelTextSize * screenDensity;
            followedPaint.StrokeWidth = 2.0f * screenDensity;
            selectedPaint.StrokeWidth = 2.5f * screenDensity;

            List<(Aircraft Item, float X, float Y)> projected = new List<(Aircraft Item, float X, float Y)>();
            List<(string Hex, float X, float Y)> hits = new List<(string Hex, float X, float Y)>();

            IReadOnlyList<AircraftFilter> activeFilters = AppServices.Filters.ActiveFilters;
            bool trailsEnabled = AppSettings.TrailDuration > TimeSpan.Zero;

            foreach (Aircraft item in snapshot)
            {
                if ((isolated && !selection.Contains(item.Hex)) || (onlyMilitary && !item.IsMilitary))
                {
                    continue;
                }

                if (!selection.Contains(item.Hex) && !AircraftFilterEvaluator.Passes(item, activeFilters, routeCache.Get))
                {
                    continue;
                }

                (double Latitude, double Longitude) position = MapMath.Extrapolate(item, elapsedSeconds);
                (float X, float Y) screen = WebMercator.ProjectToScreen(position.Latitude, position.Longitude, latitude, longitude, zoomLevel, width, height);

                if (selection.Contains(item.Hex))
                {
                    TrailPoint[] trail = trails.TryGetValue(item.Hex, out TrailPoint[]? local) ? local : Array.Empty<TrailPoint>();

                    if (flightTracks != null && trailsEnabled && item.Hex == primary)
                    {
                        trail = flightTracks.Compose(item.Hex, trail);
                    }

                    DrawTrail(canvas, trail, item, screen.X, screen.Y, latitude, longitude, zoomLevel, width, height);
                }

                projected.Add((item, screen.X, screen.Y));
            }

            Volatile.Write(ref visibleAircraftCount, projected.Count);
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

            if (ThreatsAllowed && AppSettings.ShowThreats)
            {
                Volatile.Write(ref threatHits, threatLayer.Draw(canvas, AppServices.Threats.Threats, latitude, longitude, zoomLevel, width, height, screenDensity));
            }
            else if (threatHits.Count > 0)
            {
                Volatile.Write(ref threatHits, Array.Empty<(Threat Threat, float X, float Y)>());
            }

            if (userKnown)
            {
                (float X, float Y) userScreen = WebMercator.ProjectToScreen(currentUserLatitude, currentUserLongitude, latitude, longitude, zoomLevel, width, height);

                userPaint.Color = UserOutlineColor;
                canvas.DrawCircle(userScreen.X, userScreen.Y, 11.0f * screenDensity, userPaint);
                userPaint.Color = UserColor;
                canvas.DrawCircle(userScreen.X, userScreen.Y, 8.0f * screenDensity, userPaint);
            }

            (Aircraft Item, float X, float Y)? pointerTarget = null;

            if (primary != null)
            {
                foreach ((Aircraft Item, float X, float Y) entry in projected)
                {
                    if (entry.Item.Hex == primary)
                    {
                        pointerTarget = entry;
                    }
                }
            }

            DrawOffscreenPointer(canvas, pointerTarget, latitude, longitude, width, height, screenDensity, GetCoveringRectangles(popupArea));

            if (popup != null && popupArea != null)
            {
                popupRenderer.Draw(canvas, popup, popupArea, screenDensity);
            }

            if (AppSettings.ShowAltitudeLegend)
            {
                altitudeLegendRenderer.Draw(canvas, width, height, screenDensity, frameAltitudeStops);
            }

            if (dataLoop.LoadEndedUtc.Ticks == 0 && snapshot.Count == 0)
            {
                string startupText = Loc.T("Wczytywanie…");
                startupPaint.TextSize = 18.0f * screenDensity;
                float startupHalfWidth = startupPaint.MeasureText(startupText) / 2f + 20f * screenDensity;
                float startupHalfHeight = 24f * screenDensity;
                float startupBaseline = height / 2f - (startupPaint.Ascent() + startupPaint.Descent()) / 2f;

                canvas.DrawRoundRect(width / 2f - startupHalfWidth, height / 2f - startupHalfHeight, width / 2f + startupHalfWidth, height / 2f + startupHalfHeight, startupHalfHeight, startupHalfHeight, startupPlatePaint);
                canvas.DrawText(startupText, width / 2f, startupBaseline, startupPaint);
            }

            attributionPaint.TextSize = 11.0f * screenDensity;
            canvas.DrawText(TileProvider.OpenStreetMapAttribution, width - 8f * screenDensity, height - 8f * screenDensity, attributionPaint);

            versionPaint.TextSize = 11.0f * screenDensity;
            string versionText = (BuildInfo.IsDev ? "DEV " : string.Empty) + "v" + BuildInfo.Version + " (" + BuildInfo.Build + ")";
            float statusDiameter = 18f * screenDensity;
            float statusLeft = 8f * screenDensity;
            float baseline = height - 8f * screenDensity;
            StatusKind status = GetStatusKind();

            statusIconRenderer.Draw(canvas, status, statusLeft + statusDiameter / 2f, baseline - 4f * screenDensity, statusDiameter / 2f, screenDensity);

            float versionLeft = statusLeft + statusDiameter + 6f * screenDensity;
            canvas.DrawText(versionText, versionLeft, baseline, versionPaint);

            string? statusText = GetStatusText(status);

            if (statusText != null)
            {
                canvas.DrawText(statusText, versionLeft + versionPaint.MeasureText(versionText) + 12f * screenDensity, baseline, versionPaint);
            }
        }

        /// <summary>
        /// Determines the state shown by the status icon. The most important problem wins: no network, request limit, then the running downloads.
        /// </summary>
        /// <returns>State of the application.</returns>
        private StatusKind GetStatusKind()
        {
            bool aircraftBusy = dataLoop.IsLoading
                || DateTime.UtcNow - dataLoop.LoadEndedUtc < LoadingIndicatorMinimumVisible;

            if (dataLoop.RateLimitRemaining > TimeSpan.Zero)
            {
                return StatusKind.RateLimited;
            }

            if (dataLoop.HasFailed)
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

            return tileLayer.IsLoading ? StatusKind.LoadingMap : StatusKind.Ok;
        }

        /// <summary>
        /// Returns the description shown next to the version number.
        /// </summary>
        /// <param name="status">State of the application.</param>
        /// <returns>Text, or null when the state needs no description.</returns>
        private string? GetStatusText(StatusKind status)
        {
            switch (status)
            {
                case StatusKind.Offline:
                    return Loc.T("Brak połączenia z siecią – samoloty niedostępne");
                case StatusKind.RateLimited:
                    return Loc.T("Limit zapytań – ponowienie za ") + Math.Ceiling(dataLoop.RateLimitRemaining.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
                case StatusKind.SearchingAircraft:
                    return Loc.T("Wyszukiwanie samolotów…");
                case StatusKind.FetchingDetails:
                    return Loc.T("Pobieranie danych samolotu…");
                case StatusKind.LoadingMap:
                    return Loc.T("Wczytywanie mapy…");
                default:
                    return null;
            }
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
            AircraftKind kind = item.GetKind();
            float rotation = kind == AircraftKind.Ground || kind == AircraftKind.Balloon ? 0f : (float)(item.TrackDegrees ?? 0.0);
            float iconScale = AppSettings.IconScale;

            aircraftSpriteCache.Draw(canvas, kind, x, y, rotation, GetAltitudeColor(item.OnGround, item.AltitudeFeet), screenDensity, iconScale, AppSettings.AircraftOutlineWidth, AppSettings.AircraftShadowStrength, AppSettings.AircraftCockpitStrength);

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
    }
}

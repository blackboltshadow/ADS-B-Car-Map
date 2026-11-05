using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the airports and, at high zoom, their runways on the map. Shared by the phone and Android Auto maps through <see cref="MapController"/>. Larger airports are drawn first and keep their code labels when labels would overlap.
    /// </summary>
    public sealed class AirportLayer
    {
        /// <summary>
        /// Zoom level from which favorite airports are drawn, whatever their size.
        /// </summary>
        private const double FavoriteFromZoom = 5.0;

        /// <summary>
        /// Zoom level from which runways are drawn.
        /// </summary>
        private const double RunwaysFromZoom = 11.0;

        /// <summary>
        /// Maximum number of airports drawn in one frame.
        /// </summary>
        private const int MaxMarkers = 220;

        /// <summary>
        /// Maximum number of small airports drawn in one frame. The ones closest to the center of the screen are kept, so the map stays readable where there are many airfields.
        /// </summary>
        private const int MaxSmallMarkers = 60;

        /// <summary>
        /// Maximum number of heliports drawn in one frame, the ones closest to the center of the screen.
        /// </summary>
        private const int MaxHeliportMarkers = 40;

        /// <summary>
        /// Part of the screen size added on every side when looking for airports, so that markers do not appear at the edge.
        /// </summary>
        private const double ViewMargin = 0.1;

        /// <summary>
        /// Color of a civil airport.
        /// </summary>
        internal static readonly Color CivilColor = Color.Rgb(245, 245, 245);

        /// <summary>
        /// Color of a military airport.
        /// </summary>
        internal static readonly Color MilitaryColor = Color.Rgb(235, 80, 80);

        /// <summary>
        /// Color of a heliport.
        /// </summary>
        internal static readonly Color HeliportColor = Color.Rgb(110, 180, 255);

        /// <summary>
        /// Color of the runway that the wind favors.
        /// </summary>
        private static readonly Color RunwayInUseColor = Color.Rgb(90, 225, 130);

        /// <summary>
        /// Number of zoom levels above the one from which the weather is shown at which the wind arrow appears.
        /// </summary>
        private const double WindArrowZoomOffset = 2.0;

        /// <summary>
        /// Number of airports above which the memory of the favored runways is emptied.
        /// </summary>
        private const int MaxRunwayMemory = 600;

        /// <summary>
        /// Color of the ring around the selected airport.
        /// </summary>
        private static readonly Color SelectedColor = Color.Rgb(255, 215, 0);

        /// <summary>
        /// Zoom level of the frame being drawn, read by <see cref="isShownFilter"/>.
        /// </summary>
        private double frameZoom;

        /// <summary>
        /// Filter passed to the airport query, created once so that no delegate is allocated in every frame.
        /// </summary>
        private readonly Predicate<Airport> isShownFilter;

        /// <summary>
        /// Airports found in the visible part of the map, reused between frames.
        /// </summary>
        private readonly List<Airport> visible = new List<Airport>();

        /// <summary>
        /// Favorite airports found in the visible part of the map in the current frame, reused between frames.
        /// </summary>
        private readonly HashSet<Airport> favorites = new HashSet<Airport>();

        /// <summary>
        /// Rectangles of the labels already placed in the frame, reused between frames.
        /// </summary>
        private readonly List<RectF> placedLabels = new List<RectF>();

        /// <summary>
        /// Paint of the ring in the color of the flight category.
        /// </summary>
        private readonly Paint weatherRingPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Clockwise rotation of the map in the frame being drawn, in degrees.
        /// </summary>
        private double frameRotation;

        /// <summary>
        /// Paint of the wind arrow.
        /// </summary>
        private readonly Paint windPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark outline of the wind arrow.
        /// </summary>
        private readonly Paint windOutlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the runway that the wind favors.
        /// </summary>
        private readonly Paint runwayInUsePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the designator at the end of the runway that the wind favors.
        /// </summary>
        private readonly Paint runwayEndPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Runway that the wind favors by the ICAO code of the airport, kept with the report it was computed from, so that it is computed again only when the report changes.
        /// </summary>
        private readonly Dictionary<string, (Metar Report, RunwayWindResult? Result)> runwayInUse = new Dictionary<string, (Metar Report, RunwayWindResult? Result)>();

        /// <summary>
        /// Paint of the airport marker fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark marker outline.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the runways.
        /// </summary>
        private readonly Paint runwayPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark runway outline.
        /// </summary>
        private readonly Paint runwayOutlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the labels.
        /// </summary>
        private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the ring around the selected airport.
        /// </summary>
        private readonly Paint selectedPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Creates the layer.
        /// </summary>
        public AirportLayer()
        {
            isShownFilter = IsShownInFrame;
            fillPaint.SetStyle(Paint.Style.Fill);
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.Color = Color.Argb(220, 0, 0, 0);
            runwayPaint.SetStyle(Paint.Style.Stroke);
            runwayPaint.Color = Color.Argb(235, 215, 215, 215);
            runwayOutlinePaint.SetStyle(Paint.Style.Stroke);
            runwayOutlinePaint.Color = Color.Argb(200, 0, 0, 0);
            labelPaint.Color = Color.White;
            labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
            selectedPaint.SetStyle(Paint.Style.Stroke);
            selectedPaint.Color = SelectedColor;
            weatherRingPaint.SetStyle(Paint.Style.Stroke);
            windPaint.SetStyle(Paint.Style.Stroke);
            windPaint.StrokeCap = Paint.Cap.Round;
            windPaint.Color = Color.White;
            windOutlinePaint.SetStyle(Paint.Style.Stroke);
            windOutlinePaint.StrokeCap = Paint.Cap.Round;
            windOutlinePaint.Color = Color.Argb(220, 0, 0, 0);
            runwayInUsePaint.SetStyle(Paint.Style.Stroke);
            runwayInUsePaint.Color = RunwayInUseColor;
            runwayEndPaint.Color = RunwayInUseColor;
            runwayEndPaint.TextAlign = Paint.Align.Center;
            runwayEndPaint.SetTypeface(Typeface.DefaultBold);
            runwayEndPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Draws the airports for the current view and returns where the markers are, so that they can be tapped.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="database">Airport data.</param>
        /// <param name="selected">Selected airport, drawn even outside the zoom range, or null.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        /// <param name="rotationDegrees">Clockwise rotation of the map in degrees.</param>
        /// <returns>Airports drawn in the frame with the positions of their markers on the screen.</returns>
        public List<(Airport Airport, float X, float Y)> Draw(Canvas canvas, AirportDatabase database, Airport? selected, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density, double rotationDegrees)
        {
            frameRotation = rotationDegrees;
            List<(Airport Airport, float X, float Y)> hits = new List<(Airport Airport, float X, float Y)>();

            visible.Clear();
            favorites.Clear();
            placedLabels.Clear();

            if (!AppSettings.ShowAirports || !database.IsLoaded)
            {
                return hits;
            }

            frameZoom = zoom;

            bool onlyFavorites = AppSettings.ShowOnlyFavoriteAirports;

            if (onlyFavorites || zoom >= Math.Min(GetFromZoom(), FavoriteFromZoom))
            {
                (double X, double Y) center = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
                double halfWidth = width / 2.0 * (1.0 + ViewMargin);
                double halfHeight = height / 2.0 * (1.0 + ViewMargin);

                if (frameRotation != 0.0)
                {
                    halfWidth = MapRotation.GetCoveringSize(width, height) / 2.0 * (1.0 + ViewMargin);
                    halfHeight = halfWidth;
                }

                (double Latitude, double Longitude) topLeft = WebMercator.WorldPixelToLatLon(center.X - halfWidth, center.Y - halfHeight, zoom);
                (double Latitude, double Longitude) bottomRight = WebMercator.WorldPixelToLatLon(center.X + halfWidth, center.Y + halfHeight, zoom);

                if (!onlyFavorites)
                {
                    database.Query(bottomRight.Latitude, topLeft.Latitude, topLeft.Longitude, bottomRight.Longitude, isShownFilter, visible);
                }

                if (onlyFavorites || zoom >= FavoriteFromZoom)
                {
                    foreach (string code in AppServices.Favorites.Codes)
                    {
                        Airport? favorite = database.FindByIcao(code);

                        if (favorite != null
                            && favorite.Latitude >= bottomRight.Latitude && favorite.Latitude <= topLeft.Latitude
                            && favorite.Longitude >= topLeft.Longitude && favorite.Longitude <= bottomRight.Longitude)
                        {
                            favorites.Add(favorite);

                            if (!visible.Contains(favorite))
                            {
                                visible.Add(favorite);
                            }
                        }
                    }
                }
            }

            if (selected != null && !visible.Contains(selected))
            {
                visible.Add(selected);
            }

            visible.Sort((first, second) => CompareByImportance(first, second, centerLatitude, centerLongitude));

            int smallCount = 0;
            int heliportCount = 0;
            int kept = 0;

            for (int index = 0; index < visible.Count; index++)
            {
                Airport candidate = visible[index];

                bool candidateFavorite = favorites.Contains(candidate);

                if (!candidateFavorite && candidate.Type == AirportType.Small && ++smallCount > MaxSmallMarkers)
                {
                    continue;
                }

                if (!candidateFavorite && candidate.Type == AirportType.Heliport && ++heliportCount > MaxHeliportMarkers)
                {
                    continue;
                }

                visible[kept++] = candidate;
            }

            visible.RemoveRange(kept, visible.Count - kept);

            if (selected != null && !visible.Contains(selected))
            {
                visible.Add(selected);
            }

            if (visible.Count > MaxMarkers)
            {
                visible.RemoveRange(MaxMarkers, visible.Count - MaxMarkers);

                if (selected != null && !visible.Contains(selected))
                {
                    visible.Add(selected);
                }
            }

            bool mapWeather = AppSettings.ShowAirportWeather && AppSettings.ShowAirportMapWeather && zoom >= AppSettings.AirportMapWeatherFromZoom;

            if (mapWeather)
            {
                AirportMapWeather.Request(visible, zoom);
            }

            if (zoom >= RunwaysFromZoom)
            {
                DrawRunways(canvas, database, centerLatitude, centerLongitude, zoom, width, height, density, mapWeather && AppSettings.ShowRunwayInUse);
            }

            float iconScale = AppSettings.IconScale;
            bool labels = AppSettings.ShowAirportLabels;

            labelPaint.TextSize = AppSettings.AirportLabelSize * density;

            foreach (Airport airport in visible)
            {
                (float X, float Y) screen = WebMercator.ProjectToScreen(airport.Latitude, airport.Longitude, centerLatitude, centerLongitude, zoom, width, height, frameRotation);

                if (screen.X < -40f || screen.Y < -40f || screen.X > width + 40f || screen.Y > height + 40f)
                {
                    continue;
                }

                float radius = GetRadiusDp(airport.Type) * density * Math.Max(0.8f, iconScale);

                outlinePaint.StrokeWidth = 1.5f * density;
                fillPaint.Color = GetMarkerColor(airport);
                canvas.DrawCircle(screen.X, screen.Y, radius, fillPaint);
                canvas.DrawCircle(screen.X, screen.Y, radius, outlinePaint);

                if (mapWeather)
                {
                    DrawWeather(canvas, airport, screen.X, screen.Y, radius, density, zoom);
                }

                bool isFavorite = favorites.Contains(airport);

                if (isFavorite)
                {
                    selectedPaint.StrokeWidth = 2f * density;
                    canvas.DrawCircle(screen.X, screen.Y, radius + 3.5f * density, selectedPaint);
                }

                if (airport == selected)
                {
                    selectedPaint.StrokeWidth = 2.5f * density;
                    canvas.DrawCircle(screen.X, screen.Y, radius + 8f * density, selectedPaint);
                }

                if (labels || airport == selected || isFavorite)
                {
                    DrawLabel(canvas, airport.Code, screen.X + radius + 3f * density, screen.Y, density, airport == selected || isFavorite);
                }

                hits.Add((airport, screen.X, screen.Y));
            }

            return hits;
        }

        /// <summary>
        /// Tells whether an airport is drawn in the current frame: its category is switched on in the settings and the zoom is high enough for its size class.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>True when the airport is drawn.</returns>
        private bool IsShownInFrame(Airport airport)
        {
            if (frameZoom < GetFromZoom() || frameZoom > AppSettings.AirportToZoom + 0.999 || !AppSettings.IsAirportCategoryEnabled(airport))
            {
                return false;
            }

            return airport.IsMilitary || airport.Type != AirportType.Small || !AppSettings.SmallAirportsPavedOnly || airport.HasLongPavedRunway;
        }

        /// <summary>
        /// Returns the zoom level from which airports are drawn: the lower end of the range from the settings, never above the highest zoom the map allows or the upper end of the range. The range is the same for all airport categories.
        /// </summary>
        /// <returns>Zoom level.</returns>
        internal static double GetFromZoom()
        {
            return Math.Min(Math.Min(AppSettings.AirportFromZoom, AppSettings.MaxZoom), AppSettings.AirportToZoom);
        }

        /// <summary>
        /// Returns the color of an airport marker: red for military airports, light blue for heliports and white for the others.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Marker color.</returns>
        internal static Color GetMarkerColor(Airport airport)
        {
            if (airport.IsMilitary)
            {
                return MilitaryColor;
            }

            return airport.Type == AirportType.Heliport ? HeliportColor : CivilColor;
        }

        /// <summary>
        /// Orders airports from the most to the least important: by size class, then those with scheduled service first, then those closer to the center of the screen.
        /// </summary>
        /// <param name="first">First airport.</param>
        /// <param name="second">Second airport.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <returns>Comparison result.</returns>
        private int CompareByImportance(Airport first, Airport second, double centerLatitude, double centerLongitude)
        {
            bool firstFavorite = favorites.Contains(first);
            bool secondFavorite = favorites.Contains(second);

            if (firstFavorite != secondFavorite)
            {
                return firstFavorite ? -1 : 1;
            }

            int byType = first.Type.CompareTo(second.Type);

            if (byType != 0)
            {
                return byType;
            }

            int byService = second.HasScheduledService.CompareTo(first.HasScheduledService);

            if (byService != 0)
            {
                return byService;
            }

            return GetSquaredDistance(first, centerLatitude, centerLongitude).CompareTo(GetSquaredDistance(second, centerLatitude, centerLongitude));
        }

        /// <summary>
        /// Returns a value that grows with the distance of an airport from a point, good enough to compare airports on one screen.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="latitude">Latitude of the point.</param>
        /// <param name="longitude">Longitude of the point.</param>
        /// <returns>Squared distance in degrees, with the longitude scaled by the latitude.</returns>
        private static double GetSquaredDistance(Airport airport, double latitude, double longitude)
        {
            double dx = (airport.Longitude - longitude) * Math.Cos(latitude * Math.PI / 180.0);
            double dy = airport.Latitude - latitude;

            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Returns the radius of an airport marker in dp.
        /// </summary>
        /// <param name="type">Size class of the airport.</param>
        /// <returns>Radius in dp.</returns>
        internal static float GetRadiusDp(AirportType type)
        {
            switch (type)
            {
                case AirportType.Large:
                    return 6.5f;
                case AirportType.Medium:
                    return 5f;
                case AirportType.Heliport:
                    return 3f;
                default:
                    return 3.5f;
            }
        }

        /// <summary>
        /// Draws a label unless it would overlap a label that is already placed.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="text">Text of the label.</param>
        /// <param name="x">Left edge of the text in pixels.</param>
        /// <param name="centerY">Vertical center of the marker in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        /// <param name="force">Whether to draw the label even when it overlaps another one.</param>
        private void DrawLabel(Canvas canvas, string text, float x, float centerY, float density, bool force)
        {
            float textWidth = labelPaint.MeasureText(text);
            float textHeight = labelPaint.TextSize;
            float padding = 2f * density;
            RectF bounds = new RectF(x - padding, centerY - textHeight / 2f - padding, x + textWidth + padding, centerY + textHeight / 2f + padding);

            if (!force)
            {
                foreach (RectF placed in placedLabels)
                {
                    if (RectF.Intersects(placed, bounds))
                    {
                        return;
                    }
                }
            }

            placedLabels.Add(bounds);
            canvas.DrawText(text, x, centerY + textHeight * 0.35f, labelPaint);
        }

        /// <summary>
        /// Draws the runways of the visible airports.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="database">Airport data.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawRunways(Canvas canvas, AirportDatabase database, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density, bool highlightRunwayInUse)
        {
            double metersPerPixel = 156543.03392 * Math.Cos(centerLatitude * Math.PI / 180.0) / Math.Pow(2.0, zoom);

            foreach (Airport airport in visible)
            {
                IReadOnlyList<Runway> airportRunways = database.GetRunways(airport);
                RunwayWindResult? favored = highlightRunwayInUse ? GetRunwayInUse(airport, airportRunways) : null;

                foreach (Runway runway in airportRunways)
                {
                    (float X, float Y) low = WebMercator.ProjectToScreen(runway.LowEndLatitude, runway.LowEndLongitude, centerLatitude, centerLongitude, zoom, width, height, frameRotation);
                    (float X, float Y) high = WebMercator.ProjectToScreen(runway.HighEndLatitude, runway.HighEndLongitude, centerLatitude, centerLongitude, zoom, width, height, frameRotation);

                    if ((low.X < -50f && high.X < -50f) || (low.Y < -50f && high.Y < -50f) || (low.X > width + 50f && high.X > width + 50f) || (low.Y > height + 50f && high.Y > height + 50f))
                    {
                        continue;
                    }

                    double widthMeters = (runway.WidthFeet > 0 ? runway.WidthFeet : 100) * 0.3048;
                    float stroke = (float)Math.Clamp(widthMeters / metersPerPixel, 1.5 * density, 14.0 * density);

                    runwayOutlinePaint.StrokeWidth = stroke + 2f * density;
                    runwayPaint.StrokeWidth = stroke;
                    canvas.DrawLine(low.X, low.Y, high.X, high.Y, runwayOutlinePaint);

                    if (favored != null && ReferenceEquals(favored.Runway, runway))
                    {
                        runwayInUsePaint.StrokeWidth = stroke;
                        canvas.DrawLine(low.X, low.Y, high.X, high.Y, runwayInUsePaint);
                        DrawRunwayEnd(canvas, favored.IsHighEnd ? high : low, favored.IsHighEnd ? low : high, favored.EndName, density);
                    }
                    else
                    {
                        canvas.DrawLine(low.X, low.Y, high.X, high.Y, runwayPaint);
                    }
                }
            }
        }

        /// <summary>
        /// Writes the designator of the favored runway end just beyond that end of the runway.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="end">Screen position of the favored end.</param>
        /// <param name="other">Screen position of the other end.</param>
        /// <param name="name">Designator of the favored end.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawRunwayEnd(Canvas canvas, (float X, float Y) end, (float X, float Y) other, string name, float density)
        {
            float dx = end.X - other.X;
            float dy = end.Y - other.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length < 1f)
            {
                return;
            }

            runwayEndPaint.TextSize = 11f * density;
            canvas.DrawText(name, end.X + dx / length * 12f * density, end.Y + dy / length * 12f * density + 4f * density, runwayEndPaint);
        }

        /// <summary>
        /// Returns the runway end that the wind of the report of an airport favors, computed again only when the report changes.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <returns>The favored end, or null when the airport has no report of its own that is current, or the wind favors no runway.</returns>
        private RunwayWindResult? GetRunwayInUse(Airport airport, IReadOnlyList<Runway> runways)
        {
            Metar? report = GetCurrentReport(airport);

            if (report == null || runways.Count == 0)
            {
                return null;
            }

            if (runwayInUse.TryGetValue(airport.Icao, out (Metar Report, RunwayWindResult? Result) known) && ReferenceEquals(known.Report, report))
            {
                return known.Result;
            }

            if (runwayInUse.Count > MaxRunwayMemory)
            {
                runwayInUse.Clear();
            }

            RunwayWindResult? result = RunwayWind.Choose(runways, report.WindDirectionDegrees, report.IsWindVariable, report.WindSpeedKnots);
            runwayInUse[airport.Icao] = (report, result);

            return result;
        }

        /// <summary>
        /// Returns the report of the airport itself, when it is not older than the time after which the panel marks it as out of date.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <returns>The report, or null when there is none, it comes from another station or it is out of date.</returns>
        private static Metar? GetCurrentReport(Airport airport)
        {
            WeatherEntry? entry = AppServices.Weather.Get(airport.Icao);
            Metar? report = entry?.Report;

            if (entry == null || report == null || entry.DistanceKilometers.HasValue || DateTime.UtcNow - report.ObservedAt.UtcDateTime > AirportWeatherText.StaleAfter)
            {
                return null;
            }

            return report;
        }

        /// <summary>
        /// Draws the ring in the color of the flight category and, at a higher zoom, the wind arrow of an airport that has a current report.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="airport">The airport.</param>
        /// <param name="x">Screen position of the marker.</param>
        /// <param name="y">Screen position of the marker.</param>
        /// <param name="radius">Radius of the marker in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        /// <param name="zoom">Zoom level.</param>
        private void DrawWeather(Canvas canvas, Airport airport, float x, float y, float radius, float density, double zoom)
        {
            Metar? report = GetCurrentReport(airport);

            if (report == null)
            {
                return;
            }

            int? color = AirportWeatherText.GetCategoryColor(report.FlightCategory);

            if (color.HasValue)
            {
                weatherRingPaint.Color = new Color(color.Value);
                weatherRingPaint.StrokeWidth = 2.5f * density;
                canvas.DrawCircle(x, y, radius + 5.5f * density, weatherRingPaint);
            }

            if (AppSettings.ShowAirportMapWind && zoom >= AppSettings.AirportMapWeatherFromZoom + WindArrowZoomOffset)
            {
                DrawWindArrow(canvas, report, x + radius + 14f * density, y - radius - 10f * density, density);
            }
        }

        /// <summary>
        /// Draws the shaft and the two barbs of an arrow with one paint.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="paint">Paint of the lines.</param>
        /// <param name="tailX">Screen position of the tail.</param>
        /// <param name="tailY">Screen position of the tail.</param>
        /// <param name="headX">Screen position of the head.</param>
        /// <param name="headY">Screen position of the head.</param>
        /// <param name="directionX">Horizontal part of the unit vector from the tail to the head.</param>
        /// <param name="directionY">Vertical part of the unit vector from the tail to the head.</param>
        /// <param name="barb">Length of a barb in pixels.</param>
        private static void DrawArrowLines(Canvas canvas, Paint paint, float tailX, float tailY, float headX, float headY, float directionX, float directionY, float barb)
        {
            const float Angle = 0.5f;
            float sine = (float)Math.Sin(Angle);
            float cosine = (float)Math.Cos(Angle);

            canvas.DrawLine(tailX, tailY, headX, headY, paint);
            canvas.DrawLine(headX, headY, headX + (-directionX * cosine + directionY * sine) * barb, headY + (-directionX * sine - directionY * cosine) * barb, paint);
            canvas.DrawLine(headX, headY, headX + (-directionX * cosine - directionY * sine) * barb, headY + (directionX * sine - directionY * cosine) * barb, paint);
        }

        /// <summary>
        /// Draws an arrow that points where the wind blows; its length grows with the speed of the wind. Nothing is drawn for a variable wind, a calm or a report without a wind.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="report">The report.</param>
        /// <param name="centerX">Screen position of the middle of the arrow.</param>
        /// <param name="centerY">Screen position of the middle of the arrow.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawWindArrow(Canvas canvas, Metar report, float centerX, float centerY, float density)
        {
            if (report.IsWindVariable || !report.WindDirectionDegrees.HasValue || !report.WindSpeedKnots.HasValue || report.WindSpeedKnots.Value < 1)
            {
                return;
            }

            double toward = (report.WindDirectionDegrees.Value + 180.0 + frameRotation) * Math.PI / 180.0;
            float directionX = (float)Math.Sin(toward);
            float directionY = -(float)Math.Cos(toward);
            float length = (10f + Math.Min(report.WindSpeedKnots.Value, 30) * 0.5f) * density;
            float tailX = centerX - directionX * length / 2f;
            float tailY = centerY - directionY * length / 2f;
            float headX = centerX + directionX * length / 2f;
            float headY = centerY + directionY * length / 2f;
            float barb = 4.5f * density;

            windOutlinePaint.StrokeWidth = 4.5f * density;
            windPaint.StrokeWidth = 2f * density;
            DrawArrowLines(canvas, windOutlinePaint, tailX, tailY, headX, headY, directionX, directionY, barb);
            DrawArrowLines(canvas, windPaint, tailX, tailY, headX, headY, directionX, directionY, barb);
        }
    }
}

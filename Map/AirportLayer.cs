using AdsbCarMap.Data;
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
        /// <returns>Airports drawn in the frame with the positions of their markers on the screen.</returns>
        public List<(Airport Airport, float X, float Y)> Draw(Canvas canvas, AirportDatabase database, Airport? selected, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
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

            if (zoom >= RunwaysFromZoom)
            {
                DrawRunways(canvas, database, centerLatitude, centerLongitude, zoom, width, height, density);
            }

            float iconScale = AppSettings.IconScale;
            bool labels = AppSettings.ShowAirportLabels;

            labelPaint.TextSize = AppSettings.AirportLabelSize * density;

            foreach (Airport airport in visible)
            {
                (float X, float Y) screen = WebMercator.ProjectToScreen(airport.Latitude, airport.Longitude, centerLatitude, centerLongitude, zoom, width, height);

                if (screen.X < -40f || screen.Y < -40f || screen.X > width + 40f || screen.Y > height + 40f)
                {
                    continue;
                }

                float radius = GetRadiusDp(airport.Type) * density * Math.Max(0.8f, iconScale);

                outlinePaint.StrokeWidth = 1.5f * density;
                fillPaint.Color = GetMarkerColor(airport);
                canvas.DrawCircle(screen.X, screen.Y, radius, fillPaint);
                canvas.DrawCircle(screen.X, screen.Y, radius, outlinePaint);

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
        private void DrawRunways(Canvas canvas, AirportDatabase database, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
            double metersPerPixel = 156543.03392 * Math.Cos(centerLatitude * Math.PI / 180.0) / Math.Pow(2.0, zoom);

            foreach (Airport airport in visible)
            {
                foreach (Runway runway in database.GetRunways(airport))
                {
                    (float X, float Y) low = WebMercator.ProjectToScreen(runway.LowEndLatitude, runway.LowEndLongitude, centerLatitude, centerLongitude, zoom, width, height);
                    (float X, float Y) high = WebMercator.ProjectToScreen(runway.HighEndLatitude, runway.HighEndLongitude, centerLatitude, centerLongitude, zoom, width, height);

                    if ((low.X < -50f && high.X < -50f) || (low.Y < -50f && high.Y < -50f) || (low.X > width + 50f && high.X > width + 50f) || (low.Y > height + 50f && high.Y > height + 50f))
                    {
                        continue;
                    }

                    double widthMeters = (runway.WidthFeet > 0 ? runway.WidthFeet : 100) * 0.3048;
                    float stroke = (float)Math.Clamp(widthMeters / metersPerPixel, 1.5 * density, 14.0 * density);

                    runwayOutlinePaint.StrokeWidth = stroke + 2f * density;
                    runwayPaint.StrokeWidth = stroke;
                    canvas.DrawLine(low.X, low.Y, high.X, high.Y, runwayOutlinePaint);
                    canvas.DrawLine(low.X, low.Y, high.X, high.Y, runwayPaint);
                }
            }
        }
    }
}

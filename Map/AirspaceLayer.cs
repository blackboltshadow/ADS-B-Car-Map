using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the airspace areas on the phone map: a translucent fill and an outline in the color of the kind of the area, a solid outline for an active area and a dashed one for a planned area, and the designation of the area as a label. The areas are drawn under the aircraft.
    /// </summary>
    public sealed class AirspaceLayer
    {
        /// <summary>
        /// Zoom from which the labels are drawn.
        /// </summary>
        private const double LabelFromZoom = 8.0;

        /// <summary>
        /// Distance in pixels below which the next corner of an outline is skipped; it keeps the path short when the map is zoomed out.
        /// </summary>
        private const float MinimumVertexSpacing = 1.5f;

        /// <summary>
        /// Distance in pixels around the screen within which an area is still drawn.
        /// </summary>
        private const float CullMargin = 20f;

        /// <summary>
        /// Clockwise rotation of the map in the frame being drawn, in degrees.
        /// </summary>
        private double frameRotation;

        /// <summary>
        /// Paint of the fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the outline.
        /// </summary>
        private readonly Paint strokePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the labels.
        /// </summary>
        private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Path of the outline, reused for every area.
        /// </summary>
        private readonly AndroidPath path = new AndroidPath();

        /// <summary>
        /// Labels placed in the current frame, so that they do not overlap.
        /// </summary>
        private readonly List<RectF> placedLabels = new List<RectF>();

        /// <summary>
        /// Dash pattern of the outline of a planned area, created for the current density.
        /// </summary>
        private DashPathEffect? dash;

        /// <summary>
        /// Density for which <see cref="dash"/> was created.
        /// </summary>
        private float dashDensity;

        /// <summary>
        /// Prepares the paints.
        /// </summary>
        public AirspaceLayer()
        {
            fillPaint.SetStyle(Paint.Style.Fill);
            strokePaint.SetStyle(Paint.Style.Stroke);
            strokePaint.StrokeJoin = Paint.Join.Round;
            labelPaint.Color = Color.White;
            labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Draws the areas for the current view.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="views">The areas to draw, with their states.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        /// <param name="rotationDegrees">Clockwise rotation of the map in degrees.</param>
        public void Draw(Canvas canvas, IReadOnlyList<AirspaceZoneView> views, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density, double rotationDegrees)
        {
            frameRotation = rotationDegrees;
            if (views.Count == 0)
            {
                return;
            }

            if (dash == null || dashDensity != density)
            {
                dash = new DashPathEffect(new float[] { 8f * density, 6f * density }, 0f);
                dashDensity = density;
            }

            placedLabels.Clear();
            labelPaint.TextSize = 11f * density;

            foreach (AirspaceZoneView view in views)
            {
                if (IsOffScreen(view.Zone.Bounds, centerLatitude, centerLongitude, zoom, width, height, frameRotation))
                {
                    continue;
                }

                DrawZone(canvas, view, centerLatitude, centerLongitude, zoom, width, height, density);
            }

            if (zoom >= LabelFromZoom)
            {
                DrawLabels(canvas, views, centerLatitude, centerLongitude, zoom, width, height, density);
            }
        }

        /// <summary>
        /// Draws one area.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="view">The area with its state.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawZone(Canvas canvas, AirspaceZoneView view, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
            int color = AirspaceText.GetColor(view.Zone.Type);
            int fillAlpha;
            int strokeAlpha;

            switch (view.State)
            {
                case AirspaceZoneState.Active:
                    fillAlpha = 70;
                    strokeAlpha = 235;
                    break;
                case AirspaceZoneState.Planned:
                    fillAlpha = 38;
                    strokeAlpha = 205;
                    break;
                default:
                    fillAlpha = 18;
                    strokeAlpha = 120;
                    break;
            }

            fillPaint.Color = Color.Argb(fillAlpha, Color.GetRedComponent(color), Color.GetGreenComponent(color), Color.GetBlueComponent(color));
            strokePaint.Color = Color.Argb(strokeAlpha, Color.GetRedComponent(color), Color.GetGreenComponent(color), Color.GetBlueComponent(color));
            strokePaint.StrokeWidth = (view.State == AirspaceZoneState.Active ? 2.2f : 1.6f) * density;
            strokePaint.SetPathEffect(view.State == AirspaceZoneState.Active ? null : dash);

            foreach (IReadOnlyList<AirspaceVertex> outline in view.Zone.Outlines)
            {
                path.Reset();
                int kept = 0;
                float lastX = 0f;
                float lastY = 0f;

                foreach (AirspaceVertex vertex in outline)
                {
                    (float X, float Y) point = WebMercator.ProjectToScreen(vertex.Latitude, vertex.Longitude, centerLatitude, centerLongitude, zoom, width, height, frameRotation);

                    if (kept > 0 && Math.Abs(point.X - lastX) < MinimumVertexSpacing && Math.Abs(point.Y - lastY) < MinimumVertexSpacing)
                    {
                        continue;
                    }

                    if (kept == 0)
                    {
                        path.MoveTo(point.X, point.Y);
                    }
                    else
                    {
                        path.LineTo(point.X, point.Y);
                    }

                    lastX = point.X;
                    lastY = point.Y;
                    kept++;
                }

                if (kept < 3)
                {
                    continue;
                }

                path.Close();
                canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, strokePaint);
            }
        }

        /// <summary>
        /// Draws the designations of the areas that are on the screen, skipping a label that would overlap another.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="views">The areas.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawLabels(Canvas canvas, IReadOnlyList<AirspaceZoneView> views, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
            foreach (AirspaceZoneView view in views)
            {
                (double Latitude, double Longitude) labelPoint = AirspaceGeometry.GetLabelPoint(view.Zone);
                (float X, float Y) screen = WebMercator.ProjectToScreen(labelPoint.Latitude, labelPoint.Longitude, centerLatitude, centerLongitude, zoom, width, height, frameRotation);

                if (screen.X < 0f || screen.Y < 0f || screen.X > width || screen.Y > height)
                {
                    continue;
                }

                string text = view.Zone.Designator;
                float textWidth = labelPaint.MeasureText(text);
                RectF box = new RectF(screen.X - textWidth / 2f - 2f * density, screen.Y - labelPaint.TextSize, screen.X + textWidth / 2f + 2f * density, screen.Y + 4f * density);

                if (placedLabels.Any((RectF other) => RectF.Intersects(other, box)))
                {
                    continue;
                }

                placedLabels.Add(box);
                canvas.DrawText(text, screen.X - textWidth / 2f, screen.Y, labelPaint);
            }
        }

        /// <summary>
        /// Tells whether the rectangle that contains an area is outside the screen.
        /// </summary>
        /// <param name="bounds">Rectangle of the area in degrees.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="rotationDegrees">Clockwise rotation of the map in degrees. A turned map is never culled, because the rectangle turned with it is no longer aligned with the screen.</param>
        /// <returns>True when no part of the rectangle can be seen.</returns>
        private static bool IsOffScreen(AirspaceBounds bounds, double centerLatitude, double centerLongitude, double zoom, int width, int height, double rotationDegrees)
        {
            if (rotationDegrees != 0.0)
            {
                return false;
            }

            (float X, float Y) northWest = WebMercator.ProjectToScreen(bounds.MaxLatitude, bounds.MinLongitude, centerLatitude, centerLongitude, zoom, width, height, rotationDegrees);
            (float X, float Y) southEast = WebMercator.ProjectToScreen(bounds.MinLatitude, bounds.MaxLongitude, centerLatitude, centerLongitude, zoom, width, height, rotationDegrees);

            return southEast.X < -CullMargin || northWest.X > width + CullMargin || southEast.Y < -CullMargin || northWest.Y > height + CullMargin;
        }
    }
}

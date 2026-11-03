using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the airborne threats on the phone map: a marker with the heading, the circle of the uncertainty of the position, the recent path and a label. A threat of which only the region is known is not drawn, because it has no place.
    /// </summary>
    public sealed class ThreatLayer
    {
        /// <summary>
        /// Zoom from which the labels are drawn.
        /// </summary>
        private const double LabelFromZoom = 7.0;

        /// <summary>
        /// Radius of the marker in dp.
        /// </summary>
        private const float MarkerRadiusDp = 14f;

        /// <summary>
        /// Opacity of a track that has not been confirmed for a while.
        /// </summary>
        private const int StaleAlpha = 110;

        /// <summary>
        /// Opacity of an icon whose heading is only an assumed course.
        /// </summary>
        private const int PresumptiveAlpha = 170;

        /// <summary>
        /// Earth circumference in meters at the equator, for the size of a pixel.
        /// </summary>
        private const double MetersPerPixelAtZoomZero = 156543.03392;

        /// <summary>
        /// Paint of the marker fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark outline of the marker and the arrow.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the circle of the uncertainty.
        /// </summary>
        private readonly Paint uncertaintyPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the recent path.
        /// </summary>
        private readonly Paint trailPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the labels.
        /// </summary>
        private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Draws the icons in the style of the aircraft icons.
        /// </summary>
        private readonly AircraftIconPainter iconPainter = new AircraftIconPainter(new AircraftShapes());

        /// <summary>
        /// Shapes of the threat icons.
        /// </summary>
        private readonly ThreatShapes shapes = new ThreatShapes();

        /// <summary>
        /// Path of the recent path, reused every frame.
        /// </summary>
        private readonly Android.Graphics.Path trailLine = new Android.Graphics.Path();

        /// <summary>
        /// Labels placed in the current frame, so that they do not overlap.
        /// </summary>
        private readonly List<RectF> placedLabels = new List<RectF>();

        /// <summary>
        /// Dash pattern of the dashed lines, created for the current density.
        /// </summary>
        private DashPathEffect? dash;

        /// <summary>
        /// Density for which <see cref="dash"/> was created.
        /// </summary>
        private float dashDensity;

        /// <summary>
        /// Prepares the paints.
        /// </summary>
        public ThreatLayer()
        {
            fillPaint.SetStyle(Paint.Style.Fill);
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.Color = Color.Argb(230, 20, 20, 20);
            uncertaintyPaint.SetStyle(Paint.Style.Fill);
            trailPaint.SetStyle(Paint.Style.Stroke);
            trailPaint.StrokeCap = Paint.Cap.Round;
            trailPaint.StrokeJoin = Paint.Join.Round;
            labelPaint.Color = Color.White;
            labelPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Draws the threats for the current view and returns where the markers are, so that they can be tapped.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="threats">Threats to draw.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        /// <returns>The drawn threats with the screen position of their markers.</returns>
        public List<(Threat Threat, float X, float Y)> Draw(Canvas canvas, IReadOnlyList<Threat> threats, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
            List<(Threat Threat, float X, float Y)> hits = new List<(Threat Threat, float X, float Y)>();

            if (threats.Count == 0)
            {
                return hits;
            }

            if (dash == null || dashDensity != density)
            {
                dash = new DashPathEffect(new float[] { 8f * density, 6f * density }, 0f);
                dashDensity = density;
            }

            placedLabels.Clear();
            labelPaint.TextSize = 12f * density;

            double metersPerPixel = MetersPerPixelAtZoomZero * Math.Cos(centerLatitude * Math.PI / 180.0) / Math.Pow(2.0, zoom);
            List<(Threat Threat, float X, float Y)> visible = new List<(Threat Threat, float X, float Y)>();

            foreach (Threat threat in threats)
            {
                if (!ThreatText.HasDrawablePosition(threat))
                {
                    continue;
                }

                (float X, float Y) screen = WebMercator.ProjectToScreen(threat.Latitude, threat.Longitude, centerLatitude, centerLongitude, zoom, width, height);
                float reach = (float)Math.Min(threat.UncertaintyKm * 1000.0 / metersPerPixel, 4000.0) + 40f * density;

                if (screen.X < -reach || screen.Y < -reach || screen.X > width + reach || screen.Y > height + reach)
                {
                    continue;
                }

                visible.Add((threat, screen.X, screen.Y));
            }

            foreach ((Threat Threat, float X, float Y) entry in visible)
            {
                DrawUncertainty(canvas, entry.Threat, entry.X, entry.Y, metersPerPixel, density);
                DrawTrail(canvas, entry.Threat, entry.X, entry.Y, centerLatitude, centerLongitude, zoom, width, height, density);
            }

            foreach ((Threat Threat, float X, float Y) entry in visible)
            {
                DrawMarker(canvas, entry.Threat, entry.X, entry.Y, density);
                hits.Add(entry);
            }

            if (zoom >= LabelFromZoom)
            {
                foreach ((Threat Threat, float X, float Y) entry in visible)
                {
                    DrawLabel(canvas, entry.Threat, entry.X, entry.Y, density);
                }
            }

            return hits;
        }

        /// <summary>
        /// Draws the circle within which the real position probably is. A position that is only approximate gets a dashed edge.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="threat">Threat.</param>
        /// <param name="x">Horizontal position of the marker in pixels.</param>
        /// <param name="y">Vertical position of the marker in pixels.</param>
        /// <param name="metersPerPixel">Size of a pixel in meters.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawUncertainty(Canvas canvas, Threat threat, float x, float y, double metersPerPixel, float density)
        {
            float radius = (float)Math.Min(threat.UncertaintyKm * 1000.0 / metersPerPixel, 4000.0);

            if (radius < MarkerRadiusDp * density * 1.5f)
            {
                return;
            }

            int color = ThreatText.ColorFor(threat);
            int alpha = threat.Status == ThreatStatus.Stale ? 12 : 30;

            uncertaintyPaint.Color = WithAlpha(color, alpha);
            canvas.DrawCircle(x, y, radius, uncertaintyPaint);

            trailPaint.SetPathEffect(threat.IsApproximate ? dash : null);
            trailPaint.StrokeWidth = 1.5f * density;
            trailPaint.Color = WithAlpha(color, threat.Status == ThreatStatus.Stale ? 50 : 120);
            trailPaint.SetStyle(Paint.Style.Stroke);
            canvas.DrawCircle(x, y, radius, trailPaint);
            trailPaint.SetPathEffect(null);
        }

        /// <summary>
        /// Draws the recent path of the threat up to its marker.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="threat">Threat.</param>
        /// <param name="x">Horizontal position of the marker in pixels.</param>
        /// <param name="y">Vertical position of the marker in pixels.</param>
        /// <param name="centerLatitude">Latitude of the screen center.</param>
        /// <param name="centerLongitude">Longitude of the screen center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawTrail(Canvas canvas, Threat threat, float x, float y, double centerLatitude, double centerLongitude, double zoom, int width, int height, float density)
        {
            if (threat.Trail.Count == 0)
            {
                return;
            }

            trailLine.Reset();

            for (int index = 0; index < threat.Trail.Count; index++)
            {
                (float X, float Y) point = WebMercator.ProjectToScreen(threat.Trail[index].Latitude, threat.Trail[index].Longitude, centerLatitude, centerLongitude, zoom, width, height);

                if (index == 0)
                {
                    trailLine.MoveTo(point.X, point.Y);
                }
                else
                {
                    trailLine.LineTo(point.X, point.Y);
                }
            }

            trailLine.LineTo(x, y);
            trailPaint.SetPathEffect(null);
            trailPaint.SetStyle(Paint.Style.Stroke);
            trailPaint.StrokeWidth = 2f * density;
            trailPaint.Color = WithAlpha(ThreatText.ColorFor(threat), threat.Status == ThreatStatus.Stale ? 60 : 150);
            canvas.DrawPath(trailLine, trailPaint);
        }

        /// <summary>
        /// Draws the icon of a threat in the style of the aircraft icons, with the same size, outline, shadow and accent settings, rotated by the heading. A track that has not been confirmed for a while and an assumed course are drawn fainter.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="threat">Threat.</param>
        /// <param name="x">Horizontal position of the marker in pixels.</param>
        /// <param name="y">Vertical position of the marker in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawMarker(Canvas canvas, Threat threat, float x, float y, float density)
        {
            (AndroidPath Path, float Scale) shape = shapes.Get(threat.Kind, threat.HeadingDegrees != null);
            bool faint = threat.Status == ThreatStatus.Stale;
            int alpha = faint ? StaleAlpha : threat.IsPresumptiveCourse ? PresumptiveAlpha : 255;
            float rotation = (float)(threat.HeadingDegrees ?? 0.0);

            iconPainter.DrawShape(canvas, shape.Path, shape.Scale, shapes.GetAccent(threat.Kind), x, y, rotation, WithAlpha(ThreatText.ColorFor(threat), alpha), density, AppSettings.ThreatIconScale, AppSettings.ThreatOutlineWidth, faint ? 0.0f : AppSettings.AircraftShadowStrength, AppSettings.AircraftCockpitStrength);
        }

        /// <summary>
        /// Draws the label of a threat unless it would overlap a label that is already placed.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="threat">Threat.</param>
        /// <param name="x">Horizontal position of the marker in pixels.</param>
        /// <param name="y">Vertical position of the marker in pixels.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawLabel(Canvas canvas, Threat threat, float x, float y, float density)
        {
            string text = ThreatText.BuildLabel(threat);
            float left = x + (MarkerRadiusDp + 4f) * density;
            float textWidth = labelPaint.MeasureText(text);
            float textHeight = labelPaint.TextSize;
            float padding = 2f * density;
            RectF bounds = new RectF(left - padding, y - textHeight / 2f - padding, left + textWidth + padding, y + textHeight / 2f + padding);

            foreach (RectF placed in placedLabels)
            {
                if (RectF.Intersects(placed, bounds))
                {
                    return;
                }
            }

            placedLabels.Add(bounds);
            canvas.DrawText(text, left, y + textHeight * 0.35f, labelPaint);
        }

        /// <summary>
        /// Replaces the opacity of a color.
        /// </summary>
        /// <param name="color">Color as an ARGB number.</param>
        /// <param name="alpha">New opacity from 0 to 255.</param>
        /// <returns>The color with the new opacity.</returns>
        private static Color WithAlpha(int color, int alpha)
        {
            return Color.Argb(alpha, (color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF);
        }
    }
}

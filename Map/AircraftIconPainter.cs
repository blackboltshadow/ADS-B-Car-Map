using AdsbCarMap.Data;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws one aircraft icon: a drop shadow, a dark outline, the fill and a cockpit accent. Shared by the map and the settings previews, so the icon looks the same everywhere.
    /// </summary>
    public sealed class AircraftIconPainter
    {
        /// <summary>
        /// Shadow offset in dp at the default icon size and full shadow strength.
        /// </summary>
        private const float ShadowOffsetDp = 2.5f;

        /// <summary>
        /// Shadow opacity (0-255) at full shadow strength.
        /// </summary>
        private const int ShadowMaxAlpha = 120;

        /// <summary>
        /// Canopy opacity (0-255) at full accent strength.
        /// </summary>
        private const int CanopyMaxAlpha = 210;

        /// <summary>
        /// Shapes of the icons.
        /// </summary>
        private readonly AircraftShapes shapes;

        /// <summary>
        /// Paint of the icon fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark icon outline.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the shadow, which covers the outlined silhouette.
        /// </summary>
        private readonly Paint shadowPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the cockpit accent.
        /// </summary>
        private readonly Paint canopyPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Creates the painter.
        /// </summary>
        /// <param name="shapes">Shapes of the icons.</param>
        public AircraftIconPainter(AircraftShapes shapes)
        {
            this.shapes = shapes;

            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.StrokeJoin = Paint.Join.Round;
            outlinePaint.Color = Color.Argb(235, 0, 0, 0);

            shadowPaint.SetStyle(Paint.Style.FillAndStroke);
            shadowPaint.StrokeJoin = Paint.Join.Round;
        }

        /// <summary>
        /// Draws the icon centered at the given point and rotated by the heading.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="kind">Kind of the aircraft.</param>
        /// <param name="x">Horizontal position of the icon center in pixels.</param>
        /// <param name="y">Vertical position of the icon center in pixels.</param>
        /// <param name="rotation">Heading in degrees clockwise from the top.</param>
        /// <param name="color">Fill color.</param>
        /// <param name="density">Screen density factor.</param>
        /// <param name="iconScale">Icon size as a multiple of the default size.</param>
        /// <param name="outlineWidth">Outline width in dp on each side, 0 for none.</param>
        /// <param name="shadowStrength">Shadow strength from 0 (none) to 1.</param>
        /// <param name="cockpitStrength">Cockpit accent strength from 0 (none) to 1.</param>
        public void Draw(Canvas canvas, AircraftKind kind, float x, float y, float rotation, Color color, float density, float iconScale, float outlineWidth, float shadowStrength, float cockpitStrength)
        {
            (AndroidPath Path, float Scale) shape = shapes.Get(kind);
            float scale = density * shape.Scale * iconScale;
            float strokeWidth = 2.0f * outlineWidth / (shape.Scale * iconScale);

            if (shadowStrength > 0.0f && kind != AircraftKind.Ground)
            {
                float offset = ShadowOffsetDp * density * iconScale * shadowStrength;

                shadowPaint.Color = Color.Argb((int)(ShadowMaxAlpha * shadowStrength), 0, 0, 0);
                shadowPaint.StrokeWidth = strokeWidth;

                canvas.Save();
                canvas.Translate(x + offset, y + offset);
                canvas.Rotate(rotation);
                canvas.Scale(scale, scale);
                canvas.DrawPath(shape.Path, shadowPaint);
                canvas.Restore();
            }

            fillPaint.Color = color;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.Rotate(rotation);
            canvas.Scale(scale, scale);

            if (outlineWidth > 0.0f)
            {
                outlinePaint.StrokeWidth = strokeWidth;
                canvas.DrawPath(shape.Path, outlinePaint);
            }

            canvas.DrawPath(shape.Path, fillPaint);

            AndroidPath? canopy = shapes.GetCanopy(kind);

            if (cockpitStrength > 0.0f && canopy != null)
            {
                canopyPaint.Color = Color.Argb((int)(CanopyMaxAlpha * cockpitStrength), 12, 22, 40);
                canvas.DrawPath(canopy, canopyPaint);
            }

            canvas.Restore();
        }
    }
}

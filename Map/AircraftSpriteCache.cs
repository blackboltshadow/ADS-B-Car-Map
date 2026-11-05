using AdsbCarMap.Data;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the aircraft icons of the map from bitmaps that are drawn once per kind and color, so that an icon costs a few calls to the graphics system instead of about twenty. The look is the same as that of AircraftIconPainter; only the shadow keeps its direction on the screen, not on the icon, as there.
    /// </summary>
    public sealed class AircraftSpriteCache
    {
        /// <summary>
        /// Shadow offset in dp at the default icon size and full shadow strength.
        /// </summary>
        private const float ShadowOffsetDp = 2.5f;

        /// <summary>
        /// Number of sprites after which the cache is emptied, so that the gradient of the altitude colors cannot fill the memory.
        /// </summary>
        private const int MaximumSprites = 400;

        /// <summary>
        /// Step by which the color channels are rounded, so that similar colors share a sprite.
        /// </summary>
        private const int ColorStep = 8;

        /// <summary>
        /// Draws the sprites.
        /// </summary>
        private readonly AircraftIconPainter painter;

        /// <summary>
        /// Shapes of the icons.
        /// </summary>
        private readonly AircraftShapes shapes;

        /// <summary>
        /// Paint used to draw the sprites on the map.
        /// </summary>
        private readonly Paint spritePaint = new Paint(PaintFlags.FilterBitmap);

        /// <summary>
        /// Bitmaps of the icons by kind and color.
        /// </summary>
        private readonly Dictionary<(AircraftKind Kind, int Color), Bitmap> icons = new Dictionary<(AircraftKind Kind, int Color), Bitmap>();

        /// <summary>
        /// Bitmaps of the shadows by kind.
        /// </summary>
        private readonly Dictionary<AircraftKind, Bitmap> shadows = new Dictionary<AircraftKind, Bitmap>();

        /// <summary>
        /// Values of the settings the sprites were drawn for.
        /// </summary>
        private (float Density, float IconScale, float Outline, float Shadow, float Cockpit) drawnFor;

        /// <summary>
        /// Creates the cache.
        /// </summary>
        /// <param name="painter">Painter that draws the sprites.</param>
        /// <param name="shapes">Shapes of the icons.</param>
        public AircraftSpriteCache(AircraftIconPainter painter, AircraftShapes shapes)
        {
            this.painter = painter;
            this.shapes = shapes;
        }

        /// <summary>
        /// Draws the icon centered at the given point and rotated by the heading. Call it from the drawing thread only.
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
            (float, float, float, float, float) wanted = (density, iconScale, outlineWidth, shadowStrength, cockpitStrength);

            if (wanted != drawnFor || icons.Count >= MaximumSprites)
            {
                Clear();
                drawnFor = wanted;
            }

            Bitmap? icon = GetIcon(kind, Round(color));

            if (icon == null)
            {
                return;
            }

            float half = icon.Width / 2.0f;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.Rotate(rotation);

            if (shadowStrength > 0.0f && kind != AircraftKind.Ground)
            {
                Bitmap? shadow = GetShadow(kind);

                if (shadow != null)
                {
                    float offset = ShadowOffsetDp * density * iconScale * shadowStrength;
                    double radians = -rotation * Math.PI / 180.0;
                    float offsetX = (float)(offset * (Math.Cos(radians) - Math.Sin(radians)));
                    float offsetY = (float)(offset * (Math.Sin(radians) + Math.Cos(radians)));

                    canvas.DrawBitmap(shadow, offsetX - half, offsetY - half, spritePaint);
                }
            }

            canvas.DrawBitmap(icon, -half, -half, spritePaint);
            canvas.Restore();
        }

        /// <summary>
        /// Releases all sprites.
        /// </summary>
        public void Clear()
        {
            foreach (Bitmap bitmap in icons.Values)
            {
                bitmap.Recycle();
            }

            foreach (Bitmap bitmap in shadows.Values)
            {
                bitmap.Recycle();
            }

            icons.Clear();
            shadows.Clear();
        }

        /// <summary>
        /// Rounds a color to the step shared by similar colors.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>The rounded color as a number.</returns>
        private static int Round(Color color)
        {
            int red = Math.Min(255, (color.R + ColorStep / 2) / ColorStep * ColorStep);
            int green = Math.Min(255, (color.G + ColorStep / 2) / ColorStep * ColorStep);
            int blue = Math.Min(255, (color.B + ColorStep / 2) / ColorStep * ColorStep);

            return (red << 16) | (green << 8) | blue;
        }

        /// <summary>
        /// Returns the bitmap of an icon, drawing it when it is not there yet.
        /// </summary>
        /// <param name="kind">Kind of the aircraft.</param>
        /// <param name="rounded">Rounded fill color.</param>
        /// <returns>The bitmap, or null when it cannot be created.</returns>
        private Bitmap? GetIcon(AircraftKind kind, int rounded)
        {
            if (icons.TryGetValue((kind, rounded), out Bitmap? cached))
            {
                return cached;
            }

            (AndroidPath Path, float Scale) shape = shapes.Get(kind);
            Bitmap? bitmap = CreateBitmap(shape);

            if (bitmap == null)
            {
                return null;
            }

            using Canvas canvas = new Canvas(bitmap);
            float center = bitmap.Width / 2.0f;
            Color fill = Color.Rgb((rounded >> 16) & 255, (rounded >> 8) & 255, rounded & 255);

            painter.DrawShape(canvas, shape.Path, shape.Scale, shapes.GetCanopy(kind), center, center, 0.0f, fill, drawnFor.Density, drawnFor.IconScale, drawnFor.Outline, 0.0f, drawnFor.Cockpit);
            icons[(kind, rounded)] = bitmap;

            return bitmap;
        }

        /// <summary>
        /// Returns the bitmap of a shadow, drawing it when it is not there yet.
        /// </summary>
        /// <param name="kind">Kind of the aircraft.</param>
        /// <returns>The bitmap, or null when it cannot be created.</returns>
        private Bitmap? GetShadow(AircraftKind kind)
        {
            if (shadows.TryGetValue(kind, out Bitmap? cached))
            {
                return cached;
            }

            (AndroidPath Path, float Scale) shape = shapes.Get(kind);
            Bitmap? bitmap = CreateBitmap(shape);

            if (bitmap == null)
            {
                return null;
            }

            using Canvas canvas = new Canvas(bitmap);
            float center = bitmap.Width / 2.0f;

            painter.DrawShadow(canvas, shape.Path, shape.Scale, center, center, 0.0f, 0.0f, drawnFor.Density, drawnFor.IconScale, drawnFor.Outline, drawnFor.Shadow);
            shadows[kind] = bitmap;

            return bitmap;
        }

        /// <summary>
        /// Creates an empty square bitmap that fits the icon with its outline, so that the icon can be rotated inside it.
        /// </summary>
        /// <param name="shape">Shape and its scale.</param>
        /// <returns>The bitmap, or null when it cannot be created.</returns>
        private Bitmap? CreateBitmap((AndroidPath Path, float Scale) shape)
        {
            RectF bounds = new RectF();

            shape.Path.ComputeBounds(bounds, true);

            float scale = drawnFor.Density * shape.Scale * drawnFor.IconScale;
            float extent = Math.Max(Math.Max(Math.Abs(bounds.Left), Math.Abs(bounds.Right)), Math.Max(Math.Abs(bounds.Top), Math.Abs(bounds.Bottom))) * scale;
            int size = Math.Max(8, (int)Math.Ceiling((extent + drawnFor.Outline * drawnFor.Density + 2.0f) * 2.0f));

            return Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        }
    }
}

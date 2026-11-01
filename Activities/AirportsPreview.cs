using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Preview of the airport categories on a dark background imitating the map: a large, a medium, a small and a military airport and a heliport, drawn with the marker size and the colors used on the map. The categories that are switched off in the settings are grayed out. Every marker is labelled with the name of its category in the size chosen for the airport codes.
    /// </summary>
    public sealed class AirportsPreview : View
    {
        /// <summary>
        /// Height of the preview in dp.
        /// </summary>
        private const float HeightDp = 108f;

        /// <summary>
        /// Categories shown in the preview: the name, the category, the size class of the marker, and the position along the width.
        /// </summary>
        private static readonly (string Name, AirportCategory Category, AirportType Type, float Position)[] Samples = new (string Name, AirportCategory Category, AirportType Type, float Position)[]
        {
            ("Duże", AirportCategory.Large, AirportType.Large, 0.1f),
            ("Średnie", AirportCategory.Medium, AirportType.Medium, 0.3f),
            ("Małe", AirportCategory.Small, AirportType.Small, 0.5f),
            ("Wojskowe", AirportCategory.Military, AirportType.Medium, 0.7f),
            ("Helipady", AirportCategory.Heliport, AirportType.Heliport, 0.9f),
        };

        /// <summary>
        /// Paint of the marker fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark marker outline.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the category names.
        /// </summary>
        private readonly Paint namePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark background imitating the map.
        /// </summary>
        private readonly Paint backgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the note with the zoom levels.
        /// </summary>
        private readonly Paint notePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing the background.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public AirportsPreview(Context context) : base(context)
        {
            fillPaint.SetStyle(Paint.Style.Fill);
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.Color = Color.Argb(220, 0, 0, 0);
            namePaint.TextAlign = Paint.Align.Center;
            namePaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
            backgroundPaint.Color = Color.Rgb(40, 44, 52);
            notePaint.Color = Color.Rgb(170, 170, 170);
            notePaint.TextAlign = Paint.Align.Center;
        }

        /// <summary>
        /// Chooses the size of the view: full width and a fixed height.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(240), widthMeasureSpec), (int)Dp(HeightDp));
        }

        /// <summary>
        /// Draws the categories the way the map draws airports.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;
            float iconScale = AppSettings.IconScale;
            float centerY = Dp(HeightDp) * 0.55f;

            bounds.Set(0f, 0f, Width, Height);
            canvas.DrawRoundRect(bounds, Dp(8), Dp(8), backgroundPaint);

            namePaint.TextSize = AppSettings.AirportLabelSize * density;
            outlinePaint.StrokeWidth = 1.5f * density;

            foreach ((string Name, AirportCategory Category, AirportType Type, float Position) sample in Samples)
            {
                bool enabled = AppSettings.IsAirportCategoryEnabled(sample.Category);
                float x = Width * sample.Position;
                float radius = AirportLayer.GetRadiusDp(sample.Type) * density * Math.Max(0.8f, iconScale);

                fillPaint.Color = enabled ? GetColor(sample.Category) : Color.Argb(90, 130, 130, 130);
                canvas.DrawCircle(x, centerY, radius, fillPaint);

                if (enabled)
                {
                    canvas.DrawCircle(x, centerY, radius, outlinePaint);
                }

                namePaint.Color = enabled ? Color.White : Color.Argb(120, 170, 170, 170);
                canvas.DrawText(sample.Name, x, centerY - radius - 5f * density, namePaint);
            }

            notePaint.TextSize = Dp(10);
            canvas.DrawText(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Widoczne od zoomu: duże {0:0.#} · średnie {1:0.#} · małe {2:0.#} · helipady {3:0.#}",
                    AirportLayer.GetFromZoom(AirportType.Large),
                    AirportLayer.GetFromZoom(AirportType.Medium),
                    AirportLayer.GetFromZoom(AirportType.Small),
                    AirportLayer.GetFromZoom(AirportType.Heliport)),
                Width / 2f,
                Height - Dp(9),
                notePaint);
        }

        /// <summary>
        /// Returns the marker color of a category, the same as on the map.
        /// </summary>
        /// <param name="category">Category.</param>
        /// <returns>Marker color.</returns>
        private static Color GetColor(AirportCategory category)
        {
            if (category == AirportCategory.Military)
            {
                return AirportLayer.MilitaryColor;
            }

            return category == AirportCategory.Heliport ? AirportLayer.HeliportColor : AirportLayer.CivilColor;
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private float Dp(float value)
        {
            return value * (Resources?.DisplayMetrics?.Density ?? 1.0f);
        }
    }
}

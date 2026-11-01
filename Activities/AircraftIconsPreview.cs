using AdsbCarMap.Data;
using AdsbCarMap.Map;
using Android.Content;
using Android.Graphics;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Preview of all aircraft icons on a dark background imitating the map, drawn with the outline width chosen on the settings screen. Icons have the same size and the same outline as on the map.
    /// </summary>
    public sealed class AircraftIconsPreview : View
    {
        /// <summary>
        /// Number of icons in one row.
        /// </summary>
        private const int IconsPerRow = 5;

        /// <summary>
        /// Height of one row of icons in dp. Fixed, so the layout does not jump while the icon size slider is dragged.
        /// </summary>
        private const float CellHeightDp = 64f;

        /// <summary>
        /// Shapes of the icons, the same as the ones used by the map.
        /// </summary>
        private readonly AircraftShapes shapes = new AircraftShapes();

        /// <summary>
        /// Aircraft kinds in drawing order.
        /// </summary>
        private readonly AircraftKind[] kinds = Enum.GetValues<AircraftKind>();

        /// <summary>
        /// Paint of the icon fill.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark icon outline.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark background imitating the map.
        /// </summary>
        private readonly Paint backgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing the background.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Outline width in dp, the same as the aircraft outline setting.
        /// </summary>
        private float outlineWidth;

        /// <summary>
        /// Icon size as a multiple of the default size, the same as the icon size setting.
        /// </summary>
        private float iconScale = 1.0f;

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public AircraftIconsPreview(Context context) : base(context)
        {
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.StrokeJoin = Paint.Join.Round;
            outlinePaint.Color = Color.Argb(235, 0, 0, 0);
            backgroundPaint.Color = Color.Rgb(40, 44, 52);
        }

        /// <summary>
        /// Outline width in dp. Changing it redraws the preview.
        /// </summary>
        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Icon size as a multiple of the default size. Changing it redraws the preview.
        /// </summary>
        public float IconScale
        {
            get => iconScale;
            set
            {
                iconScale = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Chooses the size of the view: full width and as many rows as the icons need.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int rows = (kinds.Length + IconsPerRow - 1) / IconsPerRow;

            SetMeasuredDimension(ResolveSize((int)Dp(240), widthMeasureSpec), (int)(rows * Dp(CellHeightDp) + Dp(8)));
        }

        /// <summary>
        /// Draws all icons in a grid, each in a different altitude color, the ground one in gray.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;
            float cellWidth = Width / (float)IconsPerRow;
            float cellHeight = Dp(CellHeightDp);

            bounds.Set(0f, 0f, Width, Height);
            canvas.DrawRoundRect(bounds, Dp(8), Dp(8), backgroundPaint);

            for (int index = 0; index < kinds.Length; index++)
            {
                AircraftKind kind = kinds[index];
                (AndroidPath Path, float Scale) shape = shapes.Get(kind);
                float centerX = (index % IconsPerRow + 0.5f) * cellWidth;
                float centerY = Dp(4) + (index / IconsPerRow + 0.5f) * cellHeight;

                fillPaint.Color = kind == AircraftKind.Ground ? AltitudePalette.GroundColor : AltitudePalette.StopColors[index % AltitudePalette.StopColors.Length];

                canvas.Save();
                canvas.Translate(centerX, centerY);
                canvas.Scale(density * shape.Scale * iconScale, density * shape.Scale * iconScale);

                if (outlineWidth > 0.0f)
                {
                    outlinePaint.StrokeWidth = 2.0f * outlineWidth / (shape.Scale * iconScale);
                    canvas.DrawPath(shape.Path, outlinePaint);
                }

                canvas.DrawPath(shape.Path, fillPaint);
                canvas.Restore();
            }
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

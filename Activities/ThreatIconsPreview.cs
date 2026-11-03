using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Preview of all threat icons with their names on a dark background imitating the map, drawn with the size and the outline chosen on the settings screen. The icons look as they do on the map.
    /// </summary>
    public sealed class ThreatIconsPreview : View
    {
        /// <summary>
        /// Number of icons in one row.
        /// </summary>
        private const int IconsPerRow = 3;

        /// <summary>
        /// Height of one row of icons in dp. Fixed, so the layout does not jump while the size slider is dragged.
        /// </summary>
        private const float CellHeightDp = 92f;

        /// <summary>
        /// Distance of the icon center from the top of its cell in dp.
        /// </summary>
        private const float IconCenterDp = 36f;

        /// <summary>
        /// Draws the icons the same way as the map does.
        /// </summary>
        private readonly AircraftIconPainter iconPainter = new AircraftIconPainter(new AircraftShapes());

        /// <summary>
        /// Shapes of the threat icons.
        /// </summary>
        private readonly ThreatShapes shapes = new ThreatShapes();

        /// <summary>
        /// Paint of the dark background imitating the map.
        /// </summary>
        private readonly Paint backgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the names under the icons.
        /// </summary>
        private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing the background.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Icon size as a multiple of the default size, the same as the threat icon size setting.
        /// </summary>
        private float iconScale = AppSettings.ThreatIconScale;

        /// <summary>
        /// Outline width in dp, the same as the threat outline setting.
        /// </summary>
        private float outlineWidth = AppSettings.ThreatOutlineWidth;

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public ThreatIconsPreview(Context context) : base(context)
        {
            backgroundPaint.Color = Color.Rgb(40, 44, 52);
            labelPaint.Color = Color.Rgb(220, 220, 220);
            labelPaint.TextAlign = Paint.Align.Center;
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
        /// Chooses the size of the view: full width and as many rows as the icons need.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int rows = (Enum.GetValues<ThreatKind>().Length + 1 + IconsPerRow - 1) / IconsPerRow;

            SetMeasuredDimension(ResolveSize((int)Dp(240), widthMeasureSpec), (int)(rows * Dp(CellHeightDp) + Dp(8)));
        }

        /// <summary>
        /// Draws every kind with its name, followed by the four-rotor drone without a heading.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;
            float cellWidth = Width / (float)IconsPerRow;

            bounds.Set(0f, 0f, Width, Height);
            canvas.DrawRoundRect(bounds, Dp(8), Dp(8), backgroundPaint);
            labelPaint.TextSize = 11f * density;

            List<(ThreatKind Kind, bool HeadingKnown, string Name)> items = new List<(ThreatKind Kind, bool HeadingKnown, string Name)>();

            foreach (ThreatKind kind in Enum.GetValues<ThreatKind>())
            {
                items.Add((kind, true, ThreatText.KindName(kind)));
            }

            items.Add((ThreatKind.Fpv, false, Loc.T("Dron FPV (bez kursu)")));

            for (int index = 0; index < items.Count; index++)
            {
                (ThreatKind Kind, bool HeadingKnown, string Name) item = items[index];
                float centerX = (index % IconsPerRow + 0.5f) * cellWidth;
                float top = Dp(4) + (index / IconsPerRow) * Dp(CellHeightDp);
                (Android.Graphics.Path Path, float Scale) shape = shapes.Get(item.Kind, item.HeadingKnown);
                Color color = Color.Argb(255, (ThreatText.ColorFor(item.Kind) >> 16) & 0xFF, (ThreatText.ColorFor(item.Kind) >> 8) & 0xFF, ThreatText.ColorFor(item.Kind) & 0xFF);

                iconPainter.DrawShape(canvas, shape.Path, shape.Scale, shapes.GetAccent(item.Kind), centerX, top + Dp(IconCenterDp), 0f, color, density, iconScale, outlineWidth, AppSettings.AircraftShadowStrength, AppSettings.AircraftCockpitStrength);
                canvas.DrawText(item.Name, centerX, top + Dp(CellHeightDp) - Dp(10), labelPaint);
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

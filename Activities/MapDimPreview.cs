using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Preview of the map dimming: a small imitation of a map (land, water, a park and roads) drawn in the light or dark theme chosen in the settings, with the black dimming layer and three sample aircraft on top. The dimming value is changed live while the slider moves.
    /// </summary>
    public sealed class MapDimPreview : View
    {
        /// <summary>
        /// Height of the preview in dp.
        /// </summary>
        private const float HeightDp = 140f;

        /// <summary>
        /// Paint of the map imitation, it carries the dark color filter of the dark theme.
        /// </summary>
        private readonly Paint mapPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dimming layer.
        /// </summary>
        private readonly Paint dimPaint = new Paint();

        /// <summary>
        /// Paint of the sample aircraft.
        /// </summary>
        private readonly Paint aircraftPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the text with the dimming value.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle of the preview.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Path reused when drawing the water and the aircraft.
        /// </summary>
        private readonly AndroidPath path = new AndroidPath();

        /// <summary>
        /// Color filter of the dark theme.
        /// </summary>
        private readonly ColorMatrixColorFilter darkFilter = new ColorMatrixColorFilter(MapTheme.CreateDarkMatrix());

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public MapDimPreview(Context context) : base(context)
        {
            dimPaint.SetStyle(Paint.Style.Fill);
            textPaint.TextAlign = Paint.Align.Center;
            textPaint.Color = Color.White;
            textPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Opacity of the black dimming layer, 0 to 255.
        /// </summary>
        public int Dim { get; set; } = AppSettings.MapDim;

        /// <summary>
        /// Chooses the size of the view: full width and a fixed height.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(260), widthMeasureSpec), (int)Dp(HeightDp));
        }

        /// <summary>
        /// Draws the map imitation, the dimming layer and the aircraft.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float width = Width;
            float height = Height;
            bounds.Set(0f, 0f, width, height);

            bool systemDark = ((Resources?.Configuration?.UiMode ?? 0) & UiMode.NightMask) == UiMode.NightYes;
            bool dark = MapTheme.IsDark(AppSettings.MapThemeIndex, systemDark);

            canvas.Save();
            canvas.ClipRect(bounds);

            mapPaint.SetColorFilter(dark ? darkFilter : null);
            mapPaint.SetStyle(Paint.Style.Fill);
            mapPaint.Color = Color.Rgb(242, 239, 233);
            canvas.DrawRect(bounds, mapPaint);

            mapPaint.Color = Color.Rgb(200, 225, 190);
            canvas.DrawRect(width * 0.05f, height * 0.12f, width * 0.32f, height * 0.5f, mapPaint);

            mapPaint.Color = Color.Rgb(170, 211, 223);
            path.Reset();
            path.MoveTo(width * 0.62f, 0f);
            path.CubicTo(width * 0.55f, height * 0.3f, width * 0.8f, height * 0.5f, width * 0.7f, height);
            path.LineTo(width, height);
            path.LineTo(width, 0f);
            path.Close();
            canvas.DrawPath(path, mapPaint);

            mapPaint.SetStyle(Paint.Style.Stroke);
            mapPaint.StrokeCap = Paint.Cap.Round;
            mapPaint.StrokeWidth = Dp(7);
            mapPaint.Color = Color.Rgb(190, 190, 190);
            DrawRoads(canvas, width, height);
            mapPaint.StrokeWidth = Dp(5);
            mapPaint.Color = Color.Rgb(255, 255, 255);
            DrawRoads(canvas, width, height);
            mapPaint.StrokeWidth = Dp(2);
            mapPaint.Color = Color.Rgb(250, 200, 100);
            canvas.DrawLine(0f, height * 0.78f, width * 0.66f, height * 0.78f, mapPaint);

            int dim = Math.Clamp(Dim, 0, 255);

            if (dim > 0)
            {
                dimPaint.Color = Color.Argb(dim, 0, 0, 0);
                canvas.DrawRect(bounds, dimPaint);
            }

            DrawAircraft(canvas, width * 0.22f, height * 0.3f, 35f, Color.Rgb(255, 150, 40));
            DrawAircraft(canvas, width * 0.5f, height * 0.62f, 100f, Color.Rgb(80, 220, 120));
            DrawAircraft(canvas, width * 0.82f, height * 0.3f, 300f, Color.Rgb(90, 160, 255));

            textPaint.TextSize = Dp(12);
            canvas.DrawText(
                string.Format(System.Globalization.CultureInfo.InvariantCulture, Localization.Loc.T("Przyciemnienie: {0} z 220"), dim),
                width / 2f,
                height - Dp(8),
                textPaint);

            canvas.Restore();
        }

        /// <summary>
        /// Draws the road network of the map imitation with the current paint.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        /// <param name="width">Width of the preview in pixels.</param>
        /// <param name="height">Height of the preview in pixels.</param>
        private void DrawRoads(Canvas canvas, float width, float height)
        {
            canvas.DrawLine(0f, height * 0.78f, width * 0.66f, height * 0.78f, mapPaint);
            canvas.DrawLine(width * 0.4f, 0f, width * 0.46f, height, mapPaint);
            canvas.DrawLine(0f, height * 0.18f, width * 0.6f, height * 0.5f, mapPaint);
        }

        /// <summary>
        /// Draws one sample aircraft as an arrowhead with a dark outline.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        /// <param name="x">Horizontal center in pixels.</param>
        /// <param name="y">Vertical center in pixels.</param>
        /// <param name="heading">Heading in degrees.</param>
        /// <param name="color">Fill color, as the altitude color on the map.</param>
        private void DrawAircraft(Canvas canvas, float x, float y, float heading, Color color)
        {
            float size = Dp(12);

            path.Reset();
            path.MoveTo(0f, -size);
            path.LineTo(size * 0.75f, size * 0.8f);
            path.LineTo(0f, size * 0.4f);
            path.LineTo(-size * 0.75f, size * 0.8f);
            path.Close();

            canvas.Save();
            canvas.Translate(x, y);
            canvas.Rotate(heading);
            aircraftPaint.SetStyle(Paint.Style.Fill);
            aircraftPaint.Color = color;
            canvas.DrawPath(path, aircraftPaint);
            aircraftPaint.SetStyle(Paint.Style.Stroke);
            aircraftPaint.StrokeWidth = Dp(1.5f);
            aircraftPaint.Color = Color.Argb(220, 0, 0, 0);
            canvas.DrawPath(path, aircraftPaint);
            canvas.Restore();
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

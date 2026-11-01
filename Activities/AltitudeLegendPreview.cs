using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Static preview of the altitude legend bar shown in the help popup. Uses the current thresholds and the current altitude unit, so it looks like the legend on the map.
    /// </summary>
    public sealed class AltitudeLegendPreview : View
    {
        /// <summary>
        /// Paint of the gradient bar.
        /// </summary>
        private readonly Paint barPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the labels.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark background imitating the map.
        /// </summary>
        private readonly Paint backgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public AltitudeLegendPreview(Context context) : base(context)
        {
            textPaint.TextAlign = Paint.Align.Center;
            textPaint.Color = Color.White;
            textPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
            backgroundPaint.Color = Color.Rgb(40, 44, 52);
        }

        /// <summary>
        /// Chooses the size of the view: full width and a fixed height.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(240), widthMeasureSpec), (int)Dp(56));
        }

        /// <summary>
        /// Draws the bar with the threshold labels the same way as the map does.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            double[] stops = AppSettings.AltitudeStopsMeters;
            int stepCount = AltitudePalette.StopColors.Length;
            float left = Dp(28);
            float right = Width - Dp(28);
            float barWidth = right - left;
            float barHeight = Dp(8);
            float top = Dp(30);

            bounds.Set(0f, 0f, Width, Height);
            canvas.DrawRoundRect(bounds, Dp(8), Dp(8), backgroundPaint);

            int[] colors = new int[stepCount + 1];
            float[] positions = new float[stepCount + 1];

            colors[0] = AltitudePalette.StopColors[0].ToArgb();
            positions[0] = 0f;

            for (int index = 0; index < stepCount; index++)
            {
                colors[index + 1] = AltitudePalette.StopColors[index].ToArgb();
                positions[index + 1] = (index + 1f) / stepCount;
            }

            using LinearGradient gradient = new LinearGradient(left, 0f, right, 0f, colors, positions, Shader.TileMode.Clamp!);

            barPaint.SetShader(gradient);
            bounds.Set(left, top, right, top + barHeight);
            canvas.DrawRoundRect(bounds, barHeight / 2f, barHeight / 2f, barPaint);
            barPaint.SetShader(null);

            textPaint.TextSize = Dp(9);

            for (int index = 0; index <= stepCount; index++)
            {
                double meters = index == 0 ? 0.0 : stops[Math.Min(index - 1, stops.Length - 1)];

                canvas.DrawText(AltitudeFormatter.FormatLegend(meters, index == stepCount), left + barWidth * index / stepCount, top - Dp(6), textPaint);
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

using AdsbCarMap.Settings;
using Android.Graphics;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws the altitude legend bar on a canvas.
    /// </summary>
    internal sealed class AltitudeLegendRenderer
    {
        /// <summary>
        /// Paint used to draw the gradient bar of the altitude legend.
        /// </summary>
        private readonly Paint legendPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the dark outline of the altitude legend bar.
        /// </summary>
        private readonly Paint legendOutlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the altitude legend labels.
        /// </summary>
        private readonly Paint legendTextPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle of the altitude legend bar, reused every frame.
        /// </summary>
        private readonly RectF legendBounds = new RectF();

        /// <summary>
        /// Initializes the legend paints.
        /// </summary>
        public AltitudeLegendRenderer()
        {
            legendTextPaint.Color = Color.White;
            legendTextPaint.TextAlign = Paint.Align.Center;
            legendTextPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Draws the altitude legend above the bottom edge: a gradient bar with the same colors and thresholds as the aircraft icons and trails. The thresholds are spaced evenly, because the colors blend linearly between them.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <param name="stops">Altitude thresholds in meters, spaced evenly on the bar.</param>
        public void Draw(Canvas canvas, int width, int height, float screenDensity, double[] stops)
        {
            int stepCount = AltitudePalette.StopColors.Length;
            float barWidth = Math.Min(width - 32f * screenDensity, Math.Clamp(width * 0.4f, 240f * screenDensity, 400f * screenDensity));
            float barHeight = 7f * screenDensity;
            float left = (width - barWidth) / 2f;
            float bottom = height - 8f * screenDensity - 16f * screenDensity;
            float top = bottom - barHeight;
            float outline = screenDensity;

            int[] colors = new int[stepCount + 1];
            float[] positions = new float[stepCount + 1];

            colors[0] = AltitudePalette.StopColors[0].ToArgb();
            positions[0] = 0f;

            for (int index = 0; index < stepCount; index++)
            {
                colors[index + 1] = AltitudePalette.StopColors[index].ToArgb();
                positions[index + 1] = (index + 1f) / stepCount;
            }

            legendBounds.Set(left - outline, top - outline, left + barWidth + outline, bottom + outline);
            legendOutlinePaint.Color = Color.Argb(170, 0, 0, 0);
            canvas.DrawRoundRect(legendBounds, barHeight / 2f + outline, barHeight / 2f + outline, legendOutlinePaint);

            using LinearGradient gradient = new LinearGradient(left, 0f, left + barWidth, 0f, colors, positions, Shader.TileMode.Clamp!);

            legendPaint.SetShader(gradient);
            legendBounds.Set(left, top, left + barWidth, bottom);
            canvas.DrawRoundRect(legendBounds, barHeight / 2f, barHeight / 2f, legendPaint);
            legendPaint.SetShader(null);

            legendTextPaint.TextSize = 9f * screenDensity;

            for (int index = 0; index <= stepCount; index++)
            {
                double meters = index == 0 ? 0.0 : stops[Math.Min(index - 1, stops.Length - 1)];
                string text = AltitudeFormatter.FormatLegend(meters, index == stepCount);

                canvas.DrawText(text, left + barWidth * index / stepCount, top - 3f * screenDensity, legendTextPaint);
            }
        }
    }
}

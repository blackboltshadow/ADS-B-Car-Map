using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Draws an aircraft trail: a dark outline under a line in the altitude colors that becomes more transparent toward the oldest point. Shared by the map and the trail preview on the settings screen, so both always look the same.
    /// </summary>
    public sealed class TrailPainter
    {
        /// <summary>
        /// Smallest sum of the RGB channel differences between the ends of a segment for which the segment is drawn with a gradient instead of a solid color.
        /// </summary>
        private const int GradientMinimumDifference = 6;

        /// <summary>
        /// Paint of the colored trail line.
        /// </summary>
        private readonly Paint linePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the dark outline under the trail.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Path of the outline, reused between calls.
        /// </summary>
        private readonly AndroidPath outlinePath = new AndroidPath();

        /// <summary>
        /// Creates the painter.
        /// </summary>
        public TrailPainter()
        {
            linePaint.SetStyle(Paint.Style.Stroke);
            linePaint.StrokeCap = Paint.Cap.Round;
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.StrokeCap = Paint.Cap.Round;
            outlinePaint.StrokeJoin = Paint.Join.Round;
        }

        /// <summary>
        /// Draws a trail through the given points.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="xs">Horizontal positions of the points in pixels, from the oldest to the newest.</param>
        /// <param name="ys">Vertical positions of the points in pixels, in the same order.</param>
        /// <param name="colors">Altitude colors of the points, in the same order.</param>
        /// <param name="lineWidth">Width of the trail line in pixels.</param>
        /// <param name="outlineWidth">Width of the dark outline on each side of the line in pixels.</param>
        /// <param name="outlineAlpha">Opacity of the outline from 0 to 255.</param>
        /// <param name="minAlpha">Opacity of the oldest segment from 0 to 255.</param>
        /// <param name="maxAlpha">Opacity of the newest segment from 0 to 255.</param>
        public void Draw(Canvas canvas, float[] xs, float[] ys, Color[] colors, float lineWidth, float outlineWidth, int outlineAlpha, int minAlpha, int maxAlpha)
        {
            int segmentCount = xs.Length - 1;

            if (segmentCount < 1)
            {
                return;
            }

            linePaint.StrokeWidth = lineWidth;
            outlinePaint.StrokeWidth = lineWidth + 2.0f * outlineWidth;
            outlinePaint.Color = Color.Argb(outlineAlpha, 0, 0, 0);

            outlinePath.Reset();
            outlinePath.MoveTo(xs[0], ys[0]);

            for (int index = 1; index <= segmentCount; index++)
            {
                outlinePath.LineTo(xs[index], ys[index]);
            }

            canvas.DrawPath(outlinePath, outlinePaint);

            Color startColor = colors[0];

            for (int index = 1; index <= segmentCount; index++)
            {
                Color endColor = colors[index];
                int startAlpha = minAlpha + (maxAlpha - minAlpha) * (index - 1) / segmentCount;
                int endAlpha = minAlpha + (maxAlpha - minAlpha) * index / segmentCount;
                int difference = Math.Abs(startColor.R - endColor.R) + Math.Abs(startColor.G - endColor.G) + Math.Abs(startColor.B - endColor.B);

                if (difference > GradientMinimumDifference)
                {
                    using LinearGradient gradient = new LinearGradient(
                        xs[index - 1],
                        ys[index - 1],
                        xs[index],
                        ys[index],
                        Color.Argb(startAlpha, startColor.R, startColor.G, startColor.B),
                        Color.Argb(endAlpha, endColor.R, endColor.G, endColor.B),
                        Shader.TileMode.Clamp!);

                    linePaint.SetShader(gradient);
                    canvas.DrawLine(xs[index - 1], ys[index - 1], xs[index], ys[index], linePaint);
                    linePaint.SetShader(null);
                }
                else
                {
                    linePaint.Color = Color.Argb(endAlpha, endColor.R, endColor.G, endColor.B);
                    canvas.DrawLine(xs[index - 1], ys[index - 1], xs[index], ys[index], linePaint);
                }

                startColor = endColor;
            }
        }
    }
}

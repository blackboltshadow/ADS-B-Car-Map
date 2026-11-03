using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Flight progress bar: a line from the departure airport to the arrival airport with an aircraft marker at the point already flown.
    /// </summary>
    public sealed class RouteProgressView : View
    {
        /// <summary>
        /// Paint of the whole route.
        /// </summary>
        private readonly Paint trackPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the part of the route already flown.
        /// </summary>
        private readonly Paint progressPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the airport circles.
        /// </summary>
        private readonly Paint endPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the aircraft marker.
        /// </summary>
        private readonly Paint markerPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Shape of the aircraft marker pointing right.
        /// </summary>
        private readonly Android.Graphics.Path markerPath = new Android.Graphics.Path();

        /// <summary>
        /// Fraction of the route already flown, from 0 to 1.
        /// </summary>
        private float progress;

        /// <summary>
        /// Creates the progress bar view.
        /// </summary>
        /// <param name="context">Activity context.</param>
        public RouteProgressView(Context context) : base(context)
        {
            trackPaint.Color = Color.Argb(110, 255, 255, 255);
            trackPaint.SetStyle(Paint.Style.Stroke);
            trackPaint.StrokeCap = Paint.Cap.Round;

            progressPaint.Color = Color.Rgb(47, 128, 237);
            progressPaint.SetStyle(Paint.Style.Stroke);
            progressPaint.StrokeCap = Paint.Cap.Round;

            endPaint.Color = Color.White;

            markerPaint.Color = Color.Rgb(120, 190, 255);
        }

        /// <summary>
        /// Sets the fraction of the route flown and refreshes the view.
        /// </summary>
        /// <param name="value">Fraction of the route flown, limited to the range from 0 to 1.</param>
        public void SetProgress(float value)
        {
            progress = Math.Clamp(value, 0.0f, 1.0f);
            Invalidate();
        }

        /// <summary>
        /// Draws the route line, the airport circles and the aircraft marker.
        /// </summary>
        /// <param name="canvas">View canvas.</param>
        protected override void OnDraw(Canvas? canvas)
        {
            if (canvas == null)
            {
                return;
            }

            base.OnDraw(canvas);

            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;
            float centerY = Height / 2.0f;
            float left = 8.0f * density;
            float right = Width - 8.0f * density;
            float markerX = left + (right - left) * progress;

            trackPaint.StrokeWidth = 3.0f * density;
            progressPaint.StrokeWidth = 3.0f * density;

            canvas.DrawLine(left, centerY, right, centerY, trackPaint);
            canvas.DrawLine(left, centerY, markerX, centerY, progressPaint);
            canvas.DrawCircle(left, centerY, 4.5f * density, endPaint);
            canvas.DrawCircle(right, centerY, 4.5f * density, endPaint);

            markerPath.Reset();
            markerPath.MoveTo(markerX + 9.0f * density, centerY);
            markerPath.LineTo(markerX - 6.0f * density, centerY - 6.0f * density);
            markerPath.LineTo(markerX - 3.0f * density, centerY);
            markerPath.LineTo(markerX - 6.0f * density, centerY + 6.0f * density);
            markerPath.Close();
            canvas.DrawPath(markerPath, markerPaint);
        }
    }
}

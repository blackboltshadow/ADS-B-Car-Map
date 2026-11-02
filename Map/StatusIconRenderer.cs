using Android.Graphics;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// State shown by the status icon in the bottom left corner.
    /// </summary>
    internal enum StatusKind
    {
        /// <summary>
        /// Everything works.
        /// </summary>
        Ok,

        /// <summary>
        /// The last aircraft download failed.
        /// </summary>
        Offline,

        /// <summary>
        /// The server asked to wait before the next request.
        /// </summary>
        RateLimited,

        /// <summary>
        /// The aircraft list is being downloaded.
        /// </summary>
        SearchingAircraft,

        /// <summary>
        /// Details (route) of the selected aircraft are being downloaded.
        /// </summary>
        FetchingDetails,

        /// <summary>
        /// Map tiles are being downloaded.
        /// </summary>
        LoadingMap,
    }

    /// <summary>
    /// Draws the round status icon on a canvas.
    /// </summary>
    internal sealed class StatusIconRenderer
    {
        /// <summary>
        /// Duration of one full turn of the loading indicator spinner, in milliseconds.
        /// </summary>
        private const int SpinnerTurnMilliseconds = 900;

        /// <summary>
        /// Paint used to draw the spinner of the loading indicator.
        /// </summary>
        private readonly Paint spinnerPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle of the loading indicator spinner, reused every frame.
        /// </summary>
        private readonly RectF spinnerBounds = new RectF();

        /// <summary>
        /// Paint used to fill the background circle of the status icon.
        /// </summary>
        private readonly Paint statusFillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Initializes the spinner paint.
        /// </summary>
        public StatusIconRenderer()
        {
            spinnerPaint.Color = Color.White;
            spinnerPaint.SetStyle(Paint.Style.Stroke);
            spinnerPaint.StrokeCap = Paint.Cap.Round;
            spinnerPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);
        }

        /// <summary>
        /// Draws the round status icon: a green check, a red exclamation mark, an orange pause, a radar, a magnifier or a map spinner.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="status">State to show.</param>
        /// <param name="centerX">Horizontal center of the icon in pixels.</param>
        /// <param name="centerY">Vertical center of the icon in pixels.</param>
        /// <param name="radius">Radius of the icon background in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        public void Draw(Canvas canvas, StatusKind status, float centerX, float centerY, float radius, float screenDensity)
        {
            switch (status)
            {
                case StatusKind.Ok:
                    statusFillPaint.Color = Color.Rgb(46, 160, 67);
                    break;
                case StatusKind.Offline:
                    statusFillPaint.Color = Color.Rgb(214, 48, 49);
                    break;
                case StatusKind.RateLimited:
                    statusFillPaint.Color = Color.Rgb(230, 126, 34);
                    break;
                default:
                    statusFillPaint.Color = Color.Rgb(41, 121, 255);
                    break;
            }

            canvas.DrawCircle(centerX, centerY, radius, statusFillPaint);

            spinnerPaint.StrokeWidth = 1.8f * screenDensity;

            float phase = (Environment.TickCount64 % SpinnerTurnMilliseconds) * 360f / SpinnerTurnMilliseconds;

            switch (status)
            {
                case StatusKind.Ok:
                    canvas.DrawLine(centerX - 0.45f * radius, centerY, centerX - 0.12f * radius, centerY + 0.38f * radius, spinnerPaint);
                    canvas.DrawLine(centerX - 0.12f * radius, centerY + 0.38f * radius, centerX + 0.5f * radius, centerY - 0.38f * radius, spinnerPaint);
                    break;
                case StatusKind.Offline:
                    canvas.DrawLine(centerX, centerY - 0.5f * radius, centerX, centerY + 0.1f * radius, spinnerPaint);
                    canvas.DrawPoint(centerX, centerY + 0.5f * radius, spinnerPaint);
                    break;
                case StatusKind.RateLimited:
                    canvas.DrawLine(centerX - 0.27f * radius, centerY - 0.4f * radius, centerX - 0.27f * radius, centerY + 0.4f * radius, spinnerPaint);
                    canvas.DrawLine(centerX + 0.27f * radius, centerY - 0.4f * radius, centerX + 0.27f * radius, centerY + 0.4f * radius, spinnerPaint);
                    break;
                case StatusKind.SearchingAircraft:
                    {
                        float sweepRadius = 0.6f * radius;
                        double angle = phase * Math.PI / 180.0;

                        canvas.DrawCircle(centerX, centerY, sweepRadius, spinnerPaint);
                        canvas.DrawLine(centerX, centerY, centerX + sweepRadius * (float)Math.Cos(angle), centerY + sweepRadius * (float)Math.Sin(angle), spinnerPaint);
                        break;
                    }

                case StatusKind.FetchingDetails:
                    canvas.DrawCircle(centerX - 0.1f * radius, centerY - 0.1f * radius, 0.36f * radius, spinnerPaint);
                    canvas.DrawLine(centerX + 0.16f * radius, centerY + 0.16f * radius, centerX + 0.55f * radius, centerY + 0.55f * radius, spinnerPaint);
                    break;
                case StatusKind.LoadingMap:
                    spinnerBounds.Set(centerX - 0.55f * radius, centerY - 0.55f * radius, centerX + 0.55f * radius, centerY + 0.55f * radius);
                    canvas.DrawArc(spinnerBounds, phase, 270f, false, spinnerPaint);
                    break;
            }
        }
    }
}

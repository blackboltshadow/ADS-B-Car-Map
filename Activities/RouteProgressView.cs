using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Pasek postępu lotu: linia od lotniska wylotu do lotniska przylotu ze znacznikiem samolotu w miejscu, które już przeleciał.
    /// </summary>
    public sealed class RouteProgressView : View
    {
        /// <summary>
        /// Pędzel całej trasy.
        /// </summary>
        private readonly Paint trackPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Pędzel przebytej części trasy.
        /// </summary>
        private readonly Paint progressPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Pędzel kółek lotnisk.
        /// </summary>
        private readonly Paint endPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Pędzel znacznika samolotu.
        /// </summary>
        private readonly Paint markerPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Kształt znacznika samolotu skierowanego w prawo.
        /// </summary>
        private readonly Android.Graphics.Path markerPath = new Android.Graphics.Path();

        /// <summary>
        /// Przebyta część trasy od 0 do 1.
        /// </summary>
        private float progress;

        /// <summary>
        /// Tworzy widok paska postępu.
        /// </summary>
        /// <param name="context">Kontekst Activity.</param>
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
        /// Ustawia przebytą część trasy i odświeża widok.
        /// </summary>
        /// <param name="value">Przebyta część trasy, ograniczana do zakresu od 0 do 1.</param>
        public void SetProgress(float value)
        {
            progress = Math.Clamp(value, 0.0f, 1.0f);
            Invalidate();
        }

        /// <summary>
        /// Rysuje linię trasy, kółka lotnisk i znacznik samolotu.
        /// </summary>
        /// <param name="canvas">Płótno widoku.</param>
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

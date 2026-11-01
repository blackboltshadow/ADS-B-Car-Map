using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Control for choosing the range of zoom levels in which the airports are visible. The scale covers the whole zoom range of the map from the settings, with a handle for the zoom level from which the airports appear and a handle for the level above which they disappear. The right end of the scale means no upper limit.
    /// </summary>
    public sealed class ZoomRangeView : View
    {
        /// <summary>
        /// Value stored as the upper limit when the right handle is at the end of the scale, meaning that the airports are visible at every higher zoom.
        /// </summary>
        public const int NoUpperLimit = 18;

        /// <summary>
        /// Zoom level from which the airports appear.
        /// </summary>
        private int from = 5;

        /// <summary>
        /// Zoom level above which the airports disappear, or <see cref="NoUpperLimit"/>.
        /// </summary>
        private int to = NoUpperLimit;

        /// <summary>
        /// Handle being dragged: 0 the left one, 1 the right one, -1 none.
        /// </summary>
        private int activeHandle = -1;

        /// <summary>
        /// Paint of the scale and the handles.
        /// </summary>
        private readonly Paint linePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the handles fill and of the marks.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the texts.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Creates the control.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public ZoomRangeView(Context context) : base(context)
        {
            linePaint.SetStyle(Paint.Style.Stroke);
            linePaint.StrokeCap = Paint.Cap.Round;
            fillPaint.SetStyle(Paint.Style.Fill);
            textPaint.TextAlign = Paint.Align.Center;
        }

        /// <summary>
        /// Event raised after the user changed the range.
        /// </summary>
        public event System.Action? RangeChanged;

        /// <summary>
        /// Returns one end of the range.
        /// </summary>
        /// <param name="index">0 for the zoom from which the airports appear, 1 for the upper limit.</param>
        /// <returns>Zoom level, the upper limit may be <see cref="NoUpperLimit"/>.</returns>
        public int GetValue(int index)
        {
            return index == 0 ? from : to;
        }

        /// <summary>
        /// Sets one end of the range without raising <see cref="RangeChanged"/>.
        /// </summary>
        /// <param name="index">0 for the zoom from which the airports appear, 1 for the upper limit.</param>
        /// <param name="value">Zoom level.</param>
        public void SetValue(int index, int value)
        {
            if (index == 0)
            {
                from = value;
            }
            else
            {
                to = value;
            }

            Invalidate();
        }

        /// <summary>
        /// Chooses the size of the view: full width and a fixed height.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(260), widthMeasureSpec), (int)Dp(84));
        }

        /// <summary>
        /// Draws the scale, the selected range, the marks of the airport classes and the handles.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            int scaleMin = AppSettings.MinZoom;
            int scaleMax = AppSettings.MaxZoom;
            float left = Dp(24);
            float right = Width - Dp(24);
            float lineY = Dp(30);
            int shownFrom = Math.Clamp(from, scaleMin, scaleMax);
            int shownTo = Math.Clamp(to, scaleMin, scaleMax);
            float fromX = ToX(shownFrom, scaleMin, scaleMax, left, right);
            float toX = ToX(shownTo, scaleMin, scaleMax, left, right);

            linePaint.StrokeWidth = Dp(4);
            linePaint.Color = Color.Argb(90, 255, 255, 255);
            canvas.DrawLine(left, lineY, right, lineY, linePaint);
            linePaint.Color = Color.Rgb(110, 180, 255);
            canvas.DrawLine(fromX, lineY, toX, lineY, linePaint);

            textPaint.TextSize = Dp(10);
            textPaint.Color = Color.Rgb(180, 180, 180);
            linePaint.StrokeWidth = Dp(1);
            linePaint.Color = Color.Argb(120, 255, 255, 255);

            for (int level = scaleMin; level <= scaleMax; level++)
            {
                float x = ToX(level, scaleMin, scaleMax, left, right);

                canvas.DrawLine(x, lineY + Dp(6), x, lineY + Dp(11), linePaint);
                canvas.DrawText(level.ToString(System.Globalization.CultureInfo.InvariantCulture), x, lineY + Dp(24), textPaint);
            }


            DrawHandle(canvas, fromX, lineY, activeHandle == 0);
            DrawHandle(canvas, toX, lineY, activeHandle == 1);

            textPaint.TextSize = Dp(11);
            textPaint.Color = Color.White;
            canvas.DrawText(
                "Widoczne od zoomu " + shownFrom.ToString(System.Globalization.CultureInfo.InvariantCulture) + (to >= scaleMax ? " bez górnego limitu" : " do zoomu " + shownTo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Width / 2f,
                Dp(76),
                textPaint);
        }

        /// <summary>
        /// Draws a handle of the range.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        /// <param name="x">Horizontal center of the handle in pixels.</param>
        /// <param name="y">Vertical center of the handle in pixels.</param>
        /// <param name="active">Whether the handle is being dragged.</param>
        private void DrawHandle(Canvas canvas, float x, float y, bool active)
        {
            float radius = Dp(active ? 13 : 11);

            fillPaint.Color = Color.White;
            canvas.DrawCircle(x, y, radius, fillPaint);
            linePaint.StrokeWidth = Dp(2.5f);
            linePaint.Color = Color.Rgb(110, 180, 255);
            canvas.DrawCircle(x, y, radius, linePaint);
        }

        /// <summary>
        /// Handles dragging of the two handles.
        /// </summary>
        /// <param name="e">Touch event.</param>
        /// <returns>True when the event was used.</returns>
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null || !Enabled)
            {
                return false;
            }

            int scaleMin = AppSettings.MinZoom;
            int scaleMax = AppSettings.MaxZoom;
            float left = Dp(24);
            float right = Width - Dp(24);

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    {
                        float fromX = ToX(Math.Clamp(from, scaleMin, scaleMax), scaleMin, scaleMax, left, right);
                        float toX = ToX(Math.Clamp(to, scaleMin, scaleMax), scaleMin, scaleMax, left, right);
                        float fromDistance = Math.Abs(e.GetX() - fromX);
                        float toDistance = Math.Abs(e.GetX() - toX);

                        if (Math.Min(fromDistance, toDistance) > Dp(32))
                        {
                            return false;
                        }

                        activeHandle = fromDistance < toDistance || (fromDistance == toDistance && e.GetX() < fromX) ? 0 : 1;
                        Parent?.RequestDisallowInterceptTouchEvent(true);
                        Invalidate();

                        return true;
                    }

                case MotionEventActions.Move:
                    {
                        if (activeHandle < 0)
                        {
                            return false;
                        }

                        float fraction = Math.Clamp((e.GetX() - left) / (right - left), 0f, 1f);
                        int level = (int)Math.Round(scaleMin + fraction * (scaleMax - scaleMin));
                        bool changed;

                        if (activeHandle == 0)
                        {
                            int upper = Math.Clamp(to, scaleMin, scaleMax);
                            int next = Math.Min(level, upper);

                            changed = next != from;
                            from = next;
                        }
                        else
                        {
                            int lower = Math.Clamp(from, scaleMin, scaleMax);
                            int next = Math.Max(level, lower);

                            next = next >= scaleMax ? NoUpperLimit : next;
                            changed = next != to;
                            to = next;
                        }

                        Invalidate();

                        if (changed)
                        {
                            RangeChanged?.Invoke();
                        }

                        return true;
                    }

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    activeHandle = -1;
                    Parent?.RequestDisallowInterceptTouchEvent(false);
                    Invalidate();

                    return true;
            }

            return base.OnTouchEvent(e);
        }

        /// <summary>
        /// Converts a zoom level to a horizontal position on the scale.
        /// </summary>
        /// <param name="level">Zoom level.</param>
        /// <param name="scaleMin">Lowest zoom of the scale.</param>
        /// <param name="scaleMax">Highest zoom of the scale.</param>
        /// <param name="left">Left end of the scale in pixels.</param>
        /// <param name="right">Right end of the scale in pixels.</param>
        /// <returns>Position in pixels.</returns>
        private static float ToX(int level, int scaleMin, int scaleMax, float left, float right)
        {
            return scaleMax <= scaleMin ? left : left + (level - scaleMin) / (float)(scaleMax - scaleMin) * (right - left);
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

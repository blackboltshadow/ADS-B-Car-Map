using AdsbCarMap.Localization;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Control for choosing the lowest and the highest zoom level of the map on one scale. The handles cannot cross: the lower one always stays at least one level below the upper one.
    /// </summary>
    public sealed class MapZoomLimitsView : View
    {
        /// <summary>
        /// Lowest zoom level of the scale.
        /// </summary>
        public const int ScaleMin = 1;

        /// <summary>
        /// Highest zoom level of the scale.
        /// </summary>
        public const int ScaleMax = 18;

        /// <summary>
        /// Smallest distance between the handles in zoom levels.
        /// </summary>
        private const int MinGap = 1;

        /// <summary>
        /// Lowest zoom level allowed on the map.
        /// </summary>
        private int lower = 4;

        /// <summary>
        /// Highest zoom level allowed on the map.
        /// </summary>
        private int upper = 13;

        /// <summary>
        /// Handle being dragged: 0 the left one, 1 the right one, -1 none.
        /// </summary>
        private int activeHandle = -1;

        /// <summary>
        /// Paint of the scale and the handles.
        /// </summary>
        private readonly Paint linePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the handles fill.
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
        public MapZoomLimitsView(Context context) : base(context)
        {
            linePaint.SetStyle(Paint.Style.Stroke);
            linePaint.StrokeCap = Paint.Cap.Round;
            fillPaint.SetStyle(Paint.Style.Fill);
            textPaint.TextAlign = Paint.Align.Center;
        }

        /// <summary>
        /// Event raised after the user changed the limits.
        /// </summary>
        public event System.Action? RangeChanged;

        /// <summary>
        /// Returns one end of the range.
        /// </summary>
        /// <param name="index">0 for the lowest zoom, 1 for the highest zoom.</param>
        /// <returns>Zoom level.</returns>
        public int GetValue(int index)
        {
            return index == 0 ? lower : upper;
        }

        /// <summary>
        /// Sets one end of the range without raising <see cref="RangeChanged"/>. When the value would make the handles cross, the other end is moved away.
        /// </summary>
        /// <param name="index">0 for the lowest zoom, 1 for the highest zoom.</param>
        /// <param name="value">Zoom level.</param>
        public void SetValue(int index, int value)
        {
            int clamped = Math.Clamp(value, ScaleMin, ScaleMax);

            if (index == 0)
            {
                lower = clamped;
                upper = Math.Max(upper, lower + MinGap);
            }
            else
            {
                upper = clamped;
                lower = Math.Min(lower, upper - MinGap);
            }

            lower = Math.Max(lower, ScaleMin);
            upper = Math.Min(upper, ScaleMax);
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
        /// Draws the scale, the selected range and the handles.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float left = Dp(24);
            float right = Width - Dp(24);
            float lineY = Dp(30);
            float lowerX = ToX(lower, left, right);
            float upperX = ToX(upper, left, right);

            linePaint.StrokeWidth = Dp(4);
            linePaint.Color = Color.Argb(90, 255, 255, 255);
            canvas.DrawLine(left, lineY, right, lineY, linePaint);
            linePaint.Color = Color.Rgb(110, 180, 255);
            canvas.DrawLine(lowerX, lineY, upperX, lineY, linePaint);

            textPaint.TextSize = Dp(10);
            textPaint.Color = Color.Rgb(180, 180, 180);
            linePaint.StrokeWidth = Dp(1);
            linePaint.Color = Color.Argb(120, 255, 255, 255);

            for (int level = ScaleMin; level <= ScaleMax; level++)
            {
                float x = ToX(level, left, right);

                canvas.DrawLine(x, lineY + Dp(6), x, lineY + Dp(11), linePaint);
                canvas.DrawText(level.ToString(System.Globalization.CultureInfo.InvariantCulture), x, lineY + Dp(24), textPaint);
            }

            DrawHandle(canvas, lowerX, lineY, activeHandle == 0);
            DrawHandle(canvas, upperX, lineY, activeHandle == 1);

            textPaint.TextSize = Dp(11);
            textPaint.Color = Color.White;
            canvas.DrawText(
                string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc.T("Zoom mapy od {0} do {1}"), lower, upper),
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

            float left = Dp(24);
            float right = Width - Dp(24);

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    {
                        float lowerDistance = Math.Abs(e.GetX() - ToX(lower, left, right));
                        float upperDistance = Math.Abs(e.GetX() - ToX(upper, left, right));

                        if (Math.Min(lowerDistance, upperDistance) > Dp(32))
                        {
                            return false;
                        }

                        activeHandle = lowerDistance < upperDistance || (lowerDistance == upperDistance && e.GetX() < ToX(lower, left, right)) ? 0 : 1;
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
                        int level = (int)Math.Round(ScaleMin + fraction * (ScaleMax - ScaleMin));
                        bool changed;

                        if (activeHandle == 0)
                        {
                            int next = Math.Min(level, upper - MinGap);

                            changed = next != lower;
                            lower = next;
                        }
                        else
                        {
                            int next = Math.Max(level, lower + MinGap);

                            changed = next != upper;
                            upper = next;
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
        /// <param name="left">Left end of the scale in pixels.</param>
        /// <param name="right">Right end of the scale in pixels.</param>
        /// <returns>Position in pixels.</returns>
        private static float ToX(int level, float left, float right)
        {
            return left + (level - ScaleMin) / (float)(ScaleMax - ScaleMin) * (right - left);
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

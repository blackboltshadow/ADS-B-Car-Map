using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Slider with two thumbs that chooses a range of steps, e.g. a band of heights: the left thumb is the beginning and the right thumb the end. The framework has no such control. The view only holds step numbers; the caller turns them into values and texts.
    /// </summary>
    public sealed class RangeSliderView : View
    {
        /// <summary>
        /// Radius of a thumb in dp.
        /// </summary>
        private const float ThumbRadiusDp = 11f;

        /// <summary>
        /// Thickness of the track in dp.
        /// </summary>
        private const float TrackDp = 4f;

        /// <summary>
        /// Height of the view in dp, enough for a thumb to be touched with a finger.
        /// </summary>
        private const float HeightDp = 48f;

        /// <summary>
        /// Color of the part of the track outside the range.
        /// </summary>
        private static readonly Color TrackColor = Color.Rgb(90, 90, 90);

        /// <summary>
        /// Color of the part of the track inside the range and of the thumbs.
        /// </summary>
        private static readonly Color RangeColor = Color.Rgb(120, 190, 255);

        /// <summary>
        /// Paint of the track and the thumbs.
        /// </summary>
        private readonly Paint paint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Density of the screen.
        /// </summary>
        private readonly float density;

        /// <summary>
        /// Number of the steps; at least two.
        /// </summary>
        private int stepCount = 2;

        /// <summary>
        /// Step of the left thumb.
        /// </summary>
        private int low;

        /// <summary>
        /// Step of the right thumb.
        /// </summary>
        private int high = 1;

        /// <summary>
        /// The thumb being dragged: 0 none, 1 left, 2 right.
        /// </summary>
        private int dragging;

        /// <summary>
        /// Creates a slider.
        /// </summary>
        /// <param name="context">Context of the view.</param>
        public RangeSliderView(Context context)
            : base(context)
        {
            density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        }

        /// <summary>
        /// Event raised while a thumb is dragged, with the steps of the left and the right thumb.
        /// </summary>
        public event Action<int, int>? RangeChanged;

        /// <summary>
        /// Event raised when the finger is lifted after the range was changed, with the steps of the left and the right thumb.
        /// </summary>
        public event Action<int, int>? RangeCommitted;

        /// <summary>
        /// Step of the left thumb.
        /// </summary>
        public int Low => low;

        /// <summary>
        /// Step of the right thumb.
        /// </summary>
        public int High => high;

        /// <summary>
        /// Sets the number of steps and the position of the thumbs.
        /// </summary>
        /// <param name="steps">Number of steps; values below two are raised to two.</param>
        /// <param name="lowStep">Step of the left thumb.</param>
        /// <param name="highStep">Step of the right thumb; not lower than the left one.</param>
        public void SetRange(int steps, int lowStep, int highStep)
        {
            stepCount = Math.Max(2, steps);
            low = Math.Clamp(lowStep, 0, stepCount - 1);
            high = Math.Clamp(highStep, low, stepCount - 1);
            Invalidate();
        }

        /// <summary>
        /// Gives the view its height and takes the whole width offered.
        /// </summary>
        /// <param name="widthMeasureSpec">Width constraint.</param>
        /// <param name="heightMeasureSpec">Height constraint.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(GetDefaultSize(SuggestedMinimumWidth, widthMeasureSpec), (int)(HeightDp * density));
        }

        /// <summary>
        /// Draws the track, the chosen part of it and the two thumbs.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            float centerY = Height / 2f;
            float lowX = XOf(low);
            float highX = XOf(high);

            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeCap = Paint.Cap.Round;
            paint.StrokeWidth = TrackDp * density;
            paint.Color = TrackColor;
            canvas.DrawLine(XOf(0), centerY, XOf(stepCount - 1), centerY, paint);
            paint.Color = RangeColor;
            canvas.DrawLine(lowX, centerY, highX, centerY, paint);

            paint.SetStyle(Paint.Style.Fill);
            canvas.DrawCircle(lowX, centerY, ThumbRadiusDp * density, paint);
            canvas.DrawCircle(highX, centerY, ThumbRadiusDp * density, paint);
        }

        /// <summary>
        /// Moves the thumb nearest to the finger. The parent is asked not to scroll while a thumb is held.
        /// </summary>
        /// <param name="e">Touch event.</param>
        /// <returns>True, because the view handles the gesture.</returns>
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null)
            {
                return false;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    Parent?.RequestDisallowInterceptTouchEvent(true);
                    dragging = PickThumb(e.GetX());
                    MoveThumb(e.GetX());

                    return true;
                case MotionEventActions.Move:
                    MoveThumb(e.GetX());

                    return true;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    MoveThumb(e.GetX());
                    dragging = 0;
                    Parent?.RequestDisallowInterceptTouchEvent(false);
                    RangeCommitted?.Invoke(low, high);

                    return true;
                default:
                    return base.OnTouchEvent(e);
            }
        }

        /// <summary>
        /// Chooses the thumb a touch at the given position grabs: the nearer one, and when both are at the same place the one on the side of the finger.
        /// </summary>
        /// <param name="x">Horizontal position of the finger.</param>
        /// <returns>1 for the left thumb, 2 for the right one.</returns>
        private int PickThumb(float x)
        {
            float toLow = Math.Abs(x - XOf(low));
            float toHigh = Math.Abs(x - XOf(high));

            if (toLow == toHigh)
            {
                return x < XOf(low) ? 1 : 2;
            }

            return toLow < toHigh ? 1 : 2;
        }

        /// <summary>
        /// Moves the grabbed thumb to the step under the finger without passing the other thumb.
        /// </summary>
        /// <param name="x">Horizontal position of the finger.</param>
        private void MoveThumb(float x)
        {
            int step = StepAt(x);
            int newLow = low;
            int newHigh = high;

            if (dragging == 1)
            {
                newLow = Math.Min(step, high);
            }
            else if (dragging == 2)
            {
                newHigh = Math.Max(step, low);
            }

            if (newLow == low && newHigh == high)
            {
                return;
            }

            low = newLow;
            high = newHigh;
            Invalidate();
            RangeChanged?.Invoke(low, high);
        }

        /// <summary>
        /// Gives the horizontal position of a step.
        /// </summary>
        /// <param name="step">Step number.</param>
        /// <returns>Position in pixels.</returns>
        private float XOf(int step)
        {
            float padding = ThumbRadiusDp * density + PaddingLeft;
            float usable = Width - padding - ThumbRadiusDp * density - PaddingRight;

            return padding + usable * step / (stepCount - 1);
        }

        /// <summary>
        /// Finds the step nearest to a horizontal position.
        /// </summary>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <returns>Step number.</returns>
        private int StepAt(float x)
        {
            float start = XOf(0);
            float usable = XOf(stepCount - 1) - start;

            if (usable <= 0f)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round((x - start) / usable * (stepCount - 1)), 0, stepCount - 1);
        }
    }
}

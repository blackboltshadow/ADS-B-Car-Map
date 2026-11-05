using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Handle at the top of a card that tells that the card can be made bigger: three short lines. When the card fills the screen the handle shows an arrow pointing down, which tells that it can be minimized. A tap switches the state, and dragging the handle reports how far the finger moved, so that the card can follow it.
    /// </summary>
    public sealed class ExpandHandleView : View
    {
        /// <summary>
        /// Length of a line of the handle in dp.
        /// </summary>
        private const float LineLengthDp = 28f;

        /// <summary>
        /// Distance between the lines of the handle in dp.
        /// </summary>
        private const float LineGapDp = 5f;

        /// <summary>
        /// Thickness of the lines in dp.
        /// </summary>
        private const float LineWidthDp = 2.5f;

        /// <summary>
        /// Half of the width of the arrow in dp.
        /// </summary>
        private const float ArrowHalfWidthDp = 14f;

        /// <summary>
        /// Height of the arrow in dp.
        /// </summary>
        private const float ArrowHeightDp = 7f;

        /// <summary>
        /// Paint of the lines and of the arrow.
        /// </summary>
        private readonly Paint paint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Position of the finger on the screen when it touched the handle. The position on the screen is used, because the handle moves together with the card while it is dragged.
        /// </summary>
        private float downY;

        /// <summary>
        /// Whether the finger has moved far enough for the gesture to be a drag.
        /// </summary>
        private bool isDragging;

        /// <summary>
        /// Whether the card is expanded.
        /// </summary>
        private bool isExpanded;

        /// <summary>
        /// Distance in pixels that the finger must travel before the gesture counts as a drag and not as a tap.
        /// </summary>
        private readonly float touchSlop;

        /// <summary>
        /// Creates the handle.
        /// </summary>
        /// <param name="context">Activity context.</param>
        public ExpandHandleView(Context context) : base(context)
        {
            Clickable = true;
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeCap = Paint.Cap.Round;
            paint.StrokeJoin = Paint.Join.Round;
            touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? 8f;
        }

        /// <summary>
        /// Event raised when the handle is tapped.
        /// </summary>
        public event System.Action? Tapped;

        /// <summary>
        /// Event raised when the finger has moved far enough for the gesture to be a drag.
        /// </summary>
        public event System.Action? DragStarted;

        /// <summary>
        /// Event raised while the handle is dragged, with the distance in pixels that the finger has moved since it touched the handle; negative when it moved up.
        /// </summary>
        public event System.Action<float>? Dragged;

        /// <summary>
        /// Event raised when the finger is lifted after a drag, with the distance in pixels that the finger has moved in total.
        /// </summary>
        public event System.Action<float>? DragEnded;

        /// <summary>
        /// Whether the card fills the screen, which is drawn as an arrow instead of the three lines.
        /// </summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded == value)
                {
                    return;
                }

                isExpanded = value;
                Invalidate();
            }
        }

        /// <inheritdoc />
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null)
            {
                return base.OnTouchEvent(e);
            }

            switch (e.Action)
            {
                case MotionEventActions.Down:
                    downY = e.RawY;
                    isDragging = false;
                    Parent?.RequestDisallowInterceptTouchEvent(true);

                    return true;
                case MotionEventActions.Move:
                    float moved = e.RawY - downY;

                    if (!isDragging && Math.Abs(moved) > touchSlop)
                    {
                        isDragging = true;
                        DragStarted?.Invoke();
                    }

                    if (isDragging)
                    {
                        Dragged?.Invoke(moved);
                    }

                    return true;
                case MotionEventActions.Up:
                    if (isDragging)
                    {
                        isDragging = false;
                        DragEnded?.Invoke(e.RawY - downY);
                    }
                    else
                    {
                        PerformClick();
                        Tapped?.Invoke();
                    }

                    return true;
                case MotionEventActions.Cancel:
                    if (isDragging)
                    {
                        isDragging = false;
                        DragEnded?.Invoke(e.RawY - downY);
                    }

                    return true;
                default:
                    return base.OnTouchEvent(e);
            }
        }

        /// <inheritdoc />
        public override bool PerformClick()
        {
            return base.PerformClick();
        }

        /// <inheritdoc />
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;
            float centerX = Width / 2f;
            float centerY = Height / 2f;

            paint.StrokeWidth = LineWidthDp * density;
            paint.Color = isExpanded ? Color.Rgb(120, 190, 255) : Color.Argb(200, 255, 255, 255);

            if (isExpanded)
            {
                float halfWidth = ArrowHalfWidthDp * density;
                float height = ArrowHeightDp * density;

                canvas.DrawLine(centerX - halfWidth, centerY - height / 2f, centerX, centerY + height / 2f, paint);
                canvas.DrawLine(centerX, centerY + height / 2f, centerX + halfWidth, centerY - height / 2f, paint);

                return;
            }

            float half = LineLengthDp * density / 2f;
            float gap = LineGapDp * density;

            for (int line = -1; line <= 1; line++)
            {
                canvas.DrawLine(centerX - half, centerY + line * gap, centerX + half, centerY + line * gap, paint);
            }
        }
    }
}

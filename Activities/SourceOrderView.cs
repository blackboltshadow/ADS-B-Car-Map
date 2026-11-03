using AdsbCarMap.Localization;
using AdsbCarMap.DataSources.Management.Selection;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// List of the aircraft sources in the order in which they are asked. The first row is the main source, the rows below it are asked in turn when the previous source gives no data. A row is moved by dragging its handle; the neighbors make room while the finger moves.
    /// </summary>
    public sealed class SourceOrderView : LinearLayout
    {
        /// <summary>
        /// Height of one row in dp.
        /// </summary>
        private const int RowHeightDp = 48;

        /// <summary>
        /// Gap between two rows in dp.
        /// </summary>
        private const int RowGapDp = 4;

        /// <summary>
        /// Names of the sources in the current order.
        /// </summary>
        private IReadOnlyList<string> order = new List<string>();

        /// <summary>
        /// Whether the sources below the main one are asked, which decides whether their rows are dimmed.
        /// </summary>
        private bool fallbackEnabled = true;

        /// <summary>
        /// Index of the row being dragged, or -1.
        /// </summary>
        private int dragIndex = -1;

        /// <summary>
        /// Screen position of the finger at the start of the drag.
        /// </summary>
        private float dragStartY;

        /// <summary>
        /// Index at which the dragged row would be dropped now.
        /// </summary>
        private int dragTarget = -1;

        /// <summary>
        /// Called when the user changes the order, with the new order.
        /// </summary>
        public event Action<IReadOnlyList<string>>? OrderChanged;

        /// <summary>
        /// Creates the control.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public SourceOrderView(Context context) : base(context)
        {
            Orientation = Orientation.Vertical;
        }

        /// <summary>
        /// Names of the sources in the current order.
        /// </summary>
        public IReadOnlyList<string> Order => order;

        /// <summary>
        /// Sets the order and redraws the rows. Does not call <see cref="OrderChanged"/>.
        /// </summary>
        /// <param name="newOrder">Names of the sources in the new order.</param>
        public void SetOrder(IReadOnlyList<string> newOrder)
        {
            order = new List<string>(newOrder);
            Rebuild();
        }

        /// <summary>
        /// Dims the rows below the main source when the fallback is switched off, because they are not asked then.
        /// </summary>
        /// <param name="enabled">Whether the next sources are asked when the previous one gives no data.</param>
        public void SetFallbackEnabled(bool enabled)
        {
            fallbackEnabled = enabled;
            Rebuild();
        }

        /// <summary>
        /// Recreates the rows from the current order.
        /// </summary>
        private void Rebuild()
        {
            RemoveAllViews();

            for (int index = 0; index < order.Count; index++)
            {
                AddView(CreateRow(index), CreateRowParams(index));
            }
        }

        /// <summary>
        /// Returns the layout parameters of a row.
        /// </summary>
        /// <param name="index">Index of the row.</param>
        /// <returns>The parameters with the fixed height and the gap above the row.</returns>
        private LayoutParams CreateRowParams(int index)
        {
            LayoutParams parameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(RowHeightDp));
            parameters.TopMargin = index == 0 ? 0 : Dp(RowGapDp);

            return parameters;
        }

        /// <summary>
        /// Creates the row of a source: the handle, the name and the badge telling its role.
        /// </summary>
        /// <param name="index">Index of the source in the order.</param>
        /// <returns>The row.</returns>
        private LinearLayout CreateRow(int index)
        {
            bool isMain = index == 0;
            LinearLayout row = new LinearLayout(Context);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(4), 0, Dp(10), 0);

            GradientDrawable background = new GradientDrawable();
            background.SetColor(isMain ? Color.Argb(70, 76, 175, 80) : Color.Argb(30, 255, 255, 255));
            background.SetStroke(Dp(1), isMain ? Color.Argb(190, 129, 199, 132) : Color.Argb(90, 255, 255, 255));
            background.SetCornerRadius(Dp(8));
            row.Background = background;
            row.Alpha = isMain || fallbackEnabled ? 1.0f : 0.45f;

            TextView handle = new TextView(Context);
            handle.Text = "≡";
            handle.TextSize = 26f;
            handle.Gravity = GravityFlags.Center;
            handle.SetTextColor(Color.White);
            handle.ContentDescription = Loc.T("Przeciągnij, aby zmienić kolejność: ") + order[index];
            handle.Touch += (sender, args) => OnHandleTouch(row, args);
            row.AddView(handle, new LayoutParams(Dp(44), ViewGroup.LayoutParams.MatchParent));

            TextView name = new TextView(Context);
            name.Text = order[index];
            name.TextSize = 15f;
            name.SetTextColor(Color.White);
            name.SetTypeface(null, isMain ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            row.AddView(name, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            TextView badge = new TextView(Context);
            badge.Text = isMain ? Loc.T("GŁÓWNE") : Loc.T("ZAPASOWE ") + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            badge.TextSize = 11f;
            badge.SetTextColor(isMain ? Color.Argb(255, 165, 214, 167) : Color.Argb(200, 255, 255, 255));
            badge.SetTypeface(null, TypefaceStyle.Bold);
            row.AddView(badge, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            return row;
        }

        /// <summary>
        /// Handles the touch of a row handle: the row follows the finger, the rows it passes make room and the drop applies the new order.
        /// </summary>
        /// <param name="row">Row whose handle is touched.</param>
        /// <param name="args">Touch event.</param>
        private void OnHandleTouch(LinearLayout row, View.TouchEventArgs args)
        {
            MotionEvent? motion = args.Event;

            if (motion == null)
            {
                return;
            }

            switch (motion.ActionMasked)
            {
                case MotionEventActions.Down:
                    dragIndex = IndexOfChild(row);
                    dragTarget = dragIndex;
                    dragStartY = motion.RawY;
                    row.Elevation = Dp(6);
                    Parent?.RequestDisallowInterceptTouchEvent(true);
                    args.Handled = true;
                    break;
                case MotionEventActions.Move:
                    if (dragIndex >= 0)
                    {
                        MoveDrag(row, motion.RawY - dragStartY);
                    }

                    args.Handled = true;
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (dragIndex >= 0)
                    {
                        FinishDrag(motion.ActionMasked == MotionEventActions.Up);
                    }

                    Parent?.RequestDisallowInterceptTouchEvent(false);
                    args.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Moves the dragged row with the finger and shifts the rows between its start and the current target.
        /// </summary>
        /// <param name="row">Dragged row.</param>
        /// <param name="deltaY">Distance the finger has moved since the start.</param>
        private void MoveDrag(LinearLayout row, float deltaY)
        {
            float step = Dp(RowHeightDp + RowGapDp);
            float minimum = -dragIndex * step;
            float maximum = (order.Count - 1 - dragIndex) * step;
            float limited = Math.Clamp(deltaY, minimum, maximum);

            row.TranslationY = limited;
            dragTarget = Math.Clamp(dragIndex + (int)Math.Round(limited / step), 0, order.Count - 1);

            for (int index = 0; index < ChildCount; index++)
            {
                View? other = GetChildAt(index);

                if (other == null || index == dragIndex)
                {
                    continue;
                }

                float shift = 0f;

                if (dragTarget > dragIndex && index > dragIndex && index <= dragTarget)
                {
                    shift = -step;
                }
                else if (dragTarget < dragIndex && index >= dragTarget && index < dragIndex)
                {
                    shift = step;
                }

                other.TranslationY = shift;
            }
        }

        /// <summary>
        /// Ends the drag: puts the rows back and, when the row was dropped at another position, applies the new order.
        /// </summary>
        /// <param name="drop">True when the finger was lifted, false when the gesture was cancelled.</param>
        private void FinishDrag(bool drop)
        {
            int from = dragIndex;
            int to = dragTarget;
            dragIndex = -1;
            dragTarget = -1;

            for (int index = 0; index < ChildCount; index++)
            {
                View? child = GetChildAt(index);

                if (child != null)
                {
                    child.TranslationY = 0f;
                    child.Elevation = 0f;
                }
            }

            if (!drop || from == to)
            {
                return;
            }

            order = SourceOrder.Move(order, from, to);

            Post(() =>
            {
                Rebuild();
                OrderChanged?.Invoke(order);
            });
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)Math.Round(value * Resources!.DisplayMetrics!.Density);
        }
    }
}

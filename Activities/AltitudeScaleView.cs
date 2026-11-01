using System.Globalization;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Editor of the altitude color scale. Shows the gradient bar with one draggable handle for every color threshold, the ground (zero) marker and a sample climbing trail drawn with the edited thresholds. Tapping a handle opens a dialog to type an exact value. The values are kept in meters and displayed in the unit from the settings.
    /// </summary>
    public sealed class AltitudeScaleView : View
    {
        /// <summary>
        /// Highest altitude of the scale in meters.
        /// </summary>
        private const double MaxMeters = 20000.0;

        /// <summary>
        /// Smallest distance between two neighboring thresholds in meters. It is also the lowest allowed value of the first threshold, so the orange zone never shrinks to zero and hides behind the gray ground marker.
        /// </summary>
        private const double MinimumGapMeters = 50.0;

        /// <summary>
        /// Snapping step in meters when the unit is metric.
        /// </summary>
        private const double MetricStepMeters = 50.0;

        /// <summary>
        /// Snapping step in feet when the unit is feet or flight level.
        /// </summary>
        private const double FeetStep = 100.0;

        /// <summary>
        /// Feet to meters conversion factor.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Number of segments of the sample trail.
        /// </summary>
        private const int PreviewSegments = 60;

        /// <summary>
        /// Threshold values in meters. Sorted lazily, see <see cref="EnsureSorted"/>.
        /// </summary>
        private readonly double[] values = new double[AltitudePalette.StopColors.Length];

        /// <summary>
        /// Whether <see cref="values"/> is known to be sorted.
        /// </summary>
        private bool sorted;

        /// <summary>
        /// Index of the handle being dragged, or -1.
        /// </summary>
        private int activeHandle = -1;

        /// <summary>
        /// Whether the finger moved far enough to count as a drag instead of a tap.
        /// </summary>
        private bool moved;

        /// <summary>
        /// Horizontal position of the touch start in pixels.
        /// </summary>
        private float downX;

        /// <summary>
        /// Paint of the gradient bar.
        /// </summary>
        private readonly Paint barPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the fills of handles and markers.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the outlines and the sample trail.
        /// </summary>
        private readonly Paint strokePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the text.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing the bar.
        /// </summary>
        private readonly RectF barBounds = new RectF();

        /// <summary>
        /// Event raised when the user changes a threshold.
        /// </summary>
        public event System.Action? ValuesChanged;

        /// <summary>
        /// Creates the editor.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public AltitudeScaleView(Context context) : base(context)
        {
            fillPaint.SetStyle(Paint.Style.Fill);
            strokePaint.SetStyle(Paint.Style.Stroke);
            strokePaint.StrokeCap = Paint.Cap.Round;
            textPaint.TextAlign = Paint.Align.Center;
        }

        /// <summary>
        /// Returns a threshold value. Thresholds are always in ascending order, so index 0 is the lowest one (orange).
        /// </summary>
        /// <param name="index">Index of the threshold.</param>
        /// <returns>Altitude in meters.</returns>
        public double GetValue(int index)
        {
            EnsureSorted();

            return values[index];
        }

        /// <summary>
        /// Sets a threshold value without raising <see cref="ValuesChanged"/>.
        /// </summary>
        /// <param name="index">Index of the setting being loaded.</param>
        /// <param name="meters">Altitude in meters.</param>
        public void SetValue(int index, double meters)
        {
            values[index] = meters;
            sorted = false;
            Invalidate();
        }

        /// <summary>
        /// Chooses the size of the view: full width and a fixed height.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(300), widthMeasureSpec), (int)Dp(190));
        }

        /// <summary>
        /// Draws the scale: labels, the gradient bar, the handles and the sample trail.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            EnsureSorted();

            float left = BarLeft();
            float right = BarRight();
            float barTop = Dp(70);
            float barBottom = barTop + Dp(22);
            float centerY = (barTop + barBottom) / 2f;
            float barWidth = right - left;
            int count = values.Length;

            int[] colors = new int[count + 2];
            float[] positions = new float[count + 2];

            colors[0] = AltitudePalette.StopColors[0].ToArgb();
            positions[0] = 0f;

            for (int index = 0; index < count; index++)
            {
                colors[index + 1] = AltitudePalette.StopColors[index].ToArgb();
                positions[index + 1] = Math.Min(1f, (float)ToFraction(values[index]));
            }

            colors[count + 1] = AltitudePalette.StopColors[count - 1].ToArgb();
            positions[count + 1] = 1f;

            using LinearGradient gradient = new LinearGradient(left, 0f, right, 0f, colors, positions, Shader.TileMode.Clamp!);

            barPaint.SetShader(gradient);
            barBounds.Set(left, barTop, right, barBottom);
            canvas.DrawRoundRect(barBounds, Dp(11), Dp(11), barPaint);
            barPaint.SetShader(null);

            textPaint.TextSize = Dp(10);
            textPaint.Color = Color.Rgb(180, 180, 180);
            fillPaint.Color = AltitudePalette.GroundColor;
            canvas.DrawCircle(left, centerY, Dp(7), fillPaint);
            canvas.DrawText("ziemia", left, barBottom + Dp(26), textPaint);

            strokePaint.StrokeWidth = Dp(2);

            for (int index = 0; index < count; index++)
            {
                float x = left + (float)ToFraction(values[index]) * barWidth;
                bool labelAbove = index % 2 == 0;
                float labelY = labelAbove ? barTop - Dp(26) : barBottom + Dp(26);

                strokePaint.Color = Color.Argb(110, 255, 255, 255);
                canvas.DrawLine(x, labelAbove ? labelY + Dp(4) : barBottom, x, labelAbove ? barTop : labelY - Dp(12), strokePaint);

                fillPaint.Color = AltitudePalette.StopColors[index];
                canvas.DrawCircle(x, centerY, Dp(index == activeHandle ? 13 : 11), fillPaint);
                strokePaint.Color = Color.White;
                canvas.DrawCircle(x, centerY, Dp(index == activeHandle ? 13 : 11), strokePaint);

                textPaint.Color = Color.White;
                canvas.DrawText(AltitudeFormatter.FormatLegend(values[index], false), x, labelY, textPaint);
            }

            DrawPreview(canvas, left, barWidth);
        }

        /// <summary>
        /// Draws a sample climbing trail below the bar, using exactly the same colors as the map.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        /// <param name="left">Left edge of the bar in pixels.</param>
        /// <param name="barWidth">Width of the bar in pixels.</param>
        private void DrawPreview(Canvas canvas, float left, float barWidth)
        {
            float previewBottom = Height - Dp(10);
            float previewHeight = Dp(34);

            textPaint.Color = Color.Rgb(180, 180, 180);
            textPaint.TextAlign = Paint.Align.Left;
            canvas.DrawText("Podgląd śladu wznoszącego się samolotu", left, previewBottom - previewHeight - Dp(8), textPaint);
            textPaint.TextAlign = Paint.Align.Center;

            strokePaint.StrokeWidth = Dp(4);

            for (int segment = 0; segment < PreviewSegments; segment++)
            {
                float fromFraction = segment / (float)PreviewSegments;
                float toFraction = (segment + 1) / (float)PreviewSegments;
                double middleMeters = FromFraction((fromFraction + toFraction) / 2f);

                strokePaint.Color = AltitudePalette.GetColor(middleMeters, values);
                canvas.DrawLine(
                    left + fromFraction * barWidth,
                    previewBottom - fromFraction * previewHeight,
                    left + toFraction * barWidth,
                    previewBottom - toFraction * previewHeight,
                    strokePaint);
            }
        }

        /// <summary>
        /// Handles dragging of the handles and tapping a handle to type its value.
        /// </summary>
        /// <param name="e">Touch event.</param>
        /// <returns>True when the event was used.</returns>
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null)
            {
                return false;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    activeHandle = FindHandle(e.GetX());

                    if (activeHandle < 0)
                    {
                        return false;
                    }

                    Parent?.RequestDisallowInterceptTouchEvent(true);
                    downX = e.GetX();
                    moved = false;
                    Invalidate();

                    return true;

                case MotionEventActions.Move:
                    if (activeHandle < 0)
                    {
                        return false;
                    }

                    if (Math.Abs(e.GetX() - downX) > ViewConfiguration.Get(Context!)!.ScaledTouchSlop)
                    {
                        moved = true;
                    }

                    if (moved)
                    {
                        MoveHandle(activeHandle, FromFraction((e.GetX() - BarLeft()) / (BarRight() - BarLeft())), true);
                    }

                    return true;

                case MotionEventActions.Up:
                    {
                        int handle = activeHandle;
                        bool wasTap = !moved;

                        activeHandle = -1;
                        Parent?.RequestDisallowInterceptTouchEvent(false);
                        Invalidate();

                        if (handle >= 0 && wasTap)
                        {
                            ShowValueDialog(handle);
                        }

                        return true;
                    }

                case MotionEventActions.Cancel:
                    activeHandle = -1;
                    Parent?.RequestDisallowInterceptTouchEvent(false);
                    Invalidate();

                    return true;
            }

            return base.OnTouchEvent(e);
        }

        /// <summary>
        /// Finds the handle closest to a horizontal touch position.
        /// </summary>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <returns>Index of the handle, or -1 when no handle is close enough.</returns>
        private int FindHandle(float x)
        {
            EnsureSorted();

            int best = -1;
            float bestDistance = Dp(28);

            for (int index = 0; index < values.Length; index++)
            {
                float distance = Math.Abs(BarLeft() + (float)ToFraction(values[index]) * (BarRight() - BarLeft()) - x);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            return best;
        }

        /// <summary>
        /// Moves a handle, snapping the value to the step of the unit and keeping it between its neighbors.
        /// </summary>
        /// <param name="index">Index of the handle.</param>
        /// <param name="meters">Requested altitude in meters.</param>
        /// <param name="snap">Whether to snap the value to the step of the unit.</param>
        private void MoveHandle(int index, double meters, bool snap)
        {
            double value = snap ? Snap(meters) : meters;
            double lower = index == 0 ? MinimumGapMeters : values[index - 1] + MinimumGapMeters;
            double upper = index == values.Length - 1 ? MaxMeters : values[index + 1] - MinimumGapMeters;

            value = Math.Round(Math.Clamp(value, lower, Math.Max(lower, upper)));

            if (Math.Abs(value - values[index]) < 0.5)
            {
                return;
            }

            values[index] = value;
            Invalidate();
            ValuesChanged?.Invoke();
        }

        /// <summary>
        /// Snaps an altitude to the step of the selected unit.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <returns>Snapped altitude in meters.</returns>
        private static double Snap(double meters)
        {
            if (AppSettings.AltitudeUnit == AltitudeUnit.Metric)
            {
                return Math.Round(meters / MetricStepMeters) * MetricStepMeters;
            }

            return Math.Round(meters / MetersPerFoot / FeetStep) * FeetStep * MetersPerFoot;
        }

        /// <summary>
        /// Opens a dialog in which the exact value of a threshold can be typed in the selected unit.
        /// </summary>
        /// <param name="index">Index of the handle.</param>
        private void ShowValueDialog(int index)
        {
            AltitudeUnit unit = AppSettings.AltitudeUnit;
            double meters = values[index];
            string unitName = unit == AltitudeUnit.Metric ? "m" : unit == AltitudeUnit.Feet ? "ft" : "FL";
            double shown = unit == AltitudeUnit.Metric ? meters : unit == AltitudeUnit.Feet ? meters / MetersPerFoot : meters / MetersPerFoot / 100.0;

            EditText input = new EditText(Context);
            input.InputType = InputTypes.ClassNumber;
            input.SetSingleLine(true);
            input.Text = Math.Round(shown).ToString("0", CultureInfo.InvariantCulture);
            input.SetSelectAllOnFocus(true);

            AlertDialog.Builder builder = new AlertDialog.Builder(Context)!;
            builder.SetTitle(AltitudePalette.StopNames[index] + " [" + unitName + "]");
            builder.SetView(input);
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.SetPositiveButton("OK", (sender, args) =>
            {
                if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed))
                {
                    double typedMeters = unit == AltitudeUnit.Metric ? typed : unit == AltitudeUnit.Feet ? typed * MetersPerFoot : typed * 100.0 * MetersPerFoot;

                    MoveHandle(index, typedMeters, false);
                }
            });
            builder.Show();
        }

        /// <summary>
        /// Sorts the thresholds when they were changed from outside.
        /// </summary>
        private void EnsureSorted()
        {
            if (!sorted)
            {
                Array.Sort(values);
                values[0] = Math.Max(values[0], MinimumGapMeters);
                sorted = true;
            }
        }

        /// <summary>
        /// Converts an altitude to a position on the bar. The scale is a square root, so the low thresholds do not crowd together.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <returns>Position from 0 to 1.</returns>
        private static double ToFraction(double meters)
        {
            return Math.Sqrt(Math.Clamp(meters, 0.0, MaxMeters) / MaxMeters);
        }

        /// <summary>
        /// Converts a position on the bar to an altitude.
        /// </summary>
        /// <param name="fraction">Position from 0 to 1.</param>
        /// <returns>Altitude in meters.</returns>
        private static double FromFraction(double fraction)
        {
            double clamped = Math.Clamp(fraction, 0.0, 1.0);

            return MaxMeters * clamped * clamped;
        }

        /// <summary>
        /// Left edge of the bar in pixels.
        /// </summary>
        /// <returns>Position in pixels.</returns>
        private float BarLeft()
        {
            return Dp(28);
        }

        /// <summary>
        /// Right edge of the bar in pixels.
        /// </summary>
        /// <returns>Position in pixels.</returns>
        private float BarRight()
        {
            return Width - Dp(28);
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

using Android.Graphics;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Style of a popup line.
    /// </summary>
    internal enum PopupLineStyle
    {
        /// <summary>
        /// Regular white text.
        /// </summary>
        Normal,

        /// <summary>
        /// Bold white text.
        /// </summary>
        Bold,

        /// <summary>
        /// Accent colored text.
        /// </summary>
        Accent,

        /// <summary>
        /// Progress bar with a description below it.
        /// </summary>
        Progress,
    }

    /// <summary>
    /// One line of the selected aircraft popup.
    /// </summary>
    /// <param name="Text">Text of the line.</param>
    /// <param name="Style">Style of the line.</param>
    /// <param name="Fraction">Filled part of the progress bar from 0 to 1, used by the progress style only.</param>
    internal readonly record struct PopupLine(string Text, PopupLineStyle Style, float Fraction);

    /// <summary>
    /// Measures and draws the selection popup on a canvas.
    /// </summary>
    internal sealed class PopupRenderer
    {
        /// <summary>
        /// Background color of the selected aircraft popup.
        /// </summary>
        private static readonly Color PopupBackgroundColor = Color.Argb(225, 24, 28, 34);

        /// <summary>
        /// Color of the accent lines and the progress bar of the popup.
        /// </summary>
        private static readonly Color PopupAccentColor = Color.Rgb(120, 190, 255);

        /// <summary>
        /// Text size of the popup lines in dp.
        /// </summary>
        private const float PopupTextSize = 15.0f;

        /// <summary>
        /// Popup margin from the screen edge and padding inside the popup, in dp.
        /// </summary>
        private const float PopupMargin = 12.0f;

        /// <summary>
        /// Popup distance from the bottom edge of the screen, in dp. Leaves room for the version number.
        /// </summary>
        private const float PopupBottomOffset = 30.0f;

        /// <summary>
        /// Maximum popup width in dp.
        /// </summary>
        private const float PopupMaxWidth = 380.0f;

        /// <summary>
        /// Maximum popup width as a part of the screen width.
        /// </summary>
        private const float PopupMaxWidthFraction = 0.5f;

        /// <summary>
        /// Popup line height as a multiple of the text size.
        /// </summary>
        private const float PopupLineSpacing = 1.35f;

        /// <summary>
        /// Height of the popup progress bar in dp.
        /// </summary>
        private const float PopupBarHeight = 8.0f;

        /// <summary>
        /// Height of the tab strip of the popup in dp.
        /// </summary>
        private const float TabStripHeight = 34.0f;

        /// <summary>
        /// Side of the square close button next to the popup in dp.
        /// </summary>
        private const float CloseButtonSize = 56.0f;

        /// <summary>
        /// Gap between the popup and its close button in dp.
        /// </summary>
        private const float CloseButtonGap = 8.0f;

        /// <summary>
        /// Paint used to draw the close button and its cross.
        /// </summary>
        private readonly Paint closeButtonPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the background of the selected aircraft popup.
        /// </summary>
        private readonly Paint popupBackgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the regular lines of the popup.
        /// </summary>
        private readonly Paint popupPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the bold lines of the popup.
        /// </summary>
        private readonly Paint popupBoldPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint used to draw the accent lines of the popup and the filled part of its progress bar.
        /// </summary>
        private readonly Paint popupAccentPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Initializes the popup paints.
        /// </summary>
        public PopupRenderer()
        {
            popupBackgroundPaint.Color = PopupBackgroundColor;

            popupPaint.Color = Color.White;

            popupBoldPaint.Color = Color.White;
            popupBoldPaint.SetTypeface(Typeface.DefaultBold);

            popupAccentPaint.Color = PopupAccentColor;

            closeButtonPaint.Color = PopupBackgroundColor;
        }

        /// <summary>
        /// Finds the area of the close button, which sits to the right of the popup at its top edge.
        /// </summary>
        /// <param name="area">Area of the popup.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Area of the button in screen pixels.</returns>
        public static RectF GetCloseButtonArea(RectF area, float screenDensity)
        {
            float left = area.Right + CloseButtonGap * screenDensity;

            return new RectF(left, area.Top, left + CloseButtonSize * screenDensity, area.Top + CloseButtonSize * screenDensity);
        }

        /// <summary>
        /// Draws the close button with a cross.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="area">Area of the popup.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        private void DrawCloseButton(Canvas canvas, RectF area, float screenDensity)
        {
            RectF button = GetCloseButtonArea(area, screenDensity);
            float radius = 10.0f * screenDensity;
            float inset = button.Width() * 0.3f;

            closeButtonPaint.Color = PopupBackgroundColor;
            closeButtonPaint.SetStyle(Paint.Style.Fill);
            canvas.DrawRoundRect(button, radius, radius, closeButtonPaint);

            closeButtonPaint.Color = Color.White;
            closeButtonPaint.SetStyle(Paint.Style.Stroke);
            closeButtonPaint.StrokeWidth = 4.0f * screenDensity;
            closeButtonPaint.StrokeCap = Paint.Cap.Round;
            canvas.DrawLine(button.Left + inset, button.Top + inset, button.Right - inset, button.Bottom - inset, closeButtonPaint);
            canvas.DrawLine(button.Right - inset, button.Top + inset, button.Left + inset, button.Bottom - inset, closeButtonPaint);
        }

        /// <summary>
        /// Returns the height of one popup line in pixels.
        /// </summary>
        /// <param name="line">Popup line.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Line height in pixels.</returns>
        private static float GetPopupLineHeight(PopupLine line, float screenDensity)
        {
            float lineHeight = PopupTextSize * PopupLineSpacing * screenDensity;

            return line.Style == PopupLineStyle.Progress ? lineHeight + (PopupBarHeight + 4.0f) * screenDensity : lineHeight;
        }

        /// <summary>
        /// Computes the area the popup covers: the bottom left corner of the screen above the version number.
        /// </summary>
        /// <param name="lines">Popup lines.</param>
        /// <param name="width">Surface width in pixels.</param>
        /// <param name="height">Surface height in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Area of the popup in screen pixels.</returns>
        /// <param name="hasTabs">Whether the popup has a tab strip at the top.</param>
        public RectF Measure(IReadOnlyList<PopupLine> lines, int width, int height, float screenDensity, bool hasTabs = false)
        {
            float margin = PopupMargin * screenDensity;
            float popupWidth = Math.Min(PopupMaxWidth * screenDensity, width * PopupMaxWidthFraction);
            float popupHeight = (2.0f * margin) + (hasTabs ? TabStripHeight * screenDensity : 0.0f);

            foreach (PopupLine line in lines)
            {
                popupHeight += GetPopupLineHeight(line, screenDensity);
            }

            float bottom = height - PopupBottomOffset * screenDensity;
            float top = Math.Max(margin, bottom - popupHeight);

            return new RectF(margin, top, margin + popupWidth, bottom);
        }

        /// <summary>
        /// Draws the popup with the data of the selected aircraft.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="lines">Popup lines.</param>
        /// <param name="area">Area of the popup.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <param name="tabs">Titles of the tabs, or null when the popup has no tab strip.</param>
        /// <param name="selectedTab">Index of the shown tab.</param>
        public void Draw(Canvas canvas, IReadOnlyList<PopupLine> lines, RectF area, float screenDensity, IReadOnlyList<string>? tabs = null, int selectedTab = 0)
        {
            float margin = PopupMargin * screenDensity;
            float radius = 8.0f * screenDensity;
            float textWidth = area.Width() - 2.0f * margin;
            float textSize = PopupTextSize * screenDensity;
            float y = area.Top + margin;

            popupPaint.TextSize = textSize;
            popupBoldPaint.TextSize = textSize;
            popupAccentPaint.TextSize = textSize;

            canvas.DrawRoundRect(area, radius, radius, popupBackgroundPaint);
            DrawCloseButton(canvas, area, screenDensity);

            if (tabs != null && tabs.Count > 0)
            {
                float tabWidth = textWidth / tabs.Count;
                float stripHeight = TabStripHeight * screenDensity;

                for (int index = 0; index < tabs.Count; index++)
                {
                    bool selected = index == selectedTab;
                    Paint tabPaint = selected ? popupBoldPaint : popupPaint;
                    float tabLeft = area.Left + margin + (index * tabWidth);
                    string title = ClipText(tabPaint, tabs[index], tabWidth - (8.0f * screenDensity));

                    tabPaint.Alpha = selected ? 255 : 160;
                    canvas.DrawText(title, tabLeft + ((tabWidth - tabPaint.MeasureText(title)) / 2.0f), y + textSize + (2.0f * screenDensity), tabPaint);
                    tabPaint.Alpha = 255;

                    if (selected)
                    {
                        canvas.DrawRect(tabLeft + (4.0f * screenDensity), y + stripHeight - (6.0f * screenDensity), tabLeft + tabWidth - (4.0f * screenDensity), y + stripHeight - (3.0f * screenDensity), popupAccentPaint);
                    }
                }

                y += stripHeight;
            }

            foreach (PopupLine line in lines)
            {
                float lineHeight = GetPopupLineHeight(line, screenDensity);

                if (y + lineHeight > area.Bottom)
                {
                    break;
                }

                Paint paint = line.Style switch
                {
                    PopupLineStyle.Bold => popupBoldPaint,
                    PopupLineStyle.Accent => popupAccentPaint,
                    _ => popupPaint,
                };

                if (line.Style == PopupLineStyle.Progress)
                {
                    float barHeight = PopupBarHeight * screenDensity;
                    float barLeft = area.Left + margin;
                    float barTop = y + 2.0f * screenDensity;
                    float barRadius = barHeight / 2.0f;

                    popupPaint.Alpha = 70;
                    canvas.DrawRoundRect(barLeft, barTop, barLeft + textWidth, barTop + barHeight, barRadius, barRadius, popupPaint);
                    popupPaint.Alpha = 255;
                    canvas.DrawRoundRect(barLeft, barTop, barLeft + Math.Max(barHeight, textWidth * Math.Clamp(line.Fraction, 0.0f, 1.0f)), barTop + barHeight, barRadius, barRadius, popupAccentPaint);
                    y += (PopupBarHeight + 4.0f) * screenDensity;
                }

                canvas.DrawText(ClipText(paint, line.Text, textWidth), area.Left + margin, y + textSize, paint);
                y += PopupTextSize * PopupLineSpacing * screenDensity;
            }
        }

        /// <summary>
        /// Finds the tab under a tap.
        /// </summary>
        /// <param name="area">Area of the popup in the last frame.</param>
        /// <param name="tabCount">Number of tabs.</param>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        /// <returns>Index of the tab, or -1 when the tap is outside the tab strip.</returns>
        public static int HitTab(RectF area, int tabCount, float x, float y, float screenDensity)
        {
            float margin = PopupMargin * screenDensity;
            float top = area.Top + margin;

            if (tabCount <= 0 || x < area.Left || x > area.Right || y < top - margin || y > top + TabStripHeight * screenDensity)
            {
                return -1;
            }

            float tabWidth = (area.Width() - (2.0f * margin)) / tabCount;

            return Math.Clamp((int)((x - area.Left - margin) / tabWidth), 0, tabCount - 1);
        }

        /// <summary>
        /// Shortens a text with an ellipsis so that it fits in the given width.
        /// </summary>
        /// <param name="paint">Paint the text is drawn with.</param>
        /// <param name="text">Text to fit.</param>
        /// <param name="maxWidth">Available width in pixels.</param>
        /// <returns>The text, or its beginning followed by an ellipsis.</returns>
        private static string ClipText(Paint paint, string text, float maxWidth)
        {
            if (paint.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            int count = paint.BreakText(text, true, maxWidth - paint.MeasureText("…"), null);

            return text.Substring(0, Math.Max(0, count)) + "…";
        }
    }
}

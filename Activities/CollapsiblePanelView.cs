using AdsbCarMap.Localization;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Framed panel of a card with a header that is always visible and a body that opens and closes when the header is tapped. The header has two texts, because a closed panel summarizes its content and an open one does not need to. A panel that can be opened shows a hint with an arrow ("rozwiń ▼" or "zwiń ▲"), and an open panel gets a thicker frame in the color of its header and a line between the header and the body.
    /// </summary>
    public sealed class CollapsiblePanelView : LinearLayout
    {
        /// <summary>
        /// Color of the frame of a closed panel and of the line under the header.
        /// </summary>
        private static readonly Color FrameColor = Color.Argb(70, 255, 255, 255);

        /// <summary>
        /// Color of the hint and of the arrow of a closed panel.
        /// </summary>
        private static readonly Color HintColor = Color.Rgb(190, 190, 190);

        /// <summary>
        /// Row with the header and the hint, which is the area that opens and closes the panel.
        /// </summary>
        private readonly LinearLayout headerRow;

        /// <summary>
        /// Header of the panel.
        /// </summary>
        private readonly TextView headerText;

        /// <summary>
        /// Hint with the arrow telling that the panel opens or closes.
        /// </summary>
        private readonly TextView hintText;

        /// <summary>
        /// Line between the header and the body, shown only while the panel is open.
        /// </summary>
        private readonly View divider;

        /// <summary>
        /// Body of the panel, shown only while the panel is open.
        /// </summary>
        private readonly TextView bodyText;

        /// <summary>
        /// Table of the body, shown instead of the text while the panel holds a table.
        /// </summary>
        private readonly TableLayout tableHost;

        /// <summary>
        /// Background with the frame.
        /// </summary>
        private readonly GradientDrawable frame;

        /// <summary>
        /// Content of the table that is built now, so that the table is built again only when the content changes.
        /// </summary>
        private string tableSignature = string.Empty;

        /// <summary>
        /// Whether the body is a table.
        /// </summary>
        private bool hasTable;

        /// <summary>
        /// Index of the column of the table drawn in a fixed-width font, or -1 for none.
        /// </summary>
        private int monospaceColumn = 1;

        /// <summary>
        /// Header shown while the panel is closed.
        /// </summary>
        private string collapsedHeader = string.Empty;

        /// <summary>
        /// Header shown while the panel is open.
        /// </summary>
        private string expandedHeader = string.Empty;

        /// <summary>
        /// Color of the header, which is also the color of the frame of an open panel.
        /// </summary>
        private Color accent = Color.White;

        /// <summary>
        /// Whether the panel is open.
        /// </summary>
        private bool expanded;

        /// <summary>
        /// Whether the panel has a body that can be opened.
        /// </summary>
        private bool hasBody;

        /// <summary>
        /// Creates the panel with its views.
        /// </summary>
        /// <param name="context">Activity context.</param>
        public CollapsiblePanelView(Context context) : base(context)
        {
            Orientation = Orientation.Vertical;
            SetPadding(Dp(10), 0, Dp(10), 0);

            frame = new GradientDrawable();
            frame.SetCornerRadius(Dp(10));
            Background = frame;

            headerRow = new LinearLayout(context);
            headerRow.Orientation = Orientation.Horizontal;
            headerRow.SetGravity(GravityFlags.CenterVertical);
            headerRow.SetPadding(0, Dp(9), 0, Dp(9));
            headerRow.Click += (sender, args) => Toggle();
            AddView(headerRow, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            headerText = new TextView(context);
            headerText.TextSize = 13f;
            headerText.SetTypeface(null, TypefaceStyle.Bold);
            headerText.SetTextColor(Color.White);
            headerRow.AddView(headerText, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            hintText = new TextView(context);
            hintText.TextSize = 12f;
            hintText.SetTextColor(HintColor);
            hintText.SetPadding(Dp(10), 0, 0, 0);
            headerRow.AddView(hintText, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            divider = new View(context);
            divider.SetBackgroundColor(FrameColor);
            divider.Visibility = ViewStates.Gone;
            AddView(divider, new LayoutParams(ViewGroup.LayoutParams.MatchParent, Math.Max(1, Dp(1))));

            bodyText = new TextView(context);
            bodyText.TextSize = 13f;
            bodyText.SetTextColor(Color.White);
            bodyText.SetTextIsSelectable(true);
            bodyText.SetPadding(0, Dp(8), 0, Dp(10));
            bodyText.Visibility = ViewStates.Gone;
            AddView(bodyText, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            tableHost = new TableLayout(context);
            tableHost.SetColumnShrinkable(2, true);
            tableHost.SetColumnStretchable(2, true);
            tableHost.SetPadding(0, Dp(4), 0, Dp(6));
            tableHost.Visibility = ViewStates.Gone;
            AddView(tableHost, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Visibility = ViewStates.Gone;
            Apply();
        }

        /// <summary>
        /// Event raised when the user opens or closes the panel.
        /// </summary>
        public event System.Action<bool>? ExpandedChanged;

        /// <summary>
        /// Whether the panel is open. Setting it does not raise <see cref="ExpandedChanged"/>.
        /// </summary>
        public bool Expanded
        {
            get => expanded;
            set
            {
                expanded = value;
                Apply();
            }
        }

        /// <summary>
        /// Fills the panel. Texts that did not change are not set again, so that a text the user is selecting stays selected when the card is refreshed periodically.
        /// </summary>
        /// <param name="collapsedHeaderText">Header shown while the panel is closed; empty hides the whole panel.</param>
        /// <param name="expandedHeaderText">Header shown while the panel is open.</param>
        /// <param name="body">Lines of the body; an empty list leaves the header only and the panel cannot be opened.</param>
        /// <param name="headerColor">Color of the header and of the frame of an open panel.</param>
        public void SetContent(string collapsedHeaderText, string expandedHeaderText, IReadOnlyList<string> body, Color headerColor)
        {
            collapsedHeader = collapsedHeaderText;
            expandedHeader = expandedHeaderText;
            accent = headerColor;
            hasBody = body.Count > 0;
            ClearTable();

            string text = string.Join("\n", body);

            if (!string.Equals(bodyText.Text, text, StringComparison.Ordinal))
            {
                bodyText.Text = text;
            }

            Apply();
        }

        /// <summary>
        /// Fills the panel with a table instead of text. The table is built again only when its content changed.
        /// </summary>
        /// <param name="collapsedHeaderText">Header shown while the panel is closed; empty hides the whole panel.</param>
        /// <param name="expandedHeaderText">Header shown while the panel is open.</param>
        /// <param name="columns">Headings of the columns; the first column is drawn in the color of the header, and the third one takes the remaining width.</param>
        /// <param name="rows">Rows of the table, each with one text per column; an empty list leaves the header only and the panel cannot be opened.</param>
        /// <param name="headerColor">Color of the header and of the frame of an open panel.</param>
        /// <param name="footer">Lines of selectable text under the table, or null for none.</param>
        /// <param name="monospaceColumn">Index of the column drawn in a fixed-width font, or -1 for none.</param>
        public void SetTableContent(string collapsedHeaderText, string expandedHeaderText, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows, Color headerColor, IReadOnlyList<string>? footer = null, int monospaceColumn = 1)
        {
            this.monospaceColumn = monospaceColumn;
            collapsedHeader = collapsedHeaderText;
            expandedHeader = expandedHeaderText;
            accent = headerColor;
            hasBody = rows.Count > 0;
            hasTable = hasBody;

            string footerText = string.Join("\n", footer ?? Array.Empty<string>());

            if (!string.Equals(bodyText.Text, footerText, StringComparison.Ordinal))
            {
                bodyText.Text = footerText;
            }

            string signature = monospaceColumn.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u0003" + string.Join("\u0001", columns) + "\u0003" + string.Join("\u0003", rows.Select((IReadOnlyList<string> row) => string.Join("\u0001", row))) + "\u0003" + accent.ToArgb().ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (!string.Equals(signature, tableSignature, StringComparison.Ordinal))
            {
                tableSignature = signature;
                BuildTable(columns, rows);
            }

            Apply();
        }

        /// <summary>
        /// Removes the table, when there is one, because the panel now holds text.
        /// </summary>
        private void ClearTable()
        {
            if (!hasTable && tableSignature.Length == 0)
            {
                return;
            }

            hasTable = false;
            tableSignature = string.Empty;
            tableHost.RemoveAllViews();
        }

        /// <summary>
        /// Builds the table: a heading row, a line under it and a row for each item, with every second row slightly lighter.
        /// </summary>
        /// <param name="columns">Headings of the columns.</param>
        /// <param name="rows">Rows of the table.</param>
        private void BuildTable(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            tableHost.RemoveAllViews();
            tableHost.AddView(CreateRow(columns, true, 0));

            View line = new View(Context);
            line.SetBackgroundColor(FrameColor);
            tableHost.AddView(line, new TableLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Math.Max(1, Dp(1))));

            for (int index = 0; index < rows.Count; index++)
            {
                tableHost.AddView(CreateRow(rows[index], false, index));
            }
        }

        /// <summary>
        /// Creates one row of the table.
        /// </summary>
        /// <param name="cells">Texts of the cells.</param>
        /// <param name="isHeading">Whether it is the heading row.</param>
        /// <param name="index">Position of the row among the rows of data, used to stripe them.</param>
        /// <returns>The row.</returns>
        private TableRow CreateRow(IReadOnlyList<string> cells, bool isHeading, int index)
        {
            TableRow row = new TableRow(Context);

            if (!isHeading && index % 2 == 1)
            {
                row.SetBackgroundColor(Color.Argb(22, 255, 255, 255));
            }

            for (int column = 0; column < cells.Count; column++)
            {
                TextView cell = new TextView(Context);
                cell.Text = cells[column];
                cell.TextSize = isHeading ? 11f : 13f;
                cell.SetPadding(Dp(column == 0 ? 2 : 6), Dp(isHeading ? 2 : 5), Dp(8), Dp(isHeading ? 4 : 5));

                if (isHeading)
                {
                    cell.SetTypeface(null, TypefaceStyle.Bold);
                    cell.SetTextColor(HintColor);
                }
                else if (column == 0)
                {
                    cell.SetTypeface(null, TypefaceStyle.Bold);
                    cell.SetTextColor(accent);
                }
                else if (column == monospaceColumn)
                {
                    cell.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
                    cell.SetTextColor(Color.White);
                }
                else
                {
                    cell.SetTextColor(Color.White);
                }

                row.AddView(cell, new TableRow.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            }

            return row;
        }

        /// <summary>
        /// Opens or closes the panel after a tap on the header.
        /// </summary>
        private void Toggle()
        {
            if (!hasBody)
            {
                return;
            }

            expanded = !expanded;
            Apply();
            ExpandedChanged?.Invoke(expanded);
        }

        /// <summary>
        /// Shows the header, the hint, the frame and the body that fit the state of the panel.
        /// </summary>
        private void Apply()
        {
            bool isOpen = expanded && hasBody;
            string header = isOpen ? expandedHeader : collapsedHeader;

            if (!string.Equals(headerText.Text, header, StringComparison.Ordinal))
            {
                headerText.Text = header;
            }

            headerText.SetTextColor(accent);
            hintText.Text = isOpen ? Loc.T("zwiń") + " ▲" : Loc.T("rozwiń") + " ▼";
            hintText.SetTextColor(isOpen ? accent : HintColor);
            hintText.Visibility = hasBody ? ViewStates.Visible : ViewStates.Gone;
            headerRow.Clickable = hasBody;
            headerRow.ContentDescription = hasBody ? (isOpen ? Loc.T("Zwiń panel") : Loc.T("Rozwiń panel")) : null;
            frame.SetColor(Color.Argb(isOpen ? 52 : 30, 255, 255, 255));
            frame.SetStroke(isOpen ? Math.Max(2, Dp(2)) : Math.Max(1, Dp(1)), isOpen ? accent : FrameColor);
            divider.Visibility = isOpen ? ViewStates.Visible : ViewStates.Gone;
            bodyText.Visibility = isOpen && (!hasTable || !string.IsNullOrEmpty(bodyText.Text)) ? ViewStates.Visible : ViewStates.Gone;
            tableHost.Visibility = isOpen && hasTable ? ViewStates.Visible : ViewStates.Gone;
            Visibility = header.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

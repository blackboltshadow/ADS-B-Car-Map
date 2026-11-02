using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Editor of a user-made aircraft filter. A filter has a name, says whether the matching aircraft are shown or hidden, and is a tree of conditions and groups. A group combines its children with AND or OR, and every condition and group can be negated (NOT). Groups can be nested without a limit that matters in practice.
    /// </summary>
    [Activity(Label = "Kreator filtru", Exported = false, Theme = "@android:style/Theme.Material", WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout)]
    public class FilterEditorActivity : Activity
    {
        /// <summary>
        /// Name of the intent extra with the identifier of the user-made filter to edit.
        /// </summary>
        public const string EditedIdExtra = "edited_filter_id";

        /// <summary>
        /// Name of the intent extra with the identifier of the filter to copy.
        /// </summary>
        public const string CopiedIdExtra = "copied_filter_id";

        /// <summary>
        /// Name of the intent extra with the identifier of the predefined filter to show read-only.
        /// </summary>
        public const string ViewedIdExtra = "viewed_filter_id";

        /// <summary>
        /// Number of nesting levels after which the indentation stops growing, so that a deep tree still fits on the screen.
        /// </summary>
        private const int MaxIndentLevels = 6;

        /// <summary>
        /// Maximum number of characters of the description of a group in the list of target groups.
        /// </summary>
        private const int MaxTargetSummaryLength = 60;

        /// <summary>
        /// Identifier of the menu item that moves a node up.
        /// </summary>
        private const int MenuMoveUpId = 1;

        /// <summary>
        /// Identifier of the menu item that moves a node down.
        /// </summary>
        private const int MenuMoveDownId = 2;

        /// <summary>
        /// Identifier of the menu item that moves a node to another group.
        /// </summary>
        private const int MenuMoveToId = 3;

        /// <summary>
        /// Identifier of the menu item that copies a node.
        /// </summary>
        private const int MenuCopyId = 4;

        /// <summary>
        /// Identifier of the menu item that removes a node.
        /// </summary>
        private const int MenuRemoveId = 5;

        /// <summary>
        /// Colors of the bars at the left edge of the groups, used in turn by the nesting level.
        /// </summary>
        private static readonly Color[] LevelColors = new Color[]
        {
            Color.Rgb(33, 150, 243),
            Color.Rgb(76, 175, 80),
            Color.Rgb(171, 71, 188),
            Color.Rgb(255, 152, 0),
            Color.Rgb(0, 188, 212),
            Color.Rgb(236, 64, 122),
        };

        /// <summary>
        /// Color that marks a negated node.
        /// </summary>
        private static readonly Color NegationColor = Color.Rgb(229, 57, 53);

        /// <summary>
        /// Color of the frame around the node a validation problem refers to.
        /// </summary>
        private static readonly Color ProblemColor = Color.Rgb(255, 193, 7);

        /// <summary>
        /// The view of every node currently shown, to scroll to a node.
        /// </summary>
        private readonly Dictionary<FilterDraftNode, View> nodeViews = new Dictionary<FilterDraftNode, View>();

        /// <summary>
        /// The filter being edited.
        /// </summary>
        private FilterDraft draft = new FilterDraft();

        /// <summary>
        /// Identifier of the edited filter, or null when a new filter is created.
        /// </summary>
        private string? editedId;

        /// <summary>
        /// Whether the filter is only shown and cannot be changed.
        /// </summary>
        private bool readOnly;

        /// <summary>
        /// Identifier of the predefined filter shown read-only, used to make a copy of it.
        /// </summary>
        private string? viewedId;

        /// <summary>
        /// The node a validation problem refers to, framed until the next change.
        /// </summary>
        private FilterDraftNode? problemNode;

        /// <summary>
        /// Scrolling container of the whole screen.
        /// </summary>
        private ScrollView? scrollView;

        /// <summary>
        /// Field with the name of the filter.
        /// </summary>
        private EditText? nameInput;

        /// <summary>
        /// Container of the tree of nodes.
        /// </summary>
        private LinearLayout? treeLayout;

        /// <summary>
        /// Text that describes the filter being edited.
        /// </summary>
        private TextView? previewText;

        /// <summary>
        /// Sentence that describes the filter being edited.
        /// </summary>
        private TextView? sentenceText;

        /// <summary>
        /// Builds the screen from the intent: a new filter, an edited one, a copy or a read-only view.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);

            if (!LoadDraft())
            {
                Finish();

                return;
            }

            SetContentView(BuildContent());
            RenderTree();

            if (readOnly)
            {
                Title = "Filtr wbudowany";
            }
        }

        /// <summary>
        /// Closes the screen without saving when the back arrow is tapped.
        /// </summary>
        /// <param name="item">Selected bar item.</param>
        /// <returns>True when the item was handled.</returns>
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home)
            {
                Finish();

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        /// <summary>
        /// Prepares the draft according to the intent extras.
        /// </summary>
        /// <returns>False when the filter to edit or copy no longer exists.</returns>
        private bool LoadDraft()
        {
            string? sourceId = Intent?.GetStringExtra(EditedIdExtra);
            bool copy = false;

            if (sourceId == null)
            {
                sourceId = Intent?.GetStringExtra(CopiedIdExtra);
                copy = sourceId != null;
            }

            if (sourceId == null)
            {
                sourceId = Intent?.GetStringExtra(ViewedIdExtra);
                readOnly = sourceId != null;
                viewedId = sourceId;
            }

            if (sourceId == null)
            {
                draft = FilterDraft.CreateNew();

                return true;
            }

            AircraftFilter? source = AppServices.Filters.Find(sourceId);
            FilterDraft? loaded = source == null ? null : FilterDraft.FromFilter(source);

            if (loaded == null)
            {
                Toast.MakeText(this, "Nie można otworzyć tego filtru w kreatorze.", ToastLength.Long)?.Show();

                return false;
            }

            draft = loaded;

            if (copy)
            {
                string name = draft.Name + " (kopia)";

                draft.Name = name.Length > FilterDraft.MaxNameLength ? name.Substring(0, FilterDraft.MaxNameLength) : name;
            }
            else if (!readOnly)
            {
                editedId = sourceId;
            }

            return true;
        }

        /// <summary>
        /// Creates the whole view of the screen.
        /// </summary>
        /// <returns>Scrollable view with the name, the mode, the tree, the descriptions and the buttons.</returns>
        private View BuildContent()
        {
            scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(16), Dp(8), Dp(16), Dp(32));
            scrollView.AddView(root);

            if (readOnly)
            {
                TextView notice = new TextView(this);
                notice.Text = "To filtr wbudowany, tylko do odczytu. Aby go zmodyfikować, skopiuj go do własnych filtrów.";
                notice.TextSize = 13f;
                notice.SetTextColor(ProblemColor);
                notice.SetPadding(0, Dp(4), 0, Dp(8));
                root.AddView(notice);
            }

            nameInput = new EditText(this);
            nameInput.Hint = "Nazwa filtru";
            nameInput.SetSingleLine(true);
            nameInput.SetFilters(new IInputFilter[] { new InputFilterLengthFilter(FilterDraft.MaxNameLength) });
            nameInput.Text = draft.Name;
            nameInput.Enabled = !readOnly;
            nameInput.TextChanged += (sender, args) =>
            {
                draft.Name = nameInput.Text ?? string.Empty;
                UpdatePreview();
            };
            root.AddView(nameInput);

            root.AddView(CreateSectionTitle("Co robi filtr"));
            root.AddView(CreateChoice(
                new string[] { "Pokaż tylko pasujące samoloty", "Ukryj pasujące samoloty" },
                draft.Mode == FilterMode.Show ? 0 : 1,
                index => draft.Mode = index == 0 ? FilterMode.Show : FilterMode.Hide));

            root.AddView(CreateSectionTitle("Warunki"));

            treeLayout = new LinearLayout(this);
            treeLayout.Orientation = Orientation.Vertical;
            root.AddView(treeLayout);

            previewText = new TextView(this);
            previewText.TextSize = 14f;
            previewText.SetTextColor(Color.White);
            previewText.SetPadding(0, Dp(14), 0, Dp(4));
            root.AddView(previewText);

            sentenceText = new TextView(this);
            sentenceText.TextSize = 14f;
            sentenceText.SetTextColor(Color.Rgb(190, 190, 190));
            sentenceText.SetPadding(0, Dp(4), 0, Dp(4));
            root.AddView(sentenceText);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(12), 0, 0);
            root.AddView(buttons);

            if (readOnly)
            {
                Button copyButton = new Button(this);
                copyButton.Text = "Kopiuj do własnych";
                copyButton.Click += (sender, args) => OpenCopy();
                buttons.AddView(copyButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

                Button closeButton = new Button(this);
                closeButton.Text = "Zamknij";
                closeButton.Click += (sender, args) => Finish();
                buttons.AddView(closeButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            }
            else
            {
                Button saveButton = new Button(this);
                saveButton.Text = "Zapisz";
                saveButton.Click += (sender, args) => Save();
                buttons.AddView(saveButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

                Button cancelButton = new Button(this);
                cancelButton.Text = "Anuluj";
                cancelButton.Click += (sender, args) => Finish();
                buttons.AddView(cancelButton, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            }

            return scrollView;
        }

        /// <summary>
        /// Creates a title of a section of the editor.
        /// </summary>
        /// <param name="text">Title.</param>
        /// <returns>The title view.</returns>
        private TextView CreateSectionTitle(string text)
        {
            TextView title = new TextView(this);
            title.Text = text;
            title.TextSize = 15f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            title.SetPadding(0, Dp(14), 0, Dp(4));

            return title;
        }

        /// <summary>
        /// Creates a group of radio buttons.
        /// </summary>
        /// <param name="labels">Texts of the options.</param>
        /// <param name="selected">Index of the selected option.</param>
        /// <param name="onSelected">Called with the index of the option the user selected.</param>
        /// <returns>The group.</returns>
        private RadioGroup CreateChoice(string[] labels, int selected, System.Action<int> onSelected)
        {
            RadioGroup group = new RadioGroup(this);
            int[] ids = new int[labels.Length];

            for (int index = 0; index < labels.Length; index++)
            {
                RadioButton button = new RadioButton(this);
                button.Id = View.GenerateViewId();
                button.Text = labels[index];
                button.Enabled = !readOnly;
                ids[index] = button.Id;
                group.AddView(button);
            }

            group.Check(ids[selected]);
            group.CheckedChange += (sender, args) =>
            {
                int chosen = Array.IndexOf(ids, args.CheckedId);

                if (chosen >= 0)
                {
                    onSelected(chosen);
                    UpdatePreview();
                }
            };

            return group;
        }

        /// <summary>
        /// Rebuilds the views of the whole tree. Any change of the structure, of a negation or of a mode goes through here.
        /// </summary>
        /// <param name="keepProblem">True to keep the frame around the node of a validation problem.</param>
        private void RenderTree(bool keepProblem = false)
        {
            if (treeLayout == null)
            {
                return;
            }

            if (!keepProblem)
            {
                problemNode = null;
            }

            nodeViews.Clear();
            treeLayout.RemoveAllViews();
            treeLayout.AddView(CreateNodeView(draft.Root, 0));
            UpdatePreview();
        }

        /// <summary>
        /// Creates the view of a node and, for a group, of everything inside it.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <param name="depth">Depth of the node; 0 for the root group.</param>
        /// <returns>The view.</returns>
        private View CreateNodeView(FilterDraftNode node, int depth)
        {
            return node is FilterDraftGroup group ? CreateGroupView(group, depth) : CreateConditionView((FilterDraftCondition)node);
        }

        /// <summary>
        /// Creates the view of a group: a bar in the color of its level, the header, the children and the buttons that add to it.
        /// </summary>
        /// <param name="group">Group.</param>
        /// <param name="depth">Depth of the group.</param>
        /// <returns>The view.</returns>
        private View CreateGroupView(FilterDraftGroup group, int depth)
        {
            LinearLayout outer = new LinearLayout(this);
            outer.Orientation = Orientation.Horizontal;
            outer.Background = CreateNodeBackground(group);
            outer.SetPadding(0, 0, Dp(4), 0);

            View bar = new View(this);
            bar.SetBackgroundColor(LevelColors[depth % LevelColors.Length]);
            outer.AddView(bar, new LinearLayout.LayoutParams(Dp(5), ViewGroup.LayoutParams.MatchParent));

            LinearLayout content = new LinearLayout(this);
            content.Orientation = Orientation.Vertical;
            content.SetPadding(depth < MaxIndentLevels ? Dp(8) : Dp(2), Dp(4), Dp(4), Dp(6));
            outer.AddView(content, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            content.AddView(CreateGroupHeader(group, depth));

            if (group.Collapsed)
            {
                TextView summary = new TextView(this);
                summary.Text = group.Children.Count == 0 ? "(pusta grupa)" : FilterDescriber.DescribeNode(group.ToNode(), false);
                summary.TextSize = 12f;
                summary.SetTextColor(Color.Rgb(190, 190, 190));
                summary.SetSingleLine(true);
                summary.Ellipsize = TextUtils.TruncateAt.End;
                summary.SetPadding(Dp(4), Dp(2), 0, Dp(2));
                content.AddView(summary);
            }
            else
            {
                foreach (FilterDraftNode child in group.Children)
                {
                    LinearLayout.LayoutParams childParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                    childParams.SetMargins(0, Dp(4), 0, 0);
                    content.AddView(CreateNodeView(child, depth + 1), childParams);
                }

                if (!readOnly)
                {
                    content.AddView(CreateGroupFooter(group));
                }
            }

            nodeViews[group] = outer;

            return outer;
        }

        /// <summary>
        /// Creates the header of a group: the collapse arrow, the NOT switch, the AND/OR selector and the node menu.
        /// </summary>
        /// <param name="group">Group.</param>
        /// <param name="depth">Depth of the group.</param>
        /// <returns>The header.</returns>
        private View CreateGroupHeader(FilterDraftGroup group, int depth)
        {
            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Horizontal;
            header.SetGravity(GravityFlags.CenterVertical);

            TextView arrow = new TextView(this);
            arrow.Text = group.Collapsed ? "▸" : "▾";
            arrow.TextSize = 20f;
            arrow.SetTextColor(Color.White);
            arrow.SetPadding(Dp(4), Dp(4), Dp(10), Dp(4));
            arrow.ContentDescription = group.Collapsed ? "Rozwiń grupę" : "Zwiń grupę";
            arrow.Click += (sender, args) =>
            {
                group.Collapsed = !group.Collapsed;
                RenderTree();
            };
            header.AddView(arrow);

            header.AddView(CreateNegationChip(group));

            TextView mode = new TextView(this);
            mode.Text = (group.Mode == FilterGroupMode.All ? "ORAZ" : "LUB") + " ▾";
            mode.TextSize = 13f;
            mode.SetTypeface(null, TypefaceStyle.Bold);
            mode.SetTextColor(Color.White);
            mode.SetPadding(Dp(12), Dp(5), Dp(12), Dp(5));
            mode.ContentDescription = "Sposób łączenia warunków grupy";

            GradientDrawable modeBackground = new GradientDrawable();
            modeBackground.SetCornerRadius(Dp(12));
            modeBackground.SetColor(Color.Argb(90, 47, 128, 237));
            modeBackground.SetStroke(Dp(1), Color.Argb(200, 47, 128, 237));
            mode.Background = modeBackground;

            if (!readOnly)
            {
                mode.Click += (sender, args) => ShowModeMenu(mode, group);
            }

            LinearLayout.LayoutParams modeParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            modeParams.SetMargins(Dp(8), 0, 0, 0);
            header.AddView(mode, modeParams);

            if (depth >= MaxIndentLevels)
            {
                TextView level = new TextView(this);
                level.Text = "poz. " + depth.ToString(CultureInfo.InvariantCulture);
                level.TextSize = 11f;
                level.SetTextColor(Color.Rgb(160, 160, 160));
                level.SetPadding(Dp(8), 0, 0, 0);
                header.AddView(level);
            }

            header.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1f));

            if (!readOnly && group.Parent != null)
            {
                header.AddView(CreateMenuButton(group));
            }

            return header;
        }

        /// <summary>
        /// Creates the two buttons at the end of a group that add a condition and a group to it.
        /// </summary>
        /// <param name="group">Group.</param>
        /// <returns>The row with the buttons.</returns>
        private View CreateGroupFooter(FilterDraftGroup group)
        {
            LinearLayout footer = new LinearLayout(this);
            footer.Orientation = Orientation.Horizontal;

            Button addCondition = new Button(this, null, Android.Resource.Attribute.BorderlessButtonStyle);
            addCondition.Text = "＋ warunek";
            addCondition.Click += (sender, args) =>
            {
                group.Add(new FilterDraftCondition());
                RenderTree();
            };
            footer.AddView(addCondition);

            Button addGroup = new Button(this, null, Android.Resource.Attribute.BorderlessButtonStyle);
            addGroup.Text = "＋ grupa";
            addGroup.Click += (sender, args) =>
            {
                FilterDraftGroup created = new FilterDraftGroup();

                created.Add(new FilterDraftCondition());
                group.Add(created);
                RenderTree();
            };
            footer.AddView(addGroup);

            return footer;
        }

        /// <summary>
        /// Creates the view of a condition: the NOT switch, the field, the node menu and the input of the operand.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The view.</returns>
        private View CreateConditionView(FilterDraftCondition condition)
        {
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(8), Dp(6), Dp(4), Dp(6));
            card.Background = CreateNodeBackground(condition);

            LinearLayout top = new LinearLayout(this);
            top.Orientation = Orientation.Horizontal;
            top.SetGravity(GravityFlags.CenterVertical);
            card.AddView(top);

            top.AddView(CreateNegationChip(condition));

            LinearLayout.LayoutParams spinnerParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
            spinnerParams.SetMargins(Dp(6), 0, 0, 0);
            top.AddView(CreateFieldSpinner(condition), spinnerParams);

            if (!readOnly)
            {
                top.AddView(CreateMenuButton(condition));
            }

            card.AddView(CreateOperandView(condition));
            nodeViews[condition] = card;

            return card;
        }

        /// <summary>
        /// Creates the background of a node: red when the node is negated, framed in yellow when a validation problem refers to it.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>The background.</returns>
        private GradientDrawable CreateNodeBackground(FilterDraftNode node)
        {
            GradientDrawable background = new GradientDrawable();
            background.SetCornerRadius(Dp(10));
            background.SetColor(node.Negated ? Color.Argb(60, 229, 57, 53) : Color.Argb(22, 255, 255, 255));

            if (ReferenceEquals(node, problemNode))
            {
                background.SetStroke(Dp(3), ProblemColor);
            }
            else if (node.Negated)
            {
                background.SetStroke(Dp(2), NegationColor);
            }
            else
            {
                background.SetStroke(Dp(1), Color.Argb(70, 255, 255, 255));
            }

            return background;
        }

        /// <summary>
        /// Creates the NOT switch of a node. When it is on it is filled with red and the whole node turns red.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>The switch.</returns>
        private TextView CreateNegationChip(FilterDraftNode node)
        {
            TextView chip = new TextView(this);
            chip.Text = "NIE";
            chip.TextSize = 12f;
            chip.SetTypeface(null, TypefaceStyle.Bold);
            chip.Gravity = GravityFlags.Center;
            chip.SetPadding(Dp(10), Dp(5), Dp(10), Dp(5));
            chip.ContentDescription = node.Negated ? "Negacja NIE włączona" : "Negacja NIE wyłączona";

            GradientDrawable background = new GradientDrawable();
            background.SetCornerRadius(Dp(12));

            if (node.Negated)
            {
                background.SetColor(NegationColor);
                background.SetStroke(Dp(1), Color.White);
                chip.SetTextColor(Color.White);
            }
            else
            {
                background.SetColor(Color.Transparent);
                background.SetStroke(Dp(1), Color.Rgb(130, 130, 130));
                chip.SetTextColor(Color.Rgb(170, 170, 170));
            }

            chip.Background = background;

            if (!readOnly)
            {
                chip.Click += (sender, args) =>
                {
                    node.Negated = !node.Negated;
                    RenderTree();
                };
            }

            return chip;
        }

        /// <summary>
        /// Creates the button that opens the menu of a node.
        /// </summary>
        /// <param name="node">Node.</param>
        /// <returns>The button.</returns>
        private View CreateMenuButton(FilterDraftNode node)
        {
            TextView button = new TextView(this);
            button.Text = "⋮";
            button.TextSize = 22f;
            button.SetTextColor(Color.White);
            button.Gravity = GravityFlags.Center;
            button.SetPadding(Dp(10), Dp(2), Dp(10), Dp(2));
            button.ContentDescription = "Menu węzła";
            button.Click += (sender, args) => ShowNodeMenu(button, node);

            return button;
        }

        /// <summary>
        /// Shows the menu that chooses between AND and OR for a group.
        /// </summary>
        /// <param name="anchor">View under which the menu appears.</param>
        /// <param name="group">Group.</param>
        private void ShowModeMenu(View anchor, FilterDraftGroup group)
        {
            PopupMenu popup = new PopupMenu(this, anchor);
            popup.Menu?.Add(0, 1, 0, "ORAZ (wszystkie warunki)");
            popup.Menu?.Add(0, 2, 1, "LUB (którykolwiek warunek)");
            popup.MenuItemClick += (sender, args) =>
            {
                args.Handled = true;
                group.Mode = args.Item?.ItemId == 1 ? FilterGroupMode.All : FilterGroupMode.Any;
                RenderTree();
            };
            popup.Show();
        }

        /// <summary>
        /// Shows the menu of a node: move up, move down, move to another group, copy and remove.
        /// </summary>
        /// <param name="anchor">View under which the menu appears.</param>
        /// <param name="node">Node.</param>
        private void ShowNodeMenu(View anchor, FilterDraftNode node)
        {
            bool hasTarget = draft.GetGroups().Any((FilterDraftGroup group) => !ReferenceEquals(group, node.Parent) && draft.CanMoveTo(node, group));

            PopupMenu popup = new PopupMenu(this, anchor);
            popup.Menu?.Add(0, MenuMoveUpId, 0, "Przesuń wyżej")?.SetEnabled(draft.CanMoveUp(node));
            popup.Menu?.Add(0, MenuMoveDownId, 1, "Przesuń niżej")?.SetEnabled(draft.CanMoveDown(node));
            popup.Menu?.Add(0, MenuMoveToId, 2, "Przenieś do grupy…")?.SetEnabled(hasTarget);
            popup.Menu?.Add(0, MenuCopyId, 3, "Kopiuj");
            popup.Menu?.Add(0, MenuRemoveId, 4, "Usuń");
            popup.MenuItemClick += (sender, args) =>
            {
                args.Handled = true;

                int id = args.Item?.ItemId ?? 0;

                if (id == MenuMoveUpId)
                {
                    draft.MoveUp(node);
                    RenderTree();
                }
                else if (id == MenuMoveDownId)
                {
                    draft.MoveDown(node);
                    RenderTree();
                }
                else if (id == MenuMoveToId)
                {
                    ShowMoveTargets(node);
                }
                else if (id == MenuCopyId)
                {
                    draft.Duplicate(node);
                    RenderTree();
                }
                else if (id == MenuRemoveId)
                {
                    RemoveNode(node);
                }
            };
            popup.Show();
        }

        /// <summary>
        /// Removes a node. A group that contains something is removed only after confirmation.
        /// </summary>
        /// <param name="node">Node.</param>
        private void RemoveNode(FilterDraftNode node)
        {
            if (node is not FilterDraftGroup group || group.Children.Count == 0)
            {
                draft.Remove(node);
                RenderTree();

                return;
            }

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Usunąć grupę?");
            builder.SetMessage("Grupa zawiera " + group.Children.Count + " elementów, które też zostaną usunięte.");
            builder.SetPositiveButton("Usuń", (sender, args) =>
            {
                draft.Remove(node);
                RenderTree();
            });
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Shows the list of groups a node can be moved to and moves the node to the one chosen.
        /// </summary>
        /// <param name="node">Node to move.</param>
        private void ShowMoveTargets(FilterDraftNode node)
        {
            IReadOnlyList<FilterDraftGroup> allGroups = draft.GetGroups();
            List<FilterDraftGroup> targets = new List<FilterDraftGroup>();
            List<string> labels = new List<string>();

            for (int index = 0; index < allGroups.Count; index++)
            {
                FilterDraftGroup group = allGroups[index];

                if (ReferenceEquals(group, node.Parent) || !draft.CanMoveTo(node, group))
                {
                    continue;
                }

                string summary = group.Children.Count == 0 ? "pusta" : FilterDescriber.DescribeNode(group.ToNode(), false);

                if (summary.Length > MaxTargetSummaryLength)
                {
                    summary = summary.Substring(0, MaxTargetSummaryLength) + "…";
                }

                labels.Add((index + 1).ToString(CultureInfo.InvariantCulture) + ". " + (group.Parent == null ? "Grupa główna" : "Grupa") + " " + (group.Mode == FilterGroupMode.All ? "ORAZ" : "LUB") + ": " + summary);
                targets.Add(group);
            }

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Przenieś do grupy");
            builder.SetItems(labels.ToArray(), (sender, args) =>
            {
                FilterDraftGroup target = targets[args.Which];

                target.Collapsed = false;
                draft.MoveTo(node, target);
                RenderTree();
            });
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Creates the spinner that selects the field of a condition. Changing the field replaces the operand input.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The spinner.</returns>
        private Spinner CreateFieldSpinner(FilterDraftCondition condition)
        {
            List<string> labels = FilterFieldInfo.All.Select((FilterField field) => FilterFieldInfo.GetLabel(field)).ToList();
            int selected = Math.Max(0, FilterFieldInfo.All.ToList().IndexOf(condition.Field));

            Spinner spinner = new Spinner(this);
            spinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, labels);
            spinner.SetSelection(selected);
            spinner.Enabled = !readOnly;
            spinner.ItemSelected += (sender, args) =>
            {
                FilterField chosen = FilterFieldInfo.All[args.Position];

                if (chosen != condition.Field)
                {
                    condition.Field = chosen;
                    condition.Text = string.Empty;
                    condition.Minimum = null;
                    condition.Maximum = null;
                    RenderTree();
                }
            };

            return spinner;
        }

        /// <summary>
        /// Creates the input for the operand of a condition, which depends on its field.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The input view.</returns>
        private View CreateOperandView(FilterDraftCondition condition)
        {
            switch (FilterFieldInfo.GetOperandType(condition.Field))
            {
                case FilterOperandType.Text:
                    return CreateTextOperand(condition);
                case FilterOperandType.Kind:
                    return CreateKindOperand(condition);
                case FilterOperandType.Manufacturer:
                    return CreateManufacturerOperand(condition);
                case FilterOperandType.Range:
                    return CreateRangeOperand(condition);
                default:
                    TextView none = new TextView(this);
                    none.Text = "Bez wartości.";
                    none.TextSize = 12f;
                    none.SetTextColor(Color.Rgb(180, 180, 180));
                    none.SetPadding(Dp(4), Dp(2), 0, Dp(2));

                    return none;
            }
        }

        /// <summary>
        /// Creates a text input for the operand.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The input.</returns>
        private View CreateTextOperand(FilterDraftCondition condition)
        {
            EditText input = new EditText(this);
            input.Hint = FilterFieldInfo.GetHint(condition.Field);
            input.SetSingleLine(true);
            input.InputType = InputTypes.ClassText | InputTypes.TextFlagCapCharacters;
            input.Text = condition.Text;
            input.Enabled = !readOnly;
            input.TextChanged += (sender, args) =>
            {
                condition.Text = input.Text ?? string.Empty;
                UpdatePreview();
            };

            return input;
        }

        /// <summary>
        /// Creates a spinner with the aircraft kinds.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The spinner.</returns>
        private View CreateKindOperand(FilterDraftCondition condition)
        {
            IReadOnlyList<AircraftKind> kinds = FilterFieldInfo.GetSelectableKinds();
            List<string> labels = kinds.Select((AircraftKind kind) => AircraftClassifier.GetDisplayName(kind)).ToList();

            if (!kinds.Contains(condition.Kind))
            {
                condition.Kind = kinds[0];
            }

            Spinner spinner = new Spinner(this);
            spinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, labels);
            spinner.SetSelection(kinds.ToList().IndexOf(condition.Kind));
            spinner.Enabled = !readOnly;
            spinner.ItemSelected += (sender, args) =>
            {
                condition.Kind = kinds[args.Position];
                UpdatePreview();
            };

            return spinner;
        }

        /// <summary>
        /// Creates a spinner with the known manufacturers.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The spinner.</returns>
        private View CreateManufacturerOperand(FilterDraftCondition condition)
        {
            IReadOnlyList<string> names = AircraftManufacturers.Names;
            int selected = names.ToList().FindIndex((string name) => string.Equals(name, condition.Text, StringComparison.OrdinalIgnoreCase));

            if (selected < 0)
            {
                selected = Math.Max(0, names.ToList().IndexOf("Boeing"));
            }

            condition.Text = names[selected];

            Spinner spinner = new Spinner(this);
            spinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, names.ToList());
            spinner.SetSelection(selected);
            spinner.Enabled = !readOnly;
            spinner.ItemSelected += (sender, args) =>
            {
                condition.Text = names[args.Position];
                UpdatePreview();
            };

            return spinner;
        }

        /// <summary>
        /// Creates two numeric inputs for the lower and the upper bound of a range.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <returns>The inputs in one line.</returns>
        private View CreateRangeOperand(FilterDraftCondition condition)
        {
            LinearLayout line = new LinearLayout(this);
            line.Orientation = Orientation.Horizontal;

            line.AddView(CreateNumberInput("od [m]", condition.Minimum, value => condition.Minimum = value), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            line.AddView(CreateNumberInput("do [m]", condition.Maximum, value => condition.Maximum = value), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            return line;
        }

        /// <summary>
        /// Creates a numeric input.
        /// </summary>
        /// <param name="hint">Hint shown in the empty input.</param>
        /// <param name="initial">Initial value or null.</param>
        /// <param name="onChanged">Called with the entered number, or null when the input is empty or invalid.</param>
        /// <returns>The input.</returns>
        private EditText CreateNumberInput(string hint, double? initial, System.Action<double?> onChanged)
        {
            EditText input = new EditText(this);
            input.Hint = hint;
            input.SetSingleLine(true);
            input.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            input.Enabled = !readOnly;

            if (initial.HasValue)
            {
                input.Text = initial.Value.ToString("0.##", CultureInfo.InvariantCulture);
            }

            input.TextChanged += (sender, args) =>
            {
                string text = (input.Text ?? string.Empty).Trim().Replace(',', '.');

                onChanged(double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null);
                UpdatePreview();
            };

            return input;
        }

        /// <summary>
        /// Shows the description and the sentence that describe the filter as it is currently edited.
        /// </summary>
        private void UpdatePreview()
        {
            if (previewText == null || sentenceText == null)
            {
                return;
            }

            AircraftFilter preview = draft.ToFilter("preview");
            string? sentence = FilterDescriber.DescribeSentence(preview);

            previewText.Text = "Opis: " + FilterDescriber.Describe(preview);
            sentenceText.Text = sentence == null ? string.Empty : "Czytaj: " + sentence;
            sentenceText.Visibility = sentence == null ? ViewStates.Gone : ViewStates.Visible;
        }

        /// <summary>
        /// Opens the editor with a copy of the predefined filter shown here and closes this read-only view.
        /// </summary>
        private void OpenCopy()
        {
            if (viewedId == null)
            {
                return;
            }

            Android.Content.Intent intent = new Android.Content.Intent(this, typeof(FilterEditorActivity));

            intent.PutExtra(CopiedIdExtra, viewedId);
            StartActivity(intent);
            Finish();
        }

        /// <summary>
        /// Checks the filter, saves it and closes the screen. A problem is reported in a message; the node it refers to is expanded, framed and scrolled into view, and the screen stays open.
        /// </summary>
        private void Save()
        {
            string? problem = draft.Validate(out FilterDraftNode? node);

            if (problem == null)
            {
                AppServices.Filters.Save(draft.ToFilter(editedId ?? AircraftFilterStore.CreateId()));
                Finish();

                return;
            }

            Toast.MakeText(this, problem, ToastLength.Long)?.Show();

            if (node == null)
            {
                nameInput?.RequestFocus();

                return;
            }

            for (FilterDraftGroup? ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                ancestor.Collapsed = false;
            }

            problemNode = node;
            RenderTree(true);

            if (nodeViews.TryGetValue(node, out View? view))
            {
                view.Post(() => view.RequestRectangleOnScreen(new Rect(0, 0, view.Width, view.Height)));
            }
        }

        /// <summary>
        /// Converts dp to pixels of the phone screen.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

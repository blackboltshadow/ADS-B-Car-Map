using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;
using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen with the list of aircraft filters. Each filter has a switch that turns it on or off; user-made filters can be edited and removed, predefined ones copied. A button at the top opens the filter editor.
    /// </summary>
    [Activity(Label = "Filtry samolotów", Exported = false, Theme = "@android:style/Theme.Material")]
    public class FiltersActivity : Activity
    {
        /// <summary>
        /// Hint shown under the button that adds a filter.
        /// </summary>
        private TextView? hintText;

        /// <summary>
        /// Container of the rows.
        /// </summary>
        private LinearLayout? listLayout;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Filtry samolotów");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Starts listening for changes of the filters and shows the list.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            AppServices.Filters.Changed += OnFiltersChanged;
            Render();
        }

        /// <summary>
        /// Stops listening for changes.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Filters.Changed -= OnFiltersChanged;

            base.OnPause();
        }

        /// <summary>
        /// Closes the screen when the back arrow is tapped.
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
        /// Creates the whole view of the screen.
        /// </summary>
        /// <returns>View with the add button at the top and the scrollable list under it.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            LinearLayout header = new LinearLayout(this);
            header.Orientation = Orientation.Vertical;
            header.SetPadding(Dp(16), Dp(8), Dp(16), Dp(4));
            root.AddView(header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Button addButton = new Button(this);
            addButton.Text = Loc.T("＋ Nowy filtr");
            addButton.Click += (sender, args) => OpenEditor(null, null);
            header.AddView(addButton);

            hintText = new TextView(this);
            hintText.TextSize = 12f;
            hintText.SetTextColor(Color.Rgb(180, 180, 180));
            hintText.SetPadding(0, Dp(4), 0, Dp(4));
            hintText.Text = Loc.T("Włączone filtry „Ukryj” zawsze wygrywają. Gdy włączony jest co najmniej jeden filtr „Pokaż”, widać tylko samoloty pasujące do któregokolwiek z nich. Zaznaczony samolot jest zawsze widoczny. Filtry działają też w Android Auto.");
            header.AddView(hintText);

            ScrollView scrollView = new ScrollView(this);
            root.AddView(scrollView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            listLayout = new LinearLayout(this);
            listLayout.Orientation = Orientation.Vertical;
            listLayout.SetPadding(0, 0, 0, Dp(32));
            scrollView.AddView(listLayout);

            return root;
        }

        /// <summary>
        /// Refreshes the list on the main thread after the filters changed.
        /// </summary>
        private void OnFiltersChanged()
        {
            RunOnUiThread(Render);
        }

        /// <summary>
        /// Shows all filters.
        /// </summary>
        private void Render()
        {
            if (listLayout == null)
            {
                return;
            }

            listLayout.RemoveAllViews();

            IReadOnlyList<AircraftFilter> filters = AppServices.Filters.All;

            for (int index = 0; index < filters.Count; index++)
            {
                listLayout.AddView(CreateRow(filters[index], index));
            }
        }

        /// <summary>
        /// Creates the row of a filter: the switch, the name with the description and the buttons.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="index">Position on the list.</param>
        /// <returns>The row.</returns>
        private View CreateRow(AircraftFilter filter, int index)
        {
            bool builtIn = AppServices.Filters.IsBuiltIn(filter.Id);

            LinearLayout row = new LinearLayout(this);
            row.Orientation = Orientation.Horizontal;
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(12), Dp(6), Dp(4), Dp(6));
            row.SetBackgroundColor(index % 2 == 0 ? Color.Argb(28, 255, 255, 255) : Color.Transparent);

            Switch toggle = new Switch(this);
            toggle.Checked = AppServices.Filters.IsActive(filter.Id);
            toggle.ContentDescription = Loc.T("Włącz filtr ") + filter.Name;
            toggle.CheckedChange += (sender, args) => AppServices.Filters.SetActive(filter.Id, args.IsChecked);
            row.AddView(toggle, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            LinearLayout texts = new LinearLayout(this);
            texts.Orientation = Orientation.Vertical;
            texts.SetPadding(Dp(12), 0, Dp(4), 0);

            TextView title = new TextView(this);
            title.Text = builtIn ? filter.Name + Loc.T(" (wbudowany)") : filter.Name;
            title.TextSize = 17f;
            title.SetTextColor(Color.White);
            title.SetTypeface(null, TypefaceStyle.Bold);
            texts.AddView(title);

            TextView details = new TextView(this);
            details.Text = FilterDescriber.Describe(filter, AppSettings.AltitudeUnit, AppSettings.VerticalSpeedUnit);
            details.TextSize = 13f;
            details.SetTextColor(Color.Rgb(190, 190, 190));
            texts.AddView(details);

            texts.Clickable = true;
            texts.Click += (sender, args) => OpenEditor(builtIn ? null : filter.Id, builtIn ? filter.Id : null);
            row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            if (builtIn)
            {
                row.AddView(CreateIconButton(Resource.Drawable.ic_visibility, Loc.T("Pokaż filtr (tylko do odczytu)"), () => OpenEditor(null, filter.Id)));
            }
            else
            {
                row.AddView(CreateIconButton(Resource.Drawable.ic_edit, Loc.T("Edytuj filtr"), () => OpenEditor(filter.Id, null)));
                row.AddView(CreateIconButton(Resource.Drawable.ic_delete, Loc.T("Usuń filtr"), () => ConfirmRemove(filter)));
            }

            return row;
        }

        /// <summary>
        /// Opens the filter editor.
        /// </summary>
        /// <param name="editedId">Identifier of the user-made filter to edit, or null to create a new one.</param>
        /// <param name="viewedId">Identifier of a predefined filter to show read-only, or null.</param>
        private void OpenEditor(string? editedId, string? viewedId)
        {
            Intent intent = new Intent(this, typeof(FilterEditorActivity));

            if (viewedId != null)
            {
                intent.PutExtra(FilterEditorActivity.ViewedIdExtra, viewedId);
            }

            if (editedId != null)
            {
                intent.PutExtra(FilterEditorActivity.EditedIdExtra, editedId);
            }

            StartActivity(intent);
        }

        /// <summary>
        /// Asks for confirmation and removes a user-made filter.
        /// </summary>
        /// <param name="filter">Filter to remove.</param>
        private void ConfirmRemove(AircraftFilter filter)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Usunąć filtr?"));
            builder.SetMessage(filter.Name);
            builder.SetPositiveButton(Loc.T("Usuń"), (sender, args) => AppServices.Filters.Remove(filter.Id));
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Creates a small round button of a row that shows a vector icon.
        /// </summary>
        /// <param name="iconResource">Identifier of the vector drawable.</param>
        /// <param name="description">Description for accessibility services.</param>
        /// <param name="action">Action performed on a tap.</param>
        /// <returns>The button.</returns>
        private View CreateIconButton(int iconResource, string description, System.Action action)
        {
            ImageView button = new ImageView(this);
            button.SetImageResource(iconResource);
            button.SetScaleType(ImageView.ScaleType.CenterInside);
            button.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
            button.ContentDescription = description;

            GradientDrawable circle = new GradientDrawable();
            circle.SetShape(ShapeType.Oval);
            circle.SetColor(Color.Argb(60, 255, 255, 255));
            circle.SetStroke(Dp(1), Color.Argb(150, 255, 255, 255));
            button.Background = circle;
            button.Click += (sender, args) => action();

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(Dp(36), Dp(36));
            parameters.SetMargins(Dp(4), 0, Dp(4), 0);
            button.LayoutParameters = parameters;

            return button;
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

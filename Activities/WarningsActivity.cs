using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Services;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen with the official notices of the regional warning system. The user chooses what is shown: each category separately, the voivodeships, whether notices that are over are shown, and whether only alarms are shown. The feed runs only while this screen is open.
    /// </summary>
    [Activity(Label = "Ostrzeżenia", Exported = false, Theme = "@android:style/Theme.Material")]
    public class WarningsActivity : Activity
    {
        /// <summary>
        /// Most notices listed at once, so that a long list does not slow the screen down.
        /// </summary>
        private const int MaximumListed = 100;

        /// <summary>
        /// Container of the whole content, rebuilt when the data changes.
        /// </summary>
        private LinearLayout? content;

        /// <summary>
        /// Identifiers of the notices whose full text is shown.
        /// </summary>
        private readonly HashSet<string> expanded = new HashSet<string>();

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Ostrzeżenia");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);

            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);
            content = new LinearLayout(this);
            content.Orientation = Orientation.Vertical;
            content.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(content);
            SetContentView(scrollView);
        }

        /// <summary>
        /// Starts the feed and shows the current notices.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            AppServices.Warnings.Changed -= OnFeedChanged;
            AppServices.Warnings.Changed += OnFeedChanged;
            AppServices.Warnings.Start();
            Rebuild();
        }

        /// <summary>
        /// Stops the feed, so that the application does not connect to the service while the screen is closed.
        /// </summary>
        protected override void OnPause()
        {
            AppServices.Warnings.Changed -= OnFeedChanged;
            AppServices.Warnings.Stop();
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
        /// Rebuilds the list on the main thread after the feed changed.
        /// </summary>
        private void OnFeedChanged()
        {
            RunOnUiThread(Rebuild);
        }

        /// <summary>
        /// Fills the screen: the state, the choice of what to show and the list of the notices.
        /// </summary>
        private void Rebuild()
        {
            if (content == null)
            {
                return;
            }

            content.RemoveAllViews();
            WarningFilterConfig config = AppServices.WarningConfig.Config;
            PollingFeed<Warning> feed = AppServices.Warnings;
            IReadOnlyList<Warning> selected = config.Select(feed.Items, DateTime.UtcNow);

            TextView attribution = AddText(WarningText.Attribution + " · " + WarningText.BuildStateText(feed.State, selected.Count), 14f, Color.Rgb(120, 190, 255));
            attribution.Click += (sender, args) => OpenSource();
            AddText(WarningText.Disclaimer, 13f, Color.Rgb(180, 180, 180));

            AddFilters(config, feed);
            AddSection(Loc.T("Komunikaty"));

            if (selected.Count == 0)
            {
                AddText(feed.State == PollingFeedState.Loading ? Loc.T("Pobieranie komunikatów…") : Loc.T("Brak komunikatów dla wybranego filtra."), 14f, Color.Rgb(180, 180, 180));
            }

            foreach (Warning warning in selected.Take(MaximumListed))
            {
                AddWarning(warning);
            }

            if (selected.Count > MaximumListed)
            {
                AddText(Loc.F("Pokazano {0} najnowszych z {1}. Zawęź filtr, żeby zobaczyć pozostałe.", MaximumListed, selected.Count), 13f, Color.Rgb(180, 180, 180));
            }
        }

        /// <summary>
        /// Adds the controls that choose what is shown.
        /// </summary>
        /// <param name="config">Current configuration.</param>
        /// <param name="feed">The feed, to learn the categories that really appear.</param>
        private void AddFilters(WarningFilterConfig config, PollingFeed<Warning> feed)
        {
            AddSection(Loc.T("Co pokazywać"));

            List<string> categories = new List<string>(WarningCategories.Known);

            foreach (string category in feed.Items.Select((Warning warning) => warning.Category).Concat(config.ShownCategories))
            {
                if (!categories.Any((string known) => string.Equals(known, category, StringComparison.OrdinalIgnoreCase)))
                {
                    categories.Add(category);
                }
            }

            foreach (string category in categories)
            {
                string code = category;
                int count = feed.Items.Count((Warning warning) => string.Equals(warning.Category, code, StringComparison.OrdinalIgnoreCase));
                string text = WarningCategories.GetName(code) + (count > 0 ? " (" + count + ")" : string.Empty);
                Switch categorySwitch = CreateSwitch(text, config.IsCategoryShown(code));
                categorySwitch.CheckedChange += (sender, args) => ApplyConfig(AppServices.WarningConfig.Config.WithCategory(code, args.IsChecked));
                content!.AddView(categorySwitch);
            }

            Button provinces = new Button(this);
            provinces.Text = Loc.T("Województwa: ") + DescribeProvinces(config);
            provinces.Click += (sender, args) => ChooseProvinces();
            content!.AddView(provinces);

            Switch expired = CreateSwitch(Loc.T("Pokazuj komunikaty, które wygasły"), config.ShowExpired);
            expired.CheckedChange += (sender, args) => ApplyConfig(AppServices.WarningConfig.Config.WithShowExpired(args.IsChecked));
            content.AddView(expired);

            Switch alarms = CreateSwitch(Loc.T("Tylko oznaczone jako alarm"), config.OnlyAlarms);
            alarms.CheckedChange += (sender, args) => ApplyConfig(AppServices.WarningConfig.Config.WithOnlyAlarms(args.IsChecked));
            content.AddView(alarms);

            Button reset = new Button(this);
            reset.Text = Loc.T("Przywróć domyślny wybór");
            reset.Click += (sender, args) => ApplyConfig(WarningFilterConfig.Default);
            content.AddView(reset);
        }

        /// <summary>
        /// Describes the chosen voivodeships in a few words.
        /// </summary>
        /// <param name="config">Current configuration.</param>
        /// <returns>"wszystkie", the names of up to three voivodeships, or their number.</returns>
        private static string DescribeProvinces(WarningFilterConfig config)
        {
            IReadOnlyList<string> slugs = config.Provinces;

            if (slugs.Count == 0)
            {
                return Loc.T("wszystkie");
            }

            return slugs.Count <= 3 ? string.Join(", ", slugs.Select((string slug) => WarningProvinces.GetName(slug))) : Loc.F("wybrane: {0}", slugs.Count);
        }

        /// <summary>
        /// Shows the list of voivodeships with check boxes. Choosing none means all of them.
        /// </summary>
        private void ChooseProvinces()
        {
            WarningFilterConfig config = AppServices.WarningConfig.Config;
            string[] names = WarningProvinces.All.Select((WarningProvince province) => province.Name).ToArray();
            bool[] checkedItems = WarningProvinces.All.Select((WarningProvince province) => config.Provinces.Contains(province.Slug, StringComparer.OrdinalIgnoreCase)).ToArray();

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Województwa"));
            builder.SetMultiChoiceItems(names, checkedItems, (sender, args) => checkedItems[args.Which] = args.IsChecked);
            builder.SetPositiveButton(Loc.T("Gotowe"), (sender, args) =>
            {
                IEnumerable<string> chosen = WarningProvinces.All.Where((WarningProvince province, int index) => checkedItems[index]).Select((WarningProvince province) => province.Slug);
                ApplyConfig(AppServices.WarningConfig.Config.WithProvinces(chosen));
            });
            builder.SetNeutralButton(Loc.T("Wszystkie"), (sender, args) => ApplyConfig(AppServices.WarningConfig.Config.WithProvinces(new List<string>())));
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Stores a new configuration, shows the result at once and downloads again, because the categories and the voivodeships decide what is asked for.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        private void ApplyConfig(WarningFilterConfig config)
        {
            AppServices.WarningConfig.Save(config);
            Rebuild();
            _ = Task.Run(async () =>
            {
                try
                {
                    await AppServices.Warnings.RefreshAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    DevLogger.Warn("Warnings", "Pobranie komunikatów po zmianie filtra nie powiodło się", exception);
                }
            });
        }

        /// <summary>
        /// Adds the card of one notice. A tap shows or hides the full text.
        /// </summary>
        /// <param name="warning">The notice.</param>
        private void AddWarning(Warning warning)
        {
            bool isExpanded = expanded.Contains(warning.Id);
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            card.SetBackgroundColor(warning.IsAlarm ? Color.Argb(70, 255, 80, 80) : Color.Argb(40, 255, 255, 255));

            TextView title = new TextView(this);
            title.Text = (warning.IsAlarm ? Loc.T("ALARM · ") : string.Empty) + WarningText.GetTitle(warning);
            title.TextSize = 16f;
            title.SetTypeface(null, TypefaceStyle.Bold);
            title.SetTextColor(warning.IsAlarm ? Color.Rgb(255, 150, 150) : Color.White);
            card.AddView(title);

            TextView subtitle = new TextView(this);
            subtitle.Text = WarningText.GetSubtitle(warning);
            subtitle.TextSize = 12f;
            subtitle.SetTextColor(Color.Rgb(180, 180, 180));
            card.AddView(subtitle);

            TextView body = new TextView(this);
            IReadOnlyList<string> lines = WarningText.GetLines(warning, DateTime.UtcNow, TimeZoneInfo.Local);
            body.Text = isExpanded ? string.Join("\n", lines) : lines[0];
            body.TextSize = 13f;
            body.SetTextColor(Color.White);
            body.SetPadding(0, Dp(6), 0, 0);
            body.SetMaxLines(isExpanded ? int.MaxValue : 2);
            body.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            card.AddView(body);

            card.Click += (sender, args) =>
            {
                if (!expanded.Add(warning.Id))
                {
                    expanded.Remove(warning.Id);
                }

                Rebuild();
            };

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, 0, 0, Dp(8));
            content!.AddView(card, parameters);
        }

        /// <summary>
        /// Opens the page of the feed of the notices.
        /// </summary>
        private void OpenSource()
        {
            try
            {
                StartActivity(new Android.Content.Intent(Android.Content.Intent.ActionView, Android.Net.Uri.Parse(DataSources.Rso.RsoClient.DefaultBaseAddress)));
            }
            catch (Android.Content.ActivityNotFoundException)
            {
                Toast.MakeText(this, DataSources.Rso.RsoClient.DefaultBaseAddress, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Creates a switch with white text.
        /// </summary>
        /// <param name="text">Text of the switch.</param>
        /// <param name="isChecked">Initial position.</param>
        /// <returns>The switch.</returns>
        private Switch CreateSwitch(string text, bool isChecked)
        {
            Switch result = new Switch(this);
            result.Text = text;
            result.Checked = isChecked;
            result.SetTextColor(Color.White);
            result.SetPadding(0, Dp(8), 0, Dp(8));

            return result;
        }

        /// <summary>
        /// Adds a section heading.
        /// </summary>
        /// <param name="text">Text of the heading.</param>
        private void AddSection(string text)
        {
            TextView heading = new TextView(this);
            heading.Text = text;
            heading.TextSize = 17f;
            heading.SetTextColor(Color.White);
            heading.SetTypeface(null, TypefaceStyle.Bold);
            heading.SetPadding(0, Dp(18), 0, Dp(6));
            content!.AddView(heading);
        }

        /// <summary>
        /// Adds a paragraph of text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <returns>The text view.</returns>
        private TextView AddText(string text, float size, Color color)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);
            view.SetPadding(0, Dp(4), 0, Dp(8));
            content!.AddView(view);

            return view;
        }

        /// <summary>
        /// Converts density-independent pixels to pixels.
        /// </summary>
        /// <param name="value">Size in dp.</param>
        /// <returns>Size in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1f));
        }
    }
}

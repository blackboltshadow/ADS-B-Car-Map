using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// "Dev log" screen: the unexpected events collected by the application, which the user can share, copy or clear, so that they can be passed on to the developer.
    /// </summary>
    [Activity(Label = "Dev log", Exported = false, Theme = "@android:style/Theme.Material")]
    public class DevLogActivity : Activity
    {
        /// <summary>
        /// Authority of the file provider declared in the manifest.
        /// </summary>
        private const string FileProviderAuthority = "com.blackboltshadow.adsbcarmap.fileprovider";

        /// <summary>
        /// Maximum number of entries drawn on the screen; the report contains all of them.
        /// </summary>
        private const int MaxShownEntries = 200;

        /// <summary>
        /// Time in milliseconds by which a refresh is delayed, so that a burst of events redraws the screen once.
        /// </summary>
        private const int RefreshDelayMilliseconds = 500;

        /// <summary>
        /// Container that holds the title, the buttons and the list; rebuilt after the log changes.
        /// </summary>
        private LinearLayout? root;

        /// <summary>
        /// Handler of the main thread used to delay the refresh.
        /// </summary>
        private Handler? handler;

        /// <summary>
        /// Whether a refresh is already planned.
        /// </summary>
        private bool refreshPending;

        /// <summary>
        /// Keys of the entries whose stack trace is open, so that they stay open after a refresh.
        /// </summary>
        private readonly HashSet<string> openEntries = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            handler = new Handler(Looper.MainLooper!);

            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(root);

            SetContentView(scrollView);
            Render();
        }

        /// <summary>
        /// Shows the log again and starts following its changes.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            DevLogger.Store.Log.Changed += OnLogChanged;
            Render();
        }

        /// <summary>
        /// Stops following the changes of the log.
        /// </summary>
        protected override void OnPause()
        {
            DevLogger.Store.Log.Changed -= OnLogChanged;
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
        /// Plans a refresh of the screen after the log changed, on any thread.
        /// </summary>
        private void OnLogChanged()
        {
            handler?.Post(() =>
            {
                if (refreshPending)
                {
                    return;
                }

                refreshPending = true;
                handler!.PostDelayed(() =>
                {
                    refreshPending = false;
                    Render();
                }, RefreshDelayMilliseconds);
            });
        }

        /// <summary>
        /// Fills the screen with the current content of the log.
        /// </summary>
        private void Render()
        {
            if (root == null)
            {
                return;
            }

            root.RemoveAllViews();

            List<DevLogEntry> entries = DevLogger.Store.Log.GetEntries();

            AddText("Dev log", 20f, Color.White, true, 0);
            AddText("Nieprzewidziane zdarzenia zebrane podczas używania aplikacji. Udostępnij lub skopiuj log i przekaż go twórcy, gdy coś nie działa jak powinno. Poziom, długość i łączenie wpisów ustawia się w Ustawieniach, w sekcji Dev.", 13f, Color.Rgb(180, 180, 180), false, 6);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(8), 0, Dp(8));
            root.AddView(buttons);

            Button share = new Button(this);
            share.Text = "Udostępnij";
            share.Enabled = entries.Count > 0;
            share.Click += (sender, args) => Share();
            buttons.AddView(share, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            Button copy = new Button(this);
            copy.Text = "Kopiuj";
            copy.Enabled = entries.Count > 0;
            copy.Click += (sender, args) => CopyToClipboard();
            buttons.AddView(copy, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            Button clear = new Button(this);
            clear.Text = "Wyczyść";
            clear.Enabled = entries.Count > 0;
            clear.Click += (sender, args) => ConfirmClear();
            buttons.AddView(clear, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            if (entries.Count == 0)
            {
                AddText("Log jest pusty. Zdarzenia pojawią się tu, gdy aplikacja napotka coś nieprzewidzianego na wybranym poziomie logowania.", 14f, Color.White, false, 12);

                return;
            }

            int shown = Math.Min(entries.Count, MaxShownEntries);

            AddText(entries.Count + (entries.Count == 1 ? " wpis" : " wpisów") + (shown < entries.Count ? " (pokazano " + shown + " najnowszych, raport zawiera wszystkie)" : string.Empty), 14f, Color.Rgb(120, 190, 255), true, 8);

            for (int index = 0; index < shown; index++)
            {
                AddEntry(entries[index]);
            }
        }

        /// <summary>
        /// Adds the rows that describe one entry of the log. A tap shows or hides the stack trace.
        /// </summary>
        /// <param name="entry">Entry of the log.</param>
        private void AddEntry(DevLogEntry entry)
        {
            string key = DevLog.GetLevelName(entry.Level) + "|" + entry.Tag + "|" + entry.Message + "|" + entry.Details.Length;
            bool open = openEntries.Contains(key);

            LinearLayout block = new LinearLayout(this);
            block.Orientation = Orientation.Vertical;
            block.Clickable = entry.Details.Length > 0;

            LinearLayout.LayoutParams blockParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            blockParams.SetMargins(0, Dp(12), 0, 0);
            root!.AddView(block, blockParams);

            string title = DevLog.GetLevelName(entry.Level) + "  " + entry.Tag + (entry.Count > 1 ? "  ×" + entry.Count : string.Empty);
            block.AddView(CreateText(title, 15f, GetLevelColor(entry.Level), true));
            block.AddView(CreateText(entry.Message, 13f, Color.White, false));
            block.AddView(CreateText(Describe(entry), 11f, Color.Rgb(150, 150, 150), false));

            if (entry.Details.Length == 0)
            {
                return;
            }

            TextView hint = CreateText(open ? "▴ Ukryj szczegóły" : "▾ Pokaż szczegóły", 12f, Color.Rgb(120, 190, 255), false);
            block.AddView(hint);

            TextView details = CreateText(entry.Details, 11f, Color.Rgb(200, 200, 200), false);
            details.Typeface = Typeface.Monospace;
            details.Visibility = open ? ViewStates.Visible : ViewStates.Gone;
            block.AddView(details);

            block.Click += (sender, args) =>
            {
                bool nowOpen = details.Visibility != ViewStates.Visible;

                details.Visibility = nowOpen ? ViewStates.Visible : ViewStates.Gone;
                hint.Text = nowOpen ? "▴ Ukryj szczegóły" : "▾ Pokaż szczegóły";

                if (nowOpen)
                {
                    openEntries.Add(key);
                }
                else
                {
                    openEntries.Remove(key);
                }
            };
        }

        /// <summary>
        /// Describes the time of the entry.
        /// </summary>
        /// <param name="entry">Entry of the log.</param>
        /// <returns>One line of text.</returns>
        private static string Describe(DevLogEntry entry)
        {
            string last = FormatTime(entry.LastUtc);

            return entry.Count > 1 ? "ostatnio " + last + " · pierwszy raz " + FormatTime(entry.FirstUtc) : last;
        }

        /// <summary>
        /// Returns the color of the title of an entry.
        /// </summary>
        /// <param name="level">Level of the entry.</param>
        /// <returns>Red for errors, orange for warnings, blue for information.</returns>
        private static Color GetLevelColor(DevLogLevel level)
        {
            switch (level)
            {
                case DevLogLevel.Error:
                    return Color.Rgb(255, 110, 110);
                case DevLogLevel.Warning:
                    return Color.Rgb(255, 180, 80);
                default:
                    return Color.Rgb(120, 190, 255);
            }
        }

        /// <summary>
        /// Formats a time in UTC as local date and time.
        /// </summary>
        /// <param name="utc">Time in UTC.</param>
        /// <returns>Date and time in the local zone.</returns>
        private static string FormatTime(DateTime utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Saves the report to a file and offers it to the other applications through the system share dialog.
        /// </summary>
        private void Share()
        {
            try
            {
                Java.IO.File file = DevLogger.Store.WriteReportFile();
                Android.Net.Uri? uri = AndroidX.Core.Content.FileProvider.GetUriForFile(this, FileProviderAuthority, file);

                Intent send = new Intent(Intent.ActionSend);
                send.SetType("text/plain");
                send.PutExtra(Intent.ExtraStream, uri);
                send.PutExtra(Intent.ExtraSubject, "ADS-B Car Map – Dev log");
                send.AddFlags(ActivityFlags.GrantReadUriPermission);

                StartActivity(Intent.CreateChooser(send, "Udostępnij Dev log"));
            }
            catch (Exception exception)
            {
                DevLogger.Error("DevLog", "Udostępnienie Dev logu nie powiodło się", exception);
                Toast.MakeText(this, "Nie udało się udostępnić logu: " + exception.Message, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Copies the whole report to the clipboard.
        /// </summary>
        private void CopyToClipboard()
        {
            ClipboardManager? clipboard = (ClipboardManager?)GetSystemService(ClipboardService);

            clipboard?.PrimaryClip = ClipData.NewPlainText("Dev log", DevLogger.Store.BuildReport(true));
            Toast.MakeText(this, "Skopiowano Dev log", ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Asks for confirmation and removes every entry of the log.
        /// </summary>
        private void ConfirmClear()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Wyczyścić log?");
            builder.SetMessage("Wszystkie wpisy Dev logu zostaną usunięte.");
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.SetPositiveButton("Wyczyść", (sender, args) =>
            {
                openEntries.Clear();
                DevLogger.Store.Clear();
                Render();
            });
            builder.Show();
        }

        /// <summary>
        /// Creates a text view.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <returns>The text view.</returns>
        private TextView CreateText(string text, float size, Color color, bool bold)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);

            if (bold)
            {
                view.SetTypeface(null, TypefaceStyle.Bold);
            }

            return view;
        }

        /// <summary>
        /// Adds a text to the screen.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <param name="topMargin">Space above the text in dp.</param>
        private void AddText(string text, float size, Color color, bool bold, int topMargin)
        {
            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(topMargin), 0, 0);
            root!.AddView(CreateText(text, size, color, bold), parameters);
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

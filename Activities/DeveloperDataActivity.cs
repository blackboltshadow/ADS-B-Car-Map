using System.Globalization;
using System.Text;
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
    /// "Developer data" screen: the aircraft types seen in the traffic that the type table does not describe yet, collected while the map is in use, so that the table can be completed.
    /// </summary>
    [Activity(Label = "Dane developerskie", Exported = false, Theme = "@android:style/Theme.Material")]
    public class DeveloperDataActivity : Activity
    {
        /// <summary>
        /// Container that holds the title, the buttons and the list; rebuilt after the log changes.
        /// </summary>
        private LinearLayout? root;

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ActionBar?.SetDisplayHomeAsUpEnabled(true);

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
        /// Shows the log again, because it may have grown while the screen was in the background.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            Render();
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
        /// Fills the screen with the current content of the log.
        /// </summary>
        private void Render()
        {
            if (root == null)
            {
                return;
            }

            root.RemoveAllViews();

            AppServices.MissingTypes.Prune();

            List<MissingTypeEntry> entries = AppServices.MissingTypes.Missing.GetEntries();
            AircraftTypeTable table = AircraftTypeTable.Current;

            AddText("Brakujące typy samolotów", 20f, Color.White, true, 0);
            AddText("Typy widziane w ruchu, których nie ma w tabeli typów (" + table.TypeCount + " typów w tabeli). Dla takich typów nie działa kod IATA ani wybór całej rodziny w filtrach i nie ma pełnej nazwy. Lista powstaje sama podczas używania mapy i jest zapamiętywana.", 13f, Color.Rgb(180, 180, 180), false, 6);
            AddText("Liczba oznacza samoloty zauważone od uruchomienia aplikacji; samolot widziany po ponownym uruchomieniu liczy się jeszcze raz.", 12f, Color.Rgb(150, 150, 150), false, 4);

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(0, Dp(8), 0, Dp(8));
            root.AddView(buttons);

            Button copy = new Button(this);
            copy.Text = "Kopiuj listę";
            copy.Enabled = entries.Count > 0;
            copy.Click += (sender, args) => CopyToClipboard(entries);
            buttons.AddView(copy, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            Button clear = new Button(this);
            clear.Text = "Wyczyść listę";
            clear.Enabled = entries.Count > 0;
            clear.Click += (sender, args) => ConfirmClear();
            buttons.AddView(clear, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            if (entries.Count == 0)
            {
                AddText(table.TypeCount == 0 ? "Tabela typów nie została wczytana, więc lista nie jest zbierana." : "Lista jest pusta. Wszystkie typy widziane do tej pory są w tabeli.", 14f, Color.White, false, 12);

                return;
            }

            AddText(entries.Count + " typów", 14f, Color.Rgb(120, 190, 255), true, 8);

            foreach (MissingTypeEntry entry in entries)
            {
                AddEntry(entry);
            }
        }

        /// <summary>
        /// Adds the rows that describe one missing type.
        /// </summary>
        /// <param name="entry">Entry of the log.</param>
        private void AddEntry(MissingTypeEntry entry)
        {
            AddText(entry.Icao + "  ×" + entry.Count, 16f, Color.White, true, 12);
            AddText(Describe(entry), 12f, Color.Rgb(180, 180, 180), false, 1);
        }

        /// <summary>
        /// Describes the example aircraft and the time the type was seen.
        /// </summary>
        /// <param name="entry">Entry of the log.</param>
        /// <returns>One line of text.</returns>
        private static string Describe(MissingTypeEntry entry)
        {
            List<string> parts = new List<string>();

            if (entry.Callsign.Length > 0)
            {
                parts.Add(entry.Callsign);
            }

            if (entry.Registration.Length > 0)
            {
                parts.Add(entry.Registration);
            }

            if (entry.Category.Length > 0)
            {
                parts.Add("kategoria " + entry.Category);
            }

            if (entry.IsMilitary)
            {
                parts.Add("wojskowy");
            }

            parts.Add("pierwszy raz " + FormatTime(entry.FirstSeenUtc));
            parts.Add("ostatnio " + FormatTime(entry.LastSeenUtc));

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Formats a time in UTC as local date and time.
        /// </summary>
        /// <param name="utc">Time in UTC.</param>
        /// <returns>Date and time in the local zone.</returns>
        private static string FormatTime(DateTime utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Copies the log to the clipboard as tab separated text that can be pasted into a table.
        /// </summary>
        /// <param name="entries">Entries to copy.</param>
        private void CopyToClipboard(List<MissingTypeEntry> entries)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("icao\tcount\tcallsign\tregistration\tcategory\tmilitary\tfirst_seen\tlast_seen\n");

            foreach (MissingTypeEntry entry in entries)
            {
                builder.Append(entry.Icao).Append('\t')
                    .Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(entry.Callsign).Append('\t')
                    .Append(entry.Registration).Append('\t')
                    .Append(entry.Category).Append('\t')
                    .Append(entry.IsMilitary ? "tak" : "nie").Append('\t')
                    .Append(FormatTime(entry.FirstSeenUtc)).Append('\t')
                    .Append(FormatTime(entry.LastSeenUtc)).Append('\n');
            }

            ClipboardManager? clipboard = (ClipboardManager?)GetSystemService(ClipboardService);

            clipboard?.PrimaryClip = ClipData.NewPlainText("Brakujące typy samolotów", builder.ToString());
            Toast.MakeText(this, "Skopiowano " + entries.Count + " typów", ToastLength.Short)?.Show();
        }

        /// <summary>
        /// Asks for confirmation and removes every entry of the log.
        /// </summary>
        private void ConfirmClear()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle("Wyczyścić listę?");
            builder.SetMessage("Lista brakujących typów zostanie usunięta. Typy pojawią się znowu, gdy samoloty tych typów znów będą widoczne.");
            builder.SetNegativeButton("Anuluj", (sender, args) => { });
            builder.SetPositiveButton("Wyczyść", (sender, args) =>
            {
                AppServices.MissingTypes.Clear();
                Render();
            });
            builder.Show();
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
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);

            if (bold)
            {
                view.SetTypeface(null, TypefaceStyle.Bold);
            }

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(topMargin), 0, 0);
            root!.AddView(view, parameters);
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

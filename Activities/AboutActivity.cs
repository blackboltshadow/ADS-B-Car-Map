using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Text.Util;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// "About the application" screen: version, license, the data sources with their attribution, and what the application sends to the servers.
    /// </summary>
    [Activity(Label = "O aplikacji", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AboutActivity : Activity
    {
        /// <summary>
        /// Address of the project repository.
        /// </summary>
        private const string RepositoryUrl = "https://github.com/blackboltshadow/ADS-B-Car-Map";

        /// <summary>
        /// Builds the screen and enables the back arrow.
        /// </summary>
        /// <param name="savedInstanceState">Saved state of the activity.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("O aplikacji");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
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
        /// <returns>Scrollable view with the sections.</returns>
        private View BuildContent()
        {
            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
            scrollView.AddView(root);

            AddText(root, BuildInfo.AppName, 22f, Color.White, true, 0);
            AddText(root, Loc.T("Wersja ") + BuildInfo.Version + Loc.T(" (kompilacja ") + BuildInfo.Build + ")", 14f, Color.Rgb(180, 180, 180), false, 2);
            AddText(root, Loc.T("Mapa samolotów ADS-B na telefonie i w Android Auto. Aplikacja na licencji MIT, kod źródłowy: ") + RepositoryUrl, 14f, Color.White, false, 8, true);

            AddSection(root, Loc.T("Mapa"));
            AddText(root, Loc.T("© współtwórcy OpenStreetMap, dane na licencji ODbL (openstreetmap.org/copyright). Kafelki mapy pochodzą z serwera tile.openstreetmap.org."), 14f, Color.White, false, 4, true);

            AddSection(root, Loc.T("Samoloty i trasy"));
            AddText(root, Loc.T("Pozycje samolotów: adsb.lol, airplanes.live albo adsb.fi (otwarte dane ADS-B, wybór w ustawieniach zaawansowanych). Trasy lotów: adsbdb.com, adsb.lol i hexdb.io (kolejność w ustawieniach zaawansowanych), dane samolotów (pełna nazwa typu, właściciel): adsbdb.com i hexdb.io."), 14f, Color.White, false, 4, true);

            AddSection(root, Loc.T("Lotniska i pasy startowe"));
            AddText(root, Loc.T("Dane lotnisk i pasów startowych pochodzą z projektu OurAirports (ourairports.com), udostępnionego jako domena publiczna. ") + GetAirportDataDate(), 14f, Color.White, false, 4, true);
            AddText(root, Loc.T("Dane są dołączone do aplikacji i aktualizowane przy każdym wydaniu. Lotnisko wojskowe jest rozpoznawane po nazwie, więc zdarzają się pomyłki."), 13f, Color.Rgb(180, 180, 180), false, 6);
            AddText(root, Loc.T("Pogoda lotnisk (METAR): Aviation Weather Center, NOAA (aviationweather.gov). Raport dotyczy tylko lotnisk ze stacją pogodową, bywa spóźniony i nie zastępuje oficjalnej odprawy meteorologicznej ani nie służy do planowania lotów."), 14f, Color.White, false, 4, true);

            AddSection(root, Loc.T("Ostrzeżenia i przestrzeń powietrzna"));
            AddText(root, Loc.T("Zagrożenia powietrzne: NEPTUN (neptun.in.ua). Strefy przestrzeni powietrznej: PAŻP (airspace.pansa.pl). Komunikaty ostrzegawcze: publiczny kanał TVP (komunikaty.tvp.pl)."), 14f, Color.White, false, 4, true);
            AddText(root, Loc.T("To nieoficjalne kanały danych. Aplikacja nie jest oficjalnym systemem ostrzegania ani nie służy do planowania lotów. W razie zagrożenia kieruj się komunikatami służb."), 13f, Color.Rgb(180, 180, 180), false, 6);

            AddSection(root, Loc.T("Prywatność"));
            AddText(root, Loc.T("Aplikacja nie ma kont ani analityki i nie wysyła niczego poza tym, co potrzebne do działania mapy: do serwera samolotów (adsb.lol, airplanes.live lub adsb.fi, a gdy pierwszy nie odpowiada i zapasowe źródło jest włączone, do kolejnych) środek i promień widocznego obszaru mapy (gdy mapa podąża za Tobą, jest to Twoja okolica), do serwisów tras i danych samolotów (adsbdb.com, adsb.lol, hexdb.io, w kolejności z ustawień, a przy braku danych do kolejnych) znak wywoławczy lub kod ICAO zaznaczonego lub wyszukiwanego lotu, do serwera kafelków numery kafelków. Do każdego z nich dochodzi nagłówek User-Agent z nazwą aplikacji i opcjonalnym kontaktem właściciela. Twoja pozycja GPS służy tylko do wyświetlania na mapie i liczenia odległości na urządzeniu."), 14f, Color.White, false, 4);
            AddText(root, Loc.T("Pogoda lotnisk: gdy otwierasz kartę lotniska, do aviationweather.gov trafia kod ICAO tego lotniska (bez Twojej pozycji). Można to wyłączyć w ustawieniach lotnisk."), 14f, Color.White, false, 4);
            AddText(root, Loc.T("Pogoda na znacznikach: aplikacja pyta aviationweather.gov o kody ICAO większych lotnisk widocznych na mapie (bez współrzędnych). Można to wyłączyć w ustawieniach lotnisk."), 14f, Color.White, false, 4);

            AddSection(root, Loc.T("Biblioteki"));
            AddText(root, Loc.T("AndroidX Car App Library (Android Auto) i inne biblioteki AndroidX, Kotlin i Guava (licencja Apache 2.0) oraz .NET for Android i powiązania Xamarin (licencja MIT). Pełna lista i teksty licencji: ") + RepositoryUrl + "/blob/main/THIRD-PARTY-NOTICES.md", 14f, Color.White, false, 4, true);

            return scrollView;
        }

        /// <summary>
        /// Returns the sentence with the date of the bundled airport data, or an empty string when it is not known.
        /// </summary>
        /// <returns>Sentence such as "Baza z dnia 2026-11-01 (48 063 lotnisk).", or an empty string.</returns>
        private string GetAirportDataDate()
        {
            try
            {
                using Stream stream = Assets!.Open("Airports/airports.meta");
                using StreamReader reader = new StreamReader(stream);
                string? date = null;
                string? count = null;
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("date=", StringComparison.Ordinal))
                    {
                        date = line.Substring(5);
                    }
                    else if (line.StartsWith("airports=", StringComparison.Ordinal))
                    {
                        count = line.Substring(9);
                    }
                }

                return date == null ? string.Empty : Loc.T("Baza z dnia ") + date + (count == null ? string.Empty : " (" + count + Loc.T(" lotnisk)."));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Adds a section heading.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="title">Heading text.</param>
        private void AddSection(LinearLayout root, string title)
        {
            AddText(root, title, 16f, Color.Rgb(120, 190, 255), true, 20);
        }

        /// <summary>
        /// Adds a text to the screen.
        /// </summary>
        /// <param name="root">Container of the screen.</param>
        /// <param name="text">Text.</param>
        /// <param name="size">Text size in sp.</param>
        /// <param name="color">Text color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <param name="topMargin">Space above the text in dp.</param>
        /// <param name="linkify">Whether web addresses in the text become links.</param>
        private void AddText(LinearLayout root, string text, float size, Color color, bool bold, int topMargin, bool linkify = false)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);

            if (bold)
            {
                view.SetTypeface(null, TypefaceStyle.Bold);
            }

            if (linkify)
            {
                Linkify.AddLinks(view, MatchOptions.WebUrls);
                view.SetLinkTextColor(Color.Rgb(120, 190, 255));
            }

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(topMargin), 0, 0);
            root.AddView(view, parameters);
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

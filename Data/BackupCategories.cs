using System.Globalization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Where the data of a backup category is kept.
    /// </summary>
    public enum BackupSourceKind
    {
        /// <summary>
        /// A SharedPreferences file.
        /// </summary>
        Preferences,

        /// <summary>
        /// A file in the files folder of the application.
        /// </summary>
        File
    }

    /// <summary>
    /// A group of the application data that the user can include in the backup or leave out.
    /// </summary>
    /// <param name="Id">Stable identifier saved in the backups.</param>
    /// <param name="Title">Name shown to the user, in Polish; it is translated where it is shown.</param>
    /// <param name="Description">Short description shown to the user, in Polish; it is translated where it is shown.</param>
    /// <param name="Kind">Kind of the place where the data is kept.</param>
    /// <param name="Source">Name of the SharedPreferences file or of the file in the files folder.</param>
    /// <param name="DefaultSelected">Whether the category is in the backup until the user changes it.</param>
    /// <param name="ExcludedKeys">Values of a SharedPreferences file that depend on the phone and are never put in a backup nor overwritten when a backup is restored.</param>
    public sealed record BackupCategory(string Id, string Title, string Description, BackupSourceKind Kind, string Source, bool DefaultSelected, IReadOnlyList<string> ExcludedKeys);

    /// <summary>
    /// Catalog of the categories of the backup and the text format of the choice of the user.
    /// </summary>
    public static class BackupCategories
    {
        /// <summary>
        /// Separator of the identifiers in the saved choice.
        /// </summary>
        private const char Separator = ',';

        /// <summary>
        /// All categories in the order in which they are shown.
        /// </summary>
        public static readonly IReadOnlyList<BackupCategory> All = new List<BackupCategory>
        {
            new BackupCategory("settings", "Ustawienia aplikacji", "Wszystkie ustawienia z ekranu Ustawienia.", BackupSourceKind.Preferences, "adsb_car_map_settings", true, new List<string>()),
            new BackupCategory("filters", "Filtry samolotów", "Własne filtry i to, które są włączone.", BackupSourceKind.Preferences, "adsb_car_map_filters", true, new List<string>()),
            new BackupCategory("alerts", "Alerty", "Reguły alertów razem z alertami „Obserwuj”. Obszary mogą zdradzać miejsce, wokół którego są ustawione.", BackupSourceKind.Preferences, "adsb_car_map_alerts", true, new List<string> { "background" }),
            new BackupCategory("favorites", "Ulubione lotniska", "Lista ulubionych lotnisk.", BackupSourceKind.Preferences, "adsb_car_map_favorites", true, new List<string>()),
            new BackupCategory("airspace", "Strefy powietrzne: wybór", "Wybrane rodzaje i stany stref, filtr wysokości i powiadomienia o strefach.", BackupSourceKind.Preferences, "adsb_car_map_airspace", true, new List<string>()),
            new BackupCategory("warnings", "Ostrzeżenia RSO: wybór", "Wybrane kategorie i województwa oraz powiadomienia o komunikatach.", BackupSourceKind.Preferences, "adsb_car_map_warnings", true, new List<string>()),
            new BackupCategory("aircraftHistory", "Historia samolotów", "Samoloty zaznaczone na mapie lub obserwowane, z przypięciami.", BackupSourceKind.File, "aircraft-history.json", true, new List<string>()),
            new BackupCategory("aircraftSightings", "Obserwacje pozycji samolotów", "Zapisane przeloty i pozycje samolotów z historii. Może zająć dużo miejsca.", BackupSourceKind.File, "aircraft-sightings.json", false, new List<string>()),
            new BackupCategory("alertHistory", "Historia alertów", "Lista powiadomień pokazanych przez alerty.", BackupSourceKind.File, "alert-history.json", false, new List<string>())
        };

        /// <summary>
        /// Finds a category.
        /// </summary>
        /// <param name="id">Identifier of the category.</param>
        /// <returns>The category or null when there is none with the identifier.</returns>
        public static BackupCategory? Find(string id)
        {
            return All.FirstOrDefault((BackupCategory category) => category.Id == id);
        }

        /// <summary>
        /// Gives the identifiers of the categories that are in the backup until the user changes the choice.
        /// </summary>
        /// <returns>Identifiers in the order of the catalog.</returns>
        public static List<string> DefaultSelection()
        {
            return All.Where((BackupCategory category) => category.DefaultSelected).Select((BackupCategory category) => category.Id).ToList();
        }

        /// <summary>
        /// Writes a choice as text.
        /// </summary>
        /// <param name="ids">Identifiers of the chosen categories; unknown ones are dropped.</param>
        /// <returns>Identifiers in the order of the catalog, separated with commas; an empty string for no category.</returns>
        public static string FormatSelection(IEnumerable<string> ids)
        {
            HashSet<string> chosen = new HashSet<string>(ids);

            return string.Join(Separator, All.Where((BackupCategory category) => chosen.Contains(category.Id)).Select((BackupCategory category) => category.Id));
        }

        /// <summary>
        /// Reads a choice written by <see cref="FormatSelection"/>.
        /// </summary>
        /// <param name="text">Saved text, or null when the user has not chosen yet.</param>
        /// <returns>Identifiers of the chosen categories; the default choice for null, an empty list for an empty text.</returns>
        public static List<string> ParseSelection(string? text)
        {
            if (text == null)
            {
                return DefaultSelection();
            }

            HashSet<string> chosen = new HashSet<string>(text.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            return All.Where((BackupCategory category) => chosen.Contains(category.Id)).Select((BackupCategory category) => category.Id).ToList();
        }
    }

    /// <summary>
    /// Formats sizes of data in bytes for the screen of the backup.
    /// </summary>
    public static class ByteSizeFormatter
    {
        /// <summary>
        /// Writes a size as bytes, kilobytes or megabytes.
        /// </summary>
        /// <param name="bytes">Size in bytes.</param>
        /// <param name="culture">Culture that gives the decimal separator.</param>
        /// <returns>Text such as "812 B", "12,4 KB" or "1,35 MB".</returns>
        public static string Format(long bytes, CultureInfo culture)
        {
            if (bytes < 1024)
            {
                return bytes.ToString(culture) + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0.0", culture) + " KB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0.00", culture) + " MB";
        }
    }
}

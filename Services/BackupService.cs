using System.Globalization;
using System.Text;
using AdsbCarMap.Data;
using Android.App.Backup;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Builds the backup of the application data from the preferences and the files, writes it back, and prepares the file that the system backup takes. It uses only the Android context, not the services of the application, so that the backup agent can use it when the application is started only to make or restore a backup.
    /// </summary>
    public static class BackupService
    {
        /// <summary>
        /// Name of the folder, in the files folder of the application, that the system backup takes.
        /// </summary>
        public const string StagedDirectoryName = "backup";

        /// <summary>
        /// Name of the file with the backup that the system takes.
        /// </summary>
        private const string StagedFileName = "adsbcarmap-backup.json";

        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "BackupService";

        /// <summary>
        /// Gives the path of the file with the backup that the system takes.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <returns>Full path.</returns>
        public static string GetStagedPath(Context context)
        {
            return System.IO.Path.Combine(context.FilesDir!.AbsolutePath, StagedDirectoryName, StagedFileName);
        }

        /// <summary>
        /// Reads the current data of a category as text.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <param name="category">Category to read.</param>
        /// <returns>The data, or null when a file of the category does not exist yet.</returns>
        public static string? ReadCategory(Context context, BackupCategory category)
        {
            if (category.Kind == BackupSourceKind.Preferences)
            {
                ISharedPreferences preferences = context.GetSharedPreferences(category.Source, FileCreationMode.Private)!;

                return PreferenceSnapshot.Serialize(ReadEntries(preferences)
                    .Where((PreferenceEntry entry) => !category.ExcludedKeys.Contains(entry.Key))
                    .OrderBy((PreferenceEntry entry) => entry.Key, StringComparer.Ordinal));
            }

            string path = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, category.Source);

            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path, Encoding.UTF8) : null;
        }

        /// <summary>
        /// Builds a backup of the chosen categories from the current data.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <param name="ids">Identifiers of the categories to include.</param>
        /// <returns>The backup; categories whose file does not exist are left out.</returns>
        public static BackupBundle BuildBundle(Context context, IEnumerable<string> ids)
        {
            AircraftSightingStore.Flush();

            HashSet<string> chosen = new HashSet<string>(ids);
            List<BackupSection> sections = new List<BackupSection>();

            foreach (BackupCategory category in BackupCategories.All.Where((BackupCategory item) => chosen.Contains(item.Id)))
            {
                string? content = ReadCategory(context, category);

                if (content != null)
                {
                    sections.Add(new BackupSection(category.Id, content));
                }
            }

            return new BackupBundle(DateTime.UtcNow, BuildInfo.Version, sections);
        }

        /// <summary>
        /// Measures how much space the data of each category takes in a backup.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <returns>Sizes in bytes by identifier of the category; a category without data has size 0.</returns>
        public static Dictionary<string, long> MeasureCategories(Context context)
        {
            AircraftSightingStore.Flush();

            Dictionary<string, long> sizes = new Dictionary<string, long>();

            foreach (BackupCategory category in BackupCategories.All)
            {
                string? content = ReadCategory(context, category);
                sizes[category.Id] = content == null ? 0L : Encoding.UTF8.GetByteCount(content);
            }

            return sizes;
        }

        /// <summary>
        /// Writes the backup that the system takes, from the current data and the choice of the user. It removes the file instead when the backup is switched off or nothing is chosen.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <returns>Size of the written file in bytes, or 0 when no file is kept.</returns>
        public static long StageForSystem(Context context)
        {
            string path = GetStagedPath(context);
            List<string> selection = BackupConfigStore.Selection;

            if (!BackupConfigStore.Enabled || selection.Count == 0)
            {
                DeleteStaged(context);
                BackupConfigStore.ClearPrepared();

                return 0L;
            }

            BackupBundle bundle = BuildBundle(context, selection);
            string text = bundle.Serialize();
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            string? directory = System.IO.Path.GetDirectoryName(path);

            if (directory != null)
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            string temporary = path + ".tmp";

            System.IO.File.WriteAllBytes(temporary, bytes);
            System.IO.File.Move(temporary, path, true);
            BackupConfigStore.RecordPrepared(bytes.LongLength, BackupHashes.Format(BackupHashes.OfSections(bundle.Sections)));

            return bytes.LongLength;
        }

        /// <summary>
        /// Compares the data that would be backed up now with the data that the system took last time.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <returns>Whether a backup is made, whether the system has taken one, and which categories have newer data than the backup that the system holds.</returns>
        public static BackupSyncState GetSyncState(Context context)
        {
            List<string> selection = BackupConfigStore.Selection;
            Dictionary<string, string>? delivered = BackupConfigStore.DeliveredHashes;

            if (!BackupConfigStore.Enabled || selection.Count == 0)
            {
                return new BackupSyncState(false, delivered != null, new List<string>());
            }

            Dictionary<string, string> current = BackupHashes.OfSections(BuildBundle(context, selection).Sections);

            return new BackupSyncState(true, delivered != null, BackupHashes.FindPending(current, delivered));
        }

        /// <summary>
        /// Removes the file with the backup that the system takes.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        public static void DeleteStaged(Context context)
        {
            string path = GetStagedPath(context);

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Asks the system to make a backup. The system decides when it runs, usually within a day, when the phone is charging, on Wi-Fi and idle.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        public static void RequestSystemBackup(Context context)
        {
            new BackupManager(context).DataChanged();
            BackupConfigStore.RecordRequested();
        }

        /// <summary>
        /// Writes the data of the chosen categories of a backup into the preferences and the files of the application, replacing the current data of these categories.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <param name="bundle">Backup to apply.</param>
        /// <param name="ids">Identifiers of the categories to apply; categories that the backup does not contain are skipped.</param>
        /// <returns>Identifiers of the categories that were written.</returns>
        public static List<string> ApplyBundle(Context context, BackupBundle bundle, IEnumerable<string> ids)
        {
            HashSet<string> chosen = new HashSet<string>(ids);
            List<string> applied = new List<string>();

            foreach (BackupCategory category in BackupCategories.All.Where((BackupCategory item) => chosen.Contains(item.Id)))
            {
                BackupSection? section = bundle.Find(category.Id);

                if (section == null)
                {
                    continue;
                }

                if (category.Kind == BackupSourceKind.Preferences)
                {
                    List<PreferenceEntry>? entries = PreferenceSnapshot.Parse(section.Content);

                    if (entries == null)
                    {
                        DevLogger.Warn(LogTag, "Pominięto uszkodzoną sekcję kopii: " + category.Id);

                        continue;
                    }

                    WritePreferences(context, category, entries);
                }
                else
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(context.FilesDir!.AbsolutePath, category.Source), section.Content, new UTF8Encoding(false));
                }

                applied.Add(category.Id);
            }

            return applied;
        }

        /// <summary>
        /// Applies the backup that the system restored into the files folder, when the application was installed on a new phone or again. The choice of the categories is set to the categories that the backup contained.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <returns>Identifiers of the categories that were restored; empty when there is no readable backup.</returns>
        public static List<string> RestoreFromStaged(Context context)
        {
            string path = GetStagedPath(context);

            if (!System.IO.File.Exists(path))
            {
                return new List<string>();
            }

            if (BackupBundle.TryParse(System.IO.File.ReadAllText(path, Encoding.UTF8), out BackupBundle? bundle) != BackupParseStatus.Ok || bundle == null)
            {
                DevLogger.Warn(LogTag, "Przywrócona kopia jest nieczytelna lub z nowszej wersji aplikacji");

                return new List<string>();
            }

            List<string> applied = ApplyBundle(context, bundle, bundle.Sections.Select((BackupSection section) => section.Id));

            BackupConfigStore.Selection = applied;
            BackupConfigStore.RecordDelivered(BackupHashes.Format(BackupHashes.OfSections(BuildBundle(context, applied).Sections)));
            BackupConfigStore.RecordRestored();
            DevLogger.Info(LogTag, "Przywrócono z kopii kategorie: " + string.Join(", ", applied));

            return applied;
        }

        /// <summary>
        /// Reads all the values of a SharedPreferences file with their types.
        /// </summary>
        /// <param name="preferences">Preferences to read.</param>
        /// <returns>The values; values of unknown types are skipped.</returns>
        private static List<PreferenceEntry> ReadEntries(ISharedPreferences preferences)
        {
            List<PreferenceEntry> entries = new List<PreferenceEntry>();
            IDictionary<string, object>? all = preferences.All;

            if (all == null)
            {
                return entries;
            }

            foreach (KeyValuePair<string, object> pair in all)
            {
                object? value = ConvertValue(preferences, pair.Key, pair.Value);

                if (value != null)
                {
                    entries.Add(new PreferenceEntry(pair.Key, value));
                }
            }

            return entries;
        }

        /// <summary>
        /// Converts a value returned by SharedPreferences to one of the types that a snapshot keeps. The value arrives either as a .NET type or as a Java object, depending on the type.
        /// </summary>
        /// <param name="preferences">Preferences that the value comes from.</param>
        /// <param name="key">Name of the value.</param>
        /// <param name="value">Value returned by SharedPreferences.</param>
        /// <returns>A string, an int, a long, a float, a bool or a list of strings; null when the type is unknown.</returns>
        private static object? ConvertValue(ISharedPreferences preferences, string key, object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string or int or long or float or bool:
                    return value;
                case Java.Lang.Object javaObject:
                    string text = javaObject.ToString() ?? string.Empty;

                    switch (javaObject.Class?.SimpleName)
                    {
                        case "String":
                            return text;
                        case "Integer":
                            return int.Parse(text, CultureInfo.InvariantCulture);
                        case "Long":
                            return long.Parse(text, CultureInfo.InvariantCulture);
                        case "Float":
                            return float.Parse(text, CultureInfo.InvariantCulture);
                        case "Boolean":
                            return bool.Parse(text);
                    }

                    break;
            }

            ICollection<string>? items = value is System.Collections.IEnumerable ? preferences.GetStringSet(key, null) : null;

            return items == null ? null : new List<string>(items.OrderBy((string item) => item, StringComparer.Ordinal));
        }

        /// <summary>
        /// Replaces the values of a SharedPreferences file with the given ones. The values that the category never takes in a backup are kept as they are.
        /// </summary>
        /// <param name="context">Context of the application.</param>
        /// <param name="category">Category of the preferences.</param>
        /// <param name="entries">New values.</param>
        private static void WritePreferences(Context context, BackupCategory category, List<PreferenceEntry> entries)
        {
            ISharedPreferences preferences = context.GetSharedPreferences(category.Source, FileCreationMode.Private)!;
            List<PreferenceEntry> kept = ReadEntries(preferences).Where((PreferenceEntry entry) => category.ExcludedKeys.Contains(entry.Key)).ToList();
            ISharedPreferencesEditor editor = preferences.Edit()!;

            editor.Clear();

            foreach (PreferenceEntry entry in entries.Where((PreferenceEntry item) => !category.ExcludedKeys.Contains(item.Key)).Concat(kept))
            {
                switch (entry.Value)
                {
                    case string text:
                        editor.PutString(entry.Key, text);
                        break;
                    case int number:
                        editor.PutInt(entry.Key, number);
                        break;
                    case long number:
                        editor.PutLong(entry.Key, number);
                        break;
                    case float number:
                        editor.PutFloat(entry.Key, number);
                        break;
                    case bool flag:
                        editor.PutBoolean(entry.Key, flag);
                        break;
                    case IEnumerable<string> items:
                        editor.PutStringSet(entry.Key, new List<string>(items));
                        break;
                }
            }

            editor.Commit();
        }
    }
}

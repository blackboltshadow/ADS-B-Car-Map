using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;
using Android.OS;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Keeps the developer log in a file in the private storage of the application, so that it survives restarts and crashes, and builds the report that the user passes on. The file is written a few seconds after the last change, or at once on request.
    /// </summary>
    public sealed class DevLogStore
    {
        /// <summary>
        /// Name of the file with the log.
        /// </summary>
        private const string FileName = "dev-log.tsv";

        /// <summary>
        /// Tag used in the Android log. The store reports its own failures only there, never to the developer log, to avoid a loop.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Time between the last change of the log and writing the file.
        /// </summary>
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Lock protecting the saving state.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Path of the file with the log.
        /// </summary>
        private readonly string path;

        /// <summary>
        /// Timer that writes the file after <see cref="SaveDelay"/>.
        /// </summary>
        private readonly Timer saveTimer;

        /// <summary>
        /// Whether the log has changes that are not saved yet.
        /// </summary>
        private bool dirty;

        /// <summary>
        /// Creates the store, reads the saved log and applies the settings.
        /// </summary>
        public DevLogStore()
        {
            path = System.IO.Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, FileName);
            saveTimer = new Timer((object? state) => Flush(), null, Timeout.Infinite, Timeout.Infinite);

            ApplySettings();

            try
            {
                if (File.Exists(path))
                {
                    Log.Load(File.ReadAllText(path));
                }
            }
            catch (Exception exception)
            {
                Android.Util.Log.Warn(LogTag, "Wczytanie Dev logu nie powiodło się: " + exception.Message);
            }

            Log.Changed += OnLogChanged;
            AppSettings.Saved += ApplySettings;
        }

        /// <summary>
        /// The log.
        /// </summary>
        public DevLog Log { get; } = new DevLog();

        /// <summary>
        /// Writes the file now if there are unsaved changes. Called before the application may be killed.
        /// </summary>
        public void Flush()
        {
            lock (syncRoot)
            {
                if (!dirty)
                {
                    return;
                }

                dirty = false;

                try
                {
                    File.WriteAllText(path, Log.Serialize());
                }
                catch (Exception exception)
                {
                    Android.Util.Log.Warn(LogTag, "Zapis Dev logu nie powiódł się: " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Removes every entry of the log and deletes the file.
        /// </summary>
        public void Clear()
        {
            Log.Clear();

            lock (syncRoot)
            {
                dirty = false;

                try
                {
                    File.Delete(path);
                }
                catch (Exception exception)
                {
                    Android.Util.Log.Warn(LogTag, "Usunięcie Dev logu nie powiodło się: " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Builds the report: the description of the application and the device followed by the entries.
        /// </summary>
        /// <param name="includeDetails">Whether the stack traces are included.</param>
        /// <returns>The report as text.</returns>
        public string BuildReport(bool includeDetails)
        {
            List<string> header = new List<string>
            {
                BuildInfo.AppName + " – Dev log",
                "Wersja: " + BuildInfo.Version + " (kompilacja " + BuildInfo.Build + ")",
                "Urządzenie: " + Build.Manufacturer + " " + Build.Model + ", Android " + Build.VERSION.Release + " (API " + (int)Build.VERSION.SdkInt + ")",
                "Źródła samolotów: " + string.Join(" > ", AppSettings.DataSourceOrder) + ", zapasowe źródło: " + (AppSettings.DataSourceFallback ? "tak" : "nie"),
                "Źródła tras: " + string.Join(" > ", AppSettings.RouteSourceOrder),
                "Poziom logowania: " + DevLog.GetLevelName(AppSettings.DevLogMinimumLevel) + ", limit: " + AppSettings.DevLogMaxEntries.ToString(CultureInfo.InvariantCulture) + ", łączenie: " + (AppSettings.DevLogDeduplicate ? "tak" : "nie"),
                "Wygenerowano: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            };

            return Log.FormatReport(header, includeDetails);
        }

        /// <summary>
        /// Writes the report to a file in the cache directory, from where it can be shared.
        /// </summary>
        /// <returns>The file.</returns>
        public Java.IO.File WriteReportFile()
        {
            Java.IO.File directory = new Java.IO.File(Android.App.Application.Context.CacheDir!, "shared");
            directory.Mkdirs();

            string name = "adsbcarmap-devlog-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt";
            Java.IO.File file = new Java.IO.File(directory, name);

            File.WriteAllText(file.AbsolutePath, BuildReport(true), new System.Text.UTF8Encoding(false));

            return file;
        }

        /// <summary>
        /// Passes the current settings of the developer log to the log.
        /// </summary>
        private void ApplySettings()
        {
            Log.Configure(AppSettings.DevLogMinimumLevel, AppSettings.DevLogMaxEntries, AppSettings.DevLogDeduplicate);
        }

        /// <summary>
        /// Plans writing the file after the log changed.
        /// </summary>
        private void OnLogChanged()
        {
            lock (syncRoot)
            {
                dirty = true;
                saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }
}

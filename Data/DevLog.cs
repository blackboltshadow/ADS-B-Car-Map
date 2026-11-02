using System.Globalization;
using System.Text;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Severity of an entry of the developer log.
    /// </summary>
    public enum DevLogLevel
    {
        /// <summary>
        /// Information about an unusual but handled situation, for example a switch to the fallback source.
        /// </summary>
        Info = 0,

        /// <summary>
        /// Something went wrong but the application continued, for example a failed download.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// An unexpected failure, for example an unhandled exception.
        /// </summary>
        Error = 2,
    }

    /// <summary>
    /// One entry of the developer log. Identical events are merged into one entry with a counter when the log is set to do so.
    /// </summary>
    public sealed class DevLogEntry
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="level">Severity.</param>
        /// <param name="tag">Part of the application that reported the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="details">Text of the exception with its stack trace, or an empty string.</param>
        /// <param name="count">Number of occurrences.</param>
        /// <param name="firstUtc">Time of the first occurrence, in UTC.</param>
        /// <param name="lastUtc">Time of the last occurrence, in UTC.</param>
        public DevLogEntry(DevLogLevel level, string tag, string message, string details, int count, DateTime firstUtc, DateTime lastUtc)
        {
            Level = level;
            Tag = tag;
            Message = message;
            Details = details;
            Count = count;
            FirstUtc = firstUtc;
            LastUtc = lastUtc;
        }

        /// <summary>
        /// Severity.
        /// </summary>
        public DevLogLevel Level { get; }

        /// <summary>
        /// Part of the application that reported the event.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Description of the event.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Text of the exception with its stack trace, or an empty string.
        /// </summary>
        public string Details { get; }

        /// <summary>
        /// Number of occurrences.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Time of the first occurrence, in UTC.
        /// </summary>
        public DateTime FirstUtc { get; }

        /// <summary>
        /// Time of the last occurrence, in UTC.
        /// </summary>
        public DateTime LastUtc { get; set; }
    }

    /// <summary>
    /// Developer log: a bounded list of unexpected events that the user can export and pass on. Pure logic, the Android side (file, screen, settings) is in the application.
    /// </summary>
    public sealed class DevLog
    {
        /// <summary>
        /// Allowed numbers of entries kept in the log; the setting stores the index in this list.
        /// </summary>
        public static readonly IReadOnlyList<int> MaxEntriesOptions = new List<int> { 100, 250, 500, 1000, 2000 };

        /// <summary>
        /// Default number of entries kept in the log.
        /// </summary>
        public const int DefaultMaxEntries = 500;

        /// <summary>
        /// Maximum length of the stored text of an exception.
        /// </summary>
        public const int MaxDetailsLength = 4000;

        /// <summary>
        /// Maximum length of the stored description of an event.
        /// </summary>
        public const int MaxMessageLength = 1000;

        /// <summary>
        /// Number of columns in a line of the saved text.
        /// </summary>
        private const int ColumnCount = 7;

        /// <summary>
        /// Format of the times in the report.
        /// </summary>
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss'Z'";

        /// <summary>
        /// Lock protecting the log.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Entries in the order of their last occurrence, the oldest first.
        /// </summary>
        private readonly List<DevLogEntry> entries = new List<DevLogEntry>();

        /// <summary>
        /// Least severe level that is recorded.
        /// </summary>
        private DevLogLevel minimumLevel = DevLogLevel.Warning;

        /// <summary>
        /// Maximum number of entries kept.
        /// </summary>
        private int maxEntries = DefaultMaxEntries;

        /// <summary>
        /// Whether identical events are merged into one entry.
        /// </summary>
        private bool deduplicate = true;

        /// <summary>
        /// Raised after the content of the log changed. May be raised on any thread.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Number of entries in the log.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncRoot)
                {
                    return entries.Count;
                }
            }
        }

        /// <summary>
        /// Converts the index of the level setting to the least severe level that is recorded: 0 errors only, 1 errors and warnings, 2 everything.
        /// </summary>
        /// <param name="choiceIndex">Index of the chosen option; out of range values are limited.</param>
        /// <returns>The level.</returns>
        public static DevLogLevel GetMinimumLevel(int choiceIndex)
        {
            return (DevLogLevel)(2 - Math.Clamp(choiceIndex, 0, 2));
        }

        /// <summary>
        /// Converts the index of the size setting to the number of entries kept.
        /// </summary>
        /// <param name="choiceIndex">Index in <see cref="MaxEntriesOptions"/>; out of range values are limited.</param>
        /// <returns>Number of entries.</returns>
        public static int GetMaxEntries(int choiceIndex)
        {
            return MaxEntriesOptions[Math.Clamp(choiceIndex, 0, MaxEntriesOptions.Count - 1)];
        }

        /// <summary>
        /// Sets what is recorded and how many entries are kept. Entries over the new limit are removed, the oldest first.
        /// </summary>
        /// <param name="level">Least severe level that is recorded.</param>
        /// <param name="maximumEntries">Maximum number of entries; at least 1.</param>
        /// <param name="mergeIdentical">Whether identical events are merged into one entry with a counter.</param>
        public void Configure(DevLogLevel level, int maximumEntries, bool mergeIdentical)
        {
            bool changed;

            lock (syncRoot)
            {
                minimumLevel = level;
                maxEntries = Math.Max(1, maximumEntries);
                deduplicate = mergeIdentical;
                changed = Trim();
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Tells whether an event of the given level would be recorded.
        /// </summary>
        /// <param name="level">Level of the event.</param>
        /// <returns>True when the level is not below the configured minimum.</returns>
        public bool IsEnabled(DevLogLevel level)
        {
            lock (syncRoot)
            {
                return level >= minimumLevel;
            }
        }

        /// <summary>
        /// Records an event when its level is enabled.
        /// </summary>
        /// <param name="level">Severity.</param>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        /// <param name="nowUtc">Current time, in UTC.</param>
        /// <returns>True when the log changed.</returns>
        public bool Add(DevLogLevel level, string tag, string message, Exception? exception, DateTime nowUtc)
        {
            string details = exception == null ? string.Empty : Limit(exception.ToString(), MaxDetailsLength);
            string cleanTag = Limit(tag, 100);
            string cleanMessage = Limit(message, MaxMessageLength);

            lock (syncRoot)
            {
                if (level < minimumLevel)
                {
                    return false;
                }

                DevLogEntry? existing = deduplicate ? Find(level, cleanTag, cleanMessage, details) : null;

                if (existing != null)
                {
                    existing.Count++;
                    existing.LastUtc = nowUtc;
                    entries.Remove(existing);
                    entries.Add(existing);
                }
                else
                {
                    entries.Add(new DevLogEntry(level, cleanTag, cleanMessage, details, 1, nowUtc, nowUtc));
                    Trim();
                }
            }

            Changed?.Invoke();

            return true;
        }

        /// <summary>
        /// Returns a copy of the entries, the most recent first.
        /// </summary>
        /// <returns>Entries sorted by the time of their last occurrence, newest first.</returns>
        public List<DevLogEntry> GetEntries()
        {
            lock (syncRoot)
            {
                List<DevLogEntry> copy = new List<DevLogEntry>(entries.Count);

                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    copy.Add(Copy(entries[index]));
                }

                return copy;
            }
        }

        /// <summary>
        /// Removes every entry.
        /// </summary>
        public void Clear()
        {
            bool changed;

            lock (syncRoot)
            {
                changed = entries.Count > 0;
                entries.Clear();
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Writes the log as text for saving: a header line and one tab separated line per entry. Line breaks, tabs and backslashes in the texts are escaped.
        /// </summary>
        /// <returns>The text.</returns>
        public string Serialize()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("level\ttag\tcount\tfirst_utc\tlast_utc\tmessage\tdetails\n");

            lock (syncRoot)
            {
                foreach (DevLogEntry entry in entries)
                {
                    builder.Append(((int)entry.Level).ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(Escape(entry.Tag)).Append('\t')
                        .Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(entry.FirstUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(entry.LastUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(Escape(entry.Message)).Append('\t')
                        .Append(Escape(entry.Details)).Append('\n');
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Replaces the content of the log with text written by <see cref="Serialize"/>. Lines that cannot be read are skipped, and the newest entries beyond the limit are kept.
        /// </summary>
        /// <param name="text">Saved text, or null.</param>
        public void Load(string? text)
        {
            lock (syncRoot)
            {
                entries.Clear();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    bool header = true;

                    foreach (string line in text.Split('\n'))
                    {
                        if (header)
                        {
                            header = false;

                            continue;
                        }

                        DevLogEntry? entry = ParseLine(line.TrimEnd('\r'));

                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                    }

                    Trim();
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Writes the log as a report that the user can send: the given header lines followed by the entries from the oldest to the newest.
        /// </summary>
        /// <param name="headerLines">Lines that describe the application and the device.</param>
        /// <param name="includeDetails">Whether the stack traces are included.</param>
        /// <returns>The report.</returns>
        public string FormatReport(IReadOnlyList<string> headerLines, bool includeDetails)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string line in headerLines)
            {
                builder.Append(line).Append('\n');
            }

            lock (syncRoot)
            {
                builder.Append("Wpisów: ").Append(entries.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("----------------------------------------\n");

                foreach (DevLogEntry entry in entries)
                {
                    builder.Append('[').Append(entry.LastUtc.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append("] ")
                        .Append(GetLevelName(entry.Level)).Append(' ')
                        .Append(entry.Tag);

                    if (entry.Count > 1)
                    {
                        builder.Append(" ×").Append(entry.Count.ToString(CultureInfo.InvariantCulture))
                            .Append(" (od ").Append(entry.FirstUtc.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append(')');
                    }

                    builder.Append('\n').Append("  ").Append(entry.Message.Replace("\n", "\n  ")).Append('\n');

                    if (includeDetails && entry.Details.Length > 0)
                    {
                        builder.Append("  ").Append(entry.Details.Replace("\r", string.Empty).Replace("\n", "\n  ")).Append('\n');
                    }
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the short name of a level used in the report.
        /// </summary>
        /// <param name="level">Level.</param>
        /// <returns>INFO, WARN or ERROR.</returns>
        public static string GetLevelName(DevLogLevel level)
        {
            switch (level)
            {
                case DevLogLevel.Error:
                    return "ERROR";
                case DevLogLevel.Warning:
                    return "WARN";
                default:
                    return "INFO";
            }
        }

        /// <summary>
        /// Finds the entry that describes the same event. Must be called inside the lock.
        /// </summary>
        /// <param name="level">Severity.</param>
        /// <param name="tag">Tag.</param>
        /// <param name="message">Description.</param>
        /// <param name="details">Text of the exception.</param>
        /// <returns>The entry, or null when the event has not been recorded yet.</returns>
        private DevLogEntry? Find(DevLogLevel level, string tag, string message, string details)
        {
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                DevLogEntry entry = entries[index];

                if (entry.Level == level
                    && string.Equals(entry.Tag, tag, StringComparison.Ordinal)
                    && string.Equals(entry.Message, message, StringComparison.Ordinal)
                    && string.Equals(entry.Details, details, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Removes the oldest entries over the limit. Must be called inside the lock.
        /// </summary>
        /// <returns>True when something was removed.</returns>
        private bool Trim()
        {
            int excess = entries.Count - maxEntries;

            if (excess <= 0)
            {
                return false;
            }

            entries.RemoveRange(0, excess);

            return true;
        }

        /// <summary>
        /// Reads one line written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="line">Line without the line break.</param>
        /// <returns>The entry, or null when the line cannot be read.</returns>
        private static DevLogEntry? ParseLine(string line)
        {
            string[] columns = line.Split('\t');

            if (columns.Length < ColumnCount
                || !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                || level < (int)DevLogLevel.Info || level > (int)DevLogLevel.Error
                || !int.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                || !DateTime.TryParse(columns[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime first)
                || !DateTime.TryParse(columns[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime last))
            {
                return null;
            }

            return new DevLogEntry((DevLogLevel)level, Unescape(columns[1]), Unescape(columns[5]), Unescape(columns[6]), Math.Max(1, count), first, last);
        }

        /// <summary>
        /// Makes a copy of an entry, so that the caller cannot change the log.
        /// </summary>
        /// <param name="entry">Entry to copy.</param>
        /// <returns>The copy.</returns>
        private static DevLogEntry Copy(DevLogEntry entry)
        {
            return new DevLogEntry(entry.Level, entry.Tag, entry.Message, entry.Details, entry.Count, entry.FirstUtc, entry.LastUtc);
        }

        /// <summary>
        /// Shortens a text to the given length.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>The text, cut when it was longer.</returns>
        private static string Limit(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        /// <summary>
        /// Replaces the characters that would break the tab separated text.
        /// </summary>
        /// <param name="value">Text to escape.</param>
        /// <returns>Text without tabs, line breaks and single backslashes.</returns>
        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", string.Empty).Replace("\n", "\\n");
        }

        /// <summary>
        /// Reverses <see cref="Escape"/>.
        /// </summary>
        /// <param name="value">Escaped text.</param>
        /// <returns>The original text without carriage returns.</returns>
        private static string Unescape(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];

                if (character != '\\' || index + 1 >= value.Length)
                {
                    builder.Append(character);

                    continue;
                }

                index++;

                switch (value[index])
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        builder.Append(value[index]);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}

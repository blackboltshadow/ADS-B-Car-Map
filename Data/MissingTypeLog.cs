using AdsbCarMap.DataSources.Models;
using System.Globalization;
using System.Text;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Collects the aircraft types seen in the traffic that the type table does not describe, so that the table can be completed later.
    /// </summary>
    public sealed class MissingTypeLog
    {
        /// <summary>
        /// Maximum number of distinct types kept in the log.
        /// </summary>
        public const int MaxEntries = 2000;

        /// <summary>
        /// Maximum number of aircraft addresses remembered per type to count each aircraft once.
        /// </summary>
        private const int MaxRememberedAircraft = 5000;

        /// <summary>
        /// Number of columns in a line of the saved text.
        /// </summary>
        private const int ColumnCount = 8;

        /// <summary>
        /// Lock protecting the log.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Entries by ICAO type code.
        /// </summary>
        private readonly Dictionary<string, MissingTypeEntry> entries = new Dictionary<string, MissingTypeEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Addresses of the aircraft already counted since the application started, by type. They are not saved.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> countedAircraft = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Number of distinct types in the log.
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
        /// Adds the aircraft whose type is not in the table to the log. Aircraft without a type are ignored.
        /// </summary>
        /// <param name="aircraft">Aircraft from the last download.</param>
        /// <param name="table">Table of the known types.</param>
        /// <param name="nowUtc">Current time, in UTC.</param>
        /// <returns>True when the log changed.</returns>
        public bool Record(IReadOnlyList<Aircraft> aircraft, AircraftTypeTable table, DateTime nowUtc)
        {
            bool changed = false;

            lock (syncRoot)
            {
                foreach (Aircraft item in aircraft)
                {
                    string type = (item.TypeCode ?? string.Empty).Trim().ToUpperInvariant();

                    if (type.Length == 0 || table.IsKnown(type))
                    {
                        continue;
                    }

                    if (!entries.TryGetValue(type, out MissingTypeEntry? entry))
                    {
                        if (entries.Count >= MaxEntries)
                        {
                            continue;
                        }

                        entry = new MissingTypeEntry(type, 0, nowUtc, nowUtc, string.Empty, string.Empty, string.Empty, false);
                        entries[type] = entry;
                        changed = true;
                    }

                    if (!countedAircraft.TryGetValue(type, out HashSet<string>? counted))
                    {
                        counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        countedAircraft[type] = counted;
                    }

                    if (counted.Count < MaxRememberedAircraft && counted.Add(item.Hex))
                    {
                        entry.Count++;
                        entry.LastSeenUtc = nowUtc;
                        changed = true;
                    }

                    if (entry.Callsign.Length == 0 && !string.IsNullOrWhiteSpace(item.Callsign))
                    {
                        entry.Callsign = Clean(item.Callsign);
                        changed = true;
                    }

                    if (entry.Registration.Length == 0 && !string.IsNullOrWhiteSpace(item.Registration))
                    {
                        entry.Registration = Clean(item.Registration);
                        changed = true;
                    }

                    if (entry.Category.Length == 0 && !string.IsNullOrWhiteSpace(item.Category))
                    {
                        entry.Category = Clean(item.Category);
                        changed = true;
                    }

                    if (item.IsMilitary && !entry.IsMilitary)
                    {
                        entry.IsMilitary = true;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Returns a copy of the entries, the most frequent type first.
        /// </summary>
        /// <returns>Entries sorted by the number of aircraft, then by the type code.</returns>
        public List<MissingTypeEntry> GetEntries()
        {
            lock (syncRoot)
            {
                return entries.Values
                    .Select((MissingTypeEntry entry) => new MissingTypeEntry(entry.Icao, entry.Count, entry.FirstSeenUtc, entry.LastSeenUtc, entry.Callsign, entry.Registration, entry.Category, entry.IsMilitary))
                    .OrderByDescending((MissingTypeEntry entry) => entry.Count)
                    .ThenBy((MissingTypeEntry entry) => entry.Icao, StringComparer.Ordinal)
                    .ToList();
            }
        }

        /// <summary>
        /// Removes the entries of the types that the table describes by now, for example after an update added them to the table.
        /// </summary>
        /// <param name="table">Table of the known types.</param>
        /// <returns>Number of the removed entries.</returns>
        public int RemoveKnown(AircraftTypeTable table)
        {
            lock (syncRoot)
            {
                List<string> known = entries.Keys.Where((string icao) => table.IsKnown(icao)).ToList();

                foreach (string icao in known)
                {
                    entries.Remove(icao);
                    countedAircraft.Remove(icao);
                }

                return known.Count;
            }
        }

        /// <summary>
        /// Removes every entry.
        /// </summary>
        public void Clear()
        {
            lock (syncRoot)
            {
                entries.Clear();
                countedAircraft.Clear();
            }
        }

        /// <summary>
        /// Writes the log as text: a header line and one tab separated line per type with the code, the count, the first and the last time seen (round-trip UTC), the example callsign, registration and category, and 1 for military.
        /// </summary>
        /// <returns>The text.</returns>
        public string Serialize()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("icao\tcount\tfirst_seen_utc\tlast_seen_utc\tcallsign\tregistration\tcategory\tmilitary\n");

            foreach (MissingTypeEntry entry in GetEntries())
            {
                builder.Append(entry.Icao).Append('\t')
                    .Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(entry.FirstSeenUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(entry.LastSeenUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(entry.Callsign).Append('\t')
                    .Append(entry.Registration).Append('\t')
                    .Append(entry.Category).Append('\t')
                    .Append(entry.IsMilitary ? '1' : '0').Append('\n');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Replaces the content of the log with text written by <see cref="Serialize"/>. Lines that cannot be read are skipped.
        /// </summary>
        /// <param name="text">Saved text, or null.</param>
        public void Load(string? text)
        {
            lock (syncRoot)
            {
                entries.Clear();
                countedAircraft.Clear();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                bool header = true;

                foreach (string line in text.Split('\n'))
                {
                    if (header)
                    {
                        header = false;

                        continue;
                    }

                    string[] columns = line.TrimEnd('\r').Split('\t');

                    if (columns.Length < ColumnCount || columns[0].Length == 0 || entries.Count >= MaxEntries)
                    {
                        continue;
                    }

                    if (!int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                        || !DateTime.TryParse(columns[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime first)
                        || !DateTime.TryParse(columns[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime last))
                    {
                        continue;
                    }

                    entries[columns[0]] = new MissingTypeEntry(columns[0].ToUpperInvariant(), Math.Max(0, count), first, last, columns[4], columns[5], columns[6], columns[7] == "1");
                }
            }
        }

        /// <summary>
        /// Removes characters that would break the tab separated text.
        /// </summary>
        /// <param name="value">Text to clean.</param>
        /// <returns>The trimmed text without tabs and line breaks.</returns>
        private static string Clean(string value)
        {
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}

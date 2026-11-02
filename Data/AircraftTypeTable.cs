namespace AdsbCarMap.Data
{
    /// <summary>
    /// Table of aircraft types: ICAO and IATA codes, families and full names. It lets a filter accept an IATA code such as "738" or a whole family, and tells which types seen in the traffic are not described yet.
    /// </summary>
    public sealed partial class AircraftTypeTable
    {
        /// <summary>
        /// Number of columns in a row of the table file.
        /// </summary>
        private const int ColumnCount = 4;

        /// <summary>
        /// Table that is used until the bundled one is loaded; it knows no types.
        /// </summary>
        public static readonly AircraftTypeTable Empty = new AircraftTypeTable(new List<AircraftTypeEntry>());

        /// <summary>
        /// Table used by the filters. Replaced as a whole when the bundled table is loaded.
        /// </summary>
        private static volatile AircraftTypeTable current = Empty;

        /// <summary>
        /// Rows by ICAO code.
        /// </summary>
        private readonly Dictionary<string, List<AircraftTypeEntry>> byIcao = new Dictionary<string, List<AircraftTypeEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Rows by IATA code.
        /// </summary>
        private readonly Dictionary<string, List<AircraftTypeEntry>> byIata = new Dictionary<string, List<AircraftTypeEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Rows by family name.
        /// </summary>
        private readonly Dictionary<string, List<AircraftTypeEntry>> byFamily = new Dictionary<string, List<AircraftTypeEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a table from rows.
        /// </summary>
        /// <param name="entries">Rows of the table.</param>
        public AircraftTypeTable(IReadOnlyList<AircraftTypeEntry> entries)
        {
            Entries = entries;

            foreach (AircraftTypeEntry entry in entries)
            {
                AddTo(byIcao, entry.Icao, entry);
                AddTo(byFamily, entry.Family, entry);

                if (entry.Iata.Length > 0)
                {
                    AddTo(byIata, entry.Iata, entry);
                }
            }
        }

        /// <summary>
        /// Table used by the filters.
        /// </summary>
        public static AircraftTypeTable Current
        {
            get => current;
            set => current = value;
        }

        /// <summary>
        /// All rows of the table.
        /// </summary>
        public IReadOnlyList<AircraftTypeEntry> Entries { get; }

        /// <summary>
        /// Number of distinct ICAO type codes in the table.
        /// </summary>
        public int TypeCount => byIcao.Count;

        /// <summary>
        /// Reads a table from the lines of its file: tab separated columns IATA, ICAO, family and name, with a header line first. Lines that are empty, start with # or have no ICAO code are skipped.
        /// </summary>
        /// <param name="lines">Lines of the file.</param>
        /// <returns>The table.</returns>
        public static AircraftTypeTable Parse(IEnumerable<string> lines)
        {
            List<AircraftTypeEntry> entries = new List<AircraftTypeEntry>();
            bool header = true;

            foreach (string line in lines)
            {
                if (header)
                {
                    header = false;

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] columns = line.Split('\t');

                if (columns.Length < ColumnCount)
                {
                    continue;
                }

                string icao = columns[1].Trim().ToUpperInvariant();

                if (icao.Length == 0)
                {
                    continue;
                }

                string family = columns[2].Trim();

                entries.Add(new AircraftTypeEntry(columns[0].Trim().ToUpperInvariant(), icao, family.Length > 0 ? family : icao, columns[3].Trim()));
            }

            return new AircraftTypeTable(entries);
        }

        /// <summary>
        /// Tells whether a type code is described in the table.
        /// </summary>
        /// <param name="icao">ICAO type code, in any letter case.</param>
        /// <returns>True when the table has a row for the code.</returns>
        public bool IsKnown(string? icao)
        {
            return !string.IsNullOrWhiteSpace(icao) && byIcao.ContainsKey(icao.Trim());
        }

        /// <summary>
        /// Finds the full name of a type.
        /// </summary>
        /// <param name="icao">ICAO type code, in any letter case.</param>
        /// <returns>The name, or null when the type is not in the table.</returns>
        public string? FindName(string? icao)
        {
            if (string.IsNullOrWhiteSpace(icao) || !byIcao.TryGetValue(icao.Trim(), out List<AircraftTypeEntry>? rows))
            {
                return null;
            }

            return rows[0].Name;
        }

        /// <summary>
        /// Turns the text of a filter into the set of ICAO type codes it stands for. The text is a code in the ICAO or IATA form. With <paramref name="family"/> the whole family of every matching type is included, and the name of a family is accepted too. The typed text itself is always in the set, so an ICAO code missing from the table still works.
        /// </summary>
        /// <param name="text">Text typed in the filter.</param>
        /// <param name="family">True to include the whole family.</param>
        /// <returns>Upper case ICAO codes compared without regard to letter case; empty for an empty text.</returns>
        public HashSet<string> Resolve(string? text, bool family)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            string code = text.Trim().ToUpperInvariant();
            List<AircraftTypeEntry> matched = new List<AircraftTypeEntry>();

            result.Add(code);

            if (byIcao.TryGetValue(code, out List<AircraftTypeEntry>? icaoRows))
            {
                matched.AddRange(icaoRows);
            }

            if (byIata.TryGetValue(code, out List<AircraftTypeEntry>? iataRows))
            {
                matched.AddRange(iataRows);
            }

            if (family && byFamily.TryGetValue(code, out List<AircraftTypeEntry>? familyRows))
            {
                matched.AddRange(familyRows);
            }

            foreach (AircraftTypeEntry entry in matched)
            {
                result.Add(entry.Icao);

                if (family)
                {
                    foreach (AircraftTypeEntry sibling in byFamily[entry.Family])
                    {
                        result.Add(sibling.Icao);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Adds a row to a dictionary of lists.
        /// </summary>
        /// <param name="index">Dictionary.</param>
        /// <param name="key">Key.</param>
        /// <param name="entry">Row to add.</param>
        private static void AddTo(Dictionary<string, List<AircraftTypeEntry>> index, string key, AircraftTypeEntry entry)
        {
            if (!index.TryGetValue(key, out List<AircraftTypeEntry>? list))
            {
                list = new List<AircraftTypeEntry>();
                index[key] = list;
            }

            list.Add(entry);
        }
    }
}

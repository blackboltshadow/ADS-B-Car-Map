namespace AdsbCarMap.Data
{
    /// <summary>
    /// Maps ICAO aircraft type codes to the manufacturer names. Exact codes take precedence over prefix rules, and among prefix rules the longest prefix wins.
    /// </summary>
    public static class AircraftManufacturers
    {
        /// <summary>
        /// Manufacturers of the types listed explicitly, keyed by the ICAO type code.
        /// </summary>
        private static readonly Dictionary<string, string> ExactTypes = CreateExactTypes();

        /// <summary>
        /// Prefix rules ordered from the longest prefix to the shortest.
        /// </summary>
        private static readonly List<PrefixRule> PrefixRules = CreatePrefixRules();

        /// <summary>
        /// Sorted names of all manufacturers known to the table.
        /// </summary>
        public static IReadOnlyList<string> Names { get; } = CreateNames();

        /// <summary>
        /// Finds the manufacturer of an aircraft type.
        /// </summary>
        /// <param name="typeCode">ICAO type code, for example "B738", in any letter case, or null when unknown.</param>
        /// <returns>Manufacturer name or null when the type is unknown.</returns>
        public static string? Find(string? typeCode)
        {
            if (string.IsNullOrWhiteSpace(typeCode))
            {
                return null;
            }

            string code = typeCode.Trim().ToUpperInvariant();
            if (ExactTypes.TryGetValue(code, out string? exact))
            {
                return exact;
            }

            foreach (PrefixRule rule in PrefixRules)
            {
                if (code.StartsWith(rule.Prefix, StringComparison.Ordinal) && (rule.Length == 0 || code.Length == rule.Length))
                {
                    return rule.Manufacturer;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether an aircraft type was made by the given manufacturer.
        /// </summary>
        /// <param name="typeCode">ICAO type code or null when unknown.</param>
        /// <param name="manufacturer">Manufacturer name, compared without regard to letter case.</param>
        /// <returns>True when the manufacturer of the type is known and equals the given name.</returns>
        public static bool IsMadeBy(string? typeCode, string manufacturer)
        {
            return string.Equals(Find(typeCode), manufacturer, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the table of explicitly listed type codes.
        /// </summary>
        /// <returns>Dictionary from the type code to the manufacturer name.</returns>
        private static Dictionary<string, string> CreateExactTypes()
        {
            Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.Ordinal);

            AddExact(table, "Boeing", "B37M", "B38M", "B39M", "B3XM", "C17", "K35R", "KC46", "F15", "F18H", "F18S", "FA18", "H47", "H64", "E3TF");
            AddExact(table, "Airbus", "A20N", "A21N", "A400", "A3ST", "BCS1", "BCS3", "C295", "CN35");
            AddExact(table, "Airbus Helicopters", "B105", "PUMA", "AS32", "AS35", "AS50", "AS55", "AS65", "EC20", "EC25", "EC30", "EC35", "EC45", "EC55", "EC75");
            AddExact(table, "Antonov", "A124", "A225", "A140", "A148");
            AddExact(table, "Embraer", "E50P", "E55P", "E35L", "E550", "E390");
            AddExact(table, "Beechcraft", "B190", "B350", "PRM1", "T6", "TEX2");
            AddExact(table, "Bell", "B505", "B06", "B407", "B412", "B429", "B430", "B47G", "UH1");
            AddExact(table, "Sikorsky", "S61", "S64", "S70", "S76", "S92", "H60");
            AddExact(table, "Robinson", "R22", "R44", "R66");
            AddExact(table, "Leonardo", "A109", "A119", "A139", "A189", "C27J", "M346", "M339", "LYNX");
            AddExact(table, "NHIndustries", "NH90");
            AddExact(table, "MD Helicopters", "H500", "EXPL");
            AddExact(table, "Enstrom", "EN28", "EN48");
            AddExact(table, "Lockheed Martin", "C130", "C30J", "C5", "C5M", "F16", "F22", "F35");
            AddExact(table, "Lockheed", "L101");
            AddExact(table, "McDonnell Douglas", "F4", "KC10");
            AddExact(table, "Northrop Grumman", "F14", "F5", "T38");
            AddExact(table, "Fairchild Republic", "A10");
            AddExact(table, "Eurofighter", "EUFI", "EF2K");
            AddExact(table, "Dassault", "RFAL", "AJET", "F900", "F2TH");
            AddExact(table, "Panavia", "TORN", "TOR");
            AddExact(table, "Saab", "GRIF", "JAS39", "SF34", "SB20");
            AddExact(table, "BAE Systems", "HAWK", "B461", "B462", "B463");
            AddExact(table, "Aero Vodochody", "L39", "L159");
            AddExact(table, "Transall", "C160");
            AddExact(table, "Chengdu", "J10", "J20");
            AddExact(table, "Gulfstream", "G150", "G280", "GALX", "GA6C", "GA7C");
            AddExact(table, "Bombardier", "GLEX");
            AddExact(table, "Hawker", "H25B", "H25C");
            AddExact(table, "Honda", "HDJT");
            AddExact(table, "Cirrus", "S22T", "SF50");
            AddExact(table, "Pilatus", "PC12");
            AddExact(table, "Daher", "TBM7", "TBM8", "TBM9", "TB10", "TB20", "TB21", "TOBA");
            AddExact(table, "Piaggio", "P180");
            AddExact(table, "Tecnam", "P208", "IMPU");
            AddExact(table, "Fokker", "F27", "F28", "F50", "F70", "F100");
            AddExact(table, "Dornier", "D228", "D328");
            AddExact(table, "Let", "L410");
            AddExact(table, "Fairchild", "SW3", "SW4");
            AddExact(table, "BAE Systems", "JS31", "JS32", "JS41");
            AddExact(table, "Air Tractor", "AT3T", "AT5T", "AT6T", "AT8T");
            AddExact(table, "Comac", "C919", "AJ27");
            AddExact(table, "IAI", "ASTR");
            AddExact(table, "Grob", "G115");
            AddExact(table, "Grumman American", "AA5");
            AddExact(table, "Bellanca", "CH7A", "CH7B");
            AddExact(table, "Jabiru", "JAB4");
            AddExact(table, "Yakovlev", "YK18", "YK52");
            AddExact(table, "Piper", "J3");
            AddExact(table, "Tupolev", "T134", "T154", "T204");
            AddExact(table, "Kamov", "KA27", "KA32");
            AddExact(table, "Van's", "YK12");
            AddExact(table, "Aeroprakt", "AP22", "AP32");
            AddExact(table, "Tomark", "SD4");
            AddExact(table, "BRM Aero", "BR23");
            AddExact(table, "Pipistrel", "PIVI");
            AddExact(table, "Direct Fly", "ALTO");
            AddExact(table, "Blackshape", "PRIM");
            AddExact(table, "PZL-Swidnik", "W3");
            AddExact(table, "Aerospool", "WT9");
            AddExact(table, "AutoGyro", "CLON");
            AddExact(table, "Aquila", "A210");
            AddExact(table, "MBB/Kawasaki", "BK17");
            AddExact(table, "Aerostyle", "BREZ");
            AddExact(table, "Ikarus", "C42");
            AddExact(table, "Robin", "DR40");
            AddExact(table, "Evektor", "EVSS");
            AddExact(table, "FK Lightplanes", "FK9");
            AddExact(table, "Aviat", "HUSK");
            AddExact(table, "Partenavia", "P68");

            return table;
        }

        /// <summary>
        /// Builds the prefix rules, longest prefix first.
        /// </summary>
        /// <returns>List of prefix rules.</returns>
        private static List<PrefixRule> CreatePrefixRules()
        {
            List<PrefixRule> rules = new List<PrefixRule>();

            AddPrefixes(rules, "Boeing", 0, "B7");
            AddPrefixes(rules, "Airbus", 4, "A3");
            AddPrefixes(rules, "Airbus", 0, "A2");
            AddPrefixes(rules, "Embraer", 0, "E1", "E29", "E75");
            AddPrefixes(rules, "ATR", 0, "AT4", "AT7");
            AddPrefixes(rules, "Bombardier", 0, "CRJ", "CL", "GL");
            AddPrefixes(rules, "De Havilland Canada", 0, "DH8", "DHC");
            AddPrefixes(rules, "Cessna", 4, "C1", "C2", "C3", "C4", "C5", "C6", "C7");
            AddPrefixes(rules, "Piper", 0, "PA", "P28", "P32", "P46");
            AddPrefixes(rules, "Pilatus", 0, "PC");
            AddPrefixes(rules, "Beechcraft", 0, "BE");
            AddPrefixes(rules, "Cirrus", 0, "SR2");
            AddPrefixes(rules, "Diamond", 0, "DA");
            AddPrefixes(rules, "Mooney", 0, "M20");
            AddPrefixes(rules, "Gulfstream", 0, "GLF");
            AddPrefixes(rules, "Learjet", 0, "LJ");
            AddPrefixes(rules, "Dassault", 0, "FA");
            AddPrefixes(rules, "Airbus Helicopters", 0, "EC", "H1", "H2");
            AddPrefixes(rules, "Leonardo", 0, "AW");
            AddPrefixes(rules, "McDonnell Douglas", 0, "MD", "DC");
            AddPrefixes(rules, "BAE Systems", 0, "RJ");
            AddPrefixes(rules, "Antonov", 0, "AN");
            AddPrefixes(rules, "Ilyushin", 0, "IL");
            AddPrefixes(rules, "Sukhoi", 0, "SU");
            AddPrefixes(rules, "Mikoyan", 0, "MIG", "MG");
            AddPrefixes(rules, "Mil", 0, "MI");
            AddPrefixes(rules, "Van's", 0, "RV");

            rules.Sort((PrefixRule left, PrefixRule right) => right.Prefix.Length.CompareTo(left.Prefix.Length));
            return rules;
        }

        /// <summary>
        /// Collects the sorted manufacturer names from both tables.
        /// </summary>
        /// <returns>Sorted list of distinct names.</returns>
        private static IReadOnlyList<string> CreateNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in ExactTypes.Values)
            {
                names.Add(name);
            }

            foreach (PrefixRule rule in PrefixRules)
            {
                names.Add(rule.Manufacturer);
            }

            List<string> sorted = new List<string>(names);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        /// <summary>
        /// Adds type codes of one manufacturer to the exact table.
        /// </summary>
        /// <param name="table">Target table.</param>
        /// <param name="manufacturer">Manufacturer name.</param>
        /// <param name="codes">ICAO type codes.</param>
        private static void AddExact(Dictionary<string, string> table, string manufacturer, params string[] codes)
        {
            foreach (string code in codes)
            {
                table[code] = manufacturer;
            }
        }

        /// <summary>
        /// Adds prefix rules of one manufacturer.
        /// </summary>
        /// <param name="rules">Target list.</param>
        /// <param name="manufacturer">Manufacturer name.</param>
        /// <param name="length">Required length of the whole type code or 0 when any length matches.</param>
        /// <param name="prefixes">Type code prefixes.</param>
        private static void AddPrefixes(List<PrefixRule> rules, string manufacturer, int length, params string[] prefixes)
        {
            foreach (string prefix in prefixes)
            {
                rules.Add(new PrefixRule(prefix, manufacturer, length));
            }
        }

        /// <summary>
        /// Rule assigning a manufacturer to every type code that starts with a given prefix.
        /// </summary>
        private sealed class PrefixRule
        {
            /// <summary>
            /// Creates a prefix rule.
            /// </summary>
            /// <param name="prefix">Type code prefix.</param>
            /// <param name="manufacturer">Manufacturer name.</param>
            /// <param name="length">Required length of the whole type code or 0 when any length matches.</param>
            public PrefixRule(string prefix, string manufacturer, int length)
            {
                Prefix = prefix;
                Manufacturer = manufacturer;
                Length = length;
            }

            /// <summary>
            /// Type code prefix.
            /// </summary>
            public string Prefix { get; }

            /// <summary>
            /// Manufacturer name.
            /// </summary>
            public string Manufacturer { get; }

            /// <summary>
            /// Required length of the whole type code or 0 when any length matches.
            /// </summary>
            public int Length { get; }
        }
    }
}

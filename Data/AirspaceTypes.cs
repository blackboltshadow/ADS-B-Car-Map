using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// How many areas of one kind the source published, for the list of the kinds in the settings.
    /// </summary>
    /// <param name="Type">Kind of the areas as the source names it.</param>
    /// <param name="Total">Number of the areas of this kind.</param>
    /// <param name="Active">Number of them that are active at the moment of counting.</param>
    public sealed record AirspaceTypeCount(string Type, int Total, int Active);

    /// <summary>
    /// Knowledge about the kinds of airspace areas: names and the order. The source may publish a kind that is not listed here; such a kind is shown under its own code, after the known ones.
    /// </summary>
    public static class AirspaceTypes
    {
        /// <summary>
        /// Known kinds in the order they are listed. The first ones are those that restrict flying most.
        /// </summary>
        private static readonly IReadOnlyList<string> KnownOrder = new List<string> { "P", "R", "D", "NPZ", "TSA", "TRA", "ADHOC", "MRT", "ATZ" };

        /// <summary>
        /// Kinds shown by default: the ones that restrict flying. The others, e.g. the numerous temporary reserved areas, are for the user to switch on.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultShown = new List<string> { "P", "R", "D", "NPZ", "TSA", "ADHOC", "MRT" };

        /// <summary>
        /// Gives the name of a kind to show to the user.
        /// </summary>
        /// <param name="type">Kind of the area as the source names it.</param>
        /// <returns>Translated name with the code, or only the code for a kind whose meaning is not known here.</returns>
        public static string GetName(string type)
        {
            string code = string.IsNullOrWhiteSpace(type) ? "?" : type;

            switch (code.ToUpperInvariant())
            {
                case "P":
                    return Loc.T("Strefa zakazana") + " (P)";
                case "R":
                    return Loc.T("Strefa ograniczona") + " (R)";
                case "D":
                    return Loc.T("Strefa niebezpieczna") + " (D)";
                case "TSA":
                    return Loc.T("Strefa czasowo wydzielona") + " (TSA)";
                case "TRA":
                    return Loc.T("Strefa czasowo zarezerwowana") + " (TRA)";
                case "ADHOC":
                    return Loc.T("Rezerwacja doraźna") + " (ADHOC)";
                case "ATZ":
                    return Loc.T("Strefa ruchu lotniskowego") + " (ATZ)";
                default:
                    return code;
            }
        }

        /// <summary>
        /// Gives the position of a kind in the list: known kinds first, in their order, then the others.
        /// </summary>
        /// <param name="type">Kind of the area.</param>
        /// <returns>Position for sorting.</returns>
        public static int GetOrder(string type)
        {
            int index = -1;

            for (int i = 0; i < KnownOrder.Count; i++)
            {
                if (string.Equals(KnownOrder[i], type, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            return index >= 0 ? index : KnownOrder.Count;
        }

        /// <summary>
        /// Counts the areas by kind, so that the settings can list the kinds that really are in the data.
        /// </summary>
        /// <param name="zones">The areas.</param>
        /// <param name="nowUtc">The moment at which areas are counted as active, in UTC.</param>
        /// <returns>One entry per kind: known kinds in their order, then the others by code.</returns>
        public static IReadOnlyList<AirspaceTypeCount> Count(IEnumerable<AirspaceZone> zones, DateTime nowUtc)
        {
            Dictionary<string, int[]> counts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

            foreach (AirspaceZone zone in zones)
            {
                if (!counts.TryGetValue(zone.Type, out int[]? pair))
                {
                    pair = new int[2];
                    counts.Add(zone.Type, pair);
                }

                pair[0]++;

                if (AirspaceClassifier.GetState(zone, nowUtc) == AirspaceZoneState.Active)
                {
                    pair[1]++;
                }
            }

            List<AirspaceTypeCount> result = new List<AirspaceTypeCount>();

            foreach (KeyValuePair<string, int[]> entry in counts)
            {
                result.Add(new AirspaceTypeCount(entry.Key, entry.Value[0], entry.Value[1]));
            }

            result.Sort((AirspaceTypeCount a, AirspaceTypeCount b) =>
            {
                int byOrder = GetOrder(a.Type).CompareTo(GetOrder(b.Type));

                return byOrder != 0 ? byOrder : string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }
    }
}

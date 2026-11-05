using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using System.Globalization;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Texts about the runways that the traffic near an airport uses.
    /// </summary>
    public static class RunwayTrafficText
    {
        /// <summary>
        /// Returns the line about the runway ends in use.
        /// </summary>
        /// <param name="ends">The ends found by <see cref="RunwayInUseDetector"/>.</param>
        /// <returns>Text such as "Pas wg ruchu: 27 (przyloty 2, odloty 1)"; the ends are separated by commas and the counts that are zero are left out. Empty when there are no ends.</returns>
        public static string BuildLine(IReadOnlyList<RunwayTrafficEnd> ends)
        {
            if (ends.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();

            foreach (RunwayTrafficEnd end in ends)
            {
                List<string> counts = new List<string>();

                if (end.Approaching > 0)
                {
                    counts.Add(Loc.F("przyloty {0}", end.Approaching.ToString(CultureInfo.InvariantCulture)));
                }

                if (end.Departing > 0)
                {
                    counts.Add(Loc.F("odloty {0}", end.Departing.ToString(CultureInfo.InvariantCulture)));
                }

                if (end.OnRunway > 0)
                {
                    counts.Add(Loc.F("na pasie {0}", end.OnRunway.ToString(CultureInfo.InvariantCulture)));
                }

                parts.Add(end.EndName + " (" + string.Join(", ", counts) + ")");
            }

            return Loc.F("Pas wg ruchu: {0}", string.Join(", ", parts));
        }
    }
}

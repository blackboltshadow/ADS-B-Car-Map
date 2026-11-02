using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Decides whether an aircraft is visible under a set of active filters.
    /// </summary>
    public static class AircraftFilterEvaluator
    {
        /// <summary>
        /// Checks whether an aircraft passes the active filters. The aircraft is hidden when it matches any filter that hides. Otherwise it is shown when no filter shows or when it matches at least one of them. An unknown result never hides and never shows.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="activeFilters">Active filters.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>True when the aircraft should be visible.</returns>
        public static bool Passes(Aircraft aircraft, IReadOnlyList<AircraftFilter> activeFilters, Func<string?, FlightRoute?> routeLookup)
        {
            if (activeFilters.Count == 0)
            {
                return true;
            }

            bool anyShowFilter = false;

            foreach (AircraftFilter filter in activeFilters)
            {
                if (filter.Mode == FilterMode.Hide)
                {
                    if (filter.Expression.Evaluate(aircraft, routeLookup) == FilterResult.True)
                    {
                        return false;
                    }
                }
                else
                {
                    anyShowFilter = true;
                }
            }

            if (!anyShowFilter)
            {
                return true;
            }

            foreach (AircraftFilter filter in activeFilters)
            {
                if (filter.Mode == FilterMode.Show && filter.Expression.Evaluate(aircraft, routeLookup) == FilterResult.True)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

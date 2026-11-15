namespace AdsbCarMap.Data
{
    /// <summary>
    /// Helpers for the alert rules that watch one aircraft, shared by the aircraft card and the aircraft history.
    /// </summary>
    public static class WatchRules
    {
        /// <summary>
        /// Finds the watch rules that are about an aircraft: rules with its address or with its registration.
        /// </summary>
        /// <param name="rules">All rules.</param>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="registration">Registration of the aircraft or null when unknown.</param>
        /// <returns>The matching rules.</returns>
        public static List<AlertRule> Find(IEnumerable<AlertRule> rules, string hex, string? registration)
        {
            List<AlertRule> found = new List<AlertRule>();

            foreach (AlertRule rule in rules)
            {
                if (rule.IsWatch && rule.WatchesAircraft(hex, registration))
                {
                    found.Add(rule);
                }
            }

            return found;
        }

        /// <summary>
        /// Finds the rules that watch the address of an aircraft alone and can now be extended with its registration.
        /// </summary>
        /// <param name="rules">All rules.</param>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="registration">Registration of the aircraft or null when unknown.</param>
        /// <returns>The extended copies of the rules, which keep their identifiers; empty when there is nothing to extend.</returns>
        public static List<AlertRule> Extend(IEnumerable<AlertRule> rules, string hex, string? registration)
        {
            List<AlertRule> extended = new List<AlertRule>();

            if (string.IsNullOrWhiteSpace(registration) || FilterCondition.NormalizeRegistration(registration).Length == 0)
            {
                return extended;
            }

            foreach (AlertRule rule in rules)
            {
                if (rule.WatchedHex != null && rule.WatchedRegistration == null && string.Equals(rule.WatchedHex, hex, StringComparison.OrdinalIgnoreCase))
                {
                    extended.Add(rule.WithWatchedRegistration(registration));
                }
            }

            return extended;
        }
    }
}

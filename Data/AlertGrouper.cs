namespace AdsbCarMap.Data
{
    /// <summary>
    /// One notification to show: a single match, or several matches of the same rule that are reported together.
    /// </summary>
    public sealed class AlertNotice
    {
        /// <summary>
        /// Creates a notice.
        /// </summary>
        /// <param name="rule">Rule that fired.</param>
        /// <param name="matches">Matches reported by the notice, at least one.</param>
        /// <param name="isGroup">Whether the notice summarizes several matches.</param>
        public AlertNotice(AlertRule rule, IReadOnlyList<AlertMatch> matches, bool isGroup)
        {
            Rule = rule;
            Matches = matches;
            IsGroup = isGroup;
        }

        /// <summary>
        /// Rule that fired.
        /// </summary>
        public AlertRule Rule { get; }

        /// <summary>
        /// Matches reported by the notice.
        /// </summary>
        public IReadOnlyList<AlertMatch> Matches { get; }

        /// <summary>
        /// Whether the notice summarizes several matches.
        /// </summary>
        public bool IsGroup { get; }
    }

    /// <summary>
    /// Joins the matches of one check into notifications: when a rule fired for many aircraft at once, they are reported with a single notification instead of a flood.
    /// </summary>
    public static class AlertGrouper
    {
        /// <summary>
        /// Smallest threshold that groups anything.
        /// </summary>
        public const int MinimumThreshold = 2;

        /// <summary>
        /// Builds the notices for the matches of one check.
        /// </summary>
        /// <param name="matches">Matches returned by the engine.</param>
        /// <param name="threshold">Number of matches of one rule from which they are reported together; a value below <see cref="MinimumThreshold"/> never groups.</param>
        /// <returns>The notices, in the order in which the rules first appear in the matches.</returns>
        public static List<AlertNotice> Group(IReadOnlyList<AlertMatch> matches, int threshold)
        {
            List<AlertNotice> notices = new List<AlertNotice>();
            List<string> order = new List<string>();
            Dictionary<string, List<AlertMatch>> byRule = new Dictionary<string, List<AlertMatch>>();

            foreach (AlertMatch match in matches)
            {
                if (!byRule.TryGetValue(match.Rule.Id, out List<AlertMatch>? list))
                {
                    list = new List<AlertMatch>();
                    byRule[match.Rule.Id] = list;
                    order.Add(match.Rule.Id);
                }

                list.Add(match);
            }

            foreach (string ruleId in order)
            {
                List<AlertMatch> list = byRule[ruleId];

                if (threshold >= MinimumThreshold && list.Count >= threshold)
                {
                    notices.Add(new AlertNotice(list[0].Rule, list, true));
                }
                else
                {
                    foreach (AlertMatch match in list)
                    {
                        notices.Add(new AlertNotice(match.Rule, new List<AlertMatch> { match }, false));
                    }
                }
            }

            return notices;
        }
    }
}

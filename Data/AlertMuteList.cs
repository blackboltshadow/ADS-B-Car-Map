using System.Globalization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Alerts that are silenced until a given time, for example with the "mute for an hour" button of a notification.
    /// </summary>
    public sealed class AlertMuteList
    {
        /// <summary>
        /// Separator of the entries in the saved text.
        /// </summary>
        private const char EntrySeparator = ';';

        /// <summary>
        /// Separator of the rule identifier and the time in an entry.
        /// </summary>
        private const char ValueSeparator = '=';

        /// <summary>
        /// Time until which each rule is silenced, in UTC.
        /// </summary>
        private readonly Dictionary<string, DateTime> until = new Dictionary<string, DateTime>();

        /// <summary>
        /// Silences a rule.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <param name="untilUtc">Time at which the rule is audible again.</param>
        public void Mute(string ruleId, DateTime untilUtc)
        {
            until[ruleId] = untilUtc;
        }

        /// <summary>
        /// Makes a rule audible again.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        public void Unmute(string ruleId)
        {
            until.Remove(ruleId);
        }

        /// <summary>
        /// Checks whether a rule is silenced.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <param name="nowUtc">Current time in UTC.</param>
        /// <returns>True when the rule is silenced at that time.</returns>
        public bool IsMuted(string ruleId, DateTime nowUtc)
        {
            return until.TryGetValue(ruleId, out DateTime end) && end > nowUtc;
        }

        /// <summary>
        /// Gives the time until which a rule is silenced.
        /// </summary>
        /// <param name="ruleId">Identifier of the rule.</param>
        /// <param name="nowUtc">Current time in UTC.</param>
        /// <returns>The end of the silence in UTC or null when the rule is not silenced.</returns>
        public DateTime? GetMutedUntil(string ruleId, DateTime nowUtc)
        {
            return IsMuted(ruleId, nowUtc) ? until[ruleId] : null;
        }

        /// <summary>
        /// Forgets the silences that have ended.
        /// </summary>
        /// <param name="nowUtc">Current time in UTC.</param>
        /// <returns>True when something was removed.</returns>
        public bool Prune(DateTime nowUtc)
        {
            List<string> ended = new List<string>();

            foreach (KeyValuePair<string, DateTime> pair in until)
            {
                if (pair.Value <= nowUtc)
                {
                    ended.Add(pair.Key);
                }
            }

            foreach (string ruleId in ended)
            {
                until.Remove(ruleId);
            }

            return ended.Count > 0;
        }

        /// <summary>
        /// Writes the list as text.
        /// </summary>
        /// <returns>Entries "identifier=ticks" separated by semicolons.</returns>
        public string Format()
        {
            List<string> entries = new List<string>();

            foreach (KeyValuePair<string, DateTime> pair in until)
            {
                entries.Add(pair.Key + ValueSeparator + pair.Value.Ticks.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(EntrySeparator, entries);
        }

        /// <summary>
        /// Reads a list written by <see cref="Format"/>; damaged entries are skipped.
        /// </summary>
        /// <param name="text">Saved text or null.</param>
        /// <returns>The list.</returns>
        public static AlertMuteList Parse(string? text)
        {
            AlertMuteList list = new AlertMuteList();

            if (string.IsNullOrWhiteSpace(text))
            {
                return list;
            }

            foreach (string entry in text.Split(EntrySeparator))
            {
                int separator = entry.LastIndexOf(ValueSeparator);

                if (separator <= 0)
                {
                    continue;
                }

                if (long.TryParse(entry.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) && ticks > 0 && ticks <= DateTime.MaxValue.Ticks)
                {
                    list.until[entry.Substring(0, separator)] = new DateTime(ticks, DateTimeKind.Utc);
                }
            }

            return list;
        }
    }
}

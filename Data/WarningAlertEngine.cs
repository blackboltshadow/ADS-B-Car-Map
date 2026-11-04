using System.Globalization;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Settings of the notifications about new official notices.
    /// </summary>
    /// <param name="Enabled">Whether the notifications are switched on.</param>
    /// <param name="OnlyAlarms">Whether only the notices marked as an alarm give a notification.</param>
    public sealed record WarningAlertOptions(bool Enabled, bool OnlyAlarms)
    {
        /// <summary>
        /// Options of a new installation: switched off, all notices of the chosen categories.
        /// </summary>
        public static WarningAlertOptions Default => new WarningAlertOptions(false, false);

        /// <summary>
        /// Writes the options as text for the storage, e.g. "v1;1;0".
        /// </summary>
        /// <returns>Text that <see cref="Parse"/> reads back.</returns>
        public string Serialize()
        {
            return string.Join(";", "v1", Enabled ? "1" : "0", OnlyAlarms ? "1" : "0");
        }

        /// <summary>
        /// Reads the options from the stored text.
        /// </summary>
        /// <param name="text">Text written by <see cref="Serialize"/>, or null.</param>
        /// <returns>The options; <see cref="Default"/> for missing or damaged text.</returns>
        public static WarningAlertOptions Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Default;
            }

            string[] parts = text.Split(';');

            if (parts.Length != 3 || parts[0] != "v1" || (parts[1] != "0" && parts[1] != "1") || (parts[2] != "0" && parts[2] != "1"))
            {
                return Default;
            }

            return new WarningAlertOptions(parts[1] == "1", parts[2] == "1");
        }
    }

    /// <summary>
    /// What is remembered about a notice that was already reported.
    /// </summary>
    /// <param name="ExpiresUtc">Time after which the memory of the notice can go: the end of its validity, or a fixed time after the last change for a notice without an end.</param>
    /// <param name="Alarm">Whether the notice was marked as an alarm when it was reported.</param>
    public readonly record struct WarningReport(DateTime ExpiresUtc, bool Alarm);

    /// <summary>
    /// Decides which official notices need a notification: a notice of a category and of a voivodeship that the user watches that has not been reported before, or that became an alarm after it was reported. Every notice is reported once. The memory of what was reported is kept in the object and can be saved and restored, so that a restart of the application does not repeat the notifications. The readings of the water gauges are never reported: they are measurements that change every few minutes and are not notices.
    /// </summary>
    public sealed class WarningAlertEngine
    {
        /// <summary>
        /// How long after the end of a notice the memory of it is kept.
        /// </summary>
        private static readonly TimeSpan MemoryAfterEnd = TimeSpan.FromDays(1);

        /// <summary>
        /// How long a notice without an end is considered valid after its last change.
        /// </summary>
        private static readonly TimeSpan OpenEndedLife = TimeSpan.FromDays(14);

        /// <summary>
        /// Notices already reported, by category and identifier.
        /// </summary>
        private readonly Dictionary<string, WarningReport> reported;

        /// <summary>
        /// Creates an engine.
        /// </summary>
        /// <param name="restored">Memory saved earlier, or null when there is none.</param>
        public WarningAlertEngine(IEnumerable<KeyValuePair<string, WarningReport>>? restored)
        {
            reported = restored == null ? new Dictionary<string, WarningReport>() : new Dictionary<string, WarningReport>(restored);
        }

        /// <summary>
        /// What was reported, to be saved.
        /// </summary>
        public IReadOnlyDictionary<string, WarningReport> Reported => reported;

        /// <summary>
        /// Finds the notices that need a notification now.
        /// </summary>
        /// <param name="warnings">All the notices of the last download.</param>
        /// <param name="config">Choice of the categories and voivodeships; its display options (notices that are over, only alarms) do not matter here.</param>
        /// <param name="options">Settings of the notifications.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <param name="silent">When true, the notices are remembered without a notification. It is used at the first check after the notifications were switched on or the choice of the categories or voivodeships changed, so that the notices that have been there for days do not all report at once.</param>
        /// <returns>The notices to report, alarms first and then the most recently changed.</returns>
        public IReadOnlyList<Warning> Evaluate(IEnumerable<Warning> warnings, WarningFilterConfig config, WarningAlertOptions options, DateTime nowUtc, bool silent)
        {
            List<Warning> result = new List<Warning>();

            Prune(nowUtc);

            if (!options.Enabled)
            {
                return result;
            }

            foreach (Warning warning in warnings)
            {
                if (string.Equals(warning.Category, WarningCategories.WaterLevels, StringComparison.OrdinalIgnoreCase)
                    || !config.IsCategoryShown(warning.Category)
                    || !config.IsRegionWanted(warning)
                    || (options.OnlyAlarms && !warning.IsAlarm))
                {
                    continue;
                }

                DateTime expires = warning.ValidToUtc ?? warning.UpdatedUtc + OpenEndedLife;

                if (expires < nowUtc)
                {
                    continue;
                }

                string key = warning.Category.ToLowerInvariant() + "|" + warning.Id;
                bool known = reported.TryGetValue(key, out WarningReport previous);
                bool becameAlarm = known && warning.IsAlarm && !previous.Alarm;

                if (known && !becameAlarm)
                {
                    continue;
                }

                reported[key] = new WarningReport(expires, warning.IsAlarm);

                if (!silent)
                {
                    result.Add(warning);
                }
            }

            return result
                .OrderByDescending((Warning warning) => warning.IsAlarm)
                .ThenByDescending((Warning warning) => warning.UpdatedUtc)
                .ThenBy((Warning warning) => warning.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Forgets the notices that ended long ago.
        /// </summary>
        /// <param name="nowUtc">The moment, in UTC.</param>
        private void Prune(DateTime nowUtc)
        {
            List<string> expired = new List<string>();

            foreach (KeyValuePair<string, WarningReport> entry in reported)
            {
                if (entry.Value.ExpiresUtc + MemoryAfterEnd < nowUtc)
                {
                    expired.Add(entry.Key);
                }
            }

            foreach (string key in expired)
            {
                reported.Remove(key);
            }
        }

        /// <summary>
        /// Writes the time of an expiry for the storage.
        /// </summary>
        /// <param name="expiresUtc">The time.</param>
        /// <returns>Ticks as text.</returns>
        public static string FormatTicks(DateTime expiresUtc)
        {
            return expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        }
    }
}

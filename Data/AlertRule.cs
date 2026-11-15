using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Alert definition: which aircraft, in which area and for which events the user wants to be notified.
    /// </summary>
    public sealed class AlertRule
    {
        /// <summary>
        /// Creates a rule.
        /// </summary>
        /// <param name="id">Stable identifier of the rule.</param>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="isEnabled">Whether the rule is active.</param>
        /// <param name="area">Watched area.</param>
        /// <param name="condition">Condition the aircraft has to satisfy or null to accept every aircraft.</param>
        /// <param name="notifyOnEntry">Whether to notify when an aircraft enters the area.</param>
        /// <param name="notifyOnAppearance">Whether to notify when an aircraft shows up already inside the area.</param>
        /// <param name="cooldownMinutes">Minimum time between two notifications about the same aircraft.</param>
        /// <param name="conditionFilterId">Identifier of the saved filter that provides the condition, or null when the condition is not taken from a filter.</param>
        /// <param name="schedule">Days and quiet hours in which the rule may notify, or null for no restriction.</param>
        /// <param name="importance">How loudly the notifications of the rule are shown.</param>
        /// <param name="notifyOnExit">Whether to notify when an aircraft leaves the area.</param>
        /// <param name="watchedHex">ICAO address of the only aircraft the rule is about, or null when the rule is about every aircraft that satisfies the condition.</param>
        /// <param name="watchedRegistration">Registration of the only aircraft the rule is about, or null. When both the address and the registration are given, the rule is about the aircraft that has the address or the registration.</param>
        public AlertRule(string id, string name, bool isEnabled, AlertArea area, FilterNode? condition, bool notifyOnEntry, bool notifyOnAppearance, int cooldownMinutes, string? conditionFilterId = null, AlertSchedule? schedule = null, AlertImportance importance = AlertImportance.High, bool notifyOnExit = false, string? watchedHex = null, string? watchedRegistration = null)
        {
            string normalizedRegistration = string.IsNullOrWhiteSpace(watchedRegistration) ? string.Empty : FilterCondition.NormalizeRegistration(watchedRegistration);

            WatchedRegistration = normalizedRegistration.Length == 0 ? null : normalizedRegistration;
            NotifyOnExit = notifyOnExit;
            WatchedHex = string.IsNullOrWhiteSpace(watchedHex) ? null : watchedHex.Trim().ToLowerInvariant();
            Schedule = schedule ?? AlertSchedule.Always;
            Importance = importance;
            ConditionFilterId = conditionFilterId;
            Id = id;
            Name = name;
            IsEnabled = isEnabled;
            Area = area;
            Condition = condition;
            NotifyOnEntry = notifyOnEntry;
            NotifyOnAppearance = notifyOnAppearance;
            CooldownMinutes = cooldownMinutes;
        }

        /// <summary>
        /// Stable identifier of the rule.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Name shown to the user.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether the rule is active.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Watched area.
        /// </summary>
        public AlertArea Area { get; }

        /// <summary>
        /// Condition the aircraft has to satisfy or null to accept every aircraft.
        /// </summary>
        public FilterNode? Condition { get; }

        /// <summary>
        /// Identifier of the saved filter that provides the condition, or null when the condition is not taken from a filter.
        /// </summary>
        public string? ConditionFilterId { get; }

        /// <summary>
        /// Whether to notify when an aircraft enters the area.
        /// </summary>
        public bool NotifyOnEntry { get; }

        /// <summary>
        /// Whether to notify when an aircraft shows up already inside the area.
        /// </summary>
        public bool NotifyOnAppearance { get; }

        /// <summary>
        /// Whether to notify when an aircraft that satisfied the rule leaves the area.
        /// </summary>
        public bool NotifyOnExit { get; }

        /// <summary>
        /// Lower case ICAO address of the only aircraft the rule is about, or null when the rule is about every aircraft that satisfies the condition.
        /// </summary>
        public string? WatchedHex { get; }

        /// <summary>
        /// Registration of the only aircraft the rule is about, upper case and without dashes (for example "SPYBC"), or null.
        /// </summary>
        public string? WatchedRegistration { get; }

        /// <summary>
        /// Whether the rule is about one particular aircraft, identified by its address or its registration.
        /// </summary>
        public bool IsWatch => WatchedHex != null || WatchedRegistration != null;

        /// <summary>
        /// Checks whether the aircraft is the one the rule watches: it has the watched address or the watched registration.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="registration">Registration of the aircraft or null when unknown.</param>
        /// <returns>True when the rule is a watch rule and the aircraft is the watched one.</returns>
        public bool WatchesAircraft(string hex, string? registration)
        {
            if (WatchedHex != null && string.Equals(WatchedHex, hex, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return WatchedRegistration != null
                && !string.IsNullOrWhiteSpace(registration)
                && string.Equals(WatchedRegistration, FilterCondition.NormalizeRegistration(registration), StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates a copy of a watch rule that additionally watches a registration. The copy keeps the identifier, so it replaces the original.
        /// </summary>
        /// <param name="registration">Registration to watch.</param>
        /// <returns>The copy.</returns>
        public AlertRule WithWatchedRegistration(string registration)
        {
            return new AlertRule(Id, Name, IsEnabled, Area, Condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance, NotifyOnExit, WatchedHex, registration);
        }

        /// <summary>
        /// Minimum time between two notifications about the same aircraft.
        /// </summary>
        public int CooldownMinutes { get; }

        /// <summary>
        /// Days and quiet hours in which the rule may notify.
        /// </summary>
        public AlertSchedule Schedule { get; }

        /// <summary>
        /// How loudly the notifications of the rule are shown.
        /// </summary>
        public AlertImportance Importance { get; }

        /// <summary>
        /// Creates a copy of the rule with another condition.
        /// </summary>
        /// <param name="condition">New condition or null.</param>
        /// <returns>The copy.</returns>
        public AlertRule WithCondition(FilterNode? condition)
        {
            return new AlertRule(Id, Name, IsEnabled, Area, condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance, NotifyOnExit, WatchedHex, WatchedRegistration);
        }

        /// <summary>
        /// Creates a copy of the rule that is switched on or off.
        /// </summary>
        /// <param name="enabled">Whether the copy is enabled.</param>
        /// <returns>The copy.</returns>
        public AlertRule WithEnabled(bool enabled)
        {
            return new AlertRule(Id, Name, enabled, Area, Condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance, NotifyOnExit, WatchedHex, WatchedRegistration);
        }

        /// <summary>
        /// Creates a copy of the rule with another identifier and name, for duplicating a rule.
        /// </summary>
        /// <param name="id">Identifier of the copy.</param>
        /// <param name="name">Name of the copy.</param>
        /// <returns>The copy.</returns>
        public AlertRule WithIdentity(string id, string name)
        {
            return new AlertRule(id, name, IsEnabled, Area, Condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance, NotifyOnExit, WatchedHex, WatchedRegistration);
        }
    }
}

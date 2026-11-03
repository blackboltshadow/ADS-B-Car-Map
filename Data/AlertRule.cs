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
        public AlertRule(string id, string name, bool isEnabled, AlertArea area, FilterNode? condition, bool notifyOnEntry, bool notifyOnAppearance, int cooldownMinutes, string? conditionFilterId = null, AlertSchedule? schedule = null, AlertImportance importance = AlertImportance.High)
        {
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
            return new AlertRule(Id, Name, IsEnabled, Area, condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance);
        }

        /// <summary>
        /// Creates a copy of the rule that is switched on or off.
        /// </summary>
        /// <param name="enabled">Whether the copy is enabled.</param>
        /// <returns>The copy.</returns>
        public AlertRule WithEnabled(bool enabled)
        {
            return new AlertRule(Id, Name, enabled, Area, Condition, NotifyOnEntry, NotifyOnAppearance, CooldownMinutes, ConditionFilterId, Schedule, Importance);
        }
    }
}

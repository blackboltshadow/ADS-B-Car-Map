using AdsbCarMap.Data;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The alert rules and the global switch of alerts. Stored in its own SharedPreferences file, separate from the settings, so that resetting the settings does not remove the rules. The list is replaced as a whole on every change, so the data loop can read it without a lock.
    /// </summary>
    public sealed class AlertRuleStore
    {
        /// <summary>
        /// Name of the SharedPreferences file with the rules.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_alerts";

        /// <summary>
        /// Key of the JSON array with the rules.
        /// </summary>
        private const string RulesKey = "rules";

        /// <summary>
        /// Key of the global switch of alerts.
        /// </summary>
        private const string EnabledKey = "enabled";

        /// <summary>
        /// Key of the switch that makes the alerts work without the map.
        /// </summary>
        private const string BackgroundKey = "background";

        /// <summary>
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The rules in the order chosen by the user. Replaced as a whole on every change.
        /// </summary>
        private IReadOnlyList<AlertRule> rules;

        /// <summary>
        /// Whether alerts are switched on as a whole.
        /// </summary>
        private volatile bool enabled;

        /// <summary>
        /// Whether the alerts are checked without the map.
        /// </summary>
        private volatile bool background;

        /// <summary>
        /// Creates the store and reads it from the device.
        /// </summary>
        public AlertRuleStore()
        {
            ISharedPreferences preferences = OpenPreferences();

            rules = AlertRuleJson.Parse(preferences.GetString(RulesKey, null));
            enabled = preferences.GetBoolean(EnabledKey, true);
            background = preferences.GetBoolean(BackgroundKey, false);
        }

        /// <summary>
        /// Event raised after the rules or the global switch changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// The rules in the order chosen by the user.
        /// </summary>
        public IReadOnlyList<AlertRule> Rules => rules;

        /// <summary>
        /// Whether alerts are switched on as a whole.
        /// </summary>
        public bool IsEnabled => enabled;

        /// <summary>
        /// Whether the alerts are checked without the map, by a service with a permanent notification.
        /// </summary>
        public bool IsBackgroundEnabled => background;

        /// <summary>
        /// Switches the background checking on or off.
        /// </summary>
        /// <param name="value">True to check the alerts without the map.</param>
        public void SetBackgroundEnabled(bool value)
        {
            lock (syncRoot)
            {
                if (background == value)
                {
                    return;
                }

                background = value;
                OpenPreferences().Edit()?.PutBoolean(BackgroundKey, value)?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Switches alerts on or off as a whole.
        /// </summary>
        /// <param name="value">True to switch alerts on.</param>
        public void SetEnabled(bool value)
        {
            lock (syncRoot)
            {
                if (enabled == value)
                {
                    return;
                }

                enabled = value;
                OpenPreferences().Edit()?.PutBoolean(EnabledKey, value)?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Finds a rule by its identifier.
        /// </summary>
        /// <param name="id">Identifier of the rule.</param>
        /// <returns>The rule or null when there is none.</returns>
        public AlertRule? Find(string id)
        {
            return rules.FirstOrDefault((AlertRule rule) => rule.Id == id);
        }

        /// <summary>
        /// Adds a rule, or replaces the one with the same identifier.
        /// </summary>
        /// <param name="rule">Rule to save.</param>
        public void Save(AlertRule rule)
        {
            Change(() =>
            {
                List<AlertRule> copy = new List<AlertRule>(rules);
                int index = copy.FindIndex((AlertRule existing) => existing.Id == rule.Id);

                if (index >= 0)
                {
                    copy[index] = rule;
                }
                else
                {
                    copy.Add(rule);
                }

                rules = copy;

                return true;
            });
        }

        /// <summary>
        /// Switches one rule on or off.
        /// </summary>
        /// <param name="id">Identifier of the rule.</param>
        /// <param name="value">True to switch the rule on.</param>
        public void SetRuleEnabled(string id, bool value)
        {
            AlertRule? rule = Find(id);

            if (rule != null && rule.IsEnabled != value)
            {
                Save(rule.WithEnabled(value));
            }
        }

        /// <summary>
        /// Removes a rule.
        /// </summary>
        /// <param name="id">Identifier of the rule.</param>
        public void Remove(string id)
        {
            Change(() =>
            {
                List<AlertRule> copy = new List<AlertRule>(rules);

                if (copy.RemoveAll((AlertRule rule) => rule.Id == id) == 0)
                {
                    return false;
                }

                rules = copy;

                return true;
            });
        }

        /// <summary>
        /// Creates an identifier for a new rule.
        /// </summary>
        /// <returns>Unique identifier.</returns>
        public static string CreateId()
        {
            return "alert_" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Gives the rule the condition of its saved filter. The expression of a filter is used regardless of whether the filter shows or hides.
        /// </summary>
        /// <param name="rule">Rule to resolve.</param>
        /// <returns>The rule with the condition, the same rule when it has no filter, or null when its filter no longer exists.</returns>
        public static AlertRule? Resolve(AlertRule rule)
        {
            if (rule.ConditionFilterId == null)
            {
                return rule;
            }

            AircraftFilter? filter = AppServices.Filters.Find(rule.ConditionFilterId);

            return filter == null ? null : rule.WithCondition(filter.Expression);
        }

        /// <summary>
        /// Returns the rules that are switched on and can be evaluated now. A rule whose filter was removed is left out, so that it does not match every aircraft.
        /// </summary>
        /// <returns>The resolved active rules.</returns>
        public List<AlertRule> GetActiveRules()
        {
            List<AlertRule> result = new List<AlertRule>();

            if (!enabled)
            {
                return result;
            }

            foreach (AlertRule rule in rules)
            {
                AlertRule? resolved = rule.IsEnabled ? Resolve(rule) : null;

                if (resolved != null)
                {
                    result.Add(resolved);
                }
            }

            return result;
        }

        /// <summary>
        /// Applies a change under the lock, saves the rules and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="change">Edits the list and returns true when something changed.</param>
        private void Change(Func<bool> change)
        {
            lock (syncRoot)
            {
                if (!change())
                {
                    return;
                }

                OpenPreferences().Edit()?.PutString(RulesKey, AlertRuleJson.Serialize(rules))?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Opens the SharedPreferences file with the rules.
        /// </summary>
        /// <returns>SharedPreferences object.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

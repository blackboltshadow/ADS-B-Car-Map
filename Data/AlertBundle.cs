using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Alert rules together with the user-made filters they use, in the form written to and read from a file. A rule keeps only the identifier of its filter, so the file has to carry the filters too.
    /// </summary>
    public sealed class AlertBundle
    {
        /// <summary>
        /// Version of the file format.
        /// </summary>
        private const int FormatVersion = 1;

        /// <summary>
        /// Maximum depth of the JSON document, enough for the deepest filter that <see cref="FilterJson"/> accepts.
        /// </summary>
        private const int MaxDocumentDepth = 1100;

        /// <summary>
        /// Radius of the area of a watch rule in kilometers, the largest one the alert editor offers, so that editing the rule does not shrink it.
        /// </summary>
        public const double WatchRadiusKilometers = 300.0;

        /// <summary>
        /// Creates a bundle.
        /// </summary>
        /// <param name="rules">Rules in the bundle.</param>
        /// <param name="filters">Filters the rules refer to.</param>
        public AlertBundle(IReadOnlyList<AlertRule> rules, IReadOnlyList<AircraftFilter> filters)
        {
            Rules = rules;
            Filters = filters;
        }

        /// <summary>
        /// Rules in the bundle.
        /// </summary>
        public IReadOnlyList<AlertRule> Rules { get; }

        /// <summary>
        /// Filters the rules refer to.
        /// </summary>
        public IReadOnlyList<AircraftFilter> Filters { get; }

        /// <summary>
        /// Writes rules and the user-made filters they use as one JSON document.
        /// </summary>
        /// <param name="rules">Rules to write.</param>
        /// <param name="filterLookup">Returns a filter by its identifier or null when there is none.</param>
        /// <param name="isBuiltIn">Tells whether a filter is predefined; such filters exist on every device, so they are not written.</param>
        /// <returns>JSON text.</returns>
        public static string Serialize(IReadOnlyList<AlertRule> rules, Func<string, AircraftFilter?> filterLookup, Func<string, bool> isBuiltIn)
        {
            List<AircraftFilter> filters = new List<AircraftFilter>();
            HashSet<string> written = new HashSet<string>();

            foreach (AlertRule rule in rules)
            {
                string? filterId = rule.ConditionFilterId;

                if (filterId == null || isBuiltIn(filterId) || !written.Add(filterId))
                {
                    continue;
                }

                AircraftFilter? filter = filterLookup(filterId);

                if (filter != null)
                {
                    filters.Add(filter);
                }
            }

            StringBuilder builder = new StringBuilder();

            builder.Append("{\"version\":").Append(FormatVersion).Append(",\"alerts\":");
            builder.Append(AlertRuleJson.Serialize(rules));
            builder.Append(",\"filters\":");
            builder.Append(FilterJson.Serialize(filters));
            builder.Append('}');

            return builder.ToString();
        }

        /// <summary>
        /// Reads a document written by <see cref="Serialize"/>. A bare array of rules is accepted too.
        /// </summary>
        /// <param name="json">JSON text, or null.</param>
        /// <returns>The bundle, or null when the text is not a document with alerts.</returns>
        public static AlertBundle? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                JsonDocumentOptions options = new JsonDocumentOptions();

                options.MaxDepth = MaxDocumentDepth;

                using JsonDocument document = JsonDocument.Parse(json, options);
                JsonElement root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    return new AlertBundle(AlertRuleJson.Parse(root.GetRawText()), new List<AircraftFilter>());
                }

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("alerts", out JsonElement alerts) || alerts.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                List<AircraftFilter> filters = new List<AircraftFilter>();

                if (root.TryGetProperty("filters", out JsonElement filterList) && filterList.ValueKind == JsonValueKind.Array)
                {
                    filters = FilterJson.Parse(filterList.GetRawText());
                }

                return new AlertBundle(AlertRuleJson.Parse(alerts.GetRawText()), filters);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Decides what an import adds: every rule gets a new identifier, so importing never overwrites a rule, and only the filters that are missing are added.
        /// </summary>
        /// <param name="createId">Creates the identifier of an imported rule.</param>
        /// <param name="filterExists">Tells whether a filter with the identifier already exists, built-in or user-made.</param>
        /// <returns>The rules to save and the filters to add.</returns>
        public AlertBundle PrepareImport(Func<string> createId, Func<string, bool> filterExists)
        {
            List<AlertRule> rules = new List<AlertRule>();

            foreach (AlertRule rule in Rules)
            {
                rules.Add(rule.WithIdentity(createId(), rule.Name));
            }

            List<AircraftFilter> filters = new List<AircraftFilter>();

            foreach (AircraftFilter filter in Filters)
            {
                if (!filterExists(filter.Id) && filters.All((AircraftFilter added) => added.Id != filter.Id))
                {
                    filters.Add(filter);
                }
            }

            return new AlertBundle(rules, filters);
        }

        /// <summary>
        /// Creates the rule that watches one aircraft: it notifies when this aircraft shows up within <see cref="WatchRadiusKilometers"/> around the user.
        /// </summary>
        /// <param name="id">Identifier of the new rule.</param>
        /// <param name="name">Name of the rule.</param>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <returns>The rule.</returns>
        public static AlertRule CreateWatchRule(string id, string name, string hex)
        {
            AlertArea area = new AlertArea(AlertAreaKind.AroundMe, WatchRadiusKilometers, null, null);

            return new AlertRule(id, name, true, area, null, true, true, 60, null, null, AlertImportance.High, false, hex);
        }
    }
}

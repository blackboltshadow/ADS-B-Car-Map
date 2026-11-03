using System.Text;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// What the user wants to see among the official notices: which categories, which voivodeships, whether notices that are over are shown, and whether only alarms are shown. The categories are not a fixed list, because the source may publish a new one; a category the user has not decided about is hidden. The object does not change; every change gives a new one.
    /// </summary>
    public sealed class WarningFilterConfig
    {
        /// <summary>
        /// Prefix of the stored text; it identifies the format.
        /// </summary>
        private const string StoredPrefix = "v1";

        /// <summary>
        /// Decisions of the user by category, compared without regard to case.
        /// </summary>
        private readonly Dictionary<string, bool> categoryChoices;

        /// <summary>
        /// Voivodeships the user wants, compared without regard to case; empty means all of them.
        /// </summary>
        private readonly HashSet<string> provinces;

        /// <summary>
        /// Creates a configuration.
        /// </summary>
        /// <param name="categoryChoices">Decision of the user for every category he decided about; true means shown.</param>
        /// <param name="provinces">Voivodeships the user wants; empty means all of them.</param>
        /// <param name="showExpired">Whether notices that are over are shown.</param>
        /// <param name="onlyAlarms">Whether only the notices marked as an alarm are shown.</param>
        public WarningFilterConfig(IReadOnlyDictionary<string, bool> categoryChoices, IEnumerable<string> provinces, bool showExpired, bool onlyAlarms)
        {
            this.categoryChoices = new Dictionary<string, bool>(categoryChoices, StringComparer.OrdinalIgnoreCase);
            this.provinces = new HashSet<string>(provinces, StringComparer.OrdinalIgnoreCase);
            ShowExpired = showExpired;
            OnlyAlarms = onlyAlarms;
        }

        /// <summary>
        /// The configuration of a new installation: the categories from <see cref="WarningCategories.DefaultShown"/>, all voivodeships, notices that are over hidden, all notices and not only alarms.
        /// </summary>
        public static WarningFilterConfig Default
        {
            get
            {
                Dictionary<string, bool> choices = new Dictionary<string, bool>();

                foreach (string category in WarningCategories.DefaultShown)
                {
                    choices[category] = true;
                }

                return new WarningFilterConfig(choices, new List<string>(), false, false);
            }
        }

        /// <summary>
        /// Whether notices that are over are shown.
        /// </summary>
        public bool ShowExpired { get; }

        /// <summary>
        /// Whether only the notices marked as an alarm are shown.
        /// </summary>
        public bool OnlyAlarms { get; }

        /// <summary>
        /// The voivodeships the user wants, in the order of their codes; empty means all of them.
        /// </summary>
        public IReadOnlyList<string> Provinces => provinces.OrderBy((string slug) => slug, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// The categories shown, in the order of the list of known categories.
        /// </summary>
        public IReadOnlyList<string> ShownCategories => categoryChoices.Where((KeyValuePair<string, bool> entry) => entry.Value).Select((KeyValuePair<string, bool> entry) => entry.Key).OrderBy((string name) => WarningCategories.GetOrder(name)).ThenBy((string name) => name, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// Tells whether a category is shown.
        /// </summary>
        /// <param name="category">Category as the source names it.</param>
        /// <returns>The decision of the user; false for a category he has not decided about.</returns>
        public bool IsCategoryShown(string category)
        {
            return categoryChoices.TryGetValue(category, out bool shown) && shown;
        }

        /// <summary>
        /// Tells whether a voivodeship is wanted.
        /// </summary>
        /// <param name="slug">Identifier of the voivodeship.</param>
        /// <returns>True when all voivodeships are wanted or this one is chosen.</returns>
        public bool IsProvinceWanted(string slug)
        {
            return provinces.Count == 0 || provinces.Contains(slug);
        }

        /// <summary>
        /// Tells whether a notice passes the configuration.
        /// </summary>
        /// <param name="warning">The notice.</param>
        /// <param name="nowUtc">The moment, in UTC, at which the validity is judged.</param>
        /// <returns>True when its category is shown, it applies to a wanted voivodeship (a notice without a voivodeship applies to the whole country), it is not over unless the over ones are shown, and it is an alarm when only alarms are shown.</returns>
        public bool Passes(Warning warning, DateTime nowUtc)
        {
            if (!IsCategoryShown(warning.Category) || (OnlyAlarms && !warning.IsAlarm))
            {
                return false;
            }

            if (!ShowExpired && warning.ValidToUtc != null && warning.ValidToUtc.Value < nowUtc)
            {
                return false;
            }

            return provinces.Count == 0 || warning.Provinces.Count == 0 || warning.Provinces.Any((WarningProvince province) => provinces.Contains(province.Slug));
        }

        /// <summary>
        /// Selects the notices to show, alarms first and then the most recently changed ones.
        /// </summary>
        /// <param name="warnings">All the notices.</param>
        /// <param name="nowUtc">The moment, in UTC, at which the validity is judged.</param>
        /// <returns>The notices that pass the configuration, in the order to show them.</returns>
        public IReadOnlyList<Warning> Select(IEnumerable<Warning> warnings, DateTime nowUtc)
        {
            return warnings
                .Where((Warning warning) => Passes(warning, nowUtc))
                .OrderByDescending((Warning warning) => warning.IsAlarm)
                .ThenByDescending((Warning warning) => warning.UpdatedUtc)
                .ThenBy((Warning warning) => warning.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Gives a configuration with the decision about one category changed.
        /// </summary>
        /// <param name="category">Category as the source names it.</param>
        /// <param name="shown">Whether the category is shown.</param>
        /// <returns>The new configuration.</returns>
        public WarningFilterConfig WithCategory(string category, bool shown)
        {
            Dictionary<string, bool> choices = new Dictionary<string, bool>(categoryChoices, StringComparer.OrdinalIgnoreCase);
            choices[category] = shown;

            return new WarningFilterConfig(choices, provinces, ShowExpired, OnlyAlarms);
        }

        /// <summary>
        /// Gives a configuration with the chosen voivodeships replaced.
        /// </summary>
        /// <param name="slugs">Identifiers of the voivodeships; empty means all of them.</param>
        /// <returns>The new configuration.</returns>
        public WarningFilterConfig WithProvinces(IEnumerable<string> slugs)
        {
            return new WarningFilterConfig(categoryChoices, slugs, ShowExpired, OnlyAlarms);
        }

        /// <summary>
        /// Gives a configuration with the treatment of the notices that are over changed.
        /// </summary>
        /// <param name="showExpired">Whether notices that are over are shown.</param>
        /// <returns>The new configuration.</returns>
        public WarningFilterConfig WithShowExpired(bool showExpired)
        {
            return new WarningFilterConfig(categoryChoices, provinces, showExpired, OnlyAlarms);
        }

        /// <summary>
        /// Gives a configuration with the alarm-only mode changed.
        /// </summary>
        /// <param name="onlyAlarms">Whether only the notices marked as an alarm are shown.</param>
        /// <returns>The new configuration.</returns>
        public WarningFilterConfig WithOnlyAlarms(bool onlyAlarms)
        {
            return new WarningFilterConfig(categoryChoices, provinces, ShowExpired, onlyAlarms);
        }

        /// <summary>
        /// Writes the configuration as text for the storage, e.g. "v1;hydrologiczne+,ogolne+,stany-wod|;mazowieckie,slaskie;0;1" (a category is followed by + when shown and by a bar when hidden, because its code may hold a hyphen).
        /// </summary>
        /// <returns>Text that <see cref="Parse"/> reads back.</returns>
        public string Serialize()
        {
            List<string> names = new List<string>(categoryChoices.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder builder = new StringBuilder();

            foreach (string name in names)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(name).Append(categoryChoices[name] ? '+' : '|');
            }

            return string.Join(";", StoredPrefix, builder.ToString(), string.Join(",", Provinces), ShowExpired ? "1" : "0", OnlyAlarms ? "1" : "0");
        }

        /// <summary>
        /// Reads a configuration from the stored text.
        /// </summary>
        /// <param name="text">Text written by <see cref="Serialize"/>, or null.</param>
        /// <returns>The configuration; <see cref="Default"/> for missing, damaged or unknown text. A damaged entry is skipped.</returns>
        public static WarningFilterConfig Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Default;
            }

            string[] parts = text.Split(';');

            if (parts.Length != 5 || parts[0] != StoredPrefix || (parts[3] != "0" && parts[3] != "1") || (parts[4] != "0" && parts[4] != "1"))
            {
                return Default;
            }

            Dictionary<string, bool> choices = new Dictionary<string, bool>();

            foreach (string entry in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = entry.Trim();

                if (trimmed.Length < 2 || (trimmed[trimmed.Length - 1] != '+' && trimmed[trimmed.Length - 1] != '|'))
                {
                    continue;
                }

                choices[trimmed.Substring(0, trimmed.Length - 1)] = trimmed[trimmed.Length - 1] == '+';
            }

            IEnumerable<string> slugs = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select((string slug) => slug.Trim()).Where((string slug) => slug.Length > 0);

            return new WarningFilterConfig(choices, slugs, parts[3] == "1", parts[4] == "1");
        }
    }
}

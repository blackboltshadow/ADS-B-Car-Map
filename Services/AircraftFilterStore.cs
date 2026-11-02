using AdsbCarMap.Data;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The predefined and user-made aircraft filters together with the set of filters that are switched on, shared by the phone and Android Auto. Stored in its own SharedPreferences file, separate from the settings, so that resetting the settings does not remove the filters. The lists are replaced as a whole on every change, so the drawing thread can read them without a lock.
    /// </summary>
    public sealed class AircraftFilterStore
    {
        /// <summary>
        /// Name of the SharedPreferences file with the filters.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_filters";

        /// <summary>
        /// Key of the JSON array with the user-made filters.
        /// </summary>
        private const string CustomKey = "custom";

        /// <summary>
        /// Key of the comma separated identifiers of the filters that are switched on.
        /// </summary>
        private const string ActiveKey = "active";

        /// <summary>
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// User-made filters. Replaced as a whole on every change.
        /// </summary>
        private IReadOnlyList<AircraftFilter> custom;

        /// <summary>
        /// Identifiers of the filters that are switched on. Replaced as a whole on every change.
        /// </summary>
        private IReadOnlySet<string> activeIds;

        /// <summary>
        /// Predefined filters followed by the user-made ones. Replaced together with <see cref="custom"/>.
        /// </summary>
        private IReadOnlyList<AircraftFilter> all;

        /// <summary>
        /// The filters that are switched on, for the drawing thread. Replaced together with <see cref="activeIds"/>.
        /// </summary>
        private IReadOnlyList<AircraftFilter> activeFilters;

        /// <summary>
        /// Creates the store and reads it from the device.
        /// </summary>
        public AircraftFilterStore()
        {
            ISharedPreferences preferences = OpenPreferences();

            custom = FilterJson.Parse(preferences.GetString(CustomKey, null));

            string savedActive = preferences.GetString(ActiveKey, string.Empty) ?? string.Empty;

            activeIds = new HashSet<string>(savedActive.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            all = BuildAll(custom);
            activeFilters = BuildActive(all, activeIds);
        }

        /// <summary>
        /// Event raised after the filters or the set of switched on filters changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// Predefined filters followed by the user-made ones.
        /// </summary>
        public IReadOnlyList<AircraftFilter> All => all;

        /// <summary>
        /// The filters that are switched on.
        /// </summary>
        public IReadOnlyList<AircraftFilter> ActiveFilters => activeFilters;

        /// <summary>
        /// Tells whether a filter is a predefined one.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <returns>True for a predefined filter, which cannot be edited or removed.</returns>
        public bool IsBuiltIn(string id)
        {
            return BuiltInFilters.All.Any((AircraftFilter filter) => filter.Id == id);
        }

        /// <summary>
        /// Tells whether a filter is switched on.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <returns>True when the filter is switched on.</returns>
        public bool IsActive(string id)
        {
            return activeIds.Contains(id);
        }

        /// <summary>
        /// Finds a filter by its identifier.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <returns>The filter or null when there is none.</returns>
        public AircraftFilter? Find(string id)
        {
            return all.FirstOrDefault((AircraftFilter filter) => filter.Id == id);
        }

        /// <summary>
        /// Switches a filter on or off.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        /// <param name="active">True to switch the filter on.</param>
        public void SetActive(string id, bool active)
        {
            Change(() =>
            {
                if (activeIds.Contains(id) == active || Find(id) == null)
                {
                    return false;
                }

                HashSet<string> copy = new HashSet<string>(activeIds);

                if (active)
                {
                    copy.Add(id);
                }
                else
                {
                    copy.Remove(id);
                }

                activeIds = copy;

                return true;
            });
        }

        /// <summary>
        /// Adds a user-made filter, or replaces the one with the same identifier. A predefined filter cannot be replaced.
        /// </summary>
        /// <param name="filter">Filter to save.</param>
        public void Save(AircraftFilter filter)
        {
            Change(() =>
            {
                if (IsBuiltIn(filter.Id))
                {
                    return false;
                }

                List<AircraftFilter> copy = new List<AircraftFilter>(custom);
                int index = copy.FindIndex((AircraftFilter existing) => existing.Id == filter.Id);

                if (index >= 0)
                {
                    copy[index] = filter;
                }
                else
                {
                    copy.Add(filter);
                }

                custom = copy;

                return true;
            });
        }

        /// <summary>
        /// Removes a user-made filter and switches it off.
        /// </summary>
        /// <param name="id">Identifier of the filter.</param>
        public void Remove(string id)
        {
            Change(() =>
            {
                List<AircraftFilter> copy = new List<AircraftFilter>(custom);

                if (copy.RemoveAll((AircraftFilter filter) => filter.Id == id) == 0)
                {
                    return false;
                }

                custom = copy;

                HashSet<string> activeCopy = new HashSet<string>(activeIds);

                activeCopy.Remove(id);
                activeIds = activeCopy;

                return true;
            });
        }

        /// <summary>
        /// Creates an identifier for a new user-made filter.
        /// </summary>
        /// <returns>Unique identifier.</returns>
        public static string CreateId()
        {
            return "custom_" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Applies a change under the lock, rebuilds the published lists, saves them and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="change">Edits the fields and returns true when something changed.</param>
        private void Change(Func<bool> change)
        {
            lock (syncRoot)
            {
                if (!change())
                {
                    return;
                }

                all = BuildAll(custom);
                activeFilters = BuildActive(all, activeIds);
                OpenPreferences().Edit()?.PutString(CustomKey, FilterJson.Serialize(custom))?.PutString(ActiveKey, string.Join(",", activeIds))?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Joins the predefined filters and the user-made ones.
        /// </summary>
        /// <param name="customFilters">User-made filters.</param>
        /// <returns>All filters.</returns>
        private static IReadOnlyList<AircraftFilter> BuildAll(IReadOnlyList<AircraftFilter> customFilters)
        {
            List<AircraftFilter> result = new List<AircraftFilter>(BuiltInFilters.All);

            result.AddRange(customFilters);

            return result;
        }

        /// <summary>
        /// Selects the filters that are switched on, in the order of the full list.
        /// </summary>
        /// <param name="allFilters">All filters.</param>
        /// <param name="ids">Identifiers of the filters that are switched on.</param>
        /// <returns>The active filters.</returns>
        private static IReadOnlyList<AircraftFilter> BuildActive(IReadOnlyList<AircraftFilter> allFilters, IReadOnlySet<string> ids)
        {
            return allFilters.Where((AircraftFilter filter) => ids.Contains(filter.Id)).ToList();
        }

        /// <summary>
        /// Opens the SharedPreferences file with the filters.
        /// </summary>
        /// <returns>SharedPreferences object.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

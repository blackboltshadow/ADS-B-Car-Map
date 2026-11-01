using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// The ordered list of favorite airports, shared by the phone and Android Auto. Stored as ICAO codes in its own SharedPreferences file, separate from the settings, so that resetting the settings does not remove the favorites. The list is replaced as a whole on every change, so the drawing thread can read it without a lock.
    /// </summary>
    public sealed class FavoriteAirports
    {
        /// <summary>
        /// Name of the SharedPreferences file with the favorites.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_favorites";

        /// <summary>
        /// Key of the comma separated list of ICAO codes.
        /// </summary>
        private const string Key = "airports";

        /// <summary>
        /// Lock protecting changes of the list.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Favorite ICAO codes in the order chosen by the user. Replaced as a whole on every change.
        /// </summary>
        private IReadOnlyList<string> codes;

        /// <summary>
        /// The same codes in a set, in upper case, for fast membership tests. Replaced together with <see cref="codes"/>.
        /// </summary>
        private HashSet<string> codeSet;

        /// <summary>
        /// Creates the list and reads it from the device.
        /// </summary>
        public FavoriteAirports()
        {
            List<string> loaded = new List<string>();
            string saved = OpenPreferences().GetString(Key, string.Empty) ?? string.Empty;

            foreach (string code in saved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!loaded.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    loaded.Add(code);
                }
            }

            codes = loaded;
            codeSet = new HashSet<string>(loaded.Select(code => code.ToUpperInvariant()));
        }

        /// <summary>
        /// Event raised after the list changed, on the thread that made the change.
        /// </summary>
        public event System.Action? Changed;

        /// <summary>
        /// Favorite ICAO codes in the order chosen by the user.
        /// </summary>
        public IReadOnlyList<string> Codes => codes;

        /// <summary>
        /// Tells whether an airport is a favorite.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <returns>True when the airport is on the list.</returns>
        public bool Contains(string icao)
        {
            return codeSet.Contains(icao.ToUpperInvariant());
        }

        /// <summary>
        /// Adds an airport to the end of the list. Does nothing when it is already there.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        public void Add(string icao)
        {
            Change(list =>
            {
                if (list.Contains(icao, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                list.Add(icao);

                return true;
            });
        }

        /// <summary>
        /// Removes an airport from the list.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        public void Remove(string icao)
        {
            Change(list => list.RemoveAll(code => string.Equals(code, icao, StringComparison.OrdinalIgnoreCase)) > 0);
        }

        /// <summary>
        /// Adds the airport when it is not a favorite and removes it when it is.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <returns>True when the airport is a favorite after the change.</returns>
        public bool Toggle(string icao)
        {
            if (Contains(icao))
            {
                Remove(icao);

                return false;
            }

            Add(icao);

            return true;
        }

        /// <summary>
        /// Moves an airport up or down the list by one position.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <param name="offset">-1 moves it towards the beginning, 1 towards the end.</param>
        public void Move(string icao, int offset)
        {
            Change(list =>
            {
                int index = list.FindIndex(code => string.Equals(code, icao, StringComparison.OrdinalIgnoreCase));
                int target = index + offset;

                if (index < 0 || target < 0 || target >= list.Count)
                {
                    return false;
                }

                string code = list[index];

                list.RemoveAt(index);
                list.Insert(target, code);

                return true;
            });
        }

        /// <summary>
        /// Applies a change to a copy of the list, publishes the copy, saves it and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="change">Edits the copy and returns true when something changed.</param>
        private void Change(Func<List<string>, bool> change)
        {
            lock (syncRoot)
            {
                List<string> copy = new List<string>(codes);

                if (!change(copy))
                {
                    return;
                }

                codes = copy;
                codeSet = new HashSet<string>(copy.Select(code => code.ToUpperInvariant()));
                OpenPreferences().Edit()?.PutString(Key, string.Join(",", copy))?.Apply();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Opens the SharedPreferences file with the favorites.
        /// </summary>
        /// <returns>SharedPreferences object.</returns>
        private static ISharedPreferences OpenPreferences()
        {
            return Android.App.Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}

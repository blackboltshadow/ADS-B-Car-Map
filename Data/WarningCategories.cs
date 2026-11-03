using AdsbCarMap.Localization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Knowledge about the categories of the official notices: names, order and which ones are shown by default. The source may add a category that is not listed here; it is then shown under its own code, after the known ones.
    /// </summary>
    public static class WarningCategories
    {
        /// <summary>
        /// Category of general notices and warnings.
        /// </summary>
        public const string General = "ogolne";

        /// <summary>
        /// Category of weather warnings.
        /// </summary>
        public const string Weather = "meteorologiczne";

        /// <summary>
        /// Category of hydrological warnings, e.g. a drought or a flood danger.
        /// </summary>
        public const string Hydrological = "hydrologiczne";

        /// <summary>
        /// Category of notices about the roads.
        /// </summary>
        public const string Roads = "informacje-drogowe";

        /// <summary>
        /// Category of the readings of the water gauges, which are measurements and not warnings.
        /// </summary>
        public const string WaterLevels = "stany-wod";

        /// <summary>
        /// Known categories in the order they are listed.
        /// </summary>
        public static readonly IReadOnlyList<string> Known = new List<string> { General, Weather, Hydrological, Roads, WaterLevels };

        /// <summary>
        /// Categories shown by default: the warnings. The roads, which are many and are about traffic, and the readings of the gauges are for the user to switch on.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultShown = new List<string> { General, Weather, Hydrological };

        /// <summary>
        /// Gives the name of a category to show to the user.
        /// </summary>
        /// <param name="category">Category as the source names it.</param>
        /// <returns>The translated name, or the code of a category that is not known.</returns>
        public static string GetName(string category)
        {
            switch (category.ToLowerInvariant())
            {
                case General:
                    return Loc.T("Ogólne");
                case Weather:
                    return Loc.T("Meteorologiczne");
                case Hydrological:
                    return Loc.T("Hydrologiczne");
                case Roads:
                    return Loc.T("Informacje drogowe");
                case WaterLevels:
                    return Loc.T("Stany wód");
                default:
                    return string.IsNullOrWhiteSpace(category) ? "?" : category;
            }
        }

        /// <summary>
        /// Gives the position of a category in the list: known categories first, in their order, then the others.
        /// </summary>
        /// <param name="category">Category as the source names it.</param>
        /// <returns>Position for sorting.</returns>
        public static int GetOrder(string category)
        {
            for (int i = 0; i < Known.Count; i++)
            {
                if (string.Equals(Known[i], category, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return Known.Count;
        }
    }
}

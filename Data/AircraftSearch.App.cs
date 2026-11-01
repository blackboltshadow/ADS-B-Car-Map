using AdsbCarMap.Services;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// The part of the search that reads the application state: the route cache, the favorites and the settings.
    /// </summary>
    public static partial class AircraftSearch
    {
        /// <summary>
        /// Returns the aircraft description: type, route and altitude in the unit from the settings.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="route">Route, or null when unknown.</param>
        /// <returns>Description built from the available data.</returns>
        public static string Describe(Aircraft item, FlightRoute? route)
        {
            return Describe(item, route, AltitudeFormatter.Format);
        }

        /// <summary>
        /// Searches airports and aircraft together using the application route cache, favorites and settings.
        /// </summary>
        /// <param name="aircraft">Aircraft to search.</param>
        /// <param name="airports">Airport data.</param>
        /// <param name="routes">Flight route cache.</param>
        /// <param name="query">Query, words separated by spaces.</param>
        /// <param name="maxResults">Maximum number of rows.</param>
        /// <param name="reservedForAircraft">Number of rows kept for aircraft.</param>
        /// <param name="nearLatitude">Latitude of the point whose neighborhood is listed for an empty query.</param>
        /// <param name="nearLongitude">Longitude of the point whose neighborhood is listed for an empty query.</param>
        /// <returns>Rows of the result list.</returns>
        public static List<SearchResult> SearchAll(IReadOnlyList<Aircraft> aircraft, AirportDatabase airports, RouteCache routes, string query, int maxResults, int reservedForAircraft, double nearLatitude, double nearLongitude)
        {
            return SearchAll(
                aircraft,
                airports,
                routes.Peek,
                AppServices.Favorites.Codes,
                AppSettings.IsAirportCategoryEnabled(AirportCategory.Heliport),
                query,
                maxResults,
                reservedForAircraft,
                nearLatitude,
                nearLongitude);
        }

        /// <summary>
        /// Returns the title of an airport row: the code and the name, with a star in front for a favorite.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Text such as "WAW · Warsaw Chopin Airport".</returns>
        public static string GetAirportTitle(Airport airport)
        {
            return GetAirportTitle(airport, AppServices.Favorites.Contains(airport.Icao));
        }

        /// <summary>
        /// Returns the description of an airport row: the place, the size class and the elevation in the unit from the settings.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Text such as "Warsaw, PL · duże lotnisko · 110 m".</returns>
        public static string DescribeAirport(Airport airport)
        {
            return DescribeAirport(airport, AltitudeFormatter.Format);
        }
    }
}

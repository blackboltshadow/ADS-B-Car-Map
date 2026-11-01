using AdsbCarMap.Settings;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Searching aircraft by flight, registration, type and route, and the aircraft description for result lists.
    /// </summary>
    public static class AircraftSearch
    {
        /// <summary>
        /// Returns the aircraft whose flight, ICAO address, registration, type or route contain all the words of the query. Only routes that are already downloaded are considered, so the API is not queried for every aircraft.
        /// </summary>
        /// <param name="aircraft">Aircraft to search.</param>
        /// <param name="routes">Flight route cache.</param>
        /// <param name="query">Query, words separated by spaces.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <returns>Matching aircraft sorted by flight.</returns>
        public static List<Aircraft> Search(IReadOnlyList<Aircraft> aircraft, RouteCache routes, string query, int maxResults)
        {
            string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<Aircraft> result = new List<Aircraft>();

            if (terms.Length == 0)
            {
                return result;
            }

            foreach (Aircraft item in aircraft)
            {
                string haystack = BuildSearchText(item, routes.Peek(item.Callsign));
                bool matches = terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));

                if (matches)
                {
                    result.Add(item);
                }
            }

            return result
                .OrderBy(item => item.Callsign ?? item.Hex, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Returns all aircraft sorted by flight, for example for the list shown when the query is empty.
        /// </summary>
        /// <param name="aircraft">Aircraft list.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <returns>Aircraft sorted by flight or ICAO address.</returns>
        public static List<Aircraft> ListAll(IReadOnlyList<Aircraft> aircraft, int maxResults)
        {
            return aircraft
                .OrderBy(item => item.Callsign ?? item.Hex, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Returns the aircraft name: the flight, or the ICAO address when there is none.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <returns>Name to display.</returns>
        public static string GetName(Aircraft item)
        {
            return string.IsNullOrEmpty(item.Callsign) ? item.Hex.ToUpperInvariant() : item.Callsign;
        }

        /// <summary>
        /// Returns the airline: the name from the route, or the airline ICAO code from the beginning of the callsign when there is no route.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="route">Route, or null when unknown.</param>
        /// <returns>Airline name or code, or an empty string when it cannot be determined.</returns>
        public static string GetAirline(Aircraft item, FlightRoute? route)
        {
            if (!string.IsNullOrEmpty(route?.AirlineName))
            {
                return route.AirlineName;
            }

            string callsign = item.Callsign ?? string.Empty;

            if (callsign.Length > 3 && char.IsAsciiLetterUpper(callsign[0]) && char.IsAsciiLetterUpper(callsign[1]) && char.IsAsciiLetterUpper(callsign[2]) && char.IsAsciiDigit(callsign[3]))
            {
                return callsign.Substring(0, 3);
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the aircraft description: type, route and altitude in the unit from the settings, for example "B738 · WAW→LHR · 10 668 m".
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="route">Route, or null when unknown.</param>
        /// <returns>Description built from the available data.</returns>
        public static string Describe(Aircraft item, FlightRoute? route)
        {
            List<string> parts = new List<string>();

            if (!string.IsNullOrEmpty(item.TypeCode))
            {
                parts.Add(item.TypeCode);
            }

            if (route != null)
            {
                parts.Add(route.ToShortString());
            }

            if (item.OnGround)
            {
                parts.Add("na ziemi");
            }
            else if (item.AltitudeFeet.HasValue)
            {
                parts.Add(AltitudeFormatter.Format(item.AltitudeFeet.Value));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Builds the text in which the words of the query are searched.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="route">Route, or null when unknown.</param>
        /// <returns>Joined fields of the aircraft and the route.</returns>
        private static string BuildSearchText(Aircraft item, FlightRoute? route)
        {
            List<string?> fields = new List<string?>
            {
                item.Callsign,
                item.Hex,
                item.Registration,
                item.TypeCode,
                route?.OriginCode,
                route?.OriginCity,
                route?.DestinationCode,
                route?.DestinationCity,
                route?.AirlineName,
            };

            return string.Join(" ", fields.Where(field => !string.IsNullOrEmpty(field)));
        }
    }
}

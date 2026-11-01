
namespace AdsbCarMap.Data
{
    /// <summary>
    /// Searching aircraft by flight, registration, type and route, and the aircraft description for result lists.
    /// </summary>
    public static partial class AircraftSearch
    {
        /// <summary>
        /// Number of the nearest airports listed first when the query is empty.
        /// </summary>
        private const int NearestAirportsForEmptyQuery = 3;

        /// <summary>
        /// Returns the aircraft whose flight, ICAO address, registration, type or route contain all the words of the query. Only routes that are already downloaded are considered, so the API is not queried for every aircraft.
        /// </summary>
        /// <param name="aircraft">Aircraft to search.</param>
        /// <param name="peekRoute">Returns the already downloaded route of a callsign, or null.</param>
        /// <param name="query">Query, words separated by spaces.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <returns>Matching aircraft sorted by flight.</returns>
        public static List<Aircraft> Search(IReadOnlyList<Aircraft> aircraft, Func<string?, FlightRoute?> peekRoute, string query, int maxResults)
        {
            string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<Aircraft> result = new List<Aircraft>();

            if (terms.Length == 0)
            {
                return result;
            }

            foreach (Aircraft item in aircraft)
            {
                string haystack = BuildSearchText(item, peekRoute(item.Callsign));
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
        /// <param name="formatAltitude">Formats an altitude given in feet.</param>
        /// <returns>Description built from the available data.</returns>
        public static string Describe(Aircraft item, FlightRoute? route, Func<double, string> formatAltitude)
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
                parts.Add(formatAltitude(item.AltitudeFeet.Value));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Searches airports and aircraft together. Rows are ordered by how well they match the query, and when two rows match equally an airport comes before an aircraft. At least <paramref name="reservedForAircraft"/> rows are left for aircraft when that many aircraft match, so that a short list is not filled with airports only. With an empty query the favorite airports and the nearest airports come first, then the aircraft.
        /// </summary>
        /// <param name="aircraft">Aircraft to search.</param>
        /// <param name="airports">Airport data.</param>
        /// <param name="peekRoute">Returns the already downloaded route of a callsign, or null.</param>
        /// <param name="favoriteCodes">ICAO codes of the favorite airports, in the order of the favorites list.</param>
        /// <param name="includeHeliports">Whether heliports are searched too.</param>
        /// <param name="query">Query, words separated by spaces.</param>
        /// <param name="maxResults">Maximum number of rows.</param>
        /// <param name="reservedForAircraft">Number of rows kept for aircraft.</param>
        /// <param name="nearLatitude">Latitude of the point whose neighborhood is listed for an empty query.</param>
        /// <param name="nearLongitude">Longitude of the point whose neighborhood is listed for an empty query.</param>
        /// <returns>Rows of the result list.</returns>
        public static List<SearchResult> SearchAll(IReadOnlyList<Aircraft> aircraft, AirportDatabase airports, Func<string?, FlightRoute?> peekRoute, IReadOnlyList<string> favoriteCodes, bool includeHeliports, string query, int maxResults, int reservedForAircraft, double nearLatitude, double nearLongitude)
        {
            List<string> terms = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(AirportDatabase.Fold)
                .ToList();
            List<SearchResult> airportRows = new List<SearchResult>();
            List<SearchResult> aircraftRows = new List<SearchResult>();

            if (terms.Count == 0)
            {
                foreach (string code in favoriteCodes)
                {
                    Airport? favorite = airports.FindByIcao(code);

                    if (favorite != null)
                    {
                        airportRows.Add(new SearchResult(favorite, null, 0));
                    }
                }

                foreach (Airport airport in airports.GetNearest(nearLatitude, nearLongitude, NearestAirportsForEmptyQuery, AirportType.Medium))
                {
                    if (!airportRows.Any(row => row.Airport == airport))
                    {
                        airportRows.Add(new SearchResult(airport, null, 0));
                    }
                }

                foreach (Aircraft item in ListAll(aircraft, maxResults))
                {
                    aircraftRows.Add(new SearchResult(null, item, 0));
                }
            }
            else
            {
                foreach ((Airport Airport, int Quality) match in airports.Search(terms, maxResults, includeHeliports, airport => favoriteCodes.Contains(airport.Icao, StringComparer.OrdinalIgnoreCase)))
                {
                    airportRows.Add(new SearchResult(match.Airport, null, match.Quality));
                }

                foreach (Aircraft item in aircraft)
                {
                    int quality = GetAircraftMatchQuality(item, peekRoute(item.Callsign), terms);

                    if (quality > 0)
                    {
                        aircraftRows.Add(new SearchResult(null, item, quality));
                    }
                }

                aircraftRows = aircraftRows
                    .OrderByDescending(item => item.Quality)
                    .ThenBy(item => item.Aircraft!.Callsign ?? item.Aircraft.Hex, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            List<SearchResult> ordered = airportRows
                .Concat(aircraftRows)
                .OrderByDescending(item => item.Quality)
                .ThenBy(item => item.Airport == null ? 1 : 0)
                .ToList();

            List<SearchResult> taken = ordered.Take(maxResults).ToList();
            int reserved = Math.Min(reservedForAircraft, aircraftRows.Count);
            int missing = reserved - taken.Count(item => item.Aircraft != null);

            while (missing > 0)
            {
                int lastAirport = taken.FindLastIndex(item => item.Airport != null);

                if (lastAirport < 0)
                {
                    break;
                }

                taken.RemoveAt(lastAirport);
                taken.Add(aircraftRows.First(item => !taken.Contains(item)));
                missing--;
            }

            return taken
                .OrderByDescending(item => item.Quality)
                .ThenBy(item => item.Airport == null ? 1 : 0)
                .ToList();
        }

        /// <summary>
        /// Returns the title of an airport row: the code and the name.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="isFavorite">Whether the airport is a favorite.</param>
        /// <returns>Text such as "WAW · Warsaw Chopin Airport", with a star in front for a favorite.</returns>
        public static string GetAirportTitle(Airport airport, bool isFavorite)
        {
            return (isFavorite ? "★ " : string.Empty) + airport.Code + " · " + airport.Name;
        }

        /// <summary>
        /// Returns the description of an airport row: the place, the size class and the elevation in the unit from the settings.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="formatAltitude">Formats an altitude given in feet.</param>
        /// <returns>Text such as "Warsaw, PL · duże lotnisko · 110 m".</returns>
        public static string DescribeAirport(Airport airport, Func<double, string> formatAltitude)
        {
            List<string> parts = new List<string>();

            if (airport.Municipality.Length > 0)
            {
                parts.Add(airport.Municipality + ", " + airport.Country);
            }
            else
            {
                parts.Add(airport.Country);
            }

            parts.Add(airport.Type == AirportType.Large ? "duże lotnisko" : airport.Type == AirportType.Medium ? "średnie lotnisko" : airport.Type == AirportType.Heliport ? "helipad" : "małe lotnisko");

            if (airport.IsMilitary)
            {
                parts.Add("wojskowe");
            }

            if (airport.ElevationFeet.HasValue)
            {
                parts.Add(formatAltitude(airport.ElevationFeet.Value));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Rates how well an aircraft matches the words of a query: the weakest match among the words, or 0 when some word does not match.
        /// </summary>
        /// <param name="item">Aircraft.</param>
        /// <param name="route">Route, or null when unknown.</param>
        /// <param name="terms">Folded words of the query.</param>
        /// <returns>0 for no match, otherwise 1 to 3.</returns>
        private static int GetAircraftMatchQuality(Aircraft item, FlightRoute? route, IReadOnlyList<string> terms)
        {
            string?[] fields = new string?[]
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
            string[] folded = fields.Where(field => !string.IsNullOrEmpty(field)).Select(field => AirportDatabase.Fold(field!)).ToArray();
            int quality = AirportDatabase.ExactMatch;

            foreach (string term in terms)
            {
                int termQuality = 0;

                foreach (string field in folded)
                {
                    if (field == term)
                    {
                        termQuality = AirportDatabase.ExactMatch;

                        break;
                    }

                    if (field.StartsWith(term, StringComparison.Ordinal) || field.Contains(" " + term, StringComparison.Ordinal))
                    {
                        termQuality = Math.Max(termQuality, AirportDatabase.PrefixMatch);
                    }
                    else if (field.Contains(term, StringComparison.Ordinal))
                    {
                        termQuality = Math.Max(termQuality, AirportDatabase.ContainsMatch);
                    }
                }

                if (termQuality == 0)
                {
                    return 0;
                }

                quality = Math.Min(quality, termQuality);
            }

            return quality;
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

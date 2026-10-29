using System.Globalization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Wyszukiwanie samolotów po locie, rejestracji, typie i trasie oraz opis samolotu do list wyników.
    /// </summary>
    public static class AircraftSearch
    {
        /// <summary>
        /// Zwraca samoloty, których lot, adres ICAO, rejestracja, typ lub trasa zawierają wszystkie słowa zapytania. Uwzględnia tylko trasy już pobrane, aby nie odpytywać API o wszystkie samoloty.
        /// </summary>
        /// <param name="aircraft">Lista samolotów do przeszukania.</param>
        /// <param name="routes">Pamięć tras lotów.</param>
        /// <param name="query">Zapytanie, słowa oddzielone spacjami.</param>
        /// <param name="maxResults">Maksymalna liczba wyników.</param>
        /// <returns>Pasujące samoloty posortowane po locie.</returns>
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
        /// Zwraca wszystkie samoloty posortowane po locie, np. do listy pokazywanej przy pustym zapytaniu.
        /// </summary>
        /// <param name="aircraft">Lista samolotów.</param>
        /// <param name="maxResults">Maksymalna liczba wyników.</param>
        /// <returns>Samoloty posortowane po locie lub adresie ICAO.</returns>
        public static List<Aircraft> ListAll(IReadOnlyList<Aircraft> aircraft, int maxResults)
        {
            return aircraft
                .OrderBy(item => item.Callsign ?? item.Hex, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Zwraca nazwę samolotu: lot, a gdy go brak, adres ICAO.
        /// </summary>
        /// <param name="item">Samolot.</param>
        /// <returns>Nazwa do wyświetlenia.</returns>
        public static string GetName(Aircraft item)
        {
            return string.IsNullOrEmpty(item.Callsign) ? item.Hex.ToUpperInvariant() : item.Callsign;
        }

        /// <summary>
        /// Zwraca linię lotniczą: nazwę z trasy, a gdy jej brak, kod ICAO linii z początku znaku wywoławczego.
        /// </summary>
        /// <param name="item">Samolot.</param>
        /// <param name="route">Trasa lub null, gdy nieznana.</param>
        /// <returns>Nazwa lub kod linii albo pusty tekst, gdy nie da się ustalić.</returns>
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
        /// Zwraca opis samolotu: typ, trasę i wysokość, np. "B738 · WAW→LHR · FL350".
        /// </summary>
        /// <param name="item">Samolot.</param>
        /// <param name="route">Trasa lub null, gdy nieznana.</param>
        /// <returns>Opis z dostępnych danych.</returns>
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
                parts.Add(string.Format(CultureInfo.InvariantCulture, "FL{0:000}", Math.Round(item.AltitudeFeet.Value / 100.0)));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Składa tekst, w którym szukane są słowa zapytania.
        /// </summary>
        /// <param name="item">Samolot.</param>
        /// <param name="route">Trasa lub null, gdy nieznana.</param>
        /// <returns>Połączone pola samolotu i trasy.</returns>
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

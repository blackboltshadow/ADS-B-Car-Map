using System.Globalization;
using AdsbCarMap.Data;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Builds the texts describing the primary selected aircraft. Shared by the phone card and the Android Auto popup.
    /// </summary>
    public static class AircraftCardText
    {
        /// <summary>
        /// Culture used to format the numbers: decimal comma and a space as the thousands separator.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Feet to meters conversion factor.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Knots to kilometers per hour conversion factor.
        /// </summary>
        private const double KilometersPerHourPerKnot = 1.852;

        /// <summary>
        /// Speed in knots below which the time to arrival is not estimated.
        /// </summary>
        private const double MinimumSpeedForEstimateKnots = 80.0;

        /// <summary>
        /// Vertical rate in feet per minute below which the flight is considered level.
        /// </summary>
        private const int LevelFlightThresholdFeetPerMinute = 100;

        /// <summary>
        /// Builds the airline, type and kind line, e.g. "Ryanair · B738 · Samolot odrzutowy".
        /// </summary>
        /// <param name="aircraft">Aircraft.</param>
        /// <param name="route">Flight route or null.</param>
        /// <returns>Joined descriptors, empty when none is known.</returns>
        public static string BuildDescriptors(Aircraft aircraft, FlightRoute? route)
        {
            List<string> descriptors = new List<string>();
            string airline = AircraftSearch.GetAirline(aircraft, route);
            string kindName = AircraftClassifier.GetDisplayName(aircraft.Kind);

            if (!string.IsNullOrEmpty(airline))
            {
                descriptors.Add(airline);
            }

            if (!string.IsNullOrEmpty(aircraft.TypeCode))
            {
                descriptors.Add(aircraft.TypeCode);
            }

            if (!string.IsNullOrEmpty(kindName))
            {
                descriptors.Add(kindName);
            }

            return string.Join(" · ", descriptors);
        }

        /// <summary>
        /// Builds the route line: the origin and destination airports, or the reason why the route is not known.
        /// </summary>
        /// <param name="route">Flight route or null.</param>
        /// <param name="routeState">Route lookup state explaining why the route is missing.</param>
        /// <returns>Text to show.</returns>
        public static string BuildRouteText(FlightRoute? route, RouteLookupState routeState)
        {
            if (route != null)
            {
                return FormatAirport(route.OriginCode, route.OriginCity) + " → " + FormatAirport(route.DestinationCode, route.DestinationCity);
            }

            switch (routeState)
            {
                case RouteLookupState.NotApplicable:
                    return "Trasa: samolot nie nadaje znaku wywoławczego";

                case RouteLookupState.NotFound:
                    return "Trasa: baza adsbdb nie zna tego lotu";

                case RouteLookupState.Failed:
                    return "Trasa: nie udało się pobrać (błąd sieci lub limit zapytań)";

                default:
                    return "Trasa: pobieranie…";
            }
        }

        /// <summary>
        /// Returns the label of the button that forces the route download.
        /// </summary>
        /// <param name="routeState">Route lookup state.</param>
        /// <returns>Button label, or null when the button is not shown.</returns>
        public static string? BuildRouteActionText(RouteLookupState routeState)
        {
            switch (routeState)
            {
                case RouteLookupState.NotApplicable:
                    return null;

                case RouteLookupState.NotFound:
                    return "Sprawdź ponownie";

                case RouteLookupState.Failed:
                    return "Spróbuj ponownie";

                default:
                    return "Pobierz teraz";
            }
        }

        /// <summary>
        /// Computes the flight progress along the route.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <param name="route">Flight route or null.</param>
        /// <param name="fraction">Flown part of the route from 0 to 1.</param>
        /// <param name="text">Progress description: flown part, distance to the destination and estimated time.</param>
        /// <returns>False when the route is unknown or has no coordinates.</returns>
        public static bool TryBuildProgress(SelectedAircraftInfo info, Aircraft aircraft, FlightRoute? route, out float fraction, out string text)
        {
            fraction = 0.0f;
            text = string.Empty;

            if (route == null || !route.HasCoordinates)
            {
                return false;
            }

            double flown = GeoMath.DistanceKilometers(route.OriginLatitude!.Value, route.OriginLongitude!.Value, info.Latitude, info.Longitude);
            double remaining = GeoMath.DistanceKilometers(info.Latitude, info.Longitude, route.DestinationLatitude!.Value, route.DestinationLongitude!.Value);
            double total = flown + remaining;

            fraction = total > 0.0 ? (float)(flown / total) : 0.0f;
            text = string.Format(DisplayCulture, "przeleciano {0:0}%  ·  do celu {1:N0} km", fraction * 100.0, remaining);

            if (!aircraft.OnGround && aircraft.GroundSpeedKnots.HasValue && aircraft.GroundSpeedKnots.Value >= MinimumSpeedForEstimateKnots)
            {
                double hours = remaining / (aircraft.GroundSpeedKnots.Value * KilometersPerHourPerKnot);
                text += "  ·  ok. " + FormatDuration(hours);
            }

            return true;
        }

        /// <summary>
        /// Builds the lines of the left column: altitude, speed, track and data age.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLeftLines(SelectedAircraftInfo info, Aircraft aircraft)
        {
            List<string> lines = new List<string>();

            if (aircraft.OnGround)
            {
                lines.Add("Wysokość: na ziemi");
            }
            else if (aircraft.AltitudeFeet.HasValue)
            {
                lines.Add(string.Format(DisplayCulture, "Wysokość: {0:N0} ft ({1:N0} m)", aircraft.AltitudeFeet.Value, aircraft.AltitudeFeet.Value * MetersPerFoot));
            }
            else
            {
                lines.Add("Wysokość: brak danych");
            }

            lines.Add(aircraft.GroundSpeedKnots.HasValue
                ? string.Format(DisplayCulture, "Prędkość: {0:N0} kt ({1:N0} km/h)", aircraft.GroundSpeedKnots.Value, aircraft.GroundSpeedKnots.Value * KilometersPerHourPerKnot)
                : "Prędkość: brak danych");

            lines.Add(aircraft.TrackDegrees.HasValue
                ? string.Format(DisplayCulture, "Kurs: {0:0}° {1}", aircraft.TrackDegrees.Value, GeoMath.CompassPoint(aircraft.TrackDegrees.Value))
                : "Kurs: brak danych");

            lines.Add(string.Format(DisplayCulture, "Dane sprzed {0:0} s", info.AgeSeconds + aircraft.SecondsSincePosition));

            return lines;
        }

        /// <summary>
        /// Builds the lines of the right column: vertical rate, distance and direction from the user, and squawk.
        /// </summary>
        /// <param name="info">Data of the selected aircraft and the user position.</param>
        /// <param name="aircraft">Aircraft.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildRightLines(SelectedAircraftInfo info, Aircraft aircraft)
        {
            List<string> lines = new List<string>();

            if (aircraft.OnGround)
            {
                lines.Add("Wznoszenie: –");
            }
            else if (aircraft.VerticalRateFeetPerMinute.HasValue)
            {
                int rate = aircraft.VerticalRateFeetPerMinute.Value;

                lines.Add(Math.Abs(rate) < LevelFlightThresholdFeetPerMinute
                    ? "Wznoszenie: lot poziomy"
                    : string.Format(DisplayCulture, "{0}: {1:N0} ft/min", rate > 0 ? "Wznoszenie" : "Opadanie", Math.Abs(rate)));
            }
            else
            {
                lines.Add("Wznoszenie: brak danych");
            }

            if (info.HasUser)
            {
                double distance = GeoMath.DistanceKilometers(info.UserLatitude, info.UserLongitude, info.Latitude, info.Longitude);
                double bearing = GeoMath.BearingDegrees(info.UserLatitude, info.UserLongitude, info.Latitude, info.Longitude);

                lines.Add(string.Format(DisplayCulture, "Od Ciebie: {0:N1} km, {1} ({2:0}°)", distance, GeoMath.CompassPoint(bearing), bearing));
            }
            else
            {
                lines.Add("Od Ciebie: brak pozycji GPS");
            }

            lines.Add(string.IsNullOrEmpty(aircraft.Squawk) ? "Squawk: brak danych" : "Squawk: " + aircraft.Squawk);

            return lines;
        }

        /// <summary>
        /// Formats the title: the flight name and, when several aircraft are selected, the number of the others.
        /// </summary>
        /// <param name="info">Data of the selected aircraft.</param>
        /// <returns>Title text.</returns>
        public static string BuildTitle(SelectedAircraftInfo info)
        {
            string extra = info.SelectedCount > 1 ? string.Format(DisplayCulture, "  (+{0})", info.SelectedCount - 1) : string.Empty;

            return info.Aircraft == null ? info.Hex.ToUpperInvariant() + extra : AircraftSearch.GetName(info.Aircraft) + extra;
        }

        /// <summary>
        /// Formats an airport as its code and city, e.g. "WAW Warszawa".
        /// </summary>
        /// <param name="code">Airport code.</param>
        /// <param name="city">City or null.</param>
        /// <returns>Text to show.</returns>
        private static string FormatAirport(string code, string? city)
        {
            return string.IsNullOrEmpty(city) ? code : code + " " + city;
        }

        /// <summary>
        /// Formats a duration in hours as "1 h 12 min" or "35 min".
        /// </summary>
        /// <param name="hours">Duration in hours.</param>
        /// <returns>Text to show.</returns>
        private static string FormatDuration(double hours)
        {
            int totalMinutes = (int)Math.Round(hours * 60.0);

            if (totalMinutes < 60)
            {
                return string.Format(DisplayCulture, "{0} min", Math.Max(1, totalMinutes));
            }

            return string.Format(DisplayCulture, "{0} h {1:00} min", totalMinutes / 60, totalMinutes % 60);
        }
    }
}

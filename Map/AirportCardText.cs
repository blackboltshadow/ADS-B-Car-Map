using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Builds the texts describing the selected airport. Shared by the phone card and the Android Auto popup.
    /// </summary>
    public static partial class AirportCardText
    {
        /// <summary>
        /// Culture used for the digit grouping of the displayed numbers.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Maximum number of runways listed.
        /// </summary>
        private const int MaxRunways = 4;

        /// <summary>
        /// Feet to meters conversion factor.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Returns the title of the card: the code and the name.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Title text.</returns>
        public static string BuildTitle(Airport airport)
        {
            return airport.Code + "  ·  " + airport.Name;
        }

        /// <summary>
        /// Returns the line under the title: the size class, and whether the airport is military.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <returns>Descriptor text.</returns>
        public static string BuildDescriptor(Airport airport)
        {
            string type = airport.Type == AirportType.Large ? "Duże lotnisko" : airport.Type == AirportType.Medium ? "Średnie lotnisko" : airport.Type == AirportType.Heliport ? "Helipad" : "Małe lotnisko";

            return airport.IsMilitary ? type + " · wojskowe" : type;
        }

        /// <summary>
        /// Returns the detail lines of the card: codes, place, elevation, distance from the user and runways.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="hasUserLocation">Whether the user position is known.</param>
        /// <param name="userLatitude">Latitude of the user.</param>
        /// <param name="userLongitude">Longitude of the user.</param>
        /// <param name="unit">Altitude unit chosen by the user.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLines(Airport airport, IReadOnlyList<Runway> runways, bool hasUserLocation, double userLatitude, double userLongitude, AltitudeUnit unit)
        {
            List<string> lines = new List<string>();
            List<string> codes = new List<string>();

            if (airport.Iata.Length > 0)
            {
                codes.Add("IATA " + airport.Iata);
            }

            codes.Add("ICAO " + airport.Icao);
            lines.Add(string.Join("  ·  ", codes));

            string place = string.IsNullOrEmpty(airport.Municipality) ? airport.Country : airport.Municipality + ", " + airport.Country;

            if (place.Length > 0)
            {
                lines.Add(place);
            }

            if (airport.ElevationFeet.HasValue)
            {
                lines.Add("Wysokość: " + AltitudeFormatter.FormatWithEquivalent(airport.ElevationFeet.Value, unit));
            }

            if (hasUserLocation)
            {
                double kilometers = GeoMath.DistanceKilometers(userLatitude, userLongitude, airport.Latitude, airport.Longitude);
                double bearing = GeoMath.BearingDegrees(userLatitude, userLongitude, airport.Latitude, airport.Longitude);

                lines.Add(string.Format(DisplayCulture, "Odległość: {0:N0} km, kierunek {1}", kilometers, GeoMath.CompassPoint(bearing)));
            }

            string runwayText = BuildRunways(runways, unit);

            if (runwayText.Length > 0)
            {
                lines.Add(runwayText);
            }

            return lines;
        }

        /// <summary>
        /// Lists the longest runways with their designators and lengths.
        /// </summary>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="unit">Altitude unit chosen by the user; feet give lengths in feet, other units in meters.</param>
        /// <returns>Text such as "Pasy: 15/33 · 3 690 m, 11/29 · 2 800 m", or an empty string when there are none.</returns>
        private static string BuildRunways(IReadOnlyList<Runway> runways, AltitudeUnit unit)
        {
            if (runways.Count == 0)
            {
                return string.Empty;
            }

            bool feet = unit == AltitudeUnit.Feet;
            List<string> items = new List<string>();

            foreach (Runway runway in runways.OrderByDescending(item => item.LengthFeet).Take(MaxRunways))
            {
                string name = runway.LowEndName + "/" + runway.HighEndName;

                if (runway.LengthFeet <= 0)
                {
                    items.Add(name);
                }
                else
                {
                    string length = feet
                        ? string.Format(DisplayCulture, "{0:N0} ft", runway.LengthFeet)
                        : string.Format(DisplayCulture, "{0:N0} m", runway.LengthFeet * MetersPerFoot);

                    items.Add(name + " · " + length);
                }
            }

            return "Pasy: " + string.Join(", ", items);
        }
    }
}

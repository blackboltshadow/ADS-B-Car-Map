using AdsbCarMap.Localization;
using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// One row of the frequency table of the airport card.
    /// </summary>
    /// <param name="Type">Service type as OurAirports gives it, for example "TWR".</param>
    /// <param name="Megahertz">Frequency in megahertz with three decimal places, for example "118.300".</param>
    /// <param name="Description">Short explanation of the service type followed by the description from OurAirports; empty when there is neither.</param>
    public sealed record FrequencyRow(string Type, string Megahertz, string Description);

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
        /// Number of frequencies named in the header of the closed frequency panel.
        /// </summary>
        private const int MaxFrequenciesInHeader = 2;

        /// <summary>
        /// Service types listed first, in this order; other types follow alphabetically.
        /// </summary>
        private static readonly string[] FrequencyTypeOrder = new string[] { "ATIS", "TWR", "GND", "APP", "DEP", "CTAF", "AFIS", "UNIC", "A/D", "A/G" };

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
            string type = airport.Type == AirportType.Large ? Loc.T("Duże lotnisko") : airport.Type == AirportType.Medium ? Loc.T("Średnie lotnisko") : airport.Type == AirportType.Heliport ? Loc.T("Helipad") : Loc.T("Małe lotnisko");

            return airport.IsMilitary ? type + Loc.T(" · wojskowe") : type;
        }

        /// <summary>
        /// Returns the detail lines of the card: codes, place, elevation, distance from the user and runways.
        /// </summary>
        /// <param name="airport">Airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="hasUserLocation">Whether the user position is known.</param>
        /// <param name="userLatitude">Latitude of the user.</param>
        /// <param name="userLongitude">Longitude of the user.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLines(Airport airport, IReadOnlyList<Runway> runways, bool hasUserLocation, double userLatitude, double userLongitude, DisplayUnits units)
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
                lines.Add(Loc.T("Wysokość: ") + AltitudeFormatter.Format(airport.ElevationFeet.Value, units.Altitude));
            }

            if (hasUserLocation)
            {
                double kilometers = GeoMath.DistanceKilometers(userLatitude, userLongitude, airport.Latitude, airport.Longitude);
                double bearing = GeoMath.BearingDegrees(userLatitude, userLongitude, airport.Latitude, airport.Longitude);

                lines.Add(string.Format(DisplayCulture, Loc.T("Odległość: {0}, kierunek {1}"), UnitFormatter.FormatDistance(kilometers, units.Distance, 0), GeoMath.CompassPoint(bearing)));
            }

            string runwayText = BuildRunways(runways, units.Altitude);

            if (runwayText.Length > 0)
            {
                lines.Add(runwayText);
            }

            return lines;
        }

        /// <summary>
        /// Returns the header of the frequency panel.
        /// </summary>
        /// <param name="frequencies">Radio frequencies of the airport, or null.</param>
        /// <param name="expanded">Whether the panel is open; the closed header also names the first frequencies.</param>
        /// <returns>Text such as "Częstotliwości · TWR 118.300, ATIS 120.455 (+4)" when closed or "Częstotliwości (MHz) · 6" when open; empty when there are no frequencies.</returns>
        public static string BuildFrequencyHeader(IReadOnlyList<AirportFrequency>? frequencies, bool expanded)
        {
            List<AirportFrequency> sorted = SortFrequencies(frequencies);

            if (sorted.Count == 0)
            {
                return string.Empty;
            }

            if (expanded)
            {
                return Loc.T("Częstotliwości (MHz)") + " · " + sorted.Count.ToString(CultureInfo.InvariantCulture);
            }

            string first = string.Join(", ", sorted.Take(MaxFrequenciesInHeader).Select(FormatFrequencyShort));
            string rest = sorted.Count > MaxFrequenciesInHeader ? " (+" + (sorted.Count - MaxFrequenciesInHeader).ToString(CultureInfo.InvariantCulture) + ")" : string.Empty;

            return Loc.T("Częstotliwości") + " · " + first + rest;
        }

        /// <summary>
        /// Lists all frequencies, one per line: the main service types first in the order of <see cref="FrequencyTypeOrder"/>, then the other types alphabetically, each type by frequency. Repeated type and frequency pairs are listed once.
        /// </summary>
        /// <param name="frequencies">Radio frequencies of the airport, or null.</param>
        /// <returns>Lines such as "TWR 118.305 · wieża · Okęcie TOWER", or an empty list when there are none.</returns>
        public static List<string> BuildFrequencyLines(IReadOnlyList<AirportFrequency>? frequencies)
        {
            List<string> lines = new List<string>();

            foreach (FrequencyRow row in BuildFrequencyRows(frequencies))
            {
                string line = row.Type + " " + row.Megahertz;

                lines.Add(row.Description.Length > 0 ? line + " · " + row.Description : line);
            }

            return lines;
        }

        /// <summary>
        /// Returns the frequencies as rows of a table, in the same order as <see cref="BuildFrequencyLines"/>.
        /// </summary>
        /// <param name="frequencies">Radio frequencies of the airport, or null.</param>
        /// <returns>The rows; empty when there are no frequencies.</returns>
        public static List<FrequencyRow> BuildFrequencyRows(IReadOnlyList<AirportFrequency>? frequencies)
        {
            List<FrequencyRow> rows = new List<FrequencyRow>();

            foreach (AirportFrequency frequency in SortFrequencies(frequencies))
            {
                List<string> parts = new List<string>();
                string meaning = GetFrequencyMeaning(frequency.Type.ToUpperInvariant());

                if (meaning.Length > 0)
                {
                    parts.Add(meaning);
                }

                if (frequency.Description.Length > 0)
                {
                    parts.Add(frequency.Description);
                }

                rows.Add(new FrequencyRow(frequency.Type, frequency.Megahertz.ToString("0.000", CultureInfo.InvariantCulture), string.Join(" · ", parts)));
            }

            return rows;
        }

        /// <summary>
        /// Returns the headings of the columns of the frequency table.
        /// </summary>
        /// <returns>The type, the frequency in megahertz and the description.</returns>
        public static List<string> BuildFrequencyColumns()
        {
            return new List<string> { Loc.T("TYP"), "MHz", Loc.T("OPIS") };
        }

        /// <summary>
        /// Sorts the frequencies for listing: the main service types first in the order of <see cref="FrequencyTypeOrder"/>, then the other types alphabetically, each type by frequency. Repeated type and frequency pairs are left once.
        /// </summary>
        /// <param name="frequencies">Radio frequencies of the airport, or null.</param>
        /// <returns>The sorted list; empty when there are no frequencies.</returns>
        private static List<AirportFrequency> SortFrequencies(IReadOnlyList<AirportFrequency>? frequencies)
        {
            if (frequencies == null)
            {
                return new List<AirportFrequency>();
            }

            return frequencies
                .DistinctBy(item => (item.Type, item.Megahertz))
                .OrderBy(item => GetFrequencyRank(item.Type))
                .ThenBy(item => item.Type, StringComparer.Ordinal)
                .ThenBy(item => item.Megahertz)
                .ToList();
        }

        /// <summary>
        /// Formats a frequency without its description.
        /// </summary>
        /// <param name="frequency">The frequency.</param>
        /// <returns>Text such as "TWR 118.300".</returns>
        private static string FormatFrequencyShort(AirportFrequency frequency)
        {
            return frequency.Type + " " + frequency.Megahertz.ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns a short explanation of a frequency service type.
        /// </summary>
        /// <param name="type">Service type in upper case, for example TWR.</param>
        /// <returns>Explanation, or an empty string when the type is not known.</returns>
        private static string GetFrequencyMeaning(string type)
        {
            switch (type)
            {
                case "ATIS":
                case "D-ATIS":
                    return Loc.T("Automatyczna informacja o lotnisku");
                case "TWR":
                case "TOWER":
                    return Loc.T("wieża");
                case "GND":
                case "GROUND":
                case "GRD":
                case "GRN":
                    return Loc.T("kontrola ruchu naziemnego");
                case "APP":
                case "APPROACH":
                case "APPR":
                case "APR":
                case "APCH":
                    return Loc.T("kontrola zbliżania");
                case "DEP":
                    return Loc.T("kontrola odlotów");
                case "APP/DEP":
                    return Loc.T("zbliżanie i odloty");
                case "CTAF":
                    return Loc.T("ruch na lotnisku bez wieży (CTAF)");
                case "UNIC":
                case "UNICOM":
                    return Loc.T("informacje lotniskowe (UNICOM)");
                case "MULT":
                case "MULTICOM":
                    return Loc.T("wspólna częstotliwość (MULTICOM)");
                case "A/D":
                case "AD":
                case "ATF":
                    return Loc.T("ruch lotniskowy");
                case "A/G":
                    return Loc.T("łączność powietrze–ziemia");
                case "AFIS":
                    return Loc.T("służba informacji powietrznej lotniska (AFIS)");
                case "AWOS":
                case "ASOS":
                case "AWIS":
                case "AWIB":
                    return Loc.T("automatyczna stacja pogodowa");
                case "CLD":
                case "DEL":
                case "DELIVERY":
                case "CLNC":
                case "CLR":
                case "CLRN":
                case "CLRD":
                case "DLV":
                    return Loc.T("zezwolenia na lot (delivery)");
                case "CNTR":
                case "CTR":
                case "CENTER":
                case "ACC":
                case "ARTC":
                case "AREA":
                    return Loc.T("kontrola obszaru");
                case "INFO":
                case "INF":
                case "FIS":
                case "FIC":
                case "INFORMATION":
                    return Loc.T("służba informacji powietrznej");
                case "FSS":
                    return Loc.T("stacja obsługi lotów (FSS)");
                case "RDO":
                case "RADIO":
                    return Loc.T("radio lotniskowe");
                case "RDR":
                case "RADAR":
                    return Loc.T("radar");
                case "OPS":
                    return Loc.T("operacje lotniskowe");
                case "RCO":
                    return Loc.T("zdalne łącze radiowe (RCO)");
                case "VOLMET":
                    return Loc.T("pogoda dla załóg w locie (VOLMET)");
                case "EMR":
                case "EMERG":
                case "EMG":
                    return Loc.T("częstotliwość awaryjna");
                case "GCA":
                    return Loc.T("podejście kierowane z ziemi (GCA)");
                case "MIL":
                case "MILITARY":
                    return Loc.T("wojskowa");
                case "SAFETYCOM":
                case "SAFETY":
                    return Loc.T("częstotliwość bezpieczeństwa (SafetyCom)");
                case "ARR":
                    return Loc.T("kontrola przylotów");
                case "RMP":
                case "APRON":
                case "APN":
                    return Loc.T("płyta postojowa");
                case "TIBA":
                    return Loc.T("informacja o ruchu w pobliżu (TIBA)");
                case "VFR":
                    return Loc.T("ruch VFR");
                case "MISC":
                    return Loc.T("inna");
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Returns the position of a service type in the listing order; types outside the order go last.
        /// </summary>
        /// <param name="type">Service type.</param>
        /// <returns>Rank, smaller is listed earlier.</returns>
        private static int GetFrequencyRank(string type)
        {
            int index = Array.IndexOf(FrequencyTypeOrder, type);

            return index < 0 ? FrequencyTypeOrder.Length : index;
        }

        /// <summary>
        /// Lists the longest runways with their designators and lengths.
        /// </summary>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="unit">Altitude unit chosen by the user; metric gives lengths in meters, the other units in feet.</param>
        /// <returns>Text such as "Pasy: 15/33 · 3 690 m, 11/29 · 2 800 m", or an empty string when there are none.</returns>
        private static string BuildRunways(IReadOnlyList<Runway> runways, AltitudeUnit unit)
        {
            if (runways.Count == 0)
            {
                return string.Empty;
            }

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
                    items.Add(name + " · " + AltitudeFormatter.FormatLength(runway.LengthFeet, unit));
                }
            }

            return Loc.T("Pasy: ") + string.Join(", ", items);
        }
    }
}

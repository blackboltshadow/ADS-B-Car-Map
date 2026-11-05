using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Builds the texts and colors describing the weather report of an airport. Shared by the phone card and the Android Auto popup.
    /// </summary>
    public static class AirportWeatherText
    {
        /// <summary>
        /// Age of the observation after which the report is marked as out of date.
        /// </summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(90);

        /// <summary>
        /// Visibility in kilometers from which the report is shown as "10 km or more", because the source cuts the longer values.
        /// </summary>
        private const double VisibilityCapKilometers = 10.0;

        /// <summary>
        /// Visibility in meters above which a lower bound is shown as the cap.
        /// </summary>
        private const double VisibilityCapThresholdMeters = 9600.0;

        /// <summary>
        /// Culture used for the digit grouping of the displayed numbers.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Returns the header of the weather panel.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the report is being downloaded.</param>
        /// <param name="expanded">Whether the panel is open; the closed header also holds the wind and the visibility.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <returns>Text such as "Pogoda · VFR · 270° 12 kt · 9,0 km · 14 min temu"; when the report comes from the nearest station, the station and its distance follow the title.</returns>
        public static string BuildHeader(WeatherEntry? entry, bool isLoading, bool expanded, DisplayUnits units, DateTime utcNow)
        {
            string title = Loc.T("Pogoda");

            if (entry == null)
            {
                return title + " · " + (isLoading ? Loc.T("pobieranie…") : Loc.T("brak danych"));
            }

            if (entry.Report == null)
            {
                return title + " · " + Loc.T("brak raportu METAR dla tego lotniska");
            }

            Metar metar = entry.Report;
            TimeSpan age = utcNow - metar.ObservedAt.UtcDateTime;
            List<string> parts = new List<string> { title };

            if (entry.DistanceKilometers.HasValue)
            {
                parts.Add(Loc.F("stacja {0}, {1}", metar.IcaoCode, UnitFormatter.FormatDistance(entry.DistanceKilometers.Value, units.Distance, 0)));
            }

            if (!string.IsNullOrEmpty(metar.FlightCategory))
            {
                parts.Add(metar.FlightCategory);
            }

            if (!expanded)
            {
                string wind = FormatWindShort(metar, units);

                if (wind.Length > 0)
                {
                    parts.Add(wind);
                }

                string visibility = FormatVisibility(metar, units);

                if (visibility.Length > 0)
                {
                    parts.Add(visibility);
                }
            }

            parts.Add(FormatAge(age));

            if (age > StaleAfter)
            {
                parts.Add(Loc.T("nieaktualne"));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Returns the lines of the open panel, ending with the original text of the report.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="stationDistanceKilometers">Distance from the airport to the station when the report comes from the nearest station, or null for the report of the airport itself.</param>
        /// <param name="runways">Runways of the airport, which give a line with the runway that the wind favors; null for none. The line is left out for a report from the nearest station, because its wind is not the wind at the airport.</param>
        /// <returns>Text lines.</returns>
        public static List<string> BuildLines(Metar metar, DisplayUnits units, double? stationDistanceKilometers = null, IReadOnlyList<Runway>? runways = null)
        {
            List<string> lines = new List<string>();
            List<string> temperature = new List<string>();

            if (stationDistanceKilometers.HasValue)
            {
                lines.Add(Loc.F("Najbliższa stacja: {0}, {1} od lotniska (to lotnisko nie ma własnego METAR)", metar.IcaoCode, UnitFormatter.FormatDistance(stationDistanceKilometers.Value, units.Distance, 0)));
            }

            lines.AddRange(BuildSummaryLines(metar, units));

            string runwayLine = stationDistanceKilometers.HasValue ? string.Empty : BuildRunwayLine(metar, runways, units);

            if (runwayLine.Length > 0)
            {
                lines.Add(runwayLine);
            }

            if (metar.WeatherText != null)
            {
                lines.Add(Loc.T("Zjawiska: ") + metar.WeatherText);
            }

            if (metar.TemperatureCelsius.HasValue)
            {
                string text = Loc.T("Temperatura: ") + FormatTemperature(metar.TemperatureCelsius.Value);

                if (metar.DewPointCelsius.HasValue)
                {
                    text += Loc.T(", punkt rosy ") + FormatTemperature(metar.DewPointCelsius.Value);
                }

                temperature.Add(text);
            }

            if (metar.PressureHectopascals.HasValue)
            {
                temperature.Add("QNH " + UnitFormatter.FormatPressure(metar.PressureHectopascals.Value, units.Pressure));
            }

            if (temperature.Count > 0)
            {
                lines.Add(string.Join(" · ", temperature));
            }

            lines.Add(metar.RawText);

            return lines;
        }

        /// <summary>
        /// Returns the short lines: the wind, the visibility and the clouds. The popup of Android Auto shows these.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text lines; those the report has no data for are left out.</returns>
        public static List<string> BuildSummaryLines(Metar metar, DisplayUnits units)
        {
            List<string> lines = new List<string>();
            string wind = FormatWind(metar, units);

            if (wind.Length > 0)
            {
                lines.Add(Loc.T("Wiatr: ") + wind);
            }

            string visibility = FormatVisibility(metar, units);

            if (visibility.Length > 0)
            {
                lines.Add(Loc.T("Widzialność: ") + visibility);
            }

            lines.Add(Loc.T("Chmury: ") + FormatClouds(metar, units));

            return lines;
        }

        /// <summary>
        /// Returns the lines of the popup: the header and the short lines, or only the header when there is no report.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the weather is being downloaded.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <param name="runways">Runways of the airport for the line with the runway that the wind favors, or null for none.</param>
        /// <returns>Text lines; the first one is the header.</returns>
        public static List<string> BuildPopupLines(WeatherEntry? entry, bool isLoading, DisplayUnits units, DateTime utcNow, IReadOnlyList<Runway>? runways = null)
        {
            List<string> lines = new List<string> { BuildHeader(entry, isLoading, true, units, utcNow) };

            if (entry?.Report != null)
            {
                lines.AddRange(BuildSummaryLines(entry.Report, units));

                string runwayLine = entry.DistanceKilometers.HasValue ? string.Empty : BuildRunwayLine(entry.Report, runways, units);

                if (runwayLine.Length > 0)
                {
                    lines.Add(runwayLine);
                }
            }

            return lines;
        }

        /// <summary>
        /// Returns all the lines of the weather as one block of text: the open header and, when there is a report, everything that the open panel shows. Used where there are no panels, such as the dialog of a favorite airport.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the weather is being downloaded.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <param name="runways">Runways of the airport for the line with the runway that the wind favors, or null for none.</param>
        /// <returns>Text lines; the first one is the header.</returns>
        public static List<string> BuildTextLines(WeatherEntry? entry, bool isLoading, DisplayUnits units, DateTime utcNow, IReadOnlyList<Runway>? runways = null)
        {
            List<string> lines = new List<string> { BuildHeader(entry, isLoading, true, units, utcNow) };

            if (entry?.Report != null)
            {
                lines.AddRange(BuildLines(entry.Report, units, entry.DistanceKilometers, runways));
            }

            return lines;
        }

        /// <summary>
        /// Returns the line about the runway that the wind favors.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="runways">Runways of the airport, or null.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "Pas wg wiatru: 27 · wiatr czołowy 12 kt, boczny 3 kt z prawej"; empty when there are no runways or the wind is too weak, variable or not known.</returns>
        public static string BuildRunwayLine(Metar metar, IReadOnlyList<Runway>? runways, DisplayUnits units)
        {
            if (runways == null)
            {
                return string.Empty;
            }

            RunwayWindResult? result = RunwayWind.Choose(runways, metar.WindDirectionDegrees, metar.IsWindVariable, metar.WindSpeedKnots);

            return result == null ? string.Empty : BuildRunwayLine(result, units);
        }

        /// <summary>
        /// Returns the line about a runway end that the wind favors.
        /// </summary>
        /// <param name="result">The favored end.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "Pas wg wiatru: 27 · wiatr czołowy 12 kt, boczny 3 kt z prawej"; the crosswind is left out when it is less than a knot.</returns>
        public static string BuildRunwayLine(RunwayWindResult result, DisplayUnits units)
        {
            string line = Loc.F("Pas wg wiatru: {0} · wiatr czołowy {1}", result.EndName, UnitFormatter.FormatSpeed(Math.Max(0.0, result.HeadwindKnots), units.Speed));

            if (Math.Abs(result.CrosswindKnots) >= 1.0)
            {
                line += Loc.F(", boczny {0} {1}", UnitFormatter.FormatSpeed(Math.Abs(result.CrosswindKnots), units.Speed), result.CrosswindKnots > 0.0 ? Loc.T("z prawej") : Loc.T("z lewej"));
            }

            return line;
        }

        /// <summary>
        /// Returns the color of a flight category.
        /// </summary>
        /// <param name="category">VFR, MVFR, IFR or LIFR, in any letter case.</param>
        /// <returns>Color as ARGB, or null when the category is missing or not known.</returns>
        public static int? GetCategoryColor(string? category)
        {
            switch (category?.Trim().ToUpperInvariant())
            {
                case "VFR":
                    return unchecked((int)0xFF2ECC71);
                case "MVFR":
                    return unchecked((int)0xFF4287F5);
                case "IFR":
                    return unchecked((int)0xFFE74C3C);
                case "LIFR":
                    return unchecked((int)0xFFBA55D3);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Formats the time that has passed since an observation.
        /// </summary>
        /// <param name="age">Time since the observation; a negative time counts as zero.</param>
        /// <returns>Text such as "przed chwilą", "14 min temu", "2 h 5 min temu" or "ponad dobę temu".</returns>
        public static string FormatAge(TimeSpan age)
        {
            if (age < TimeSpan.FromMinutes(1))
            {
                return Loc.T("przed chwilą");
            }

            if (age >= TimeSpan.FromHours(24))
            {
                return Loc.T("ponad dobę temu");
            }

            int hours = (int)age.TotalHours;
            int minutes = age.Minutes;

            if (hours == 0)
            {
                return Loc.F("{0} min temu", minutes);
            }

            return minutes == 0 ? Loc.F("{0} h temu", hours) : Loc.F("{0} h {1} min temu", hours, minutes);
        }

        /// <summary>
        /// Formats the wind of a report with the gusts.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "270° 12 kt, w porywach 22 kt", "zmienny 3 kt" or "cisza"; empty when the report has no wind.</returns>
        private static string FormatWind(Metar metar, DisplayUnits units)
        {
            return FormatWind(metar.WindDirectionDegrees, metar.IsWindVariable, metar.WindSpeedKnots, metar.WindGustKnots, units);
        }

        /// <summary>
        /// Formats the direction and the speed of the wind of a report.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "270° 12 kt", "zmienny 3 kt" or "cisza"; empty when the report has no wind.</returns>
        private static string FormatWindShort(Metar metar, DisplayUnits units)
        {
            return FormatWindShort(metar.WindDirectionDegrees, metar.IsWindVariable, metar.WindSpeedKnots, units);
        }

        /// <summary>
        /// Formats the visibility of a report.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "9,0 km", "10 km lub więcej" or "co najmniej 4,8 km"; empty when the report has no visibility.</returns>
        private static string FormatVisibility(Metar metar, DisplayUnits units)
        {
            return FormatVisibility(metar.VisibilityMeters, metar.IsVisibilityLowerBound, units);
        }

        /// <summary>
        /// Formats the cloud layers of a report.
        /// </summary>
        /// <param name="metar">The report.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "BKN 2 500 ft, OVC 4 800 ft", or "brak istotnych" when the sky is clear.</returns>
        private static string FormatClouds(Metar metar, DisplayUnits units)
        {
            return metar.Clouds.Count == 0 ? Loc.T("brak istotnych") : FormatCloudLayers(metar.Clouds, units);
        }

        /// <summary>
        /// Formats the wind with the gusts.
        /// </summary>
        /// <param name="directionDegrees">Direction the wind blows from, or null.</param>
        /// <param name="isVariable">Whether the direction is variable.</param>
        /// <param name="speedKnots">Mean speed in knots, or null.</param>
        /// <param name="gustKnots">Gust speed in knots, or null.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "270° 12 kt, w porywach 22 kt", "zmienny 3 kt" or "cisza"; empty when there is no speed.</returns>
        public static string FormatWind(int? directionDegrees, bool isVariable, int? speedKnots, int? gustKnots, DisplayUnits units)
        {
            string wind = FormatWindShort(directionDegrees, isVariable, speedKnots, units);

            if (wind.Length > 0 && gustKnots.HasValue && gustKnots > (speedKnots ?? 0))
            {
                wind += Loc.T(", w porywach ") + UnitFormatter.FormatSpeed(gustKnots.Value, units.Speed);
            }

            return wind;
        }

        /// <summary>
        /// Formats the direction and the speed of the wind.
        /// </summary>
        /// <param name="directionDegrees">Direction the wind blows from, or null.</param>
        /// <param name="isVariable">Whether the direction is variable.</param>
        /// <param name="speedKnots">Mean speed in knots, or null.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "270° 12 kt", "zmienny 3 kt" or "cisza"; empty when there is no speed.</returns>
        public static string FormatWindShort(int? directionDegrees, bool isVariable, int? speedKnots, DisplayUnits units)
        {
            if (!speedKnots.HasValue)
            {
                return string.Empty;
            }

            if (speedKnots.Value == 0)
            {
                return Loc.T("cisza");
            }

            string speed = UnitFormatter.FormatSpeed(speedKnots.Value, units.Speed);

            if (isVariable)
            {
                return Loc.T("zmienny") + " " + speed;
            }

            return directionDegrees.HasValue ? directionDegrees.Value.ToString("000", CultureInfo.InvariantCulture) + "° " + speed : speed;
        }

        /// <summary>
        /// Formats a visibility.
        /// </summary>
        /// <param name="meters">Visibility in meters, or null.</param>
        /// <param name="isLowerBound">Whether the visibility is only a lower bound.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "9,0 km", "10 km lub więcej" or "co najmniej 4,8 km"; empty when there is no visibility.</returns>
        public static string FormatVisibility(double? meters, bool isLowerBound, DisplayUnits units)
        {
            if (!meters.HasValue)
            {
                return string.Empty;
            }

            double kilometers = meters.Value / 1000.0;

            if (isLowerBound)
            {
                return meters.Value >= VisibilityCapThresholdMeters
                    ? UnitFormatter.FormatDistanceCompact(VisibilityCapKilometers, units.Distance) + Loc.T(" lub więcej")
                    : Loc.T("co najmniej ") + UnitFormatter.FormatDistanceCompact(kilometers, units.Distance);
            }

            return UnitFormatter.FormatDistanceCompact(kilometers, units.Distance);
        }

        /// <summary>
        /// Formats cloud layers; the base follows the altitude unit, but flight levels are shown in feet because a base is a height above the airport.
        /// </summary>
        /// <param name="clouds">The layers.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>Text such as "BKN 2 500 ft, OVC 4 800 ft"; empty when there are no layers.</returns>
        public static string FormatCloudLayers(IReadOnlyList<CloudLayer> clouds, DisplayUnits units)
        {
            return string.Join(", ", clouds.Select((CloudLayer layer) => layer.BaseFeet.HasValue ? layer.Cover + " " + AltitudeFormatter.FormatLength(layer.BaseFeet.Value, units.Altitude) : layer.Cover));
        }

        /// <summary>
        /// Formats a temperature.
        /// </summary>
        /// <param name="celsius">Temperature in degrees Celsius.</param>
        /// <returns>Text such as "8 °C" or "-3,5 °C".</returns>
        private static string FormatTemperature(double celsius)
        {
            return celsius.ToString("0.#", DisplayCulture) + " °C";
        }
    }
}

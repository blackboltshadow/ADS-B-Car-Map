using System.Globalization;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// One row of the forecast table of the airport card.
    /// </summary>
    /// <param name="Period">Beginning and end of the period on two lines, for example "05 17:00" and "→ 06 06:00" (day of the month and time).</param>
    /// <param name="Change">Kind of the change, for example "bazowa", "czasowo" or "40% czasowo".</param>
    /// <param name="Conditions">Wind, visibility, clouds and weather of the period; only the elements that the forecast gives.</param>
    public sealed record ForecastRow(string Period, string Change, string Conditions);

    /// <summary>
    /// Builds the texts describing the forecast (TAF) of an airport. Shared by the phone card and the Android Auto popup.
    /// </summary>
    public static class AirportForecastText
    {
        /// <summary>
        /// Returns the time zone for the times of the forecast.
        /// </summary>
        /// <param name="useLocalTime">Whether to use the local time of the phone instead of UTC.</param>
        /// <returns>The time zone.</returns>
        public static TimeZoneInfo GetZone(bool useLocalTime)
        {
            return useLocalTime ? TimeZoneInfo.Local : TimeZoneInfo.Utc;
        }

        /// <summary>
        /// Returns the header of the forecast panel.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the forecast is being downloaded.</param>
        /// <param name="expanded">Whether the panel is open; the closed header also holds the basic conditions.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone of the times.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <returns>Text such as "Prognoza · do 06.11 09:00 UTC · 280° 10 kt · 8,0 km" when closed or "Prognoza (TAF) · ważna 05.11 09:00 – 06.11 09:00 UTC" when open.</returns>
        public static string BuildHeader(ForecastEntry? entry, bool isLoading, bool expanded, DisplayUnits units, TimeZoneInfo zone, DateTime utcNow)
        {
            string title = expanded ? Loc.T("Prognoza (TAF)") : Loc.T("Prognoza");

            if (entry == null)
            {
                return title + " · " + (isLoading ? Loc.T("pobieranie…") : Loc.T("brak danych"));
            }

            if (entry.Forecast == null)
            {
                return title + " · " + Loc.T("brak prognozy TAF dla tego lotniska");
            }

            Taf taf = entry.Forecast;
            string label = GetZoneLabel(zone);
            bool isExpired = taf.ValidTo.UtcDateTime <= utcNow;

            if (expanded)
            {
                string valid = Loc.F("ważna {0} – {1} {2}", FormatTime(taf.ValidFrom, zone), FormatTime(taf.ValidTo, zone), label);

                return title + " · " + valid + (isExpired ? " · " + Loc.T("wygasła") : string.Empty);
            }

            if (isExpired)
            {
                return title + " · " + Loc.F("wygasła {0} {1}", FormatTime(taf.ValidTo, zone), label);
            }

            List<string> parts = new List<string> { title, Loc.F("do {0} {1}", FormatTime(taf.ValidTo, zone), label) };
            TafPeriod? basic = taf.Periods.FirstOrDefault((TafPeriod period) => period.Change == null) ?? taf.Periods.FirstOrDefault();

            if (basic != null)
            {
                string wind = AirportWeatherText.FormatWindShort(basic.WindDirectionDegrees, basic.IsWindVariable, basic.WindSpeedKnots, units);
                string visibility = AirportWeatherText.FormatVisibility(basic.VisibilityMeters, basic.IsVisibilityLowerBound, units);

                if (wind.Length > 0)
                {
                    parts.Add(wind);
                }

                if (visibility.Length > 0)
                {
                    parts.Add(visibility);
                }
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Returns the headings of the columns of the forecast table.
        /// </summary>
        /// <returns>The period, the change and the conditions.</returns>
        public static List<string> BuildColumns()
        {
            return new List<string> { Loc.T("OKRES"), Loc.T("ZMIANA"), Loc.T("WARUNKI") };
        }

        /// <summary>
        /// Returns the rows of the forecast table, one for each period.
        /// </summary>
        /// <param name="taf">The forecast.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone of the times.</param>
        /// <returns>The rows from the earliest period.</returns>
        public static List<ForecastRow> BuildRows(Taf taf, DisplayUnits units, TimeZoneInfo zone)
        {
            return taf.Periods
                .OrderBy((TafPeriod period) => period.From)
                .Select((TafPeriod period) => new ForecastRow(FormatPeriod(period, zone), FormatChange(period), FormatConditions(period, units)))
                .ToList();
        }

        /// <summary>
        /// Returns the lines under the table: the original text of the forecast.
        /// </summary>
        /// <param name="taf">The forecast.</param>
        /// <returns>The lines.</returns>
        public static List<string> BuildFooter(Taf taf)
        {
            return new List<string> { taf.RawText };
        }

        /// <summary>
        /// Returns the lines of the popup: the closed header, which holds the validity and the basic conditions.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the forecast is being downloaded.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone of the times.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <returns>One line.</returns>
        public static List<string> BuildPopupLines(ForecastEntry? entry, bool isLoading, DisplayUnits units, TimeZoneInfo zone, DateTime utcNow)
        {
            return new List<string> { BuildHeader(entry, isLoading, false, units, zone, utcNow) };
        }

        /// <summary>
        /// Returns all the lines of the forecast as one block of text: the open header, a line for each period and the original text. Used where there are no panels, such as the dialog of a favorite airport.
        /// </summary>
        /// <param name="entry">Stored answer for the airport, or null when there is none.</param>
        /// <param name="isLoading">Whether the forecast is being downloaded.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone of the times.</param>
        /// <param name="utcNow">Current time in UTC.</param>
        /// <returns>Text lines; the first one is the header.</returns>
        public static List<string> BuildTextLines(ForecastEntry? entry, bool isLoading, DisplayUnits units, TimeZoneInfo zone, DateTime utcNow)
        {
            List<string> lines = new List<string> { BuildHeader(entry, isLoading, true, units, zone, utcNow) };

            if (entry?.Forecast != null)
            {
                foreach (ForecastRow row in BuildRows(entry.Forecast, units, zone))
                {
                    lines.Add(string.Join(" · ", new string[] { row.Period.Replace("\n", " "), row.Change, row.Conditions }.Where((string part) => part.Length > 0)));
                }

                lines.AddRange(BuildFooter(entry.Forecast));
            }

            return lines;
        }

        /// <summary>
        /// Formats the beginning and the end of a period.
        /// </summary>
        /// <param name="period">The period.</param>
        /// <param name="zone">Time zone of the times.</param>
        /// <returns>Text of two lines such as "05 17:00" and "→ 06 06:00".</returns>
        private static string FormatPeriod(TafPeriod period, TimeZoneInfo zone)
        {
            return FormatDayTime(period.From, zone) + "\n→ " + FormatDayTime(period.To, zone);
        }

        /// <summary>
        /// Describes the kind of the change of a period.
        /// </summary>
        /// <param name="period">The period.</param>
        /// <returns>Text such as "bazowa", "od", "stopniowo", "czasowo" or "40% czasowo"; an unknown kind is written as the forecast gives it.</returns>
        private static string FormatChange(TafPeriod period)
        {
            string change;

            switch (period.Change)
            {
                case null:
                    change = period.Probability.HasValue ? string.Empty : Loc.T("bazowa");
                    break;
                case "FM":
                    change = Loc.T("od");
                    break;
                case "BECMG":
                    change = Loc.T("stopniowo");
                    break;
                case "TEMPO":
                    change = Loc.T("czasowo");
                    break;
                case "PROB":
                    change = string.Empty;
                    break;
                default:
                    change = period.Change;
                    break;
            }

            if (!period.Probability.HasValue)
            {
                return change;
            }

            string probability = Loc.F("{0}%", period.Probability.Value);

            return change.Length > 0 ? probability + " " + change : probability;
        }

        /// <summary>
        /// Describes the conditions of a period.
        /// </summary>
        /// <param name="period">The period.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>The wind, the visibility, the clouds and the weather that the period gives, separated with dots; a dash when it gives none of them.</returns>
        private static string FormatConditions(TafPeriod period, DisplayUnits units)
        {
            List<string> parts = new List<string>();
            string wind = AirportWeatherText.FormatWind(period.WindDirectionDegrees, period.IsWindVariable, period.WindSpeedKnots, period.WindGustKnots, units);
            string visibility = AirportWeatherText.FormatVisibility(period.VisibilityMeters, period.IsVisibilityLowerBound, units);
            string clouds = AirportWeatherText.FormatCloudLayers(period.Clouds, units);

            if (wind.Length > 0)
            {
                parts.Add(wind);
            }

            if (visibility.Length > 0)
            {
                parts.Add(visibility);
            }

            if (clouds.Length > 0)
            {
                parts.Add(clouds);
            }

            if (period.WeatherText != null)
            {
                parts.Add(period.WeatherText);
            }

            return parts.Count == 0 ? "–" : string.Join(" · ", parts);
        }

        /// <summary>
        /// Formats a moment with the date and the time in a time zone.
        /// </summary>
        /// <param name="time">The moment.</param>
        /// <param name="zone">Time zone.</param>
        /// <returns>Text such as "06.11 09:00".</returns>
        private static string FormatTime(DateTimeOffset time, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTime(time, zone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a moment with the day of the month and the time in a time zone.
        /// </summary>
        /// <param name="time">The moment.</param>
        /// <param name="zone">Time zone.</param>
        /// <returns>Text such as "06 09:00".</returns>
        private static string FormatDayTime(DateTimeOffset time, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTime(time, zone).ToString("dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Names a time zone for the header.
        /// </summary>
        /// <param name="zone">Time zone.</param>
        /// <returns>"UTC" for UTC, otherwise "czas lokalny".</returns>
        private static string GetZoneLabel(TimeZoneInfo zone)
        {
            return zone.Equals(TimeZoneInfo.Utc) ? "UTC" : Loc.T("czas lokalny");
        }
    }
}

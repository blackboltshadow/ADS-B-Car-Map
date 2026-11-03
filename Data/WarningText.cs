using System.Globalization;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Texts of the screen with the official notices. Kept apart from the screen, so that they can be tested without Android.
    /// </summary>
    public static class WarningText
    {
        /// <summary>
        /// Culture used for numbers and dates in the texts, as in the other cards.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Name of the source that is shown with the data.
        /// </summary>
        public static string Attribution => Loc.T("Dane: RSO (TVP)");

        /// <summary>
        /// Warning that the notices are not an official alarm system.
        /// </summary>
        public static string Disclaimer => Loc.T("Komunikaty pochodzą z publicznego kanału Regionalnego Systemu Ostrzegania, który udostępnia TVP. To nie jest oficjalny system ostrzegania: aplikacja może pokazać komunikat ze spóźnieniem, w niepełnej postaci albo wcale. Alert RCB przychodzi SMS-em od operatora i nie jest tu dostępny. Zawsze kieruj się oficjalnymi komunikatami służb.");

        /// <summary>
        /// Gives the title of a notice. A reading of a gauge has no title, so it is named after the river and the station.
        /// </summary>
        /// <param name="warning">The notice.</param>
        /// <returns>The title.</returns>
        public static string GetTitle(Warning warning)
        {
            if (warning.Title.Length > 0)
            {
                return warning.Title;
            }

            string place = warning.LocationName.Length > 0 ? warning.LocationName : warning.RiverName;

            return place.Length > 0 ? place : WarningCategories.GetName(warning.Category);
        }

        /// <summary>
        /// Gives the line under the title: the category and the voivodeships.
        /// </summary>
        /// <param name="warning">The notice.</param>
        /// <returns>For example "Hydrologiczne · Podkarpackie, Lubelskie".</returns>
        public static string GetSubtitle(Warning warning)
        {
            string category = WarningCategories.GetName(warning.Category);

            if (warning.Provinces.Count == 0)
            {
                return category + " · " + Loc.T("cały kraj");
            }

            return category + " · " + string.Join(", ", warning.Provinces.Select((WarningProvince province) => province.Name));
        }

        /// <summary>
        /// Builds the lines of the description of a notice.
        /// </summary>
        /// <param name="warning">The notice.</param>
        /// <param name="nowUtc">The moment, in UTC, at which the validity is judged.</param>
        /// <param name="zone">Time zone in which the times are shown.</param>
        /// <returns>Lines: the summary, the text, the reading of the gauge, the validity and the time of the change; the ones the source did not give are left out.</returns>
        public static IReadOnlyList<string> GetLines(Warning warning, DateTime nowUtc, TimeZoneInfo zone)
        {
            List<string> lines = new List<string>();

            if (warning.Summary.Length > 0 && !string.Equals(warning.Summary, warning.Content, StringComparison.Ordinal))
            {
                lines.Add(warning.Summary);
            }

            if (warning.Content.Length > 0)
            {
                lines.Add(warning.Content);
            }

            if (warning.WaterLevel != null)
            {
                lines.AddRange(GetWaterLines(warning));
            }

            lines.Add(GetValidityLine(warning, nowUtc, zone));
            lines.Add(Loc.T("Zmieniono: ") + FormatTime(warning.UpdatedUtc, zone));

            return lines;
        }

        /// <summary>
        /// Describes the validity of a notice.
        /// </summary>
        /// <param name="warning">The notice.</param>
        /// <param name="nowUtc">The moment, in UTC, at which the validity is judged.</param>
        /// <param name="zone">Time zone in which the times are shown.</param>
        /// <returns>For example "Ważne: 03.11 08:00 – 05.11 08:00" or "Wygasło: 03.11 08:00 – 04.11 08:00".</returns>
        public static string GetValidityLine(Warning warning, DateTime nowUtc, TimeZoneInfo zone)
        {
            bool over = warning.ValidToUtc != null && warning.ValidToUtc.Value < nowUtc;
            string label = over ? Loc.T("Wygasło: ") : Loc.T("Ważne: ");

            if (warning.ValidToUtc == null)
            {
                return label + Loc.F("od {0}", FormatTime(warning.ValidFromUtc, zone));
            }

            return label + FormatTime(warning.ValidFromUtc, zone) + " – " + FormatTime(warning.ValidToUtc.Value, zone);
        }

        /// <summary>
        /// Describes the status line of the screen: what the feed is doing and how many notices pass the filter.
        /// </summary>
        /// <param name="state">State of the feed.</param>
        /// <param name="shown">Number of the notices that pass the configuration.</param>
        /// <returns>The description.</returns>
        public static string BuildStateText(PollingFeedState state, int shown)
        {
            switch (state)
            {
                case PollingFeedState.Loading:
                    return Loc.T("ładowanie…");
                case PollingFeedState.Offline:
                    return shown > 0 ? Loc.F("brak połączenia, dane mogą być nieaktualne, komunikatów: {0}", shown) : Loc.T("brak połączenia");
                case PollingFeedState.Ready:
                    return Loc.F("komunikatów: {0}", shown);
                default:
                    return Loc.F("wstrzymane, komunikatów: {0}", shown);
            }
        }

        /// <summary>
        /// Describes the feed in the same form as the other sources on the card of the sources: a group with one line for the source and lines for the last update and the last failure.
        /// </summary>
        /// <param name="enabled">Whether the screen of the notices has been opened, so that the feed is used.</param>
        /// <param name="state">State of the feed.</param>
        /// <param name="count">Number of the notices the feed holds.</param>
        /// <param name="lastUpdateUtc">Time of the last successful download, or null when there was none.</param>
        /// <param name="failure">The last failure, or null when there was none.</param>
        /// <param name="utcNow">Current time in UTC, used for the age of the update and of the failure.</param>
        /// <returns>The text of the group, without a trailing new line.</returns>
        public static string FormatFeed(bool enabled, PollingFeedState state, int count, DateTime? lastUpdateUtc, PollingFeedFailure? failure, DateTime utcNow)
        {
            string description;

            if (!enabled || state == PollingFeedState.Stopped)
            {
                description = Loc.T("wstrzymane, ekran ostrzeżeń jest zamknięty");
            }
            else if (state == PollingFeedState.Loading)
            {
                description = Loc.T("pobieranie…");
            }
            else if (state == PollingFeedState.Offline)
            {
                description = Loc.T("brak połączenia, aplikacja ponawia próby");
            }
            else
            {
                description = Loc.F("działa, komunikatów: {0}", count);
            }

            List<string> lines = new List<string> { Loc.T("Ostrzeżenia") + ":", "• RSO (TVP) — " + description };

            if (lastUpdateUtc != null)
            {
                lines.Add(Loc.T("   ostatnia aktualizacja: ") + SourceStateText.DescribeAge(utcNow - lastUpdateUtc.Value));
            }

            if (failure != null)
            {
                lines.Add(Loc.T("   ostatni błąd: ") + Loc.T(failure.Description) + ", " + SourceStateText.DescribeAge(utcNow - failure.OccurredUtc));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Describes the reading of a water gauge: the level, the levels of the warning and of the alarm, and whether they are exceeded.
        /// </summary>
        /// <param name="warning">The notice with a reading.</param>
        /// <returns>Lines of the description.</returns>
        private static IEnumerable<string> GetWaterLines(Warning warning)
        {
            WarningWaterLevel level = warning.WaterLevel!;
            List<string> lines = new List<string>();

            if (warning.RiverName.Length > 0)
            {
                lines.Add(Loc.T("Rzeka: ") + warning.RiverName);
            }

            if (level.ValueCentimeters != null)
            {
                lines.Add(Loc.T("Stan wody: ") + FormatCentimeters(level.ValueCentimeters.Value));
            }

            if (level.WarningCentimeters != null)
            {
                lines.Add(Loc.T("Stan ostrzegawczy: ") + FormatCentimeters(level.WarningCentimeters.Value));
            }

            if (level.AlarmCentimeters != null)
            {
                lines.Add(Loc.T("Stan alarmowy: ") + FormatCentimeters(level.AlarmCentimeters.Value));
            }

            if (level.ValueCentimeters != null && level.AlarmCentimeters != null && level.ValueCentimeters.Value >= level.AlarmCentimeters.Value)
            {
                lines.Add(Loc.T("Przekroczony stan alarmowy"));
            }
            else if (level.ValueCentimeters != null && level.WarningCentimeters != null && level.ValueCentimeters.Value >= level.WarningCentimeters.Value)
            {
                lines.Add(Loc.T("Przekroczony stan ostrzegawczy"));
            }

            return lines;
        }

        /// <summary>
        /// Writes a level of water. The feed gives the levels without a unit; they are read as centimeters, the unit of the gauges.
        /// </summary>
        /// <param name="centimeters">The level.</param>
        /// <returns>Text such as "67 cm".</returns>
        private static string FormatCentimeters(double centimeters)
        {
            return centimeters.ToString("0.#", DisplayCulture) + " cm";
        }

        /// <summary>
        /// Formats a moment in the given time zone.
        /// </summary>
        /// <param name="utc">The moment, in UTC.</param>
        /// <param name="zone">Time zone of the result.</param>
        /// <returns>Day, month, hour and minute.</returns>
        private static string FormatTime(DateTime utc, TimeZoneInfo zone)
        {
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

            return local.ToString("dd.MM HH:mm", DisplayCulture);
        }
    }
}

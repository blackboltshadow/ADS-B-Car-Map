using AdsbCarMap.Localization;
using System.Globalization;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Settings;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Texts of alerts shown to the user: the description of a rule and the content of a notification.
    /// </summary>
    public static class AlertText
    {
        /// <summary>
        /// Describes the area of a rule in one line.
        /// </summary>
        /// <param name="area">Area to describe.</param>
        /// <param name="unit">Unit of the radius.</param>
        /// <returns>For example "Wokół lotniska EPWA, 50 km".</returns>
        public static string DescribeArea(AlertArea area, DistanceUnit unit)
        {
            string radius = UnitFormatter.FormatDistanceCompact(area.RadiusKilometers, unit);

            switch (area.Kind)
            {
                case AlertAreaKind.AroundMe:
                    return Loc.T("Wokół mojej pozycji, ") + radius;
                case AlertAreaKind.AroundAirport:
                    return Loc.T("Wokół lotniska ") + area.AirportIcao + ", " + radius;
                case AlertAreaKind.Polygon:
                    return area.Vertices != null && area.Vertices.Count == 4 && IsRectangle(area.Vertices)
                        ? Loc.T("Prostokąt, ") + radius
                        : Loc.T("Wielokąt (") + (area.Vertices?.Count ?? 0).ToString(CultureInfo.InvariantCulture) + Loc.T(" wierzchołków), ") + radius;
                default:
                    return area.Center == null
                        ? Loc.T("Okrąg, ") + radius
                        : Loc.T("Okrąg ") + area.Center.Latitude.ToString("0.000", CultureInfo.InvariantCulture) + ", " + area.Center.Longitude.ToString("0.000", CultureInfo.InvariantCulture) + ", " + radius;
            }
        }

        /// <summary>
        /// Checks whether four vertices form a rectangle with the sides along the parallels and meridians, as drawn by the rectangle tool.
        /// </summary>
        /// <param name="vertices">Four vertices.</param>
        /// <returns>True for a rectangle.</returns>
        private static bool IsRectangle(IReadOnlyList<GeoPoint> vertices)
        {
            return vertices[0].Latitude == vertices[1].Latitude && vertices[1].Longitude == vertices[2].Longitude && vertices[2].Latitude == vertices[3].Latitude && vertices[3].Longitude == vertices[0].Longitude;
        }

        /// <summary>
        /// Builds the title of a notification that reports several aircraft of one rule.
        /// </summary>
        /// <param name="notice">Notice that groups the matches.</param>
        /// <returns>The rule name followed by the number of aircraft.</returns>
        public static string GroupTitle(AlertNotice notice)
        {
            string name = string.IsNullOrWhiteSpace(notice.Rule.Name) ? Loc.T("Alert") : notice.Rule.Name;

            return name + Loc.T(" – samolotów: ") + notice.Matches.Count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds the text of a notification that reports several aircraft of one rule.
        /// </summary>
        /// <param name="notice">Notice that groups the matches.</param>
        /// <param name="maximumNames">Number of aircraft that are named; the rest is counted.</param>
        /// <returns>The callsigns or addresses separated by commas, followed by the number of the other aircraft.</returns>
        public static string GroupBody(AlertNotice notice, int maximumNames = 4)
        {
            List<string> names = new List<string>();

            foreach (AlertMatch match in notice.Matches.Take(maximumNames))
            {
                string callsign = match.Aircraft.Callsign?.Trim() ?? string.Empty;
                names.Add(callsign.Length > 0 ? callsign : match.Aircraft.Hex.ToUpperInvariant());
            }

            string text = string.Join(", ", names);
            int others = notice.Matches.Count - names.Count;

            return others > 0 ? text + ", " + Loc.T("jeszcze ") + others.ToString(CultureInfo.InvariantCulture) : text;
        }

        /// <summary>
        /// Describes the event that fired a rule.
        /// </summary>
        /// <param name="trigger">Event.</param>
        /// <returns>For example "wlot do obszaru".</returns>
        public static string DescribeTrigger(AlertTrigger trigger)
        {
            return trigger == AlertTrigger.Entered ? Loc.T("wlot do obszaru") : Loc.T("w obszarze");
        }

        /// <summary>
        /// Describes how loudly the notifications of a rule are shown.
        /// </summary>
        /// <param name="importance">Importance of the rule.</param>
        /// <returns>The name of the level.</returns>
        public static string DescribeImportance(AlertImportance importance)
        {
            switch (importance)
            {
                case AlertImportance.Default:
                    return Loc.T("Zwykłe (dźwięk, bez wyskakiwania)");
                case AlertImportance.Low:
                    return Loc.T("Ciche (bez dźwięku i wyskakiwania)");
                default:
                    return Loc.T("Głośne (dźwięk i wyskakujące)");
            }
        }

        /// <summary>
        /// Describes the days and quiet hours of a schedule.
        /// </summary>
        /// <param name="schedule">Schedule to describe.</param>
        /// <returns>For example "pn, wt, śr · godziny ciszy 22:00–06:00", or an empty text when the schedule has no restriction.</returns>
        public static string DescribeSchedule(AlertSchedule schedule)
        {
            List<string> parts = new List<string>();

            if (schedule.ActiveDays != AlertSchedule.AllDays)
            {
                List<string> days = new List<string>();
                DayOfWeek[] order = new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

                foreach (DayOfWeek day in order)
                {
                    if (schedule.IsDayActive(day))
                    {
                        days.Add(ShortDayName(day));
                    }
                }

                parts.Add(days.Count == 0 ? Loc.T("żaden dzień") : string.Join(", ", days));
            }

            if (schedule.QuietStartMinute is int start && schedule.QuietEndMinute is int end)
            {
                parts.Add(Loc.T("godziny ciszy ") + AlertSchedule.FormatMinute(start) + "–" + AlertSchedule.FormatMinute(end));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Gives the short name of a day of the week.
        /// </summary>
        /// <param name="day">Day of the week.</param>
        /// <returns>Two or three letter name, for example "pn".</returns>
        public static string ShortDayName(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday:
                    return Loc.T("pn");
                case DayOfWeek.Tuesday:
                    return Loc.T("wt");
                case DayOfWeek.Wednesday:
                    return Loc.T("śr");
                case DayOfWeek.Thursday:
                    return Loc.T("cz");
                case DayOfWeek.Friday:
                    return Loc.T("pt");
                case DayOfWeek.Saturday:
                    return Loc.T("sb");
                default:
                    return Loc.T("nd");
            }
        }

        /// <summary>
        /// Describes which events a rule reports.
        /// </summary>
        /// <param name="rule">Rule to describe.</param>
        /// <returns>For example "wlot, pojawienie się · cisza 30 min".</returns>
        public static string DescribeEvents(AlertRule rule)
        {
            List<string> events = new List<string>();

            if (rule.NotifyOnEntry)
            {
                events.Add("wlot");
            }

            if (rule.NotifyOnAppearance)
            {
                events.Add(Loc.T("pojawienie się"));
            }

            string text = events.Count == 0 ? Loc.T("bez zdarzeń") : string.Join(", ", events);

            return text + Loc.T(" · cisza ") + rule.CooldownMinutes.ToString(CultureInfo.InvariantCulture) + Loc.T(" min");
        }

        /// <summary>
        /// Builds the title of the notification about a match.
        /// </summary>
        /// <param name="match">Match to describe.</param>
        /// <returns>The rule name followed by the event.</returns>
        public static string Title(AlertMatch match)
        {
            string name = string.IsNullOrWhiteSpace(match.Rule.Name) ? Loc.T("Alert") : match.Rule.Name;

            return name + (match.Trigger == AlertTrigger.Entered ? Loc.T(" – wlot do obszaru") : Loc.T(" – w obszarze"));
        }

        /// <summary>
        /// Builds the text of the notification about a match.
        /// </summary>
        /// <param name="match">Match to describe.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>The callsign or address, the type, the altitude and the distance, separated by dots.</returns>
        public static string Body(AlertMatch match, DisplayUnits units)
        {
            Aircraft aircraft = match.Aircraft;
            List<string> parts = new List<string>();
            string callsign = aircraft.Callsign?.Trim() ?? string.Empty;

            parts.Add(callsign.Length > 0 ? callsign : aircraft.Hex.ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(aircraft.TypeName))
            {
                parts.Add(aircraft.TypeName);
            }
            else if (!string.IsNullOrWhiteSpace(aircraft.TypeCode))
            {
                parts.Add(aircraft.TypeCode);
            }

            if (aircraft.OnGround)
            {
                parts.Add(Loc.T("na ziemi"));
            }
            else if (aircraft.AltitudeFeet != null)
            {
                parts.Add(AltitudeFormatter.Format(aircraft.AltitudeFeet.Value, units.Altitude));
            }

            parts.Add(UnitFormatter.FormatDistanceCompact(match.DistanceKilometers, units.Distance));

            return string.Join(" · ", parts);
        }
    }
}

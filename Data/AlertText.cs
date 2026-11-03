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
                    return "Wokół mojej pozycji, " + radius;
                case AlertAreaKind.AroundAirport:
                    return "Wokół lotniska " + area.AirportIcao + ", " + radius;
                default:
                    return area.Center == null
                        ? "Okrąg, " + radius
                        : "Okrąg " + area.Center.Latitude.ToString("0.000", CultureInfo.InvariantCulture) + ", " + area.Center.Longitude.ToString("0.000", CultureInfo.InvariantCulture) + ", " + radius;
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
                events.Add("pojawienie się");
            }

            string text = events.Count == 0 ? "bez zdarzeń" : string.Join(", ", events);

            return text + " · cisza " + rule.CooldownMinutes.ToString(CultureInfo.InvariantCulture) + " min";
        }

        /// <summary>
        /// Builds the title of the notification about a match.
        /// </summary>
        /// <param name="match">Match to describe.</param>
        /// <returns>The rule name followed by the event.</returns>
        public static string Title(AlertMatch match)
        {
            string name = string.IsNullOrWhiteSpace(match.Rule.Name) ? "Alert" : match.Rule.Name;

            return name + (match.Trigger == AlertTrigger.Entered ? " – wlot do obszaru" : " – w obszarze");
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
                parts.Add("na ziemi");
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

using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Texts of the airspace layer. Kept apart from the drawing, so that they can be tested without Android.
    /// </summary>
    public static class AirspaceText
    {
        /// <summary>
        /// Culture used for numbers in the texts, as in the other cards.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Meters in one foot.
        /// </summary>
        private const double FeetToMeters = 0.3048;

        /// <summary>
        /// Name of the source that is shown with the data.
        /// </summary>
        public static string Attribution => Loc.T("Dane: PAŻP");

        /// <summary>
        /// Warning that the data is not an aeronautical publication.
        /// </summary>
        public static string Disclaimer => Loc.T("Strefy pochodzą z mapy PAŻP, są nieoficjalne i mogą być spóźnione lub niepełne. Nie służą do planowania ani wykonywania lotów.");

        /// <summary>
        /// Gives the color of the areas of a kind, as an opaque ARGB value. The restricting kinds are red and orange, the reserved ones other colors, and an unknown kind is gray.
        /// </summary>
        /// <param name="type">Kind of the area as the source names it.</param>
        /// <returns>The color.</returns>
        public static int GetColor(string type)
        {
            switch (type.ToUpperInvariant())
            {
                case "P":
                case "R":
                    return unchecked((int)0xFFE53935);
                case "D":
                    return unchecked((int)0xFFFB8C00);
                case "TSA":
                    return unchecked((int)0xFFD81B60);
                case "TRA":
                    return unchecked((int)0xFF1E88E5);
                case "ADHOC":
                    return unchecked((int)0xFFFDD835);
                case "MRT":
                    return unchecked((int)0xFF8E24AA);
                case "ATZ":
                    return unchecked((int)0xFF00ACC1);
                default:
                    return unchecked((int)0xFF90A4AE);
            }
        }

        /// <summary>
        /// Describes the state of the feed for the line shown next to the attribution while the layer is on.
        /// </summary>
        /// <param name="state">State of the feed.</param>
        /// <param name="shown">Number of the areas that pass the configuration of the layer.</param>
        /// <returns>The description.</returns>
        public static string BuildStateText(PollingFeedState state, int shown)
        {
            switch (state)
            {
                case PollingFeedState.Loading:
                    return Loc.T("ładowanie…");
                case PollingFeedState.Offline:
                    return shown > 0 ? Loc.F("brak połączenia, dane mogą być nieaktualne, stref: {0}", shown) : Loc.T("brak połączenia");
                case PollingFeedState.Ready:
                    return Loc.F("stref: {0}", shown);
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Converts a height in feet to the number shown in the unit chosen by the user.
        /// </summary>
        /// <param name="feet">Height in feet.</param>
        /// <param name="unit">Unit of the result; flight levels are hundreds of feet.</param>
        /// <returns>Height in meters, feet or hundreds of feet.</returns>
        public static double FeetToUnit(int feet, AltitudeUnit unit)
        {
            return AltitudeFormatter.MetersToUnit(feet * FeetToMeters, unit);
        }

        /// <summary>
        /// Converts a number typed in the unit chosen by the user to a height in feet.
        /// </summary>
        /// <param name="value">Height in meters, feet or hundreds of feet.</param>
        /// <param name="unit">Unit of the value.</param>
        /// <returns>Height in whole feet; a negative height gives zero.</returns>
        public static int UnitToFeet(double value, AltitudeUnit unit)
        {
            double feet = AltitudeFormatter.UnitToMeters(value, unit) / FeetToMeters;

            return (int)Math.Max(0.0, Math.Round(feet));
        }

        /// <summary>
        /// Gives the title of the notification about an area that becomes active.
        /// </summary>
        /// <param name="alert">The area and the reservation.</param>
        /// <param name="nowUtc">The moment, in UTC, from which the time to the start is counted.</param>
        /// <returns>For example "Strefa EPD21 jest aktywna" or "Strefa EPD21 zostanie aktywowana za 25 min".</returns>
        public static string GetAlertTitle(AirspaceAlert alert, DateTime nowUtc)
        {
            if (alert.InForce)
            {
                return Loc.F("Strefa {0} jest aktywna", alert.Zone.Designator);
            }

            int minutes = (int)Math.Max(1.0, Math.Ceiling((alert.Reservation.StartUtc - nowUtc).TotalMinutes));

            return Loc.F("Strefa {0} zostanie aktywowana za {1} min", alert.Zone.Designator, minutes);
        }

        /// <summary>
        /// Gives the text of the notification about an area that becomes active: the kind, the distance, the heights and the period.
        /// </summary>
        /// <param name="alert">The area and the reservation.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone in which the times are shown.</param>
        /// <returns>Parts separated by dots, e.g. "Strefa niebezpieczna (D) · 35 km od Ciebie · GND – FL130 · 03.11 08:00 – 03.11 22:00".</returns>
        public static string GetAlertBody(AirspaceAlert alert, DisplayUnits units, TimeZoneInfo zone)
        {
            string distance = alert.DistanceKilometers <= 0.0
                ? Loc.T("jesteś w strefie")
                : Loc.F("{0} od Ciebie", UnitFormatter.FormatDistanceCompact(alert.DistanceKilometers, units.Distance));

            return string.Join(" · ", AirspaceTypes.GetName(alert.Zone.Type), distance, FormatLimit(alert.Reservation.Lower, units.Altitude) + " – " + FormatLimit(alert.Reservation.Upper, units.Altitude), FormatPeriod(alert.Reservation, zone));
        }

        /// <summary>
        /// Writes the band of heights of the filter in the unit chosen by the user. An upper edge at the top of the scale means "and above", so it has a plus.
        /// </summary>
        /// <param name="lowFeet">Lower edge of the band, in feet.</param>
        /// <param name="highFeet">Upper edge of the band, in feet.</param>
        /// <param name="unit">Unit chosen by the user.</param>
        /// <returns>Text such as "Od 0 m do 3 048 m" or "Od FL050 do FL660+".</returns>
        public static string FormatRange(int lowFeet, int highFeet, AltitudeUnit unit)
        {
            string high = AltitudeFormatter.Format(highFeet, unit) + (highFeet >= AirspaceAltitudeScale.FeetAt(AirspaceAltitudeScale.LastIndex) ? "+" : string.Empty);

            return Loc.F("Od {0} do {1}", AltitudeFormatter.Format(lowFeet, unit), high);
        }

        /// <summary>
        /// Gives the title of the card of an area.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <returns>The designation and the name of the kind, e.g. "EPD21 · Strefa niebezpieczna (D)".</returns>
        public static string GetTitle(AirspaceZone zone)
        {
            return zone.Designator + " · " + AirspaceTypes.GetName(zone.Type);
        }

        /// <summary>
        /// Gives the name of a state.
        /// </summary>
        /// <param name="state">State of the area.</param>
        /// <returns>Translated name.</returns>
        public static string GetStateName(AirspaceZoneState state)
        {
            switch (state)
            {
                case AirspaceZoneState.Active:
                    return Loc.T("Aktywna");
                case AirspaceZoneState.Planned:
                    return Loc.T("Planowana");
                default:
                    return Loc.T("Nieaktywna");
            }
        }

        /// <summary>
        /// Formats one height limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="unit">Unit chosen by the user. An altitude above sea level is never written as a flight level, so it is shown in feet when the unit is the flight level.</param>
        /// <returns>Text such as "GND", "2 896 m (FL095)", "9 500 ft (FL095)" or "3 500 ft AMSL".</returns>
        public static string FormatLimit(AirspaceLimit limit, AltitudeUnit unit)
        {
            switch (limit.Reference)
            {
                case AirspaceLimitReference.Ground:
                    return "GND";
                case AirspaceLimitReference.FlightLevel:
                    string level = AltitudeFormatter.Format(limit.Feet, AltitudeUnit.FlightLevel);

                    return unit == AltitudeUnit.FlightLevel ? level : AltitudeFormatter.Format(limit.Feet, unit) + " (" + level + ")";
                default:
                    return AltitudeFormatter.Format(limit.Feet, unit == AltitudeUnit.FlightLevel ? AltitudeUnit.Feet : unit) + " AMSL";
            }
        }

        /// <summary>
        /// Formats the period of a reservation in the given time zone.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <param name="zone">Time zone in which the times are shown, normally the one of the phone.</param>
        /// <returns>Text such as "03.11 08:00 – 03.11 22:00".</returns>
        public static string FormatPeriod(AirspaceReservation reservation, TimeZoneInfo zone)
        {
            return FormatTime(reservation.StartUtc, zone) + " – " + FormatTime(reservation.EndUtc, zone);
        }

        /// <summary>
        /// Builds the lines of the card of an area.
        /// </summary>
        /// <param name="view">The area with its state and the reservation that describes it.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <param name="zone">Time zone in which the times are shown.</param>
        /// <returns>Lines: state, heights, period, unit and remarks; the ones the source did not give are left out.</returns>
        public static IReadOnlyList<string> GetLines(AirspaceZoneView view, DisplayUnits units, TimeZoneInfo zone)
        {
            List<string> lines = new List<string> { Loc.T("Stan: ") + GetStateName(view.State) };
            AirspaceReservation? reservation = view.Reservation;

            if (reservation == null)
            {
                return lines;
            }

            lines.Add(Loc.T("Wysokość: ") + FormatLimit(reservation.Lower, units.Altitude) + " – " + FormatLimit(reservation.Upper, units.Altitude));
            lines.Add(Loc.T("Czas: ") + FormatPeriod(reservation, zone));

            if (reservation.Unit.Length > 0 && reservation.Unit != "ZZZZ")
            {
                lines.Add(Loc.T("Jednostka: ") + reservation.Unit);
            }

            if (reservation.Remarks != null)
            {
                lines.Add(Loc.T("Uwagi: ") + reservation.Remarks);
            }

            return lines;
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

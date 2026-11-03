using System.Globalization;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Texts and colors of the threat layer. Kept apart from the drawing, so that they can be tested without Android.
    /// </summary>
    public static class ThreatText
    {
        /// <summary>
        /// Kilometers per hour in one knot, used to show the speed in the unit chosen by the user.
        /// </summary>
        private const double KilometersPerHourPerKnot = 1.852;

        /// <summary>
        /// Culture used for numbers in the texts, as in the other cards.
        /// </summary>
        private static readonly CultureInfo DisplayCulture = new CultureInfo("pl-PL");

        /// <summary>
        /// Color of a threat that is only an observation and no signal to take shelter; deliberately not red.
        /// </summary>
        public const int AdvisoryColor = unchecked((int)0xFF90A4AE);

        /// <summary>
        /// Name of the application that has to be shown with the data, as the terms of the source require.
        /// </summary>
        public static string Attribution => Loc.T("Dane: NEPTUN");

        /// <summary>
        /// Warning that the data is not an official alarm system, as the terms of the source require.
        /// </summary>
        public static string Disclaimer => Loc.T("Dane z otwartych i zewnętrznych źródeł mogą być nieścisłe, niepełne lub spóźnione. To nie jest oficjalny system ostrzegania: zawsze kieruj się oficjalnymi sygnałami alarmu przeciwlotniczego.");

        /// <summary>
        /// One-line warning shown permanently next to the attribution.
        /// </summary>
        public static string ShortDisclaimer => Loc.T("Dane mogą być nieścisłe lub spóźnione – obowiązują oficjalne sygnały alarmowe.");

        /// <summary>
        /// Names a kind of threat.
        /// </summary>
        /// <param name="kind">Kind of threat.</param>
        /// <returns>The name in the language of the application.</returns>
        public static string KindName(ThreatKind kind)
        {
            switch (kind)
            {
                case ThreatKind.Uav:
                    return Loc.T("Dron");
                case ThreatKind.Fpv:
                    return Loc.T("Dron FPV");
                case ThreatKind.Recon:
                    return Loc.T("Dron rozpoznawczy");
                case ThreatKind.Missile:
                    return Loc.T("Rakieta");
                case ThreatKind.Ballistic:
                    return Loc.T("Rakieta balistyczna");
                case ThreatKind.Kab:
                    return Loc.T("Bomba kierowana (KAB)");
                case ThreatKind.Mig31k:
                    return Loc.T("MiG-31K");
                default:
                    return Loc.T("Nieznane zagrożenie");
            }
        }

        /// <summary>
        /// Names how sure the source is.
        /// </summary>
        /// <param name="confidence">Confidence.</param>
        /// <returns>The name in the language of the application.</returns>
        public static string ConfidenceName(ThreatConfidence confidence)
        {
            switch (confidence)
            {
                case ThreatConfidence.High:
                    return Loc.T("wysoka");
                case ThreatConfidence.Medium:
                    return Loc.T("średnia");
                default:
                    return Loc.T("niska");
            }
        }

        /// <summary>
        /// Chooses the color of a threat on the map.
        /// </summary>
        /// <param name="threat">Threat.</param>
        /// <returns>Opaque color as an ARGB number.</returns>
        public static int ColorFor(Threat threat)
        {
            return threat.Advisory ? AdvisoryColor : ColorFor(threat.Kind);
        }

        /// <summary>
        /// Chooses the color of a kind of threat on the map.
        /// </summary>
        /// <param name="kind">Kind of threat.</param>
        /// <returns>Opaque color as an ARGB number.</returns>
        public static int ColorFor(ThreatKind kind)
        {
            switch (kind)
            {
                case ThreatKind.Uav:
                    return unchecked((int)0xFFFF8C00);
                case ThreatKind.Fpv:
                    return unchecked((int)0xFFFFD21F);
                case ThreatKind.Recon:
                    return unchecked((int)0xFF4FC3F7);
                case ThreatKind.Missile:
                    return unchecked((int)0xFFFF3B30);
                case ThreatKind.Ballistic:
                    return unchecked((int)0xFFD50000);
                case ThreatKind.Kab:
                    return unchecked((int)0xFFFF6E40);
                case ThreatKind.Mig31k:
                    return unchecked((int)0xFFB388FF);
                default:
                    return unchecked((int)0xFFB0B0B0);
            }
        }

        /// <summary>
        /// Tells whether a threat is drawn as a point on the map. A threat of which only the region is known must not be drawn as a place.
        /// </summary>
        /// <param name="threat">Threat.</param>
        /// <returns>True when the threat has a position that can be drawn.</returns>
        public static bool HasDrawablePosition(Threat threat)
        {
            return !threat.AreaOnly;
        }

        /// <summary>
        /// Describes how long ago something happened.
        /// </summary>
        /// <param name="timeUtc">Time of the event, or null when unknown.</param>
        /// <param name="nowUtc">Current time.</param>
        /// <returns>For example "3 min temu", or an empty text when the time is unknown.</returns>
        public static string FormatAge(DateTime? timeUtc, DateTime nowUtc)
        {
            if (timeUtc == null)
            {
                return string.Empty;
            }

            TimeSpan age = nowUtc - timeUtc.Value;

            if (age < TimeSpan.FromMinutes(1))
            {
                return Loc.T("przed chwilą");
            }

            if (age < TimeSpan.FromHours(1))
            {
                return Loc.F("{0} min temu", (int)age.TotalMinutes);
            }

            return Loc.F("{0} h temu", (int)age.TotalHours);
        }

        /// <summary>
        /// Builds the short label drawn next to a threat on the map.
        /// </summary>
        /// <param name="threat">Threat.</param>
        /// <returns>The kind, followed by the number of objects when there is more than one.</returns>
        public static string BuildLabel(Threat threat)
        {
            string name = KindName(threat.Kind);

            return threat.Count > 1 ? name + " ×" + threat.Count.ToString(CultureInfo.InvariantCulture) : name;
        }

        /// <summary>
        /// Builds the lines of the description of a threat.
        /// </summary>
        /// <param name="threat">Threat.</param>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="units">Units chosen by the user.</param>
        /// <returns>The lines, from the most to the least important; the disclaimer is not included.</returns>
        public static List<string> BuildLines(Threat threat, DateTime nowUtc, DisplayUnits units)
        {
            List<string> lines = new List<string>();

            lines.Add(BuildLabel(threat));

            List<string> place = new List<string>();

            foreach (string part in new string[] { threat.Locality, threat.District, threat.Region })
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    place.Add(part.Trim());
                }
            }

            if (place.Count > 0)
            {
                lines.Add(string.Join(", ", place));
            }

            if (threat.AreaOnly)
            {
                lines.Add(Loc.T("Znany jest tylko obwód, dokładne położenie nieznane."));
            }
            else
            {
                if (threat.Advisory)
                {
                    lines.Add(Loc.T("Obserwacja, nie sygnał do schronienia."));
                }

                if (threat.HeadingDegrees != null)
                {
                    string heading = string.Format(DisplayCulture, Loc.T("Kierunek: {0:0}° {1}"), threat.HeadingDegrees.Value, GeoMath.CompassPoint(threat.HeadingDegrees.Value));

                    lines.Add(threat.IsPresumptiveCourse ? heading + Loc.T(" (zakładany)") : threat.IsHeadingDerived ? heading + Loc.T(" (z ruchu)") : heading);
                }

                if (threat.SpeedKmh != null)
                {
                    lines.Add(Loc.T("Prędkość: ") + UnitFormatter.FormatSpeed(threat.SpeedKmh.Value / KilometersPerHourPerKnot, units.Speed));
                }

                if (threat.UncertaintyKm > 0.0)
                {
                    lines.Add(Loc.T("Dokładność położenia: ±") + UnitFormatter.FormatDistance(threat.UncertaintyKm, units.Distance, 0) + (threat.IsApproximate ? Loc.T(" (przybliżone)") : string.Empty));
                }
            }

            lines.Add(string.Format(DisplayCulture, Loc.T("Wiarygodność: {0}, zgłoszeń: {1}"), ConfidenceName(threat.Confidence), threat.SourceCount));

            string age = FormatAge(threat.UpdatedUtc, nowUtc);

            if (age.Length > 0)
            {
                lines.Add(Loc.T("Aktualizacja: ") + age + (threat.Status == ThreatStatus.Stale ? Loc.T(" (dawno niepotwierdzane)") : string.Empty));
            }

            return lines;
        }

        /// <summary>
        /// Builds the short text of the state of the feed shown next to the attribution.
        /// </summary>
        /// <param name="isStreaming">Whether the data arrives over the permanent connection.</param>
        /// <param name="isPolling">Whether the list is downloaded periodically.</param>
        /// <param name="isConnecting">Whether the feed is connecting.</param>
        /// <param name="count">Number of the threats that are drawn.</param>
        /// <returns>For example "12 zagrożeń", "łączenie…" or "brak połączenia".</returns>
        public static string BuildStateText(bool isStreaming, bool isPolling, bool isConnecting, int count)
        {
            if (isStreaming || isPolling)
            {
                return Loc.F("zagrożeń: {0}", count);
            }

            return isConnecting ? Loc.T("łączenie…") : Loc.T("brak połączenia");
        }
    }
}

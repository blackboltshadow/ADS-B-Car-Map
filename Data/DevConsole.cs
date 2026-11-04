using System.Globalization;
using System.Text;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.Localization;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// State of the application shown in the developer console. The values are copied under the lock of the map, so the text built from them is consistent.
    /// </summary>
    /// <param name="Zoom">Current zoom level of the map.</param>
    /// <param name="ZoomTarget">Zoom level the map is animating to.</param>
    /// <param name="CenterLatitude">Latitude of the center of the map.</param>
    /// <param name="CenterLongitude">Longitude of the center of the map.</param>
    /// <param name="Following">Whether the map follows an aircraft or the user.</param>
    /// <param name="SurfaceWidth">Width of the drawing surface in pixels.</param>
    /// <param name="SurfaceHeight">Height of the drawing surface in pixels.</param>
    /// <param name="GpsKnown">Whether the position of the device is known.</param>
    /// <param name="GpsLatitude">Latitude of the device.</param>
    /// <param name="GpsLongitude">Longitude of the device.</param>
    /// <param name="GpsAtUtc">Moment of the last position of the device, or null when it is unknown.</param>
    /// <param name="WantedRadiusNauticalMiles">Radius of the area visible on the map in nautical miles, before the position is rounded.</param>
    /// <param name="Sent">Area the last query sent to the aircraft sources covered after the rounding, or null when no query has been sent yet.</param>
    /// <param name="SentAtUtc">Moment of the last query, or null when no query has been sent yet.</param>
    /// <param name="FetchedCount">Number of the aircraft in the last download.</param>
    /// <param name="VisibleCount">Number of the aircraft that pass the filters.</param>
    /// <param name="LoadEndedUtc">Moment the last download ended, or null when none has ended yet.</param>
    /// <param name="SourceName">Name of the source of the aircraft positions.</param>
    /// <param name="RateLimitRemaining">Time left until the sources are asked again after a limit, or zero.</param>
    public sealed record DevConsoleSnapshot(
        double Zoom,
        double ZoomTarget,
        double CenterLatitude,
        double CenterLongitude,
        bool Following,
        int SurfaceWidth,
        int SurfaceHeight,
        bool GpsKnown,
        double GpsLatitude,
        double GpsLongitude,
        DateTime? GpsAtUtc,
        double WantedRadiusNauticalMiles,
        QueryArea? Sent,
        DateTime? SentAtUtc,
        int FetchedCount,
        int VisibleCount,
        DateTime? LoadEndedUtc,
        string SourceName,
        TimeSpan RateLimitRemaining);

    /// <summary>
    /// Builds the text of the developer console.
    /// </summary>
    public static class DevConsoleText
    {
        /// <summary>
        /// Builds the lines of the console. The numbers use the invariant culture, so the text can be pasted into a report.
        /// </summary>
        /// <param name="snapshot">State to describe.</param>
        /// <param name="nowUtc">Current moment, used for the ages.</param>
        /// <returns>Lines separated by line breaks.</returns>
        public static string Format(DevConsoleSnapshot snapshot, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            StringBuilder text = new StringBuilder();

            text.AppendLine(Loc.F("Zoom: {0} (cel {1})", Number(snapshot.Zoom, 2), Number(snapshot.ZoomTarget, 2)));
            text.AppendLine(Loc.F("Środek mapy: {0}", Point(snapshot.CenterLatitude, snapshot.CenterLongitude)));
            text.AppendLine(Loc.F("Podążanie: {0}", snapshot.Following ? Loc.T("tak") : Loc.T("nie")));
            text.AppendLine(Loc.F("Powierzchnia: {0} x {1} px", snapshot.SurfaceWidth.ToString(CultureInfo.InvariantCulture), snapshot.SurfaceHeight.ToString(CultureInfo.InvariantCulture)));

            if (snapshot.GpsKnown)
            {
                text.AppendLine(Loc.F("GPS: {0}{1}", Point(snapshot.GpsLatitude, snapshot.GpsLongitude), AgeSuffix(snapshot.GpsAtUtc, nowUtc)));
            }
            else
            {
                text.AppendLine(Loc.T("GPS: brak pozycji"));
            }

            text.AppendLine(Loc.F("Promień widoku: {0}", Radius(snapshot.WantedRadiusNauticalMiles)));

            if (snapshot.Sent is QueryArea sent)
            {
                double offset = GeoMath.DistanceKilometers(snapshot.CenterLatitude, snapshot.CenterLongitude, sent.Latitude, sent.Longitude);

                text.AppendLine(Loc.F("Wysłano do API: {0}{1}", Point(sent.Latitude, sent.Longitude), AgeSuffix(snapshot.SentAtUtc, nowUtc)));
                text.AppendLine(Loc.F("Promień wysłany: {0}", Radius(sent.RadiusNauticalMiles)));
                text.AppendLine(Loc.F("Przesunięcie względem środka mapy: {0} km", Number(offset, 2)));
            }
            else
            {
                text.AppendLine(Loc.T("Wysłano do API: jeszcze nie było zapytania"));
            }

            text.AppendLine(Loc.F("Samoloty: {0} pobranych, {1} widocznych", snapshot.FetchedCount.ToString(CultureInfo.InvariantCulture), snapshot.VisibleCount.ToString(CultureInfo.InvariantCulture)));
            text.AppendLine(Loc.F("Ostatnie pobranie: {0}", snapshot.LoadEndedUtc == null ? Loc.T("brak") : Age(snapshot.LoadEndedUtc, nowUtc)));
            text.AppendLine(Loc.F("Źródło pozycji: {0}", snapshot.SourceName));

            if (snapshot.RateLimitRemaining > TimeSpan.Zero)
            {
                text.AppendLine(Loc.F("Limit zapytań: wstrzymano na {0} s", Number(Math.Ceiling(snapshot.RateLimitRemaining.TotalSeconds), 0)));
            }
            else
            {
                text.AppendLine(Loc.T("Limit zapytań: brak"));
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// Formats a number with the invariant culture.
        /// </summary>
        /// <param name="value">Number.</param>
        /// <param name="decimals">Number of digits after the decimal point.</param>
        /// <returns>The text of the number.</returns>
        private static string Number(double value, int decimals)
        {
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a position with five digits after the decimal point, which is about one meter.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <returns>The text of the position.</returns>
        private static string Point(double latitude, double longitude)
        {
            return Number(latitude, 5) + ", " + Number(longitude, 5);
        }

        /// <summary>
        /// Formats a radius in both kilometers and nautical miles, because the settings use kilometers and the sources nautical miles.
        /// </summary>
        /// <param name="nauticalMiles">Radius in nautical miles.</param>
        /// <returns>The text of the radius.</returns>
        private static string Radius(double nauticalMiles)
        {
            return Number(nauticalMiles * CoordinateRounder.KilometersPerNauticalMile, 1) + " km (" + Number(nauticalMiles, 1) + " NM)";
        }

        /// <summary>
        /// Formats the time that has passed since a moment.
        /// </summary>
        /// <param name="momentUtc">Moment, or null when unknown.</param>
        /// <param name="nowUtc">Current moment.</param>
        /// <returns>The age, or an empty text when the moment is unknown.</returns>
        private static string Age(DateTime? momentUtc, DateTime nowUtc)
        {
            if (momentUtc == null)
            {
                return string.Empty;
            }

            double seconds = Math.Max(0.0, (nowUtc - momentUtc.Value).TotalSeconds);

            return Loc.F("{0} s temu", Number(Math.Floor(seconds), 0));
        }

        /// <summary>
        /// Formats the age to follow a value, in parentheses with a leading space.
        /// </summary>
        /// <param name="momentUtc">Moment, or null when unknown.</param>
        /// <param name="nowUtc">Current moment.</param>
        /// <returns>The age in parentheses with a leading space, or an empty text when the moment is unknown.</returns>
        private static string AgeSuffix(DateTime? momentUtc, DateTime nowUtc)
        {
            return momentUtc == null ? string.Empty : " (" + Age(momentUtc, nowUtc) + ")";
        }
    }
}

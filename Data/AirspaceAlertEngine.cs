using System.Globalization;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Settings of the notifications about airspace areas that become active near the user.
    /// </summary>
    /// <param name="Enabled">Whether the notifications are switched on.</param>
    /// <param name="RadiusKilometers">How far from the user an area may be, in kilometers; an area the user is inside is at distance zero.</param>
    /// <param name="LeadMinutes">How long before the start of a reservation the notification is shown; zero means only when the reservation is in force.</param>
    public sealed record AirspaceAlertOptions(bool Enabled, double RadiusKilometers, int LeadMinutes)
    {
        /// <summary>
        /// Smallest radius, in kilometers.
        /// </summary>
        public const double MinimumRadiusKilometers = 1.0;

        /// <summary>
        /// Largest radius, in kilometers.
        /// </summary>
        public const double MaximumRadiusKilometers = 1000.0;

        /// <summary>
        /// Largest lead time, in minutes.
        /// </summary>
        public const int MaximumLeadMinutes = 720;

        /// <summary>
        /// Options of a new installation: switched off, 100 km and 30 minutes.
        /// </summary>
        public static AirspaceAlertOptions Default => new AirspaceAlertOptions(false, 100.0, 30);

        /// <summary>
        /// Writes the options as text for the storage, e.g. "v1;1;100;30".
        /// </summary>
        /// <returns>Text that <see cref="Parse"/> reads back.</returns>
        public string Serialize()
        {
            return string.Join(";", "v1", Enabled ? "1" : "0", RadiusKilometers.ToString("0.###", CultureInfo.InvariantCulture), LeadMinutes.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Reads the options from the stored text.
        /// </summary>
        /// <param name="text">Text written by <see cref="Serialize"/>, or null.</param>
        /// <returns>The options; <see cref="Default"/> for missing or damaged text. A radius or a lead time out of range is moved to the nearest allowed value.</returns>
        public static AirspaceAlertOptions Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Default;
            }

            string[] parts = text.Split(';');

            if (parts.Length != 4 || parts[0] != "v1" || (parts[1] != "0" && parts[1] != "1")
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double radius) || double.IsNaN(radius)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int lead))
            {
                return Default;
            }

            return new AirspaceAlertOptions(parts[1] == "1", Math.Clamp(radius, MinimumRadiusKilometers, MaximumRadiusKilometers), Math.Min(lead, MaximumLeadMinutes));
        }
    }

    /// <summary>
    /// An airspace area that became active, or will soon, near the user.
    /// </summary>
    /// <param name="Zone">The area.</param>
    /// <param name="Reservation">The reservation that caused the notification.</param>
    /// <param name="DistanceKilometers">Distance from the user to the area; zero when the user is inside.</param>
    /// <param name="InForce">True when the reservation is in force now, false when it starts later.</param>
    public sealed record AirspaceAlert(AirspaceZone Zone, AirspaceReservation Reservation, double DistanceKilometers, bool InForce);

    /// <summary>
    /// Decides which airspace areas near the user need a notification: an area of a kind the user watches, reaching into the band of heights the user set, with a reservation confirmed as active that is in force or starts within the lead time. Every reservation is reported once; the memory of what was reported is kept in the object and can be saved and restored, so that a restart of the application does not repeat the notifications.
    /// </summary>
    public sealed class AirspaceAlertEngine
    {
        /// <summary>
        /// How long after the end of a reservation the memory of it is kept.
        /// </summary>
        private static readonly TimeSpan MemoryAfterEnd = TimeSpan.FromDays(1);

        /// <summary>
        /// Reservations already reported, by key, with the time they end.
        /// </summary>
        private readonly Dictionary<string, DateTime> reported;

        /// <summary>
        /// Creates an engine.
        /// </summary>
        /// <param name="restored">Memory saved earlier, or null when there is none.</param>
        public AirspaceAlertEngine(IEnumerable<KeyValuePair<string, DateTime>>? restored)
        {
            reported = restored == null ? new Dictionary<string, DateTime>() : new Dictionary<string, DateTime>(restored);
        }

        /// <summary>
        /// What was reported, to be saved.
        /// </summary>
        public IReadOnlyDictionary<string, DateTime> Reported => reported;

        /// <summary>
        /// Finds the reservations that need a notification now.
        /// </summary>
        /// <param name="zones">All the areas.</param>
        /// <param name="config">Choice of the kinds and of the band of heights; the states of the layer do not matter here.</param>
        /// <param name="position">Position of the user, or null when it is not known, which gives no notifications.</param>
        /// <param name="options">Settings of the notifications.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <param name="silentInForce">When true, reservations already in force are remembered without a notification. It is used at the first check after the notifications were switched on, so that the areas that have been active for hours do not all report at once.</param>
        /// <returns>The areas to notify about, the nearest first.</returns>
        public IReadOnlyList<AirspaceAlert> Evaluate(IEnumerable<AirspaceZone> zones, AirspaceLayerConfig config, GeoPoint? position, AirspaceAlertOptions options, DateTime nowUtc, bool silentInForce)
        {
            List<AirspaceAlert> alerts = new List<AirspaceAlert>();

            Prune(nowUtc);

            if (!options.Enabled || position == null)
            {
                return alerts;
            }

            DateTime horizon = nowUtc.AddMinutes(options.LeadMinutes);

            foreach (AirspaceZone zone in zones)
            {
                if (!config.IsTypeShown(zone.Type))
                {
                    continue;
                }

                double? distance = null;

                foreach (AirspaceReservation reservation in zone.Reservations)
                {
                    if (reservation.Status != AirspaceReservationStatus.Activated || reservation.EndUtc <= nowUtc || reservation.StartUtc > horizon || !config.PassesAltitude(reservation))
                    {
                        continue;
                    }

                    string key = zone.Designator + "|" + reservation.StartUtc.Ticks.ToString(CultureInfo.InvariantCulture);

                    if (reported.ContainsKey(key))
                    {
                        continue;
                    }

                    distance ??= AirspaceGeometry.DistanceKilometers(zone, position.Latitude, position.Longitude);

                    if (distance.Value > options.RadiusKilometers)
                    {
                        continue;
                    }

                    bool inForce = reservation.StartUtc <= nowUtc;

                    reported[key] = reservation.EndUtc;

                    if (!inForce || !silentInForce)
                    {
                        alerts.Add(new AirspaceAlert(zone, reservation, distance.Value, inForce));
                    }
                }
            }

            alerts.Sort((AirspaceAlert a, AirspaceAlert b) => a.DistanceKilometers.CompareTo(b.DistanceKilometers));

            return alerts;
        }

        /// <summary>
        /// Forgets the reservations that ended long ago.
        /// </summary>
        /// <param name="nowUtc">The moment, in UTC.</param>
        private void Prune(DateTime nowUtc)
        {
            List<string> expired = new List<string>();

            foreach (KeyValuePair<string, DateTime> entry in reported)
            {
                if (entry.Value + MemoryAfterEnd < nowUtc)
                {
                    expired.Add(entry.Key);
                }
            }

            foreach (string key in expired)
            {
                reported.Remove(key);
            }
        }
    }
}

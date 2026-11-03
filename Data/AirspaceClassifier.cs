using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Tells what state an airspace area is in at a given moment, from its reservations.
    /// </summary>
    public static class AirspaceClassifier
    {
        /// <summary>
        /// Finds the state of an area.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <returns>Active when a reservation confirmed as active is in force, planned when a reservation is announced for now or for later, otherwise inactive.</returns>
        public static AirspaceZoneState GetState(AirspaceZone zone, DateTime nowUtc)
        {
            bool planned = false;

            foreach (AirspaceReservation reservation in zone.Reservations)
            {
                if (IsActive(reservation, nowUtc))
                {
                    return AirspaceZoneState.Active;
                }

                if (reservation.EndUtc > nowUtc)
                {
                    planned = true;
                }
            }

            return planned ? AirspaceZoneState.Planned : AirspaceZoneState.Inactive;
        }

        /// <summary>
        /// Picks the reservation that best describes the area at the moment, for the card of the area.
        /// </summary>
        /// <param name="zone">The area.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <returns>For an active area the active reservation that ends first; for a planned area the one in force that is not confirmed, or else the one that starts first; for an inactive area the one that ended last; null when the area has no reservations.</returns>
        public static AirspaceReservation? GetRelevantReservation(AirspaceZone zone, DateTime nowUtc)
        {
            AirspaceReservation? best = null;

            foreach (AirspaceReservation reservation in zone.Reservations)
            {
                if (best == null || Rank(reservation, nowUtc) < Rank(best, nowUtc)
                    || (Rank(reservation, nowUtc) == Rank(best, nowUtc) && IsBetterInRank(reservation, best, nowUtc)))
                {
                    best = reservation;
                }
            }

            return best;
        }

        /// <summary>
        /// Tells whether a reservation is confirmed as active at the moment.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <returns>True when the reservation has the active state and the moment is in its period.</returns>
        public static bool IsActive(AirspaceReservation reservation, DateTime nowUtc)
        {
            return reservation.Status == AirspaceReservationStatus.Activated && reservation.StartUtc <= nowUtc && nowUtc < reservation.EndUtc;
        }

        /// <summary>
        /// Gives a reservation the importance for the card; a smaller number is more important.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <returns>0 for an active one, 1 for one in force that is not confirmed, 2 for one that starts later, 3 for one that is over.</returns>
        private static int Rank(AirspaceReservation reservation, DateTime nowUtc)
        {
            if (IsActive(reservation, nowUtc))
            {
                return 0;
            }

            if (reservation.EndUtc <= nowUtc)
            {
                return 3;
            }

            return reservation.StartUtc <= nowUtc ? 1 : 2;
        }

        /// <summary>
        /// Compares two reservations of the same importance.
        /// </summary>
        /// <param name="candidate">The reservation that may replace the best one.</param>
        /// <param name="best">The best reservation so far.</param>
        /// <param name="nowUtc">The moment, in UTC.</param>
        /// <returns>True when the candidate is better: one that ended later, or else one that ends or starts earlier.</returns>
        private static bool IsBetterInRank(AirspaceReservation candidate, AirspaceReservation best, DateTime nowUtc)
        {
            if (Rank(candidate, nowUtc) == 3)
            {
                return candidate.EndUtc > best.EndUtc;
            }

            if (Rank(candidate, nowUtc) == 2)
            {
                return candidate.StartUtc < best.StartUtc;
            }

            return candidate.EndUtc < best.EndUtc;
        }
    }
}

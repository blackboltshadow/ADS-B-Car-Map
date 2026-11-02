using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Enrichment
{
    /// <summary>
    /// Fills the descriptive data that one source did not give with the data from another source.
    /// </summary>
    public static class AircraftMerger
    {
        /// <summary>
        /// Checks whether the type code, the registration or the emitter category of the aircraft is missing. The category "A0" means that the aircraft does not say, so it counts as missing.
        /// </summary>
        /// <param name="aircraft">The aircraft to check.</param>
        /// <returns>True when at least one of the three is missing.</returns>
        public static bool HasGaps(Aircraft aircraft)
        {
            return string.IsNullOrEmpty(aircraft.TypeCode) || string.IsNullOrEmpty(aircraft.Registration) || IsCategoryMissing(aircraft.Category);
        }

        /// <summary>
        /// Returns the aircraft with the missing type code, registration and category taken from the details. Data that the aircraft already has is never replaced.
        /// </summary>
        /// <param name="aircraft">The aircraft from the main source.</param>
        /// <param name="details">Details from another source.</param>
        /// <returns>The same aircraft when nothing was added, otherwise a copy with the filled data.</returns>
        public static Aircraft FillMissing(Aircraft aircraft, AircraftDetails details)
        {
            string? typeCode = string.IsNullOrEmpty(aircraft.TypeCode) ? details.TypeCode : aircraft.TypeCode;
            string? registration = string.IsNullOrEmpty(aircraft.Registration) ? details.Registration : aircraft.Registration;
            string? category = IsCategoryMissing(aircraft.Category) && !IsCategoryMissing(details.Category) ? details.Category : aircraft.Category;

            if (typeCode == aircraft.TypeCode && registration == aircraft.Registration && category == aircraft.Category)
            {
                return aircraft;
            }

            return new Aircraft(
                aircraft.Hex,
                aircraft.Callsign,
                aircraft.Latitude,
                aircraft.Longitude,
                aircraft.AltitudeFeet,
                aircraft.OnGround,
                aircraft.GroundSpeedKnots,
                aircraft.TrackDegrees,
                aircraft.SecondsSincePosition,
                typeCode,
                registration,
                category,
                aircraft.IsMilitary,
                aircraft.VerticalRateFeetPerMinute,
                aircraft.Squawk);
        }

        /// <summary>
        /// Checks whether an emitter category says nothing.
        /// </summary>
        /// <param name="category">The category or null.</param>
        /// <returns>True for null, an empty text and "A0".</returns>
        private static bool IsCategoryMissing(string? category)
        {
            return string.IsNullOrEmpty(category) || category == "A0";
        }
    }
}

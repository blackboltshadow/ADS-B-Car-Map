using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Enrichment
{
    /// <summary>
    /// Fills the descriptive data that one source did not give with the data from another source.
    /// </summary>
    public static class AircraftMerger
    {
        /// <summary>
        /// Checks whether the type code, the registration or the full type name of the aircraft is missing, which is worth asking the other sources about. The emitter category is filled when a source happens to give it, but its absence alone does not cause a request, because most sources do not know it.
        /// </summary>
        /// <param name="aircraft">The aircraft to check.</param>
        /// <returns>True when at least one of the three is missing.</returns>
        public static bool HasGaps(Aircraft aircraft)
        {
            return string.IsNullOrEmpty(aircraft.TypeCode) || string.IsNullOrEmpty(aircraft.Registration) || string.IsNullOrEmpty(aircraft.TypeName);
        }

        /// <summary>
        /// Returns the aircraft with the missing descriptive data taken from the details. Data that the aircraft already has is never replaced. The category "A0" means that the aircraft does not say, so it counts as missing.
        /// </summary>
        /// <param name="aircraft">The aircraft from the main source.</param>
        /// <param name="details">Details from another source.</param>
        /// <returns>The same aircraft when nothing was added, otherwise a copy with the filled data.</returns>
        public static Aircraft FillMissing(Aircraft aircraft, AircraftDetails details)
        {
            string? typeCode = string.IsNullOrEmpty(aircraft.TypeCode) ? details.TypeCode : aircraft.TypeCode;
            string? registration = string.IsNullOrEmpty(aircraft.Registration) ? details.Registration : aircraft.Registration;
            string? category = IsCategoryMissing(aircraft.Category) && !IsCategoryMissing(details.Category) ? details.Category : aircraft.Category;
            string? typeName = string.IsNullOrEmpty(aircraft.TypeName) ? details.TypeName : aircraft.TypeName;
            string? manufacturer = string.IsNullOrEmpty(aircraft.Manufacturer) ? details.Manufacturer : aircraft.Manufacturer;
            string? owner = string.IsNullOrEmpty(aircraft.Owner) ? details.Owner : aircraft.Owner;

            if (typeCode == aircraft.TypeCode && registration == aircraft.Registration && category == aircraft.Category && typeName == aircraft.TypeName && manufacturer == aircraft.Manufacturer && owner == aircraft.Owner)
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
                aircraft.Squawk,
                typeName,
                manufacturer,
                owner);
        }

        /// <summary>
        /// Merges the details of two sources into one record, keeping what the first one already has.
        /// </summary>
        /// <param name="first">Details found first.</param>
        /// <param name="second">Details found later, used only for the missing fields.</param>
        /// <returns>The same record when the second adds nothing, otherwise a new record.</returns>
        public static AircraftDetails Combine(AircraftDetails first, AircraftDetails second)
        {
            AircraftDetails combined = new AircraftDetails(
                first.Hex,
                string.IsNullOrEmpty(first.TypeCode) ? second.TypeCode : first.TypeCode,
                string.IsNullOrEmpty(first.Registration) ? second.Registration : first.Registration,
                IsCategoryMissing(first.Category) && !IsCategoryMissing(second.Category) ? second.Category : first.Category,
                string.IsNullOrEmpty(first.TypeName) ? second.TypeName : first.TypeName,
                string.IsNullOrEmpty(first.Manufacturer) ? second.Manufacturer : first.Manufacturer,
                string.IsNullOrEmpty(first.Owner) ? second.Owner : first.Owner);

            return combined == first ? first : combined;
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

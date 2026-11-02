using AdsbCarMap.DataSources.Models;
using System.Runtime.CompilerServices;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Adds the aircraft kind, which is application logic and not part of the data source model.
    /// </summary>
    public static class AircraftKindExtensions
    {
        /// <summary>
        /// Kinds already determined, kept as long as the aircraft object lives.
        /// </summary>
        private static readonly ConditionalWeakTable<Aircraft, object> Kinds = new ConditionalWeakTable<Aircraft, object>();

        /// <summary>
        /// Returns the aircraft kind determined from the category, the military flag and the type code. The result is cached per aircraft object.
        /// </summary>
        /// <param name="aircraft">The aircraft.</param>
        /// <returns>The aircraft kind.</returns>
        public static AircraftKind GetKind(this Aircraft aircraft)
        {
            if (Kinds.TryGetValue(aircraft, out object? cached))
            {
                return (AircraftKind)cached;
            }

            AircraftKind kind = AircraftClassifier.Classify(aircraft);
            Kinds.TryAdd(aircraft, kind);

            return kind;
        }
    }
}

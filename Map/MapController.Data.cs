using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Management.Enrichment;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Connection of the map controller to the aircraft download loop: the query area, the stored aircraft and trails.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Returns the center and the radius in nautical miles of the area visible on the map.
        /// </summary>
        /// <returns>Center and radius of the area to download.</returns>
        private (double Latitude, double Longitude, double RadiusNauticalMiles) GetQueryArea()
        {
            lock (stateLock)
            {
                return (centerLatitude, centerLongitude, MapMath.CalculateRadiusNauticalMiles(centerLatitude, zoom, surfaceWidth, surfaceHeight));
            }
        }

        /// <summary>
        /// Tells whether all the military aircraft should be downloaded with one request: the mode that shows only military aircraft is on and the setting allows it.
        /// </summary>
        /// <returns>True when the request for all the military aircraft is wanted.</returns>
        private bool WantsGlobalMilitary()
        {
            bool militaryMode;

            lock (stateLock)
            {
                militaryMode = militaryOnly;
            }

            return MilitaryQueryPolicy.UseGlobalRequest(militaryMode, AppSettings.MilitaryGlobal);
        }

        /// <summary>
        /// Returns the hex code of the primary selected aircraft.
        /// </summary>
        /// <returns>Hex code, or null when nothing is selected.</returns>
        private string? GetPrimaryHex()
        {
            lock (stateLock)
            {
                return primaryHex;
            }
        }

        /// <summary>
        /// Returns the hex codes of all selected aircraft.
        /// </summary>
        /// <returns>A copy of the hex codes; empty when nothing is selected.</returns>
        private IReadOnlyCollection<string> GetSelectedHexes()
        {
            lock (stateLock)
            {
                return selectedHexes.ToList();
            }
        }

        /// <summary>
        /// Stores the downloaded aircraft and updates their trails.
        /// </summary>
        /// <param name="fetched">Downloaded aircraft.</param>
        /// <param name="fetchedAtUtc">Moment of the download, in UTC.</param>
        private void PublishAircraft(IReadOnlyList<Aircraft> fetched, DateTime fetchedAtUtc)
        {
            IReadOnlyDictionary<string, TrailPoint[]> previousTrails;

            lock (stateLock)
            {
                previousTrails = latestTrails;
            }

            IReadOnlyDictionary<string, TrailPoint[]> trails = TrailTracker.Update(previousTrails, fetched, fetchedAtUtc, AppSettings.TrailDuration, AppSettings.MaxTrailPoints);

            lock (stateLock)
            {
                latestAircraft = fetched;
                latestAircraftTimestampUtc = fetchedAtUtc;
                latestTrails = trails;
            }
        }

        /// <summary>
        /// Merges the completed data of an aircraft into the current list of aircraft.
        /// </summary>
        /// <param name="filled">Aircraft with the completed type, registration and category.</param>
        private void MergeAircraftDetails(Aircraft filled)
        {
            lock (stateLock)
            {
                List<Aircraft> list = new List<Aircraft>(latestAircraft);
                int index = list.FindIndex(item => item.Hex == filled.Hex);

                if (index >= 0)
                {
                    list[index] = AircraftMerger.FillMissing(list[index], new AircraftDetails(filled.Hex, filled.TypeCode, filled.Registration, filled.Category));
                    latestAircraft = list;
                }
            }
        }
    }
}

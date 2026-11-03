using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Data of the primary selected aircraft and of the user position that the aircraft card is built from.
    /// </summary>
    /// <param name="Hex">ICAO address of the aircraft.</param>
    /// <param name="Aircraft">Latest aircraft data, or null when it is not in the last server response, e.g. it flew out of range.</param>
    /// <param name="Latitude">Estimated current latitude of the aircraft. Meaningless when there is no aircraft data.</param>
    /// <param name="Longitude">Estimated current longitude of the aircraft. Meaningless when there is no aircraft data.</param>
    /// <param name="AgeSeconds">Age of the last server response in seconds.</param>
    /// <param name="HasUser">Whether the user position is known.</param>
    /// <param name="UserLatitude">Latitude of the user.</param>
    /// <param name="UserLongitude">Longitude of the user.</param>
    /// <param name="SelectedCount">Number of all selected aircraft.</param>
    public sealed record SelectedAircraftInfo(
        string Hex,
        Aircraft? Aircraft,
        double Latitude,
        double Longitude,
        double AgeSeconds,
        bool HasUser,
        double UserLatitude,
        double UserLongitude,
        int SelectedCount);
}

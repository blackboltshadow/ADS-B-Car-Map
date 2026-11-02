namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// Descriptive data of one aircraft that a source can return by its ICAO address, without the position.
    /// </summary>
    /// <param name="Hex">ICAO address in hexadecimal notation.</param>
    /// <param name="TypeCode">ICAO aircraft type code, e.g. "B738", or null when unknown.</param>
    /// <param name="Registration">Registration or null when unknown.</param>
    /// <param name="Category">ADS-B emitter category, e.g. "A3", or null when unknown.</param>
    /// <param name="TypeName">Full name of the aircraft type, e.g. "King Air B350", or null when unknown.</param>
    /// <param name="Manufacturer">Manufacturer of the aircraft or null when unknown.</param>
    /// <param name="Owner">Registered owner or operator of the aircraft or null when unknown.</param>
    public sealed record AircraftDetails(string Hex, string? TypeCode, string? Registration, string? Category, string? TypeName = null, string? Manufacturer = null, string? Owner = null);
}

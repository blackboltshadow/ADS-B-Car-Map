namespace AdsbCarMap.DataSources.Management.Privacy
{
    /// <summary>
    /// Circle sent to a source in a query for the aircraft.
    /// </summary>
    /// <param name="Latitude">Latitude of the center in degrees.</param>
    /// <param name="Longitude">Longitude of the center in degrees.</param>
    /// <param name="RadiusNauticalMiles">Radius in nautical miles.</param>
    public readonly record struct QueryArea(double Latitude, double Longitude, double RadiusNauticalMiles);
}

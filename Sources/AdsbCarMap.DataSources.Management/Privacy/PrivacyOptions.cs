namespace AdsbCarMap.DataSources.Management.Privacy
{
    /// <summary>
    /// How precisely the position of the map is sent to the servers of the aircraft sources.
    /// </summary>
    /// <param name="GridKilometers">Size of the grid to which the center of a query is snapped, in kilometers; zero or less sends the exact position.</param>
    /// <param name="RadiusStepNauticalMiles">Step to which the radius of a query is rounded up, in nautical miles; zero or less leaves the radius as it is.</param>
    /// <param name="ExpandRadius">Whether the radius grows by the largest possible distance between the real and the sent center, so that the sent circle still covers the visible area.</param>
    /// <param name="LogRequests">Whether every query is reported with the real and the sent position.</param>
    public sealed record PrivacyOptions(double GridKilometers, double RadiusStepNauticalMiles = 0.0, bool ExpandRadius = true, bool LogRequests = false)
    {
        /// <summary>
        /// Options that send the exact position.
        /// </summary>
        public static readonly PrivacyOptions Exact = new PrivacyOptions(0.0);
    }
}

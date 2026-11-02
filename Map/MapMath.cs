using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Pure calculations used by the map: aircraft dead reckoning and the download radius for the visible area.
    /// </summary>
    public static class MapMath
    {
        /// <summary>
        /// Smallest download radius in nautical miles.
        /// </summary>
        private const double MinRadiusNauticalMiles = 10.0;

        /// <summary>
        /// Estimates the current aircraft position from its last report, ground speed and track.
        /// </summary>
        /// <param name="item">Aircraft with its last reported position.</param>
        /// <param name="elapsedSeconds">Seconds since the report was received.</param>
        /// <returns>The reported position when the aircraft is on the ground or has no speed or track; otherwise the extrapolated position.</returns>
        public static (double Latitude, double Longitude) Extrapolate(Aircraft item, double elapsedSeconds)
        {
            if (item.OnGround || !item.GroundSpeedKnots.HasValue || !item.TrackDegrees.HasValue)
            {
                return (item.Latitude, item.Longitude);
            }

            double seconds = elapsedSeconds + item.SecondsSincePosition;
            double distanceNauticalMiles = item.GroundSpeedKnots.Value * seconds / 3600.0;
            double trackRadians = item.TrackDegrees.Value * Math.PI / 180.0;
            double cosineLatitude = Math.Max(0.1, Math.Cos(item.Latitude * Math.PI / 180.0));

            double latitudeDelta = distanceNauticalMiles * Math.Cos(trackRadians) / 60.0;
            double longitudeDelta = distanceNauticalMiles * Math.Sin(trackRadians) / (60.0 * cosineLatitude);

            return (item.Latitude + latitudeDelta, item.Longitude + longitudeDelta);
        }

        /// <summary>
        /// Calculates the radius that covers the whole visible area.
        /// </summary>
        /// <param name="latitude">Latitude of the view center.</param>
        /// <param name="zoomLevel">Map zoom level.</param>
        /// <param name="width">Surface width in pixels.</param>
        /// <param name="height">Surface height in pixels.</param>
        /// <returns>Half of the screen diagonal in nautical miles, at least 10.</returns>
        public static double CalculateRadiusNauticalMiles(double latitude, double zoomLevel, int width, int height)
        {
            double metersPerPixel = 156543.03392 * Math.Cos(latitude * Math.PI / 180.0) / Math.Pow(2.0, zoomLevel);
            double halfDiagonalPixels = Math.Sqrt((double)width * width + (double)height * height) / 2.0;

            return Math.Max(MinRadiusNauticalMiles, halfDiagonalPixels * metersPerPixel / 1852.0);
        }
    }
}

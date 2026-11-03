using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Connection of the map controller to the airspace layer: which areas are drawn and which one the user tapped.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Draws the airspace areas.
        /// </summary>
        private readonly AirspaceLayer airspaceLayer = new AirspaceLayer();

        /// <summary>
        /// Areas drawn in the last frame, used to recognize taps. Replaced as a whole.
        /// </summary>
        private IReadOnlyList<AirspaceZoneView> airspaceViews = Array.Empty<AirspaceZoneView>();

        /// <summary>
        /// List of the areas from which <see cref="airspaceViews"/> was selected.
        /// </summary>
        private IReadOnlyList<AirspaceZone>? airspaceZonesUsed;

        /// <summary>
        /// Configuration with which <see cref="airspaceViews"/> was selected.
        /// </summary>
        private AirspaceLayerConfig? airspaceConfigUsed;

        /// <summary>
        /// Minute (counted from the year 1) in which <see cref="airspaceViews"/> was selected; the states of the areas change with time, so the selection is repeated every minute.
        /// </summary>
        private long airspaceMinuteUsed = -1;

        /// <summary>
        /// Whether the map may draw the layer with the airspace areas, when the user switched it on in the settings. Only the phone map sets it; the car screen never shows the layer.
        /// </summary>
        public bool AirspacesAllowed { get; set; }

        /// <summary>
        /// Event raised, on the thread that handled the tap, when the user taps an airspace area.
        /// </summary>
        public event System.Action<AirspaceZoneView>? AirspaceTapped;

        /// <summary>
        /// Gives the areas to draw: the areas of the feed that pass the configuration of the layer. The selection is repeated only when the areas, the configuration or the minute changed.
        /// </summary>
        /// <returns>The areas with their states.</returns>
        private IReadOnlyList<AirspaceZoneView> GetAirspaceViews()
        {
            IReadOnlyList<AirspaceZone> zones = AppServices.Airspace.Zones;
            AirspaceLayerConfig config = AppServices.AirspaceConfig.Config;
            DateTime nowUtc = DateTime.UtcNow;
            long minute = nowUtc.Ticks / TimeSpan.TicksPerMinute;

            if (!ReferenceEquals(zones, airspaceZonesUsed) || !ReferenceEquals(config, airspaceConfigUsed) || minute != airspaceMinuteUsed)
            {
                Volatile.Write(ref airspaceViews, config.Select(zones, nowUtc));
                airspaceZonesUsed = zones;
                airspaceConfigUsed = config;
                airspaceMinuteUsed = minute;
            }

            return Volatile.Read(ref airspaceViews);
        }

        /// <summary>
        /// Forgets the areas drawn in the last frame, when the layer is no longer drawn.
        /// </summary>
        private void ClearAirspaceViews()
        {
            if (airspaceViews.Count > 0)
            {
                Volatile.Write(ref airspaceViews, Array.Empty<AirspaceZoneView>());
                airspaceZonesUsed = null;
            }
        }

        /// <summary>
        /// Finds the airspace area under a tap.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>The smallest drawn area under the tap, or null.</returns>
        private AirspaceZoneView? FindTappedAirspace(float x, float y)
        {
            IReadOnlyList<AirspaceZoneView> views = Volatile.Read(ref airspaceViews);

            if (views.Count == 0)
            {
                return null;
            }

            double tapLatitude;
            double tapLongitude;

            lock (stateLock)
            {
                (double X, double Y) center = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
                (double Latitude, double Longitude) point = WebMercator.WorldPixelToLatLon(center.X + x - surfaceWidth / 2.0, center.Y + y - surfaceHeight / 2.0, zoom);
                tapLatitude = point.Latitude;
                tapLongitude = point.Longitude;
            }

            tapLongitude = ((tapLongitude + 540.0) % 360.0) - 180.0;

            return AirspaceGeometry.FindAt(views, tapLatitude, tapLongitude);
        }
    }
}

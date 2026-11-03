using System.Globalization;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Map center, zoom and follow mode remembered between application runs, with the text form used for storage.
    /// </summary>
    public sealed class MapViewState
    {
        /// <summary>
        /// Separator between the values in the stored text.
        /// </summary>
        private const char Separator = ';';

        /// <summary>
        /// Number of values in the stored text.
        /// </summary>
        private const int ValueCount = 4;

        /// <summary>
        /// Creates a view state.
        /// </summary>
        /// <param name="latitude">Latitude of the map center.</param>
        /// <param name="longitude">Longitude of the map center.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="followLocation">Whether the map center followed the user position.</param>
        public MapViewState(double latitude, double longitude, double zoom, bool followLocation)
        {
            Latitude = latitude;
            Longitude = longitude;
            Zoom = zoom;
            FollowLocation = followLocation;
        }

        /// <summary>
        /// Latitude of the map center.
        /// </summary>
        public double Latitude { get; }

        /// <summary>
        /// Longitude of the map center.
        /// </summary>
        public double Longitude { get; }

        /// <summary>
        /// Zoom level.
        /// </summary>
        public double Zoom { get; }

        /// <summary>
        /// Whether the map center followed the user position.
        /// </summary>
        public bool FollowLocation { get; }

        /// <summary>
        /// Converts the state to the stored text.
        /// </summary>
        /// <returns>Text with the values in the invariant culture.</returns>
        public string Format()
        {
            return string.Join(Separator, Latitude.ToString("R", CultureInfo.InvariantCulture), Longitude.ToString("R", CultureInfo.InvariantCulture), Zoom.ToString("R", CultureInfo.InvariantCulture), FollowLocation ? "1" : "0");
        }

        /// <summary>
        /// Reads the state from the stored text. Values outside the valid range or missing parts make the text invalid.
        /// </summary>
        /// <param name="text">Stored text, or null.</param>
        /// <param name="minimumZoom">Smallest allowed zoom. A stored zoom outside the allowed range is moved into it.</param>
        /// <param name="maximumZoom">Largest allowed zoom.</param>
        /// <returns>The state, or null when the text is missing or invalid.</returns>
        public static MapViewState? TryParse(string? text, double minimumZoom, double maximumZoom)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string[] parts = text.Split(Separator);

            if (parts.Length != ValueCount)
            {
                return null;
            }

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double zoom))
            {
                return null;
            }

            if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || !double.IsFinite(zoom)
                || Math.Abs(latitude) > WebMercator.MaxLatitude || Math.Abs(longitude) > 180.0)
            {
                return null;
            }

            if (parts[3] != "0" && parts[3] != "1")
            {
                return null;
            }

            double low = Math.Min(minimumZoom, maximumZoom);
            double high = Math.Max(minimumZoom, maximumZoom);

            return new MapViewState(latitude, longitude, Math.Clamp(zoom, low, high), parts[3] == "1");
        }
    }
}

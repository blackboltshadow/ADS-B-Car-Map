using Android.Graphics;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Colors of the altitude scale shared by the map (icons, trails, legend) and the scale editor on the settings screen.
    /// </summary>
    public static class AltitudePalette
    {
        /// <summary>
        /// Color of an aircraft on the ground, which is also the zero point of the altitude scale.
        /// </summary>
        public static readonly Color GroundColor = Color.Rgb(150, 150, 150);

        /// <summary>
        /// Colors of an airborne aircraft matching the altitude thresholds from <see cref="Settings.AppSettings.AltitudeStopsMeters"/>: orange, yellow, green, blue, purple.
        /// </summary>
        public static readonly Color[] StopColors = new Color[]
        {
            Color.Rgb(255, 140, 0),
            Color.Rgb(255, 220, 0),
            Color.Rgb(60, 200, 60),
            Color.Rgb(40, 130, 255),
            Color.Rgb(170, 70, 230),
        };

        /// <summary>
        /// Names of the thresholds shown on the settings screen, in the order of <see cref="StopColors"/>.
        /// </summary>
        public static readonly string[] StopNames = new string[]
        {
            "Pomarańczowy do",
            "Żółty",
            "Zielony",
            "Niebieski",
            "Fioletowy od",
        };

        /// <summary>
        /// Returns the color of an altitude: orange up to the first threshold, purple above the last one, and a linear blend between the neighboring thresholds in between.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="stops">Altitude thresholds in meters, sorted ascending, one for each color of <see cref="StopColors"/>.</param>
        /// <returns>Color of the altitude.</returns>
        public static Color GetColor(double meters, double[] stops)
        {
            AltitudeBlend blend = AltitudeBlend.Locate(meters, stops, StopColors.Length);

            if (blend.FromIndex == blend.ToIndex)
            {
                return StopColors[blend.FromIndex];
            }

            Color from = StopColors[blend.FromIndex];
            Color to = StopColors[blend.ToIndex];

            return Color.Rgb(
                AltitudeBlend.BlendChannel(from.R, to.R, blend.Fraction),
                AltitudeBlend.BlendChannel(from.G, to.G, blend.Fraction),
                AltitudeBlend.BlendChannel(from.B, to.B, blend.Fraction));
        }
    }
}

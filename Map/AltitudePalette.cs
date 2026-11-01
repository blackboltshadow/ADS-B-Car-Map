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
            if (meters <= stops[0])
            {
                return StopColors[0];
            }

            for (int index = 1; index < stops.Length; index++)
            {
                if (meters <= stops[index])
                {
                    double span = stops[index] - stops[index - 1];

                    if (span <= 0.0)
                    {
                        return StopColors[index];
                    }

                    double fraction = (meters - stops[index - 1]) / span;
                    Color from = StopColors[index - 1];
                    Color to = StopColors[index];

                    return Color.Rgb(
                        (int)Math.Round(from.R + (to.R - from.R) * fraction),
                        (int)Math.Round(from.G + (to.G - from.G) * fraction),
                        (int)Math.Round(from.B + (to.B - from.B) * fraction));
                }
            }

            return StopColors[StopColors.Length - 1];
        }
    }
}

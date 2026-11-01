namespace AdsbCarMap.Map
{
    /// <summary>
    /// Position of an altitude on the color scale: the colors to blend and how far the altitude is from the first one to the second one.
    /// </summary>
    /// <param name="FromIndex">Index of the first color.</param>
    /// <param name="ToIndex">Index of the second color, equal to <paramref name="FromIndex"/> when no blending is needed.</param>
    /// <param name="Fraction">Share of the second color from 0 to 1.</param>
    public readonly record struct AltitudeBlend(int FromIndex, int ToIndex, double Fraction)
    {
        /// <summary>
        /// Finds the colors of the scale that an altitude lies between: the first color up to the first threshold, the last color above the last one, and a linear blend between the neighboring thresholds in between.
        /// </summary>
        /// <param name="meters">Altitude in meters.</param>
        /// <param name="stops">Altitude thresholds in meters, sorted ascending, one for each color.</param>
        /// <param name="colorCount">Number of colors of the scale.</param>
        /// <returns>Colors to blend and the share of the second one.</returns>
        public static AltitudeBlend Locate(double meters, double[] stops, int colorCount)
        {
            if (meters <= stops[0])
            {
                return new AltitudeBlend(0, 0, 0.0);
            }

            for (int index = 1; index < stops.Length; index++)
            {
                if (meters <= stops[index])
                {
                    double span = stops[index] - stops[index - 1];

                    if (span <= 0.0)
                    {
                        return new AltitudeBlend(index, index, 0.0);
                    }

                    return new AltitudeBlend(index - 1, index, (meters - stops[index - 1]) / span);
                }
            }

            return new AltitudeBlend(colorCount - 1, colorCount - 1, 0.0);
        }

        /// <summary>
        /// Blends one color channel linearly.
        /// </summary>
        /// <param name="from">Value of the first color, 0 to 255.</param>
        /// <param name="to">Value of the second color, 0 to 255.</param>
        /// <param name="fraction">Share of the second color from 0 to 1.</param>
        /// <returns>Blended value, rounded.</returns>
        public static int BlendChannel(int from, int to, double fraction)
        {
            return (int)Math.Round(from + (to - from) * fraction);
        }
    }
}

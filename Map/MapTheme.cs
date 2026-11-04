namespace AdsbCarMap.Map
{
    /// <summary>
    /// Pure logic of the map theme: decides whether the tiles are drawn dark and provides the color matrix that darkens them.
    /// </summary>
    public static class MapTheme
    {
        /// <summary>
        /// Index of the theme that follows the system (phone) or the host (car) night mode.
        /// </summary>
        public const int Automatic = 0;

        /// <summary>
        /// Index of the theme that always draws light tiles.
        /// </summary>
        public const int Light = 1;

        /// <summary>
        /// Index of the theme that always draws dark tiles.
        /// </summary>
        public const int Dark = 2;

        /// <summary>
        /// Brightness of the darkened tiles; below 1 so that white map features do not glare at night.
        /// </summary>
        private const float DarkBrightness = 0.85f;

        /// <summary>
        /// Decides whether the tiles should be drawn dark.
        /// </summary>
        /// <param name="themeIndex">Index of the chosen theme.</param>
        /// <param name="systemDark">Whether the system or the car host is currently in night mode.</param>
        /// <returns>True when the dark tiles should be used.</returns>
        public static bool IsDark(int themeIndex, bool systemDark)
        {
            switch (themeIndex)
            {
                case Light:
                    return false;
                case Dark:
                    return true;
                default:
                    return systemDark;
            }
        }

        /// <summary>
        /// Creates the 4x5 color matrix that inverts the lightness of a tile and keeps its hues (invert followed by a 180 degree hue rotation).
        /// </summary>
        /// <returns>Twenty values of the matrix in row-major order.</returns>
        public static float[] CreateDarkMatrix()
        {
            float k = DarkBrightness;
            float offset = 255.0f * k;

            return new float[]
            {
                0.574f * k, -1.430f * k, -0.144f * k, 0.0f, offset,
                -0.426f * k, -0.430f * k, -0.144f * k, 0.0f, offset,
                -0.426f * k, -1.430f * k, 0.856f * k, 0.0f, offset,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f
            };
        }
    }
}

namespace AdsbCarMap.Map
{
    /// <summary>
    /// How the map is turned on the screen.
    /// </summary>
    public enum MapOrientation
    {
        /// <summary>
        /// North is at the top of the screen.
        /// </summary>
        North,

        /// <summary>
        /// The direction of travel of the user is at the top of the screen.
        /// </summary>
        Heading,
    }

    /// <summary>
    /// Pure calculations of the rotation of the map: angles, vectors and the easing of the heading. Angles are in degrees and grow clockwise on the screen, like the rotation of the Android canvas.
    /// </summary>
    public static class MapRotation
    {
        /// <summary>
        /// Speed in meters per second below which the bearing reported by the location is not trusted, because it is mostly noise when the device stands still or crawls.
        /// </summary>
        public const double MinimumBearingSpeed = 1.5;

        /// <summary>
        /// Speed of the turn of the map to the new heading, per second; a higher value is faster.
        /// </summary>
        public const double EaseRatePerSecond = 4.0;

        /// <summary>
        /// Rotation below which the map counts as not rotated, in degrees.
        /// </summary>
        public const double RotationEpsilon = 0.05;

        /// <summary>
        /// Brings an angle to the range from 0 (inclusive) to 360 (exclusive).
        /// </summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>The same direction in the range from 0 to 360.</returns>
        public static double Normalize(double degrees)
        {
            double result = degrees % 360.0;

            return result < 0.0 ? result + 360.0 : result;
        }

        /// <summary>
        /// Finds the shortest signed turn from one angle to another.
        /// </summary>
        /// <param name="from">Starting angle in degrees.</param>
        /// <param name="to">Target angle in degrees.</param>
        /// <returns>Turn in degrees in the range from -180 (exclusive) to 180 (inclusive); positive is clockwise.</returns>
        public static double ShortestDelta(double from, double to)
        {
            double delta = Normalize(to - from);

            return delta > 180.0 ? delta - 360.0 : delta;
        }

        /// <summary>
        /// Turns an angle towards its target by the shortest way with an exponential ease.
        /// </summary>
        /// <param name="current">Current angle in degrees.</param>
        /// <param name="target">Target angle in degrees.</param>
        /// <param name="deltaSeconds">Time since the previous frame in seconds.</param>
        /// <returns>The new angle in the range from 0 to 360; it snaps to the target when it is closer than a twentieth of a degree.</returns>
        public static double Ease(double current, double target, double deltaSeconds)
        {
            double delta = ShortestDelta(current, target);

            if (Math.Abs(delta) < RotationEpsilon)
            {
                return Normalize(target);
            }

            return Normalize(current + (delta * (1.0 - Math.Exp(-EaseRatePerSecond * Math.Max(0.0, deltaSeconds)))));
        }

        /// <summary>
        /// Turns a vector clockwise on the screen.
        /// </summary>
        /// <param name="x">Horizontal component.</param>
        /// <param name="y">Vertical component, growing downwards.</param>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>The turned vector.</returns>
        public static (double X, double Y) RotateVector(double x, double y, double degrees)
        {
            if (degrees == 0.0)
            {
                return (x, y);
            }

            double radians = degrees * Math.PI / 180.0;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            return ((x * cosine) - (y * sine), (x * sine) + (y * cosine));
        }

        /// <summary>
        /// Decides whether the bearing reported by the location may be used to turn the map.
        /// </summary>
        /// <param name="hasBearing">Whether the location has a bearing at all.</param>
        /// <param name="speedMetersPerSecond">Speed of the device in meters per second.</param>
        /// <returns>True when the device moves fast enough for the bearing to be reliable.</returns>
        public static bool IsBearingReliable(bool hasBearing, double speedMetersPerSecond)
        {
            return hasBearing && speedMetersPerSecond >= MinimumBearingSpeed;
        }

        /// <summary>
        /// Finds the side of a square that contains the screen at any rotation, so that the tiles drawn into it still cover the screen after it is turned.
        /// </summary>
        /// <param name="width">Screen width in pixels.</param>
        /// <param name="height">Screen height in pixels.</param>
        /// <returns>Length of the screen diagonal in pixels, rounded up.</returns>
        public static int GetCoveringSize(int width, int height)
        {
            return (int)Math.Ceiling(Math.Sqrt(((double)width * width) + ((double)height * height)));
        }
    }
}

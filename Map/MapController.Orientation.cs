using AdsbCarMap.Settings;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Orientation of the map: north at the top or the direction of travel at the top, with the compass needle.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Distance of the compass needle center from the left edge of the screen in dp.
        /// </summary>
        private const float CompassLeft = 36.0f;

        /// <summary>
        /// Distance of the compass needle center from the top edge of the screen in dp.
        /// </summary>
        private const float CompassTop = 96.0f;

        /// <summary>
        /// Radius of the compass needle plate in dp.
        /// </summary>
        private const float CompassRadius = 22.0f;

        /// <summary>
        /// Part of the screen height by which the user position is moved below the middle while the map is turned with the direction of travel, so that more of the road ahead is visible.
        /// </summary>
        private const double HeadingUserOffsetFraction = 0.22;

        /// <summary>
        /// Whether the map is turned so that the direction of travel is at the top. Guarded by the state lock.
        /// </summary>
        private bool headingUp;

        /// <summary>
        /// Direction of travel of the user in degrees from north, as reported by the last reliable location. Guarded by the state lock.
        /// </summary>
        private double bearingTarget;

        /// <summary>
        /// Bearing that is shown at the top of the screen in degrees from north; it turns towards the direction of travel, or towards north when the map is not turned. Guarded by the state lock.
        /// </summary>
        private double mapBearing;

        /// <summary>
        /// Distance in pixels by which the followed user is drawn below the middle of the screen. It moves towards its target a little every frame and is used only by the drawing thread.
        /// </summary>
        private double userFollowOffsetPixels;

        /// <summary>
        /// Paint used to fill the compass plate.
        /// </summary>
        private readonly Paint compassPlatePaint = CreatePointerPaint(Color.Argb(210, 24, 28, 34), Paint.Style.Fill!);

        /// <summary>
        /// Paint used to draw the north half of the compass needle.
        /// </summary>
        private readonly Paint compassNorthPaint = CreatePointerPaint(Color.Rgb(229, 57, 53), Paint.Style.Fill!);

        /// <summary>
        /// Paint used to draw the south half of the compass needle.
        /// </summary>
        private readonly Paint compassSouthPaint = CreatePointerPaint(Color.Argb(230, 235, 235, 235), Paint.Style.Fill!);

        /// <summary>
        /// Path of the compass needle halves, rebuilt in every frame.
        /// </summary>
        private readonly AndroidPath compassPath = new AndroidPath();

        /// <summary>
        /// Position of the compass needle drawn in the last frame, used to recognize taps, or null when it is not drawn.
        /// </summary>
        private PointerHit? compassHit;

        /// <summary>
        /// Event raised after the orientation of the map changes.
        /// </summary>
        public event System.Action? OrientationChanged;

        /// <summary>
        /// Whether the map is set to turn with the direction of travel.
        /// </summary>
        public bool IsHeadingUp
        {
            get
            {
                lock (stateLock)
                {
                    return headingUp;
                }
            }
        }

        /// <summary>
        /// Whether the map center follows the user position.
        /// </summary>
        public bool IsFollowingUser
        {
            get
            {
                lock (stateLock)
                {
                    return followLocation;
                }
            }
        }

        /// <summary>
        /// Sets how the map is turned. The map turns smoothly.
        /// </summary>
        /// <param name="orientation">The new orientation.</param>
        public void SetOrientation(MapOrientation orientation)
        {
            lock (stateLock)
            {
                if (headingUp == (orientation == MapOrientation.Heading))
                {
                    return;
                }

                headingUp = orientation == MapOrientation.Heading;
            }

            OrientationChanged?.Invoke();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Switches between north at the top and the direction of travel at the top.
        /// </summary>
        public void ToggleOrientation()
        {
            SetOrientation(IsHeadingUp ? MapOrientation.North : MapOrientation.Heading);
        }

        /// <summary>
        /// Cycles through the modes of the single center button: when the user position is not followed it starts following it, and when it is followed it switches between north at the top and the direction of travel at the top.
        /// </summary>
        public void CycleFollowOrientation()
        {
            if (!IsFollowingUser)
            {
                FollowUser();

                return;
            }

            ToggleOrientation();
        }

        /// <summary>
        /// Stores the direction of travel of the user when the location reports a reliable one.
        /// </summary>
        /// <param name="speedMetersPerSecond">Speed of the user in meters per second.</param>
        /// <param name="bearingDegrees">Direction of travel in degrees from north.</param>
        public void SetUserMotion(double speedMetersPerSecond, float bearingDegrees)
        {
            if (!MapRotation.IsBearingReliable(true, speedMetersPerSecond) || !float.IsFinite(bearingDegrees))
            {
                return;
            }

            lock (stateLock)
            {
                bearingTarget = MapRotation.Normalize(bearingDegrees);
            }
        }

        /// <summary>
        /// Turns the shown bearing towards its target. Call under the state lock.
        /// </summary>
        /// <param name="deltaSeconds">Time since the previous frame in seconds.</param>
        private void AdvanceBearing(double deltaSeconds)
        {
            mapBearing = MapRotation.Ease(mapBearing, headingUp ? bearingTarget : 0.0, deltaSeconds);
        }

        /// <summary>
        /// Finds the rotation of the map on the screen for the shown bearing.
        /// </summary>
        /// <param name="bearing">Bearing shown at the top of the screen in degrees.</param>
        /// <returns>Clockwise rotation in degrees, zero when the bearing is north.</returns>
        private static double GetRotation(double bearing)
        {
            double rotation = -MapRotation.ShortestDelta(0.0, bearing);

            return Math.Abs(rotation) < MapRotation.RotationEpsilon ? 0.0 : rotation;
        }

        /// <summary>
        /// Converts a shift on the screen to a shift on the map.
        /// </summary>
        /// <param name="screenX">Horizontal shift on the screen in pixels.</param>
        /// <param name="screenY">Vertical shift on the screen in pixels.</param>
        /// <param name="rotation">Clockwise rotation of the map in degrees.</param>
        /// <returns>The shift in world pixels.</returns>
        private static (double X, double Y) ScreenShiftToWorld(double screenX, double screenY, double rotation)
        {
            return MapRotation.RotateVector(screenX, screenY, -rotation);
        }

        /// <summary>
        /// Draws the compass needle that points to north when the map is turned, and remembers where it is for taps. A tap on it brings north back to the top.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="rotation">Clockwise rotation of the map in degrees.</param>
        /// <param name="screenDensity">Screen density factor.</param>
        private void DrawCompass(Canvas canvas, double rotation, float screenDensity)
        {
            Volatile.Write(ref compassHit, null);

            if (rotation == 0.0)
            {
                return;
            }

            float x = CompassLeft * screenDensity;
            float y = CompassTop * screenDensity;
            float radius = CompassRadius * screenDensity;
            (double X, double Y) north = MapRotation.RotateVector(0.0, -1.0, rotation);
            float directionX = (float)north.X;
            float directionY = (float)north.Y;
            float sideX = -directionY * radius * 0.28f;
            float sideY = directionX * radius * 0.28f;

            canvas.DrawCircle(x, y, radius, compassPlatePaint);

            compassPath.Reset();
            compassPath.MoveTo(x + directionX * radius * 0.78f, y + directionY * radius * 0.78f);
            compassPath.LineTo(x + sideX, y + sideY);
            compassPath.LineTo(x - sideX, y - sideY);
            compassPath.Close();
            canvas.DrawPath(compassPath, compassNorthPaint);

            compassPath.Reset();
            compassPath.MoveTo(x - directionX * radius * 0.78f, y - directionY * radius * 0.78f);
            compassPath.LineTo(x + sideX, y + sideY);
            compassPath.LineTo(x - sideX, y - sideY);
            compassPath.Close();
            canvas.DrawPath(compassPath, compassSouthPaint);

            Volatile.Write(ref compassHit, new PointerHit(string.Empty, x, y));
        }

        /// <summary>
        /// Finds whether a tap hit the compass needle.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>True when the needle was hit.</returns>
        private bool IsCompassTapped(float x, float y)
        {
            PointerHit? hit = Volatile.Read(ref compassHit);

            if (hit == null)
            {
                return false;
            }

            float reach = (CompassRadius + 8.0f) * density;

            return (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y) <= reach * reach;
        }
    }
}

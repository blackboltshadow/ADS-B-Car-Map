namespace AdsbCarMap.Map
{
    /// <summary>
    /// Finds where a pointer to an aircraft that is outside the visible part of the map sits on the edge of that part.
    /// </summary>
    public static class OffscreenPointer
    {
        /// <summary>
        /// Result of the pointer placement.
        /// </summary>
        /// <param name="X">Horizontal position of the pointer center in pixels.</param>
        /// <param name="Y">Vertical position of the pointer center in pixels.</param>
        /// <param name="AngleDegrees">Direction towards the aircraft in degrees clockwise from the top of the screen.</param>
        public readonly record struct Placement(float X, float Y, float AngleDegrees);

        /// <summary>
        /// Places the pointer on the edge of the visible part of the map.
        /// </summary>
        /// <param name="targetX">Horizontal position of the aircraft in pixels.</param>
        /// <param name="targetY">Vertical position of the aircraft in pixels.</param>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="visibleTop">Top edge in pixels below which the pointer is placed, so that it stays clear of the top buttons; an aircraft above it but on the screen still counts as visible.</param>
        /// <param name="visibleBottom">Bottom edge of the visible part in pixels, above a card or popup.</param>
        /// <param name="margin">Distance of the pointer center from the edges in pixels.</param>
        /// <returns>The placement, or null when the aircraft is visible or the visible part is too small.</returns>
        public static Placement? Place(double targetX, double targetY, double width, double visibleTop, double visibleBottom, double margin)
        {
            double left = margin;
            double right = width - margin;
            double top = visibleTop + margin;
            double bottom = visibleBottom - margin;

            if (right <= left || bottom <= top)
            {
                return null;
            }

            if (targetX >= 0.0 && targetX <= width && targetY >= 0.0 && targetY <= visibleBottom)
            {
                return null;
            }

            double centerX = (left + right) / 2.0;
            double centerY = (top + bottom) / 2.0;
            double deltaX = targetX - centerX;
            double deltaY = targetY - centerY;
            double scaleX = deltaX == 0.0 ? double.PositiveInfinity : (right - left) / 2.0 / Math.Abs(deltaX);
            double scaleY = deltaY == 0.0 ? double.PositiveInfinity : (bottom - top) / 2.0 / Math.Abs(deltaY);
            double scale = Math.Min(scaleX, scaleY);

            if (double.IsInfinity(scale))
            {
                return null;
            }

            double angle = Math.Atan2(deltaX, -deltaY) * 180.0 / Math.PI;

            if (angle < 0.0)
            {
                angle += 360.0;
            }

            return new Placement((float)(centerX + deltaX * scale), (float)(centerY + deltaY * scale), (float)angle);
        }
    }
}

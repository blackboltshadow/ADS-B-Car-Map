namespace AdsbCarMap.Map
{
    /// <summary>
    /// Decides where on the screen a followed aircraft is kept, so that it stays visible above a card or a popup that covers the bottom of the map.
    /// </summary>
    public static class FollowFraming
    {
        /// <summary>
        /// Smallest part of the screen width that a view must cover to count as a bottom sheet.
        /// </summary>
        public const double MinimumWidthFraction = 0.5;

        /// <summary>
        /// Part of the screen height from which a view counts as touching the bottom edge.
        /// </summary>
        public const double BottomEdgeFraction = 0.9;

        /// <summary>
        /// Smallest part of the screen height that a view must cover to count as a side sheet.
        /// </summary>
        public const double SideSheetHeightFraction = 0.5;

        /// <summary>
        /// Part of the screen width from which a view no longer counts as a side sheet, because it covers nearly the whole width.
        /// </summary>
        public const double SideSheetMaximumWidthFraction = 0.9;

        /// <summary>
        /// Smallest distance of the aircraft from the top edge, as a part of the screen height, so that it never sits under the top buttons.
        /// </summary>
        public const double MinimumTopFraction = 0.12;

        /// <summary>
        /// Speed of the move of the map to the new position, per second; a higher value is faster.
        /// </summary>
        public const double EaseRatePerSecond = 10.0;

        /// <summary>
        /// Finds the vertical position at which the followed aircraft should be drawn.
        /// </summary>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="height">Height of the map in pixels.</param>
        /// <param name="covered">Rectangles of the map covered by views, as left, top, right and bottom; only wide views that touch the bottom edge count.</param>
        /// <returns>Position in pixels from the top edge: the middle of the visible part above the highest covering view, or the middle of the screen when nothing covers the bottom.</returns>
        public static double GetTargetY(double width, double height, IEnumerable<(double Left, double Top, double Right, double Bottom)> covered)
        {
            double middle = height / 2.0;
            double highestTop = GetVisibleBottom(width, height, covered);

            if (highestTop >= height)
            {
                return middle;
            }

            return Math.Min(middle, Math.Max(highestTop / 2.0, height * MinimumTopFraction));
        }

        /// <summary>
        /// Finds the horizontal position at which the followed aircraft should be drawn, so that it stays in the middle of the part of the map that a card on the left or the right side leaves free.
        /// </summary>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="height">Height of the map in pixels.</param>
        /// <param name="covered">Rectangles of the map covered by views, as left, top, right and bottom; only tall views that do not span the whole width and are not bottom sheets count as side sheets.</param>
        /// <returns>Position in pixels from the left edge: the middle of the free part, or the middle of the screen when no side sheet covers the map.</returns>
        public static double GetTargetX(double width, double height, IEnumerable<(double Left, double Top, double Right, double Bottom)> covered)
        {
            double freeLeft = 0.0;
            double freeRight = width;

            foreach ((double Left, double Top, double Right, double Bottom) rectangle in covered)
            {
                double rectangleWidth = rectangle.Right - rectangle.Left;
                bool bottomSheet = rectangleWidth >= width * MinimumWidthFraction && rectangle.Bottom >= height * BottomEdgeFraction;
                bool tall = rectangle.Bottom - rectangle.Top >= height * SideSheetHeightFraction;

                if (bottomSheet || !tall || rectangleWidth >= width * SideSheetMaximumWidthFraction)
                {
                    continue;
                }

                if (rectangle.Left <= width * (1.0 - BottomEdgeFraction))
                {
                    freeLeft = Math.Max(freeLeft, rectangle.Right);
                }
                else if (rectangle.Right >= width * BottomEdgeFraction)
                {
                    freeRight = Math.Min(freeRight, rectangle.Left);
                }
            }

            if (freeRight <= freeLeft)
            {
                return width / 2.0;
            }

            return (freeLeft + freeRight) / 2.0;
        }

        /// <summary>
        /// Finds the lower edge of the part of the map that is not covered by a bottom sheet.
        /// </summary>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="height">Height of the map in pixels.</param>
        /// <param name="covered">Rectangles of the map covered by views, as left, top, right and bottom; only wide views that touch the bottom edge count.</param>
        /// <returns>Position in pixels from the top edge: the top of the highest covering view, or the height when nothing covers the bottom.</returns>
        public static double GetVisibleBottom(double width, double height, IEnumerable<(double Left, double Top, double Right, double Bottom)> covered)
        {
            double highestTop = height;

            foreach ((double Left, double Top, double Right, double Bottom) rectangle in covered)
            {
                bool wide = rectangle.Right - rectangle.Left >= width * MinimumWidthFraction;
                bool touchesBottom = rectangle.Bottom >= height * BottomEdgeFraction;

                if (wide && touchesBottom && rectangle.Top > 0.0 && rectangle.Top < highestTop)
                {
                    highestTop = rectangle.Top;
                }
            }

            return highestTop;
        }

        /// <summary>
        /// Moves a value towards its target with an exponential ease, so that the map glides instead of jumping when a card appears.
        /// </summary>
        /// <param name="current">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <param name="deltaSeconds">Time since the previous frame in seconds.</param>
        /// <returns>The new value; it snaps to the target when it is closer than half a pixel.</returns>
        public static double Ease(double current, double target, double deltaSeconds)
        {
            double difference = target - current;

            if (Math.Abs(difference) < 0.5)
            {
                return target;
            }

            return current + (difference * (1.0 - Math.Exp(-EaseRatePerSecond * Math.Max(0.0, deltaSeconds))));
        }
    }
}

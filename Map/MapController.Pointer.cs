using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Pointer on the edge of the screen towards the primary selected aircraft that is outside the visible part of the map.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Radius of the pointer circle in dp.
        /// </summary>
        private const float PointerRadius = 22.0f;

        /// <summary>
        /// Paint used to fill the pointer circle.
        /// </summary>
        private readonly Paint pointerFillPaint = CreatePointerPaint(Color.Argb(225, 24, 28, 34), Paint.Style.Fill!);

        /// <summary>
        /// Paint used to draw the pointer outline and arrow.
        /// </summary>
        private readonly Paint pointerStrokePaint = CreatePointerPaint(SelectedAircraftColor, Paint.Style.Stroke!);

        /// <summary>
        /// Paint used to fill the pointer arrow.
        /// </summary>
        private readonly Paint pointerArrowPaint = CreatePointerPaint(SelectedAircraftColor, Paint.Style.Fill!);

        /// <summary>
        /// Paint used to draw the distance under the pointer.
        /// </summary>
        private readonly Paint pointerTextPaint = CreatePointerPaint(Color.White, Paint.Style.Fill!);

        /// <summary>
        /// Path of the pointer arrow, rebuilt in every frame.
        /// </summary>
        private readonly AndroidPath pointerArrowPath = new AndroidPath();

        /// <summary>
        /// Position of the pointer drawn in the last frame, used to recognize taps, or null when no pointer is drawn.
        /// </summary>
        private PointerHit? pointerHit;

        /// <summary>
        /// Position of the pointer on the screen and the aircraft it points to.
        /// </summary>
        /// <param name="Hex">ICAO address of the aircraft.</param>
        /// <param name="X">Horizontal position of the pointer center in pixels.</param>
        /// <param name="Y">Vertical position of the pointer center in pixels.</param>
        private sealed record PointerHit(string Hex, float X, float Y);

        /// <summary>
        /// Creates a paint of the pointer.
        /// </summary>
        /// <param name="color">Color of the paint.</param>
        /// <param name="style">Style of the paint.</param>
        /// <returns>The paint.</returns>
        private static Paint CreatePointerPaint(Color color, Paint.Style style)
        {
            Paint paint = new Paint(PaintFlags.AntiAlias);

            paint.Color = color;
            paint.SetStyle(style);
            paint.TextAlign = Paint.Align.Center;

            return paint;
        }

        /// <summary>
        /// Draws the pointer towards the primary selected aircraft when it is outside the visible part of the map, and remembers where it is for taps.
        /// </summary>
        /// <param name="canvas">Surface canvas.</param>
        /// <param name="target">Primary selected aircraft with its screen position, or null when there is none or it is not shown.</param>
        /// <param name="mapLatitude">Latitude of the map center.</param>
        /// <param name="mapLongitude">Longitude of the map center.</param>
        /// <param name="width">Width of the map in pixels.</param>
        /// <param name="height">Height of the map in pixels.</param>
        /// <param name="screenDensity">Screen density.</param>
        /// <param name="covering">Areas of the map covered by a card or popup.</param>
        private void DrawOffscreenPointer(Canvas canvas, (Aircraft Item, float X, float Y)? target, double mapLatitude, double mapLongitude, int width, int height, float screenDensity, List<(double Left, double Top, double Right, double Bottom)> covering)
        {
            Volatile.Write(ref pointerHit, null);

            if (target == null)
            {
                return;
            }

            double visibleBottom = FollowFraming.GetVisibleBottom(width, height, covering);
            float radius = PointerRadius * screenDensity;
            OffscreenPointer.Placement? placement = OffscreenPointer.Place(target.Value.X, target.Value.Y, width, height * FollowFraming.MinimumTopFraction, visibleBottom, radius + 6.0f * screenDensity);

            if (placement == null)
            {
                return;
            }

            float x = placement.Value.X;
            float y = placement.Value.Y;

            pointerStrokePaint.StrokeWidth = 2.0f * screenDensity;
            canvas.DrawCircle(x, y, radius, pointerFillPaint);
            canvas.DrawCircle(x, y, radius, pointerStrokePaint);

            double radians = placement.Value.AngleDegrees * Math.PI / 180.0;
            float directionX = (float)Math.Sin(radians);
            float directionY = (float)-Math.Cos(radians);
            float tipX = x + directionX * radius * 0.65f;
            float tipY = y + directionY * radius * 0.65f;
            float baseX = x - directionX * radius * 0.4f;
            float baseY = y - directionY * radius * 0.4f;
            float sideX = -directionY * radius * 0.45f;
            float sideY = directionX * radius * 0.45f;

            pointerArrowPath.Reset();
            pointerArrowPath.MoveTo(tipX, tipY);
            pointerArrowPath.LineTo(baseX + sideX, baseY + sideY);
            pointerArrowPath.LineTo(baseX - sideX, baseY - sideY);
            pointerArrowPath.Close();
            canvas.DrawPath(pointerArrowPath, pointerArrowPaint);

            double kilometers = GeoMath.DistanceKilometers(mapLatitude, mapLongitude, target.Value.Item.Latitude, target.Value.Item.Longitude);
            pointerTextPaint.TextSize = 11.0f * screenDensity;
            pointerTextPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

            float textY = directionY > 0.0f ? y - radius - 4.0f * screenDensity : y + radius + 12.0f * screenDensity;
            canvas.DrawText(UnitFormatter.FormatDistance(kilometers, AppSettings.Units.Distance, 0), x, textY, pointerTextPaint);

            Volatile.Write(ref pointerHit, new PointerHit(target.Value.Item.Hex, x, y));
        }

        /// <summary>
        /// Finds whether a tap hit the pointer towards the selected aircraft.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>ICAO address of the aircraft, or null when the pointer was not hit.</returns>
        private string? FindTappedPointer(float x, float y)
        {
            PointerHit? hit = Volatile.Read(ref pointerHit);

            if (hit == null)
            {
                return null;
            }

            float reach = (PointerRadius + 8.0f) * density;

            return (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y) <= reach * reach ? hit.Hex : null;
        }
    }
}

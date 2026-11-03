using AdsbCarMap.Localization;
using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Android.Content;
using Android.Graphics;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Preview of the trails of three aircraft (a drone, a helicopter and an airliner) on a dark background imitating the map. Trails are drawn by the same <see cref="TrailPainter"/> as on the map, with the trail values chosen on the settings screen, so the preview shows exactly what the map will show.
    /// </summary>
    public sealed class TrailsPreview : View
    {
        /// <summary>
        /// Number of points of every sample trail.
        /// </summary>
        private const int PointCount = 36;

        /// <summary>
        /// Height of one lane with a sample aircraft in dp.
        /// </summary>
        private const float LaneHeightDp = 58f;

        /// <summary>
        /// Kinds of the sample aircraft, from the top lane.
        /// </summary>
        private static readonly AircraftKind[] SampleKinds = new AircraftKind[] { AircraftKind.Drone, AircraftKind.Helicopter, AircraftKind.Airliner };

        /// <summary>
        /// Names of the sample aircraft shown in the lanes.
        /// </summary>
        private static readonly string[] SampleNames = new string[] { "Dron", "Śmigłowiec", "Samolot pasażerski" };

        /// <summary>
        /// Draws the trails.
        /// </summary>
        private readonly TrailPainter trailPainter = new TrailPainter();

        /// <summary>
        /// Draws the icons the same way as the map does.
        /// </summary>
        private readonly AircraftIconPainter iconPainter = new AircraftIconPainter(new AircraftShapes());

        /// <summary>
        /// Paint of the dark background imitating the map.
        /// </summary>
        private readonly Paint backgroundPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the lane names.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Rectangle reused when drawing the background.
        /// </summary>
        private readonly RectF bounds = new RectF();

        /// <summary>
        /// Width of the trail line in dp.
        /// </summary>
        public float TrailWidth { get; set; } = AppSettings.TrailWidth;

        /// <summary>
        /// Width of the dark trail outline on each side in dp.
        /// </summary>
        public float TrailOutlineWidth { get; set; } = AppSettings.TrailOutlineWidth;

        /// <summary>
        /// Opacity of the dark trail outline from 0 to 255.
        /// </summary>
        public int TrailOutlineAlpha { get; set; } = AppSettings.TrailOutlineAlpha;

        /// <summary>
        /// Opacity of the oldest trail segment from 0 to 255.
        /// </summary>
        public int TrailMinAlpha { get; set; } = AppSettings.TrailMinAlpha;

        /// <summary>
        /// Opacity of the newest trail segment from 0 to 255.
        /// </summary>
        public int TrailMaxAlpha { get; set; } = AppSettings.TrailMaxAlpha;

        /// <summary>
        /// Creates the preview.
        /// </summary>
        /// <param name="context">Context of the screen.</param>
        public TrailsPreview(Context context) : base(context)
        {
            backgroundPaint.Color = Color.Rgb(40, 44, 52);
            textPaint.Color = Color.Rgb(170, 170, 170);
        }

        /// <summary>
        /// Chooses the size of the view: full width and one lane for every sample aircraft.
        /// </summary>
        /// <param name="widthMeasureSpec">Width specification.</param>
        /// <param name="heightMeasureSpec">Height specification.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            SetMeasuredDimension(ResolveSize((int)Dp(240), widthMeasureSpec), (int)(SampleKinds.Length * Dp(LaneHeightDp) + Dp(8)));
        }

        /// <summary>
        /// Draws the lanes with the sample aircraft and their trails.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            double[] stops = AppSettings.AltitudeStopsMeters;
            float density = Resources?.DisplayMetrics?.Density ?? 1.0f;

            bounds.Set(0f, 0f, Width, Height);
            canvas.DrawRoundRect(bounds, Dp(8), Dp(8), backgroundPaint);

            textPaint.TextSize = Dp(10);

            for (int lane = 0; lane < SampleKinds.Length; lane++)
            {
                float centerY = Dp(4) + (lane + 0.5f) * Dp(LaneHeightDp);
                float left = Dp(16);
                float right = Width - Dp(36);
                float[] xs = new float[PointCount];
                float[] ys = new float[PointCount];
                Color[] colors = new Color[PointCount];

                for (int index = 0; index < PointCount; index++)
                {
                    float fraction = index / (float)(PointCount - 1);

                    xs[index] = left + fraction * (right - left);
                    ys[index] = centerY + Dp(12) * (float)Math.Sin(fraction * Math.PI * (lane + 1.5)) * (lane == 2 ? 0.6f : 1.0f);
                    colors[index] = AltitudePalette.GetColor(GetSampleAltitude(lane, fraction, stops), stops);
                }

                trailPainter.Draw(canvas, xs, ys, colors, TrailWidth * density, TrailOutlineWidth * density, TrailOutlineAlpha, TrailMinAlpha, TrailMaxAlpha);

                float directionX = xs[PointCount - 1] - xs[PointCount - 2];
                float directionY = ys[PointCount - 1] - ys[PointCount - 2];
                float rotation = (float)(Math.Atan2(directionX, -directionY) * 180.0 / Math.PI);

                DrawIcon(canvas, SampleKinds[lane], xs[PointCount - 1], ys[PointCount - 1], rotation, colors[PointCount - 1], density);
                canvas.DrawText(Loc.T(SampleNames[lane]), Dp(10), centerY - Dp(LaneHeightDp) / 2f + Dp(14), textPaint);
            }
        }

        /// <summary>
        /// Returns the altitude of a sample aircraft at a point of its trail, chosen from the current color thresholds so that every color range is shown whatever the thresholds are.
        /// </summary>
        /// <param name="lane">Index of the sample aircraft.</param>
        /// <param name="fraction">Position along the trail from 0 (oldest) to 1 (newest).</param>
        /// <param name="stops">Altitude thresholds in meters, sorted ascending.</param>
        /// <returns>Altitude in meters.</returns>
        private static double GetSampleAltitude(int lane, float fraction, double[] stops)
        {
            switch (lane)
            {
                case 0:
                    return stops[0] * 0.3 + (stops[1] - stops[0] * 0.3) * 0.6 * fraction;
                case 1:
                    return stops[1] + (stops[2] - stops[1]) * 1.1 * fraction;
                default:
                    return stops[4] * 1.15 * fraction;
            }
        }

        /// <summary>
        /// Draws an aircraft icon the same way as the map does: with the icon size and outline from the settings.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        /// <param name="kind">Kind of the aircraft.</param>
        /// <param name="x">Horizontal position of the icon center in pixels.</param>
        /// <param name="y">Vertical position of the icon center in pixels.</param>
        /// <param name="rotation">Heading in degrees clockwise from the top.</param>
        /// <param name="color">Fill color of the icon.</param>
        /// <param name="density">Screen density factor.</param>
        private void DrawIcon(Canvas canvas, AircraftKind kind, float x, float y, float rotation, Color color, float density)
        {
            iconPainter.Draw(canvas, kind, x, y, rotation, color, density, AppSettings.IconScale, AppSettings.AircraftOutlineWidth, AppSettings.AircraftShadowStrength, AppSettings.AircraftCockpitStrength);
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private float Dp(float value)
        {
            return value * (Resources?.DisplayMetrics?.Density ?? 1.0f);
        }
    }
}

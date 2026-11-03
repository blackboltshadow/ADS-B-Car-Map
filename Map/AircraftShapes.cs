using AdsbCarMap.Data;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Aircraft icon shapes by kind, in density-independent units, pointing north.
    /// </summary>
    public sealed class AircraftShapes
    {
        /// <summary>
        /// Describes an aircraft silhouette assembled from a fuselage, a pair of wings, a pair of tailplanes and engines.
        /// Y grows towards the tail, X towards the right wing.
        /// </summary>
        private sealed class Silhouette
        {
            /// <summary>
            /// Half of the fuselage width at its widest point.
            /// </summary>
            public float HalfWidth;

            /// <summary>
            /// Y of the nose tip.
            /// </summary>
            public float Top;

            /// <summary>
            /// Y of the tail end.
            /// </summary>
            public float Bottom;

            /// <summary>
            /// Length of the rounded nose section.
            /// </summary>
            public float NoseLength;

            /// <summary>
            /// Length of the tapering tail section.
            /// </summary>
            public float TailLength;

            /// <summary>
            /// Half of the fuselage width at the tail end.
            /// </summary>
            public float TailHalfWidth;

            /// <summary>
            /// Wing points of the right side: root leading edge Y, tip X, tip leading edge Y, tip trailing edge Y, root trailing edge Y.
            /// </summary>
            public float[] Wing = new float[0];

            /// <summary>
            /// Tailplane points of the right side, in the same order as the wing points.
            /// </summary>
            public float[] Tailplane = new float[0];

            /// <summary>
            /// Engine nacelles of the right side as groups of four values: center X, top Y, bottom Y, half width.
            /// </summary>
            public float[] Engines = new float[0];
        }

        /// <summary>
        /// Icon shape and its scale relative to the base size, by kind.
        /// </summary>
        private readonly Dictionary<AircraftKind, (AndroidPath Path, float Scale)> shapes = new Dictionary<AircraftKind, (AndroidPath Path, float Scale)>();

        /// <summary>
        /// Cockpit accent shape, by kind. Kinds without a canopy are missing.
        /// </summary>
        private readonly Dictionary<AircraftKind, AndroidPath> canopies = new Dictionary<AircraftKind, AndroidPath>();

        /// <summary>
        /// Creates all icon shapes.
        /// </summary>
        public AircraftShapes()
        {
            shapes[AircraftKind.Unknown] = (CreateArrow(), 1.0f);

            shapes[AircraftKind.Airliner] = (Build(AircraftKind.Airliner, new Silhouette
            {
                HalfWidth = 1.9f, Top = -15f, Bottom = 15f, NoseLength = 6f, TailLength = 9f, TailHalfWidth = 0.5f,
                Wing = new float[] { -3.5f, 14f, 5.5f, 8.2f, 3.8f },
                Tailplane = new float[] { 10f, 6f, 12.8f, 14.6f, 13.2f },
                Engines = new float[] { 5.6f, -1.8f, 2.6f, 0.95f }
            }), 1.0f);

            shapes[AircraftKind.Heavy] = (Build(AircraftKind.Heavy, new Silhouette
            {
                HalfWidth = 2.3f, Top = -16f, Bottom = 16f, NoseLength = 7f, TailLength = 10f, TailHalfWidth = 0.6f,
                Wing = new float[] { -4f, 15.5f, 6.5f, 9.2f, 4.6f },
                Tailplane = new float[] { 10.5f, 6.5f, 13.6f, 15.4f, 14f },
                Engines = new float[] { 5.4f, -2.6f, 2.4f, 1.0f, 9.4f, 0.6f, 4.6f, 1.0f }
            }), 1.25f);

            shapes[AircraftKind.SmallJet] = (Build(AircraftKind.SmallJet, new Silhouette
            {
                HalfWidth = 1.5f, Top = -12f, Bottom = 13.5f, NoseLength = 5.5f, TailLength = 7f, TailHalfWidth = 0.45f,
                Wing = new float[] { -1.5f, 9f, 4.8f, 7f, 3.6f },
                Tailplane = new float[] { 9.2f, 5f, 11.4f, 13f, 12.2f },
                Engines = new float[] { 2.9f, 6.5f, 12f, 0.85f }
            }), 0.9f);

            shapes[AircraftKind.Turboprop] = (Build(AircraftKind.Turboprop, new Silhouette
            {
                HalfWidth = 1.7f, Top = -12.5f, Bottom = 13.5f, NoseLength = 5f, TailLength = 8f, TailHalfWidth = 0.5f,
                Wing = new float[] { -3.6f, 12.8f, -3.6f, 0.2f, 0.2f },
                Tailplane = new float[] { 9.2f, 5.4f, 11.2f, 13.2f, 12.6f },
                Engines = new float[] { 4.8f, -5.2f, 0.8f, 0.85f }
            }), 0.95f);

            shapes[AircraftKind.LightPlane] = (Build(AircraftKind.LightPlane, new Silhouette
            {
                HalfWidth = 1.2f, Top = -10f, Bottom = 12f, NoseLength = 4f, TailLength = 7f, TailHalfWidth = 0.4f,
                Wing = new float[] { -3.2f, 10.5f, -3.2f, 0.4f, 0.4f },
                Tailplane = new float[] { 8.2f, 4.6f, 9.6f, 11.6f, 11f }
            }), 0.85f);

            shapes[AircraftKind.Fighter] = (Build(AircraftKind.Fighter, new Silhouette
            {
                HalfWidth = 1.3f, Top = -15f, Bottom = 13f, NoseLength = 8f, TailLength = 6f, TailHalfWidth = 0.9f,
                Wing = new float[] { -1f, 8.8f, 8.8f, 10.4f, 8f },
                Tailplane = new float[] { 8.5f, 4.8f, 11.8f, 13.4f, 12.4f }
            }), 0.95f);

            shapes[AircraftKind.MilitaryTransport] = (Build(AircraftKind.MilitaryTransport, new Silhouette
            {
                HalfWidth = 2.4f, Top = -14.5f, Bottom = 15f, NoseLength = 5.5f, TailLength = 6f, TailHalfWidth = 1.3f,
                Wing = new float[] { -3.2f, 15f, -0.6f, 3.4f, 3.4f },
                Tailplane = new float[] { 8.6f, 6.8f, 10.6f, 13.8f, 12.8f },
                Engines = new float[] { 5.2f, -3.6f, 0.4f, 0.8f, 9.8f, -2.9f, 1.1f, 0.8f }
            }), 1.1f);

            shapes[AircraftKind.Glider] = (Build(AircraftKind.Glider, new Silhouette
            {
                HalfWidth = 0.95f, Top = -8.5f, Bottom = 12.5f, NoseLength = 4.5f, TailLength = 9f, TailHalfWidth = 0.3f,
                Wing = new float[] { -1.6f, 17f, -1.1f, 1.3f, 1.8f },
                Tailplane = new float[] { 9.6f, 4.2f, 10.4f, 12.2f, 11.6f }
            }), 0.9f);

            shapes[AircraftKind.Helicopter] = (CreateHelicopter(), 0.95f);
            shapes[AircraftKind.Balloon] = (CreateBalloon(), 0.9f);
            shapes[AircraftKind.Drone] = (CreateDrone(), 0.8f);
            shapes[AircraftKind.Ground] = (CreateGround(), 0.7f);

            AndroidPath helicopterCanopy = new AndroidPath();
            helicopterCanopy.AddOval(new RectF(-1.7f, -5.8f, 1.7f, -1.4f), AndroidPath.Direction.Cw!);
            canopies[AircraftKind.Helicopter] = helicopterCanopy;

            AndroidPath droneCanopy = new AndroidPath();
            droneCanopy.AddCircle(0f, 0f, 1.7f, AndroidPath.Direction.Cw!);
            canopies[AircraftKind.Drone] = droneCanopy;
        }

        /// <summary>
        /// Returns the cockpit accent shape of an aircraft kind.
        /// </summary>
        /// <param name="kind">Aircraft kind.</param>
        /// <returns>Canopy path in the same coordinates as the icon, or null when the kind has no canopy.</returns>
        public AndroidPath? GetCanopy(AircraftKind kind)
        {
            return canopies.TryGetValue(kind, out AndroidPath? canopy) ? canopy : null;
        }

        /// <summary>
        /// Returns the icon shape and its scale for an aircraft kind.
        /// </summary>
        /// <param name="kind">Aircraft kind.</param>
        /// <returns>Icon path pointing north and the scale relative to the base size.</returns>
        public (AndroidPath Path, float Scale) Get(AircraftKind kind)
        {
            return shapes[kind];
        }

        /// <summary>
        /// Builds the generic arrow used when the kind cannot be determined.
        /// </summary>
        /// <returns>Arrow path.</returns>
        private static AndroidPath CreateArrow()
        {
            AndroidPath path = new AndroidPath();
            path.MoveTo(0f, -14f);
            path.LineTo(9f, 12f);
            path.LineTo(0f, 6f);
            path.LineTo(-9f, 12f);
            path.Close();

            return path;
        }

        /// <summary>
        /// Assembles a silhouette from its parts and stores its canopy. All parts are added clockwise, so overlapping parts merge into one shape.
        /// </summary>
        /// <param name="kind">Aircraft kind the silhouette is built for, under which the canopy is stored.</param>
        /// <param name="silhouette">Silhouette description.</param>
        /// <returns>Closed path of the whole aircraft.</returns>
        private AndroidPath Build(AircraftKind kind, Silhouette silhouette)
        {
            AndroidPath canopy = new AndroidPath();
            canopy.AddOval(new RectF(-silhouette.HalfWidth * 0.5f, silhouette.Top + silhouette.NoseLength * 0.7f, silhouette.HalfWidth * 0.5f, silhouette.Top + silhouette.NoseLength * 1.3f), AndroidPath.Direction.Cw!);
            canopies[kind] = canopy;

            AndroidPath path = new AndroidPath();

            AddMirroredPolygon(path, WingPolygon(silhouette.Wing, silhouette.HalfWidth * 0.6f));
            AddMirroredPolygon(path, WingPolygon(silhouette.Tailplane, silhouette.TailHalfWidth + 0.4f));

            for (int index = 0; index + 3 < silhouette.Engines.Length; index += 4)
            {
                float x = silhouette.Engines[index];
                float top = silhouette.Engines[index + 1];
                float bottom = silhouette.Engines[index + 2];
                float half = silhouette.Engines[index + 3];

                path.AddRoundRect(new RectF(x - half, top, x + half, bottom), half, half, AndroidPath.Direction.Cw!);
                path.AddRoundRect(new RectF(-x - half, top, -x + half, bottom), half, half, AndroidPath.Direction.Cw!);
            }

            AddFuselage(path, silhouette);

            return path;
        }

        /// <summary>
        /// Adds the fuselage: a rounded nose, a straight middle section and a tapering tail.
        /// </summary>
        /// <param name="path">Target path.</param>
        /// <param name="silhouette">Silhouette description.</param>
        private static void AddFuselage(AndroidPath path, Silhouette silhouette)
        {
            float half = silhouette.HalfWidth;
            float noseEnd = silhouette.Top + silhouette.NoseLength;
            float tailStart = silhouette.Bottom - silhouette.TailLength;
            float tail = silhouette.TailHalfWidth;

            path.MoveTo(0f, silhouette.Top);
            path.CubicTo(half * 0.75f, silhouette.Top + silhouette.NoseLength * 0.12f, half, silhouette.Top + silhouette.NoseLength * 0.55f, half, noseEnd);
            path.LineTo(half, tailStart);
            path.QuadTo(half * 0.95f, silhouette.Bottom - silhouette.TailLength * 0.3f, tail, silhouette.Bottom);
            path.LineTo(-tail, silhouette.Bottom);
            path.QuadTo(-half * 0.95f, silhouette.Bottom - silhouette.TailLength * 0.3f, -half, tailStart);
            path.LineTo(-half, noseEnd);
            path.CubicTo(-half, silhouette.Top + silhouette.NoseLength * 0.55f, -half * 0.75f, silhouette.Top + silhouette.NoseLength * 0.12f, 0f, silhouette.Top);
            path.Close();
        }

        /// <summary>
        /// Builds the right-side polygon of a wing or tailplane. Points are root leading edge Y, tip X, tip leading edge Y, tip trailing edge Y and root trailing edge Y.
        /// </summary>
        /// <param name="points">Five wing values.</param>
        /// <param name="rootX">X of the root, hidden under the fuselage.</param>
        /// <returns>Flat list of X and Y values, clockwise.</returns>
        private static float[] WingPolygon(float[] points, float rootX)
        {
            if (points.Length < 5)
            {
                return new float[0];
            }

            float tipX = points[1];
            float tipLead = points[2];
            float tipTrail = points[3];
            float rootTrail = points[4];
            float rounding = Math.Min(0.6f, (tipTrail - tipLead) / 2f);

            return new float[]
            {
                rootX, points[0],
                tipX - rounding, tipLead,
                tipX, tipLead + rounding,
                tipX, tipTrail - rounding,
                tipX - rounding, tipTrail,
                rootX, rootTrail
            };
        }

        /// <summary>
        /// Adds a polygon and its mirror image, keeping the clockwise direction of both.
        /// </summary>
        /// <param name="path">Target path.</param>
        /// <param name="rightSide">Flat list of X and Y values of the right-side polygon.</param>
        private static void AddMirroredPolygon(AndroidPath path, float[] rightSide)
        {
            int count = rightSide.Length / 2;

            if (count < 3)
            {
                return;
            }

            path.MoveTo(rightSide[0], rightSide[1]);

            for (int index = 1; index < count; index++)
            {
                path.LineTo(rightSide[2 * index], rightSide[2 * index + 1]);
            }

            path.Close();

            path.MoveTo(-rightSide[2 * (count - 1)], rightSide[2 * (count - 1) + 1]);

            for (int index = count - 2; index >= 0; index--)
            {
                path.LineTo(-rightSide[2 * index], rightSide[2 * index + 1]);
            }

            path.Close();
        }

        /// <summary>
        /// Builds a helicopter: a teardrop body, a tail boom with a tail rotor and two crossed rotor blades.
        /// </summary>
        /// <returns>Helicopter path.</returns>
        private static AndroidPath CreateHelicopter()
        {
            AndroidPath path = new AndroidPath();
            path.AddOval(new RectF(-3.2f, -7f, 3.2f, 5.5f), AndroidPath.Direction.Cw!);
            path.MoveTo(-1f, 3f);
            path.LineTo(-0.6f, 14f);
            path.LineTo(0.6f, 14f);
            path.LineTo(1f, 3f);
            path.Close();
            path.AddRoundRect(new RectF(-3f, 12f, 3f, 14f), 1f, 1f, AndroidPath.Direction.Cw!);

            AndroidPath blade = new AndroidPath();
            blade.AddRoundRect(new RectF(-12.5f, -0.8f, 12.5f, 0.8f), 0.8f, 0.8f, AndroidPath.Direction.Cw!);

            Matrix first = new Matrix();
            first.SetRotate(35f);
            path.AddPath(blade, first);

            Matrix second = new Matrix();
            second.SetRotate(125f);
            path.AddPath(blade, second);

            return path;
        }

        /// <summary>
        /// Builds a balloon: a teardrop envelope and a basket.
        /// </summary>
        /// <returns>Balloon path.</returns>
        private static AndroidPath CreateBalloon()
        {
            AndroidPath path = new AndroidPath();
            path.AddCircle(0f, -3f, 7f, AndroidPath.Direction.Cw!);
            path.MoveTo(-4.5f, 2f);
            path.LineTo(-1.8f, 7f);
            path.LineTo(1.8f, 7f);
            path.LineTo(4.5f, 2f);
            path.Close();
            path.AddRoundRect(new RectF(-2.2f, 6.5f, 2.2f, 10.5f), 0.8f, 0.8f, AndroidPath.Direction.Cw!);

            return path;
        }

        /// <summary>
        /// Builds a drone: four arms crossed at right angles, four rotors and a body.
        /// </summary>
        /// <returns>Drone path.</returns>
        private static AndroidPath CreateDrone()
        {
            AndroidPath path = new AndroidPath();
            path.AddCircle(0f, 0f, 3.5f, AndroidPath.Direction.Cw!);

            AndroidPath arm = new AndroidPath();
            arm.AddRect(-9f, -1.1f, 9f, 1.1f, AndroidPath.Direction.Cw!);

            Matrix first = new Matrix();
            first.SetRotate(45f);
            path.AddPath(arm, first);

            Matrix second = new Matrix();
            second.SetRotate(-45f);
            path.AddPath(arm, second);

            path.AddCircle(6.4f, 6.4f, 3f, AndroidPath.Direction.Cw!);
            path.AddCircle(-6.4f, 6.4f, 3f, AndroidPath.Direction.Cw!);
            path.AddCircle(6.4f, -6.4f, 3f, AndroidPath.Direction.Cw!);
            path.AddCircle(-6.4f, -6.4f, 3f, AndroidPath.Direction.Cw!);

            return path;
        }

        /// <summary>
        /// Builds a ground object marker as a small rounded square.
        /// </summary>
        /// <returns>Square path.</returns>
        private static AndroidPath CreateGround()
        {
            AndroidPath path = new AndroidPath();
            path.AddRoundRect(new RectF(-5f, -5f, 5f, 5f), 1.5f, 1.5f, AndroidPath.Direction.Cw!);

            return path;
        }
    }
}

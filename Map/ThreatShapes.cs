using AdsbCarMap.DataSources.Models;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Icon shapes of the threat kinds, in density-independent units, pointing north. They are built from the same parts as the aircraft icons (fuselage, wings, tailplanes, engines), so that both kinds of icons look like one family.
    /// </summary>
    public sealed class ThreatShapes
    {
        /// <summary>
        /// Icon shape and its scale relative to the base size, by kind.
        /// </summary>
        private readonly Dictionary<ThreatKind, (AndroidPath Path, float Scale)> shapes = new Dictionary<ThreatKind, (AndroidPath Path, float Scale)>();

        /// <summary>
        /// Shape of the four-rotor drone without a nose, used when its heading is not known, so that the icon does not point anywhere.
        /// </summary>
        private readonly AndroidPath fpvSymmetric = BuildFpv(false);

        /// <summary>
        /// Accent shape (a sensor dome or a cockpit), by kind. Kinds without an accent are missing.
        /// </summary>
        private readonly Dictionary<ThreatKind, AndroidPath> accents = new Dictionary<ThreatKind, AndroidPath>();

        /// <summary>
        /// Creates all icon shapes.
        /// </summary>
        public ThreatShapes()
        {
            shapes[ThreatKind.Uav] = (BuildUav(), 1.05f);
            shapes[ThreatKind.Fpv] = (BuildFpv(true), 0.85f);
            shapes[ThreatKind.Recon] = (BuildRecon(), 0.95f);
            shapes[ThreatKind.Missile] = (BuildMissile(), 1.0f);
            shapes[ThreatKind.Ballistic] = (BuildBallistic(), 1.1f);
            shapes[ThreatKind.Kab] = (BuildKab(), 0.95f);
            shapes[ThreatKind.Mig31k] = (BuildMig31k(), 1.15f);
            shapes[ThreatKind.Unknown] = (BuildUnknown(), 0.9f);
        }

        /// <summary>
        /// Returns the icon shape and its scale for a kind.
        /// </summary>
        /// <param name="kind">Kind of threat.</param>
        /// <param name="headingKnown">Whether the heading of the threat is known. The four-rotor drone is symmetric, so without a heading it is drawn without a nose.</param>
        /// <returns>Icon path pointing north and the scale relative to the base size.</returns>
        public (AndroidPath Path, float Scale) Get(ThreatKind kind, bool headingKnown = true)
        {
            if (kind == ThreatKind.Fpv && !headingKnown)
            {
                return (fpvSymmetric, shapes[ThreatKind.Fpv].Scale);
            }

            return shapes.TryGetValue(kind, out (AndroidPath Path, float Scale) shape) ? shape : shapes[ThreatKind.Unknown];
        }

        /// <summary>
        /// Returns the accent shape of a kind.
        /// </summary>
        /// <param name="kind">Kind of threat.</param>
        /// <returns>Accent path in the same coordinates as the icon, or null when the kind has none.</returns>
        public AndroidPath? GetAccent(ThreatKind kind)
        {
            return accents.TryGetValue(kind, out AndroidPath? accent) ? accent : null;
        }

        /// <summary>
        /// Builds a delta-wing drone with a pusher propeller: a slim body, a wide swept wing and small tail fins.
        /// </summary>
        /// <returns>Drone path.</returns>
        private static AndroidPath BuildUav()
        {
            AndroidPath path = Assemble(new AircraftShapes.Silhouette
            {
                HalfWidth = 1.2f, Top = -14f, Bottom = 12.5f, NoseLength = 6.5f, TailLength = 4f, TailHalfWidth = 0.9f,
                Wing = new float[] { -2.5f, 11.5f, 9.2f, 11.6f, 9.6f },
                Tailplane = new float[] { 9.5f, 4.2f, 10.8f, 12.8f, 12f }
            });

            path.AddRoundRect(new RectF(-2.6f, 12.1f, 2.6f, 13.5f), 0.7f, 0.7f, AndroidPath.Direction.Cw!);

            return path;
        }

        /// <summary>
        /// Builds a small four-rotor drone. A drone with a known heading gets an arrowhead nose between the front rotors, so that its direction is clear.
        /// </summary>
        /// <param name="withNose">Whether to add the arrowhead nose.</param>
        /// <returns>Drone path.</returns>
        private static AndroidPath BuildFpv(bool withNose)
        {
            AndroidPath path = new AndroidPath();
            path.AddRoundRect(new RectF(-2.3f, -4f, 2.3f, 4f), 1.6f, 1.6f, AndroidPath.Direction.Cw!);

            if (withNose)
            {
                path.MoveTo(0f, -14.5f);
                path.LineTo(3.6f, -8f);
                path.LineTo(-3.6f, -8f);
                path.Close();
            }

            AndroidPath arm = new AndroidPath();
            arm.AddRoundRect(new RectF(-9.2f, -0.95f, 9.2f, 0.95f), 0.9f, 0.9f, AndroidPath.Direction.Cw!);

            Matrix first = new Matrix();
            first.SetRotate(45f);
            path.AddPath(arm, first);

            Matrix second = new Matrix();
            second.SetRotate(-45f);
            path.AddPath(arm, second);

            foreach ((float X, float Y) rotor in new (float X, float Y)[] { (6.5f, 6.5f), (-6.5f, 6.5f), (6.5f, -6.5f), (-6.5f, -6.5f) })
            {
                path.AddCircle(rotor.X, rotor.Y, 3.1f, AndroidPath.Direction.Cw!);
            }

            return path;
        }

        /// <summary>
        /// Builds a reconnaissance drone and stores its sensor dome.
        /// </summary>
        /// <returns>Drone path.</returns>
        private AndroidPath BuildRecon()
        {
            AndroidPath path = Assemble(new AircraftShapes.Silhouette
            {
                HalfWidth = 1.05f, Top = -10f, Bottom = 11.5f, NoseLength = 4.5f, TailLength = 6f, TailHalfWidth = 0.4f,
                Wing = new float[] { -2.8f, 14.5f, -2.8f, 1.6f, 1.6f },
                Tailplane = new float[] { 8.6f, 5.2f, 9.6f, 11.6f, 11f }
            });

            AndroidPath dome = new AndroidPath();
            dome.AddOval(new RectF(-0.9f, -7.4f, 0.9f, -4.2f), AndroidPath.Direction.Cw!);
            accents[ThreatKind.Recon] = dome;

            return path;
        }

        /// <summary>
        /// Builds a cruise missile: a long body with a pointed nose, short swept wings and tail fins.
        /// </summary>
        /// <returns>Missile path.</returns>
        private static AndroidPath BuildMissile()
        {
            return Assemble(new AircraftShapes.Silhouette
            {
                HalfWidth = 1.15f, Top = -15f, Bottom = 14f, NoseLength = 7f, TailLength = 3f, TailHalfWidth = 0.95f,
                Wing = new float[] { 3.5f, 6.8f, 8.2f, 10.6f, 9.6f },
                Tailplane = new float[] { 10.6f, 5.6f, 12.2f, 14f, 13.6f }
            });
        }

        /// <summary>
        /// Builds a ballistic missile: a tall body with a long ogive nose and swept fins at the tail.
        /// </summary>
        /// <returns>Missile path.</returns>
        private static AndroidPath BuildBallistic()
        {
            AndroidPath path = Assemble(new AircraftShapes.Silhouette
            {
                HalfWidth = 1.5f, Top = -16f, Bottom = 14.5f, NoseLength = 11f, TailLength = 2f, TailHalfWidth = 1.3f,
                Tailplane = new float[] { 8.2f, 5.8f, 12.4f, 14.8f, 14f }
            });

            path.AddRoundRect(new RectF(-1.1f, 14f, 1.1f, 15.6f), 0.7f, 0.7f, AndroidPath.Direction.Cw!);

            return path;
        }

        /// <summary>
        /// Builds a guided aerial bomb: a thick teardrop body with small wings and tail fins.
        /// </summary>
        /// <returns>Bomb path.</returns>
        private static AndroidPath BuildKab()
        {
            return Assemble(new AircraftShapes.Silhouette
            {
                HalfWidth = 2.7f, Top = -11.5f, Bottom = 8.5f, NoseLength = 8f, TailLength = 9f, TailHalfWidth = 0.9f,
                Wing = new float[] { -0.5f, 8.6f, 2.4f, 4.6f, 3.8f },
                Tailplane = new float[] { 5.8f, 6.4f, 8f, 11.6f, 10.2f }
            });
        }

        /// <summary>
        /// Builds the MiG-31K: a long fuselage, swept wings, two large engines, a tailplane and a cockpit.
        /// </summary>
        /// <returns>Aircraft path.</returns>
        private AndroidPath BuildMig31k()
        {
            AircraftShapes.Silhouette silhouette = new AircraftShapes.Silhouette
            {
                HalfWidth = 1.6f, Top = -16f, Bottom = 14f, NoseLength = 8.5f, TailLength = 6f, TailHalfWidth = 1.1f,
                Wing = new float[] { -0.8f, 10.8f, 8.6f, 11.4f, 8.6f },
                Tailplane = new float[] { 9f, 5.8f, 12.6f, 14.4f, 13.4f },
                Engines = new float[] { 2.7f, 4.5f, 14.2f, 1.25f }
            };

            AndroidPath path = Assemble(silhouette);

            AndroidPath cockpit = new AndroidPath();
            cockpit.AddOval(new RectF(-silhouette.HalfWidth * 0.5f, silhouette.Top + silhouette.NoseLength * 0.7f, silhouette.HalfWidth * 0.5f, silhouette.Top + silhouette.NoseLength * 1.35f), AndroidPath.Direction.Cw!);
            accents[ThreatKind.Mig31k] = cockpit;

            return path;
        }

        /// <summary>
        /// Builds the generic marker of a kind that is not known: a diamond.
        /// </summary>
        /// <returns>Diamond path.</returns>
        private static AndroidPath BuildUnknown()
        {
            AndroidPath path = new AndroidPath();
            path.MoveTo(0f, -12f);
            path.LineTo(8f, 0f);
            path.LineTo(0f, 12f);
            path.LineTo(-8f, 0f);
            path.Close();

            return path;
        }

        /// <summary>
        /// Assembles a silhouette from its parts, as the aircraft icons are assembled. All parts are added clockwise, so overlapping parts merge into one shape.
        /// </summary>
        /// <param name="silhouette">Silhouette description.</param>
        /// <returns>Closed path of the whole object.</returns>
        private static AndroidPath Assemble(AircraftShapes.Silhouette silhouette)
        {
            AndroidPath path = new AndroidPath();

            AircraftShapes.AddMirroredPolygon(path, AircraftShapes.WingPolygon(silhouette.Wing, silhouette.HalfWidth * 0.6f));
            AircraftShapes.AddMirroredPolygon(path, AircraftShapes.WingPolygon(silhouette.Tailplane, silhouette.TailHalfWidth + 0.4f));

            for (int index = 0; index + 3 < silhouette.Engines.Length; index += 4)
            {
                float x = silhouette.Engines[index];
                float top = silhouette.Engines[index + 1];
                float bottom = silhouette.Engines[index + 2];
                float half = silhouette.Engines[index + 3];

                path.AddRoundRect(new RectF(x - half, top, x + half, bottom), half, half, AndroidPath.Direction.Cw!);
                path.AddRoundRect(new RectF(-x - half, top, -x + half, bottom), half, half, AndroidPath.Direction.Cw!);
            }

            AircraftShapes.AddFuselage(path, silhouette);

            return path;
        }
    }
}

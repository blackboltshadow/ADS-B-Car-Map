using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Full screen map on which the user chooses the center of an alert circle or draws the polygon or rectangle of an alert. Phone only. The result is returned to the alert editor.
    /// </summary>
    [Activity(Label = "Obszar", Exported = false, Theme = "@android:style/Theme.Material")]
    public class AreaPickerActivity : Activity
    {
        /// <summary>
        /// Extra with the kind of the choice: <see cref="ModeCenter"/> or <see cref="ModePolygon"/>.
        /// </summary>
        public const string ModeExtra = "area_picker_mode";

        /// <summary>
        /// Mode in which the user taps the center of a circle.
        /// </summary>
        public const string ModeCenter = "center";

        /// <summary>
        /// Mode in which the user draws a polygon or a rectangle.
        /// </summary>
        public const string ModePolygon = "polygon";

        /// <summary>
        /// Extra with the latitude of the center, in the request the initial one and in the result the chosen one.
        /// </summary>
        public const string LatitudeExtra = "area_picker_latitude";

        /// <summary>
        /// Extra with the longitude of the center, in the request the initial one and in the result the chosen one.
        /// </summary>
        public const string LongitudeExtra = "area_picker_longitude";

        /// <summary>
        /// Extra with the radius in kilometers drawn around the center.
        /// </summary>
        public const string RadiusExtra = "area_picker_radius";

        /// <summary>
        /// Extra with the vertices as a flat array of latitude and longitude pairs, in the request the existing ones and in the result the drawn ones.
        /// </summary>
        public const string VerticesExtra = "area_picker_vertices";

        /// <summary>
        /// Request code under which the editor starts the picker.
        /// </summary>
        public const int RequestCode = 4711;

        /// <summary>
        /// Zoom used when there is nothing to fit.
        /// </summary>
        private const double DefaultZoom = 8.0;

        /// <summary>
        /// The map.
        /// </summary>
        private AreaPickerView? mapView;

        /// <summary>
        /// Hint above the map.
        /// </summary>
        private TextView? hintText;

        /// <summary>
        /// Button that switches between the polygon and the rectangle.
        /// </summary>
        private Button? shapeButton;

        /// <summary>
        /// Whether the activity picks the center of a circle.
        /// </summary>
        private bool centerMode;

        /// <summary>
        /// Builds the screen and shows the area that is being edited.
        /// </summary>
        /// <param name="savedInstanceState">Saved state, not used.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Obszar na mapie");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            centerMode = Intent?.GetStringExtra(ModeExtra) != ModePolygon;
            SetContentView(BuildContent());

            double latitude = Intent?.GetDoubleExtra(LatitudeExtra, double.NaN) ?? double.NaN;
            double longitude = Intent?.GetDoubleExtra(LongitudeExtra, double.NaN) ?? double.NaN;
            double radius = Intent?.GetDoubleExtra(RadiusExtra, 50.0) ?? 50.0;
            double[]? flat = Intent?.GetDoubleArrayExtra(VerticesExtra);
            List<GeoPoint> vertices = ReadVertices(flat);

            mapView!.RadiusKilometers = radius;

            if (centerMode)
            {
                mapView.SetMode(AreaPickerView.PickMode.Center);

                if (!double.IsNaN(latitude) && !double.IsNaN(longitude))
                {
                    mapView.SetPoints(new List<GeoPoint> { new GeoPoint(latitude, longitude) });
                    mapView.FitTo(latitude, longitude, radius * 1.5);
                }
                else
                {
                    (double centerLatitude, double centerLongitude) = Services.LiveTraffic.GetCenter();
                    mapView.MoveTo(centerLatitude, centerLongitude, DefaultZoom);
                }
            }
            else
            {
                mapView.SetMode(AreaPickerView.PickMode.Polygon);

                if (vertices.Count >= AlertArea.MinimumVertices)
                {
                    AlertArea area = AlertArea.CreatePolygon(vertices);
                    mapView.SetPoints(vertices);
                    mapView.FitTo(area.Center!.Latitude, area.Center.Longitude, area.RadiusKilometers * 1.3);
                }
                else
                {
                    (double centerLatitude, double centerLongitude) = Services.LiveTraffic.GetCenter();
                    mapView.MoveTo(centerLatitude, centerLongitude, DefaultZoom);
                }
            }

            UpdateHint();
        }

        /// <summary>
        /// Closes the screen on the arrow in the action bar.
        /// </summary>
        /// <param name="item">Chosen item.</param>
        /// <returns>True when handled.</returns>
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home)
            {
                Finish();

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        /// <summary>
        /// Reads the flat array of coordinates.
        /// </summary>
        /// <param name="flat">Latitude and longitude pairs or null.</param>
        /// <returns>The vertices.</returns>
        public static List<GeoPoint> ReadVertices(double[]? flat)
        {
            List<GeoPoint> vertices = new List<GeoPoint>();

            if (flat == null)
            {
                return vertices;
            }

            for (int i = 0; i + 1 < flat.Length; i += 2)
            {
                vertices.Add(new GeoPoint(flat[i], flat[i + 1]));
            }

            return vertices;
        }

        /// <summary>
        /// Flattens vertices into pairs of coordinates for an intent.
        /// </summary>
        /// <param name="vertices">Vertices to flatten.</param>
        /// <returns>Latitude and longitude pairs.</returns>
        public static double[] FlattenVertices(IReadOnlyList<GeoPoint> vertices)
        {
            double[] flat = new double[vertices.Count * 2];

            for (int i = 0; i < vertices.Count; i++)
            {
                flat[i * 2] = vertices[i].Latitude;
                flat[i * 2 + 1] = vertices[i].Longitude;
            }

            return flat;
        }

        /// <summary>
        /// Builds the map with the hint and the buttons.
        /// </summary>
        /// <returns>The root view.</returns>
        private View BuildContent()
        {
            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetFitsSystemWindows(true);

            hintText = new TextView(this);
            hintText.TextSize = 14f;
            hintText.SetTextColor(Color.White);
            hintText.SetBackgroundColor(Color.Argb(220, 30, 30, 30));
            hintText.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
            root.AddView(hintText, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            mapView = new AreaPickerView(this);
            mapView.PointsChanged += UpdateHint;
            mapView.VertexLongPressed += ShowVertexMenu;
            mapView.EdgeLongPressed += ShowEdgeMenu;
            root.AddView(mapView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            LinearLayout buttons = new LinearLayout(this);
            buttons.Orientation = Orientation.Horizontal;
            buttons.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            root.AddView(buttons, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            if (!centerMode)
            {
                Button undoButton = CreateButton(Loc.T("Cofnij"));
                undoButton.Click += (sender, args) => mapView.Undo();
                buttons.AddView(undoButton, CreateButtonParams());

                Button clearButton = CreateButton(Loc.T("Wyczyść"));
                clearButton.Click += (sender, args) => mapView.Clear();
                buttons.AddView(clearButton, CreateButtonParams());

                shapeButton = CreateButton(Loc.T("Prostokąt"));
                shapeButton.Click += (sender, args) => ToggleShape();
                buttons.AddView(shapeButton, CreateButtonParams());
            }
            else
            {
                Button cancelButton = CreateButton(Loc.T("Anuluj"));
                cancelButton.Click += (sender, args) => Finish();
                buttons.AddView(cancelButton, CreateButtonParams());
            }

            Button doneButton = CreateButton(Loc.T("Gotowe"));
            doneButton.Click += (sender, args) => Done();
            buttons.AddView(doneButton, CreateButtonParams());

            return root;
        }

        /// <summary>
        /// Asks what to do with a vertex that was pressed and held.
        /// </summary>
        /// <param name="index">Index of the vertex.</param>
        private void ShowVertexMenu(int index)
        {
            string[] items = new string[] { Loc.T("Przesuń"), Loc.T("Usuń") };
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Wierzchołek ") + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.SetItems(items, (sender, args) =>
            {
                if (args.Which == 0)
                {
                    mapView!.BeginMove(index);
                }
                else
                {
                    mapView!.RemoveVertex(index);
                }
            });
            builder.Show();
        }

        /// <summary>
        /// Offers to insert a vertex into an edge that was pressed and held.
        /// </summary>
        /// <param name="afterIndex">Index of the vertex at which the edge starts.</param>
        /// <param name="position">Point of the edge under the finger.</param>
        private void ShowEdgeMenu(int afterIndex, GeoPoint position)
        {
            string[] items = new string[] { Loc.T("Wstaw wierzchołek") };
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Bok między wierzchołkami ") + (afterIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + Loc.T(" i ") + (afterIndex + 2 > mapView!.PointCount ? 1 : afterIndex + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.SetItems(items, (sender, args) =>
            {
                if (!mapView!.InsertVertexAfter(afterIndex, position))
                {
                    Toast.MakeText(this, Loc.T("Wielokąt ma już maksymalną liczbę wierzchołków"), ToastLength.Short)?.Show();
                }
            });
            builder.Show();
        }

        /// <summary>
        /// Switches between drawing a polygon and drawing a rectangle.
        /// </summary>
        private void ToggleShape()
        {
            bool toRectangle = mapView!.Mode == AreaPickerView.PickMode.Polygon;

            mapView.SetMode(toRectangle ? AreaPickerView.PickMode.Rectangle : AreaPickerView.PickMode.Polygon);
            shapeButton!.Text = toRectangle ? Loc.T("Wielokąt") : Loc.T("Prostokąt");
            UpdateHint();
        }

        /// <summary>
        /// Shows what the user has to do next.
        /// </summary>
        private void UpdateHint()
        {
            if (hintText == null || mapView == null)
            {
                return;
            }

            int count = mapView.PointCount;

            if (mapView.MovingIndex >= 0)
            {
                hintText.Text = Loc.T("Przeciągnij wierzchołek albo dotknij nowego miejsca dla wierzchołka ") + (mapView.MovingIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + Loc.T(". „Cofnij” anuluje przesuwanie.");
            }
            else if (mapView.Mode == AreaPickerView.PickMode.Center)
            {
                hintText.Text = count == 0 ? Loc.T("Dotknij mapy, aby ustawić środek okręgu. Przeciągnij, aby przesunąć mapę, uszczypnij, aby zmienić skalę.") : Loc.T("Środek ustawiony. Dotknij innego miejsca, aby go zmienić, albo wybierz „Gotowe”.");
            }
            else if (mapView.Mode == AreaPickerView.PickMode.Rectangle)
            {
                hintText.Text = count == 0 ? Loc.T("Dotknij pierwszego rogu prostokąta.") : count == 1 ? Loc.T("Dotknij przeciwległego rogu.") : Loc.T("Prostokąt gotowy. Dotknij mapy, aby zacząć od nowa, albo wybierz „Gotowe”.");
            }
            else if (mapView.HasIntersection)
            {
                hintText.Text = Loc.T("Boki wielokąta przecinają się lub się stykają. Cofnij ostatni wierzchołek i wybierz inne miejsce.");
            }
            else
            {
                hintText.Text = count < AlertArea.MinimumVertices
                    ? Loc.T("Dotykaj mapy, aby dodawać wierzchołki wielokąta (minimum 3). Wierzchołków: ") + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : Loc.T("Wierzchołków: ") + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + Loc.T(". Dotykaj dalej, cofnij lub wybierz „Gotowe”.");
            }
        }

        /// <summary>
        /// Checks the choice and returns it to the editor.
        /// </summary>
        private void Done()
        {
            IReadOnlyList<GeoPoint> points = mapView!.Points;
            Intent result = new Intent();

            if (mapView.Mode == AreaPickerView.PickMode.Center)
            {
                if (points.Count != 1)
                {
                    Toast.MakeText(this, Loc.T("Najpierw dotknij mapy, aby ustawić środek"), ToastLength.Short)?.Show();

                    return;
                }

                result.PutExtra(LatitudeExtra, points[0].Latitude);
                result.PutExtra(LongitudeExtra, points[0].Longitude);
            }
            else if (mapView.Mode == AreaPickerView.PickMode.Rectangle)
            {
                if (points.Count != 2 || points[0].Latitude == points[1].Latitude || points[0].Longitude == points[1].Longitude)
                {
                    Toast.MakeText(this, Loc.T("Dotknij dwóch przeciwległych rogów prostokąta"), ToastLength.Short)?.Show();

                    return;
                }

                AlertArea rectangle = AlertArea.CreateRectangle(points[0], points[1]);
                result.PutExtra(VerticesExtra, FlattenVertices(rectangle.Vertices!));
            }
            else
            {
                if (points.Count < AlertArea.MinimumVertices)
                {
                    Toast.MakeText(this, Loc.T("Wielokąt potrzebuje co najmniej 3 wierzchołków"), ToastLength.Short)?.Show();

                    return;
                }

                if (mapView.HasIntersection)
                {
                    Toast.MakeText(this, Loc.T("Boki wielokąta nie mogą się przecinać ani stykać"), ToastLength.Long)?.Show();

                    return;
                }

                result.PutExtra(VerticesExtra, FlattenVertices(points));
            }

            SetResult(Result.Ok, result);
            Finish();
        }

        /// <summary>
        /// Creates a button of the bottom bar.
        /// </summary>
        /// <param name="text">Text of the button.</param>
        /// <returns>The button.</returns>
        private Button CreateButton(string text)
        {
            Button button = new Button(this);
            button.Text = text;
            button.SetAllCaps(false);

            return button;
        }

        /// <summary>
        /// Layout parameters that share the bottom bar equally.
        /// </summary>
        /// <returns>The parameters.</returns>
        private static LinearLayout.LayoutParams CreateButtonParams()
        {
            return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
        }
    }
}

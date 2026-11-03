using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Map on which the user chooses the center of a circle, draws a polygon or draws a rectangle for an alert. It only shows the tiles and the chosen shape; it knows nothing about aircraft.
    /// </summary>
    internal sealed class AreaPickerView : View
    {
        /// <summary>
        /// What a tap on the map does.
        /// </summary>
        public enum PickMode
        {
            /// <summary>
            /// A tap sets the center of a circle.
            /// </summary>
            Center,

            /// <summary>
            /// A tap adds a vertex of a polygon.
            /// </summary>
            Polygon,

            /// <summary>
            /// The first two taps set the opposite corners of a rectangle.
            /// </summary>
            Rectangle,
        }

        /// <summary>
        /// Lowest zoom of the picker.
        /// </summary>
        private const double MinimumZoom = 3.0;

        /// <summary>
        /// Highest zoom of the picker.
        /// </summary>
        private const double MaximumZoom = 17.0;

        /// <summary>
        /// Meters per pixel on the equator at zoom 0 with 256 pixel tiles.
        /// </summary>
        private const double EquatorMetersPerPixel = 156543.03392;

        /// <summary>
        /// Distance in dp from a vertex within which a long press counts as a press on the vertex.
        /// </summary>
        private const float VertexHitDp = 28f;

        /// <summary>
        /// Distance in dp from an edge within which a long press counts as a press on the edge.
        /// </summary>
        private const float EdgeHitDp = 24f;

        /// <summary>
        /// Time between two redraws, which pick up the tiles that finished loading.
        /// </summary>
        private const long RedrawDelayMilliseconds = 250;

        /// <summary>
        /// Radius of a vertex marker in dp.
        /// </summary>
        private const float VertexRadiusDp = 7f;

        /// <summary>
        /// Tiles of the map.
        /// </summary>
        private readonly TileLayer tileLayer = new TileLayer(AppServices.Tiles);

        /// <summary>
        /// Detector of the drag and tap gestures.
        /// </summary>
        private readonly GestureDetector gestureDetector;

        /// <summary>
        /// Detector of the pinch gesture.
        /// </summary>
        private readonly ScaleGestureDetector scaleDetector;

        /// <summary>
        /// Points chosen by the user: one for <see cref="PickMode.Center"/>, vertices for <see cref="PickMode.Polygon"/>, up to two corners for <see cref="PickMode.Rectangle"/>.
        /// </summary>
        private readonly List<GeoPoint> points = new List<GeoPoint>();

        /// <summary>
        /// Paint of the translucent fill of the shape.
        /// </summary>
        private readonly Paint fillPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the outline of the shape.
        /// </summary>
        private readonly Paint outlinePaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the outline of a polygon whose edges cross each other.
        /// </summary>
        private readonly Paint invalidPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the vertex markers.
        /// </summary>
        private readonly Paint vertexPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Paint of the numbers and of the attribution.
        /// </summary>
        private readonly Paint textPaint = new Paint(PaintFlags.AntiAlias);

        /// <summary>
        /// Screen density.
        /// </summary>
        private readonly float density;

        /// <summary>
        /// Latitude of the middle of the screen.
        /// </summary>
        private double centerLatitude;

        /// <summary>
        /// Longitude of the middle of the screen.
        /// </summary>
        private double centerLongitude;

        /// <summary>
        /// Current zoom.
        /// </summary>
        private double zoom = 9.0;

        /// <summary>
        /// Radius in kilometers that has to fit on the screen when the size is first known, or null.
        /// </summary>
        private double? pendingFitKilometers;

        /// <summary>
        /// Creates the view.
        /// </summary>
        /// <param name="context">Context of the activity.</param>
        public AreaPickerView(Context context) : base(context)
        {
            density = context.Resources?.DisplayMetrics?.Density ?? 1.0f;
            gestureDetector = new GestureDetector(context, new PanListener(this));
            scaleDetector = new ScaleGestureDetector(context, new ScaleListener(this));

            fillPaint.SetStyle(Paint.Style.Fill);
            fillPaint.Color = Color.Argb(70, 0, 160, 255);
            outlinePaint.SetStyle(Paint.Style.Stroke);
            outlinePaint.StrokeWidth = 3f * density;
            outlinePaint.Color = Color.Rgb(0, 140, 255);
            invalidPaint.SetStyle(Paint.Style.Stroke);
            invalidPaint.StrokeWidth = 3f * density;
            invalidPaint.Color = Color.Rgb(230, 40, 40);
            vertexPaint.SetStyle(Paint.Style.Fill);
            vertexPaint.Color = Color.White;
            textPaint.TextSize = 12f * density;
            textPaint.Color = Color.Black;
            textPaint.TextAlign = Paint.Align.Center;
        }

        /// <summary>
        /// What a tap on the map does.
        /// </summary>
        public PickMode Mode { get; private set; } = PickMode.Center;

        /// <summary>
        /// Radius in kilometers drawn around the chosen center in <see cref="PickMode.Center"/>.
        /// </summary>
        public double RadiusKilometers { get; set; } = 50.0;

        /// <summary>
        /// Number of points chosen so far.
        /// </summary>
        public int PointCount => points.Count;

        /// <summary>
        /// Whether a polygon is being drawn whose edges cross each other or touch, so that it is not a valid area.
        /// </summary>
        public bool HasIntersection => Mode == PickMode.Polygon && points.Count >= AlertArea.MinimumVertices && !AlertArea.IsSimple(points);

        /// <summary>
        /// Whether the current touch drags the vertex that waits to be moved.
        /// </summary>
        private bool draggingVertex;

        /// <summary>
        /// Index of the vertex on which the current touch started while no vertex was waiting to be moved, or -1. A drag from it starts the move, a tap on it is handled by the tap gesture.
        /// </summary>
        private int pressedIndex = -1;

        /// <summary>
        /// Horizontal position at which the current touch started.
        /// </summary>
        private float pressX;

        /// <summary>
        /// Vertical position at which the current touch started.
        /// </summary>
        private float pressY;

        /// <summary>
        /// Index of the vertex that waits to be put in a new place, by a tap on the map or by dragging it, or -1.
        /// </summary>
        public int MovingIndex { get; private set; } = -1;

        /// <summary>
        /// Raised when the chosen points change.
        /// </summary>
        public event System.Action? PointsChanged;

        /// <summary>
        /// Raised with the index of a vertex that the user pressed and held.
        /// </summary>
        public event System.Action<int>? VertexLongPressed;

        /// <summary>
        /// Raised when the user pressed and held an edge of the polygon, with the index of the vertex at which the edge starts and the point of the edge under the finger.
        /// </summary>
        public event System.Action<int, GeoPoint>? EdgeLongPressed;

        /// <summary>
        /// Points chosen so far.
        /// </summary>
        public IReadOnlyList<GeoPoint> Points => points;

        /// <summary>
        /// Switches the mode and clears the chosen points.
        /// </summary>
        /// <param name="mode">New mode.</param>
        public void SetMode(PickMode mode)
        {
            Mode = mode;
            points.Clear();
            MovingIndex = -1;
            RaiseChanged();
        }

        /// <summary>
        /// Starts moving a vertex: the next tap on the map puts the vertex there.
        /// </summary>
        /// <param name="index">Index of the vertex.</param>
        public void BeginMove(int index)
        {
            if (index >= 0 && index < points.Count)
            {
                MovingIndex = index;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Removes a vertex.
        /// </summary>
        /// <param name="index">Index of the vertex.</param>
        public void RemoveVertex(int index)
        {
            if (index >= 0 && index < points.Count)
            {
                points.RemoveAt(index);
                MovingIndex = -1;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Puts the middle of the map on a point.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="zoomLevel">Zoom level.</param>
        public void MoveTo(double latitude, double longitude, double zoomLevel)
        {
            centerLatitude = Math.Clamp(latitude, -WebMercator.MaxLatitude, WebMercator.MaxLatitude);
            centerLongitude = longitude;
            zoom = Math.Clamp(zoomLevel, MinimumZoom, MaximumZoom);
            Invalidate();
        }

        /// <summary>
        /// Puts the middle of the map on a point and chooses the zoom at which a circle of the given radius fits on the screen once the size is known.
        /// </summary>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="radiusKilometers">Radius that has to fit.</param>
        public void FitTo(double latitude, double longitude, double radiusKilometers)
        {
            MoveTo(latitude, longitude, zoom);
            pendingFitKilometers = radiusKilometers;
            ApplyPendingFit();
        }

        /// <summary>
        /// Sets the points, for example the vertices of an area that is being edited.
        /// </summary>
        /// <param name="newPoints">Points to show.</param>
        public void SetPoints(IReadOnlyList<GeoPoint> newPoints)
        {
            MovingIndex = -1;
            points.Clear();
            points.AddRange(newPoints);
            RaiseChanged();
        }

        /// <summary>
        /// Removes the last chosen point.
        /// </summary>
        public void Undo()
        {
            if (MovingIndex >= 0)
            {
                MovingIndex = -1;
                RaiseChanged();
            }
            else if (points.Count > 0)
            {
                points.RemoveAt(points.Count - 1);
                RaiseChanged();
            }
        }

        /// <summary>
        /// Removes all chosen points.
        /// </summary>
        public void Clear()
        {
            MovingIndex = -1;
            points.Clear();
            RaiseChanged();
        }

        /// <summary>
        /// Draws the map and the chosen shape, and schedules the next redraw that picks up the tiles loaded in the meantime.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            canvas.DrawColor(Color.Rgb(24, 24, 24));
            tileLayer.Draw(canvas, centerLatitude, centerLongitude, zoom, Width, Height);
            DrawShape(canvas);

            textPaint.TextAlign = Paint.Align.Left;
            canvas.DrawText(TileProvider.OpenStreetMapAttribution, 6f * density, Height - 6f * density, textPaint);
            textPaint.TextAlign = Paint.Align.Center;

            PostInvalidateDelayed(RedrawDelayMilliseconds);
        }

        /// <summary>
        /// Applies a fit requested before the size of the view was known.
        /// </summary>
        /// <param name="w">New width.</param>
        /// <param name="h">New height.</param>
        /// <param name="oldw">Old width.</param>
        /// <param name="oldh">Old height.</param>
        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            ApplyPendingFit();
        }

        /// <summary>
        /// Passes the touch to the gesture detectors.
        /// </summary>
        /// <param name="e">Touch event.</param>
        /// <returns>Always true, to keep receiving the following gesture events.</returns>
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null)
            {
                return false;
            }

            if (HandleVertexDrag(e))
            {
                return true;
            }

            scaleDetector.OnTouchEvent(e);

            if (!scaleDetector.IsInProgress)
            {
                gestureDetector.OnTouchEvent(e);
            }

            return true;
        }

        /// <summary>
        /// Lets the vertex that waits to be moved be dragged: a touch that starts on it carries it with the finger and puts it down when the finger is lifted. A touch that starts elsewhere is left to the map gestures, so the map can still be panned and a tap still puts the vertex in the tapped place.
        /// </summary>
        /// <param name="e">Touch event.</param>
        /// <returns>True when the event belongs to a vertex drag and must not reach the map gestures.</returns>
        private bool HandleVertexDrag(MotionEvent e)
        {
            if (e.ActionMasked == MotionEventActions.Down)
            {
                draggingVertex = false;
                pressedIndex = -1;

                if (MovingIndex >= 0 && MovingIndex < points.Count)
                {
                    draggingVertex = FindVertexAt(e.GetX(), e.GetY(), VertexHitDp * density) == MovingIndex;
                }
                else if (Mode != PickMode.Center)
                {
                    pressedIndex = FindVertexAt(e.GetX(), e.GetY(), VertexHitDp * density);
                    pressX = e.GetX();
                    pressY = e.GetY();
                }

                return draggingVertex;
            }

            if (pressedIndex >= 0)
            {
                if (e.ActionMasked == MotionEventActions.Move)
                {
                    float slop = ViewConfiguration.Get(Context)?.ScaledTouchSlop ?? 8f * density;

                    if ((e.GetX() - pressX) * (e.GetX() - pressX) + (e.GetY() - pressY) * (e.GetY() - pressY) > slop * slop)
                    {
                        using MotionEvent cancel = MotionEvent.Obtain(e);
                        cancel.Action = MotionEventActions.Cancel;
                        gestureDetector.OnTouchEvent(cancel);

                        MovingIndex = pressedIndex;
                        draggingVertex = true;
                    }
                }

                if (!draggingVertex)
                {
                    if (e.ActionMasked == MotionEventActions.Up || e.ActionMasked == MotionEventActions.Cancel)
                    {
                        pressedIndex = -1;
                    }

                    return false;
                }

                pressedIndex = -1;
            }

            if (!draggingVertex)
            {
                return false;
            }

            if (e.ActionMasked == MotionEventActions.Move)
            {
                if (MovingIndex >= 0 && MovingIndex < points.Count)
                {
                    points[MovingIndex] = ScreenToGeo(e.GetX(), e.GetY());
                    RaiseChanged();
                }
            }
            else if (e.ActionMasked == MotionEventActions.Up || e.ActionMasked == MotionEventActions.Cancel)
            {
                draggingVertex = false;
                MovingIndex = -1;
                RaiseChanged();
            }

            return true;
        }

        /// <summary>
        /// Finds the point of the Earth under a point of the screen.
        /// </summary>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <param name="y">Vertical position in pixels.</param>
        /// <returns>The geographic point.</returns>
        private GeoPoint ScreenToGeo(float x, float y)
        {
            (double centerX, double centerY) = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
            (double latitude, double longitude) = WebMercator.WorldPixelToLatLon(centerX + x - Width / 2.0, centerY + y - Height / 2.0, zoom);

            return new GeoPoint(latitude, ((longitude + 540.0) % 360.0) - 180.0);
        }

        /// <summary>
        /// Chooses the zoom at which the pending radius fits on the screen.
        /// </summary>
        private void ApplyPendingFit()
        {
            if (pendingFitKilometers == null || Width <= 0 || Height <= 0)
            {
                return;
            }

            double halfScreenPixels = Math.Min(Width, Height) / 2.0 * 0.8;
            double metersPerPixelNeeded = pendingFitKilometers.Value * 1000.0 / halfScreenPixels;
            double level = Math.Log2(EquatorMetersPerPixel * Math.Cos(centerLatitude * Math.PI / 180.0) / Math.Max(metersPerPixelNeeded, 0.01));

            zoom = Math.Clamp(level, MinimumZoom, MaximumZoom);
            pendingFitKilometers = null;
            Invalidate();
        }

        /// <summary>
        /// Draws the chosen points as a circle, a polygon or a rectangle.
        /// </summary>
        /// <param name="canvas">Canvas of the view.</param>
        private void DrawShape(Canvas canvas)
        {
            if (points.Count == 0)
            {
                return;
            }

            List<PointF> screen = new List<PointF>();

            foreach (GeoPoint point in points)
            {
                (float x, float y) = WebMercator.ProjectToScreen(point.Latitude, point.Longitude, centerLatitude, centerLongitude, zoom, Width, Height);
                screen.Add(new PointF(x, y));
            }

            if (Mode == PickMode.Center)
            {
                double metersPerPixel = EquatorMetersPerPixel * Math.Cos(points[0].Latitude * Math.PI / 180.0) / Math.Pow(2.0, zoom);
                float radiusPixels = (float)(RadiusKilometers * 1000.0 / Math.Max(metersPerPixel, 0.01));

                canvas.DrawCircle(screen[0].X, screen[0].Y, radiusPixels, fillPaint);
                canvas.DrawCircle(screen[0].X, screen[0].Y, radiusPixels, outlinePaint);
                canvas.DrawCircle(screen[0].X, screen[0].Y, VertexRadiusDp * density, vertexPaint);
                canvas.DrawCircle(screen[0].X, screen[0].Y, VertexRadiusDp * density, outlinePaint);

                return;
            }

            if (Mode == PickMode.Rectangle && screen.Count == 2)
            {
                float left = Math.Min(screen[0].X, screen[1].X);
                float right = Math.Max(screen[0].X, screen[1].X);
                float top = Math.Min(screen[0].Y, screen[1].Y);
                float bottom = Math.Max(screen[0].Y, screen[1].Y);

                canvas.DrawRect(left, top, right, bottom, fillPaint);
                canvas.DrawRect(left, top, right, bottom, outlinePaint);
            }
            else if (screen.Count >= 2)
            {
                using Android.Graphics.Path path = new Android.Graphics.Path();
                path.MoveTo(screen[0].X, screen[0].Y);

                for (int i = 1; i < screen.Count; i++)
                {
                    path.LineTo(screen[i].X, screen[i].Y);
                }

                if (screen.Count >= AlertArea.MinimumVertices)
                {
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }

                canvas.DrawPath(path, HasIntersection ? invalidPaint : outlinePaint);
            }

            for (int i = 0; i < screen.Count; i++)
            {
                vertexPaint.Color = i == MovingIndex ? Color.Rgb(255, 170, 0) : Color.White;
                canvas.DrawCircle(screen[i].X, screen[i].Y, VertexRadiusDp * density, vertexPaint);
                canvas.DrawCircle(screen[i].X, screen[i].Y, VertexRadiusDp * density, outlinePaint);
                canvas.DrawText((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), screen[i].X, screen[i].Y + 4f * density, textPaint);
            }
        }

        /// <summary>
        /// Handles a tap: sets the center, adds a vertex or sets a corner of the rectangle.
        /// </summary>
        /// <param name="x">Horizontal position of the tap in pixels.</param>
        /// <param name="y">Vertical position of the tap in pixels.</param>
        private void HandleTap(float x, float y)
        {
            if (MovingIndex < 0 && Mode != PickMode.Center)
            {
                int tappedVertex = FindVertexAt(x, y, VertexHitDp * density);

                if (tappedVertex >= 0)
                {
                    BeginMove(tappedVertex);

                    return;
                }
            }

            GeoPoint tapped = ScreenToGeo(x, y);

            if (MovingIndex >= 0 && MovingIndex < points.Count)
            {
                points[MovingIndex] = tapped;
                MovingIndex = -1;
            }
            else if (Mode == PickMode.Center)
            {
                points.Clear();
                points.Add(tapped);
            }
            else if (Mode == PickMode.Rectangle)
            {
                if (points.Count >= 2)
                {
                    points.Clear();
                }

                points.Add(tapped);
            }
            else
            {
                if (points.Count >= AlertArea.MaximumVertices)
                {
                    return;
                }

                points.Add(tapped);
            }

            RaiseChanged();
        }

        /// <summary>
        /// Finds the vertex under a pressed point and tells the listener about it.
        /// </summary>
        /// <param name="x">Horizontal position of the press in pixels.</param>
        /// <param name="y">Vertical position of the press in pixels.</param>
        private void HandleLongPress(float x, float y)
        {
            if (Mode == PickMode.Center)
            {
                return;
            }

            int found = FindVertexAt(x, y, VertexHitDp * density);

            if (found >= 0)
            {
                PerformHapticFeedback(FeedbackConstants.LongPress);
                VertexLongPressed?.Invoke(found);

                return;
            }

            if (Mode != PickMode.Polygon || points.Count < 2)
            {
                return;
            }

            float bestDistance = EdgeHitDp * density;
            int edgeStart = -1;
            float bestX = 0f;
            float bestY = 0f;
            int edgeCount = points.Count >= AlertArea.MinimumVertices ? points.Count : points.Count - 1;

            for (int i = 0; i < edgeCount; i++)
            {
                (float startX, float startY) = WebMercator.ProjectToScreen(points[i].Latitude, points[i].Longitude, centerLatitude, centerLongitude, zoom, Width, Height);
                GeoPoint endPoint = points[(i + 1) % points.Count];
                (float endX, float endY) = WebMercator.ProjectToScreen(endPoint.Latitude, endPoint.Longitude, centerLatitude, centerLongitude, zoom, Width, Height);
                float dx = endX - startX;
                float dy = endY - startY;
                float lengthSquared = dx * dx + dy * dy;
                float t = lengthSquared <= 0f ? 0f : Math.Clamp(((x - startX) * dx + (y - startY) * dy) / lengthSquared, 0f, 1f);
                float nearestX = startX + t * dx;
                float nearestY = startY + t * dy;
                float distance = MathF.Sqrt((x - nearestX) * (x - nearestX) + (y - nearestY) * (y - nearestY));

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    edgeStart = i;
                    bestX = nearestX;
                    bestY = nearestY;
                }
            }

            if (edgeStart >= 0)
            {
                PerformHapticFeedback(FeedbackConstants.LongPress);
                EdgeLongPressed?.Invoke(edgeStart, ScreenToGeo(bestX, bestY));
            }
        }

        /// <summary>
        /// Inserts a vertex after another one and starts moving it, so that it can be dragged to the right place at once.
        /// </summary>
        /// <param name="afterIndex">Index of the vertex after which the new one is inserted; the edge from it to the next vertex is split.</param>
        /// <param name="position">Position of the new vertex.</param>
        /// <returns>False when the polygon already has the maximum number of vertices.</returns>
        public bool InsertVertexAfter(int afterIndex, GeoPoint position)
        {
            if (points.Count >= AlertArea.MaximumVertices || afterIndex < 0 || afterIndex >= points.Count)
            {
                return false;
            }

            points.Insert(afterIndex + 1, position);
            MovingIndex = afterIndex + 1;
            RaiseChanged();

            return true;
        }

        /// <summary>
        /// Finds the vertex nearest to a point of the screen.
        /// </summary>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <param name="y">Vertical position in pixels.</param>
        /// <param name="maximumDistance">Largest distance in pixels at which a vertex still counts.</param>
        /// <returns>Index of the vertex or -1 when none is close enough.</returns>
        private int FindVertexAt(float x, float y, float maximumDistance)
        {
            float bestDistance = maximumDistance * maximumDistance;
            int found = -1;

            for (int i = 0; i < points.Count; i++)
            {
                (float pointX, float pointY) = WebMercator.ProjectToScreen(points[i].Latitude, points[i].Longitude, centerLatitude, centerLongitude, zoom, Width, Height);
                float distance = (pointX - x) * (pointX - x) + (pointY - y) * (pointY - y);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    found = i;
                }
            }

            return found;
        }

        /// <summary>
        /// Moves the map by a drag.
        /// </summary>
        /// <param name="distanceX">Horizontal movement in pixels.</param>
        /// <param name="distanceY">Vertical movement in pixels.</param>
        private void Pan(float distanceX, float distanceY)
        {
            (double centerX, double centerY) = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
            (double latitude, double longitude) = WebMercator.WorldPixelToLatLon(centerX + distanceX, centerY + distanceY, zoom);

            centerLatitude = Math.Clamp(latitude, -WebMercator.MaxLatitude, WebMercator.MaxLatitude);
            centerLongitude = ((longitude + 540.0) % 360.0) - 180.0;
            Invalidate();
        }

        /// <summary>
        /// Changes the zoom by a pinch.
        /// </summary>
        /// <param name="scaleFactor">Scale factor of the gesture step.</param>
        private void ZoomBy(float scaleFactor)
        {
            zoom = Math.Clamp(zoom + Math.Log2(scaleFactor), MinimumZoom, MaximumZoom);
            Invalidate();
        }

        /// <summary>
        /// Tells the listeners and redraws.
        /// </summary>
        private void RaiseChanged()
        {
            PointsChanged?.Invoke();
            Invalidate();
        }

        /// <summary>
        /// Passes the drag and the tap to the view.
        /// </summary>
        private sealed class PanListener : GestureDetector.SimpleOnGestureListener
        {
            /// <summary>
            /// The view that is driven.
            /// </summary>
            private readonly AreaPickerView view;

            /// <summary>
            /// Creates a listener.
            /// </summary>
            /// <param name="view">The view that is driven.</param>
            public PanListener(AreaPickerView view)
            {
                this.view = view;
            }

            /// <inheritdoc />
            public override bool OnScroll(MotionEvent? e1, MotionEvent e2, float distanceX, float distanceY)
            {
                view.Pan(distanceX, distanceY);

                return true;
            }

            /// <inheritdoc />
            public override bool OnSingleTapUp(MotionEvent e)
            {
                view.HandleTap(e.GetX(), e.GetY());

                return true;
            }

            /// <inheritdoc />
            public override void OnLongPress(MotionEvent e)
            {
                view.HandleLongPress(e.GetX(), e.GetY());
            }
        }

        /// <summary>
        /// Passes the pinch to the view.
        /// </summary>
        private sealed class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
        {
            /// <summary>
            /// The view that is driven.
            /// </summary>
            private readonly AreaPickerView view;

            /// <summary>
            /// Creates a listener.
            /// </summary>
            /// <param name="view">The view that is driven.</param>
            public ScaleListener(AreaPickerView view)
            {
                this.view = view;
            }

            /// <inheritdoc />
            public override bool OnScale(ScaleGestureDetector detector)
            {
                view.ZoomBy(detector.ScaleFactor);

                return true;
            }
        }
    }
}

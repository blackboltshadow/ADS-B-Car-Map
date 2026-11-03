using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Phone map view that draws the map through the same controller as Android Auto and handles panning and the pinch gesture.
    /// </summary>
    public sealed class PhoneMapView : SurfaceView, ISurfaceHolderCallback
    {
        /// <summary>
        /// Map state and drawing controller.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Pan gesture detector.
        /// </summary>
        private readonly GestureDetector gestureDetector;

        /// <summary>
        /// Two-finger pinch gesture detector.
        /// </summary>
        private readonly ScaleGestureDetector scaleDetector;

        /// <summary>
        /// Creates a view bound to the map controller.
        /// </summary>
        /// <param name="context">Activity context.</param>
        /// <param name="controller">Map controller.</param>
        public PhoneMapView(Context context, MapController controller) : base(context)
        {
            this.controller = controller;
            gestureDetector = new GestureDetector(context, new PanListener(controller));
            scaleDetector = new ScaleGestureDetector(context, new ScaleListener(controller));
            Holder?.AddCallback(this);
        }

        /// <summary>
        /// Needs no action, drawing starts once the surface size is known.
        /// </summary>
        /// <param name="holder">Surface holder.</param>
        public void SurfaceCreated(ISurfaceHolder holder)
        {
        }

        /// <summary>
        /// Starts drawing the map on a surface of a known size.
        /// </summary>
        /// <param name="holder">Surface holder.</param>
        /// <param name="format">Surface format.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            Android.Views.Surface? surface = holder.Surface;

            if (surface == null)
            {
                return;
            }

            int dpi = (int)(Resources?.DisplayMetrics?.DensityDpi ?? DisplayMetricsDensity.Default);

            controller.Start(surface, width, height, dpi);
        }

        /// <summary>
        /// Stops drawing when the surface is destroyed.
        /// </summary>
        /// <param name="holder">Surface holder.</param>
        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            Android.Views.Surface? destroyed = holder.Surface;

            if (destroyed != null)
            {
                controller.StopIfCurrent(destroyed);
            }
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

            if (e.ActionMasked == MotionEventActions.Down)
            {
                controller.StopFling();
            }

            scaleDetector.OnTouchEvent(e);

            if (!scaleDetector.IsInProgress)
            {
                gestureDetector.OnTouchEvent(e);
            }

            return true;
        }

        /// <summary>
        /// Passes the pan gesture to the map controller.
        /// </summary>
        private sealed class PanListener : GestureDetector.SimpleOnGestureListener
        {
            /// <summary>
            /// Map state controller.
            /// </summary>
            private readonly MapController controller;

            /// <summary>
            /// Creates a listener bound to the map controller.
            /// </summary>
            /// <param name="controller">Map controller.</param>
            public PanListener(MapController controller)
            {
                this.controller = controller;
            }

            /// <summary>
            /// Moves the map by the gesture amount.
            /// </summary>
            /// <param name="e1">Event that started the gesture.</param>
            /// <param name="e2">Current event.</param>
            /// <param name="distanceX">Horizontal movement since the previous event.</param>
            /// <param name="distanceY">Vertical movement since the previous event.</param>
            /// <returns>Always true.</returns>
            public override bool OnScroll(MotionEvent? e1, MotionEvent e2, float distanceX, float distanceY)
            {
                controller.Pan(distanceX, distanceY);

                return true;
            }

            /// <summary>
            /// Passes the tap to the controller, which selects the aircraft near the finger.
            /// </summary>
            /// <param name="e">Tap event.</param>
            /// <returns>Always true.</returns>
            public override bool OnSingleTapUp(MotionEvent e)
            {
                controller.Tap(e.GetX(), e.GetY());

                return true;
            }

            /// <summary>
            /// Starts the inertial map movement after a quick drag.
            /// </summary>
            /// <param name="e1">Event that started the gesture.</param>
            /// <param name="e2">Event that ended the gesture.</param>
            /// <param name="velocityX">Horizontal velocity in pixels per second.</param>
            /// <param name="velocityY">Vertical velocity in pixels per second.</param>
            /// <returns>Always true.</returns>
            public override bool OnFling(MotionEvent? e1, MotionEvent e2, float velocityX, float velocityY)
            {
                controller.Fling(velocityX, velocityY);

                return true;
            }
        }

        /// <summary>
        /// Passes the pinch gesture to the map controller.
        /// </summary>
        private sealed class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
        {
            /// <summary>
            /// Map state controller.
            /// </summary>
            private readonly MapController controller;

            /// <summary>
            /// Creates a listener bound to the map controller.
            /// </summary>
            /// <param name="controller">Map controller.</param>
            public ScaleListener(MapController controller)
            {
                this.controller = controller;
            }

            /// <summary>
            /// Changes the zoom according to the gesture scale.
            /// </summary>
            /// <param name="detector">Gesture detector.</param>
            /// <returns>Always true.</returns>
            public override bool OnScale(ScaleGestureDetector detector)
            {
                controller.Scale(detector.ScaleFactor);

                return true;
            }
        }
    }
}

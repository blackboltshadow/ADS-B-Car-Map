using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Zoom, panning, fling and motion integration of the map controller.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Smoothly zooms in to the next whole level.
        /// </summary>
        public void ZoomIn()
        {
            lock (stateLock)
            {
                zoomTarget = Math.Min(AppSettings.MaxZoom, Math.Floor(zoomTarget + 0.01) + 1.0);
            }
        }

        /// <summary>
        /// Smoothly zooms out to the previous whole level.
        /// </summary>
        public void ZoomOut()
        {
            lock (stateLock)
            {
                zoomTarget = Math.Max(AppSettings.MinZoom, Math.Ceiling(zoomTarget - 0.01) - 1.0);
            }
        }

        /// <summary>
        /// Changes the zoom continuously according to the pinch gesture scale.
        /// </summary>
        /// <param name="scaleFactor">Scale factor relative to the previous event.</param>
        public void Scale(float scaleFactor)
        {
            if (scaleFactor <= 0.0f)
            {
                return;
            }

            lock (stateLock)
            {
                double next = Math.Clamp(zoom + Math.Log2(scaleFactor), AppSettings.MinZoom, AppSettings.MaxZoom);
                zoom = next;
                zoomTarget = next;
            }
        }

        /// <summary>
        /// Moves the map center by the given number of pixels, stops the fling and turns off following of the user position and of the aircraft.
        /// </summary>
        /// <param name="distanceX">Horizontal movement in pixels, following the GestureDetector convention.</param>
        /// <param name="distanceY">Vertical movement in pixels, following the GestureDetector convention.</param>
        public void Pan(float distanceX, float distanceY)
        {
            bool wasFollowing;

            lock (stateLock)
            {
                wasFollowing = followLocation || followedHex != null;
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                MoveCenter(distanceX, distanceY);
            }

            if (wasFollowing)
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Whether the map center follows the user position or an aircraft.
        /// </summary>
        public bool IsFollowing
        {
            get
            {
                lock (stateLock)
                {
                    return followLocation || followedHex != null;
                }
            }
        }

        /// <summary>
        /// Starts the fling movement of the map after a fling gesture.
        /// </summary>
        /// <param name="velocityX">Horizontal finger velocity in pixels per second.</param>
        /// <param name="velocityY">Vertical finger velocity in pixels per second.</param>
        public void Fling(float velocityX, float velocityY)
        {
            bool wasFollowing;

            lock (stateLock)
            {
                wasFollowing = followLocation || followedHex != null;
                flingVelocityX = velocityX;
                flingVelocityY = velocityY;
                followLocation = false;
                followedHex = null;
                centeredAirport = null;
            }

            if (wasFollowing)
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Stops the fling movement, e.g. after the screen is touched.
        /// </summary>
        public void StopFling()
        {
            lock (stateLock)
            {
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
            }
        }

        /// <summary>
        /// Moves the map center by the given number of pixels and turns off following. Call under the state lock.
        /// </summary>
        /// <param name="distanceX">Horizontal movement in pixels.</param>
        /// <param name="distanceY">Vertical movement in pixels.</param>
        private void MoveCenter(double distanceX, double distanceY)
        {
            (double X, double Y) center = WebMercator.LatLonToWorldPixel(centerLatitude, centerLongitude, zoom);
            (double Latitude, double Longitude) moved = WebMercator.WorldPixelToLatLon(center.X + distanceX, center.Y + distanceY, zoom);

            centerLatitude = Math.Clamp(moved.Latitude, -WebMercator.MaxLatitude, WebMercator.MaxLatitude);
            centerLongitude = ((moved.Longitude + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            followLocation = false;
            followedHex = null;
            centeredAirport = null;
        }

        /// <summary>
        /// Advances the motion state by one frame time: brings the zoom closer to its target and moves the map with inertia. Call under the state lock.
        /// </summary>
        /// <param name="deltaSeconds">Time since the previous frame in seconds.</param>
        private void AdvanceMotion(double deltaSeconds)
        {
            double minimumZoom = AppSettings.MinZoom;
            double maximumZoom = AppSettings.MaxZoom;

            zoomTarget = Math.Clamp(zoomTarget, minimumZoom, maximumZoom);

            double difference = zoomTarget - zoom;

            zoom = Math.Abs(difference) < ZoomSnapThreshold
                ? zoomTarget
                : zoom + difference * (1.0 - Math.Exp(-deltaSeconds / ZoomEasingSeconds));
            zoom = Math.Clamp(zoom, minimumZoom, maximumZoom);

            if (flingVelocityX == 0.0 && flingVelocityY == 0.0)
            {
                return;
            }

            MoveCenter(-flingVelocityX * deltaSeconds, -flingVelocityY * deltaSeconds);

            double decay = Math.Exp(-deltaSeconds / FlingDecaySeconds);
            flingVelocityX *= decay;
            flingVelocityY *= decay;

            if (Math.Abs(flingVelocityX) < FlingStopVelocity && Math.Abs(flingVelocityY) < FlingStopVelocity)
            {
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
            }
        }
    }
}

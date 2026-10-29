using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace AdsbCarMap.Map;

/// <summary>
/// Widok mapy na telefonie, który rysuje mapę przez ten sam kontroler co Android Auto i obsługuje przesuwanie oraz gest przybliżania.
/// </summary>
public sealed class PhoneMapView : SurfaceView, ISurfaceHolderCallback
{
    /// <summary>
    /// Kontroler stanu mapy i rysowania.
    /// </summary>
    private readonly MapController controller;

    /// <summary>
    /// Detektor gestu przesuwania.
    /// </summary>
    private readonly GestureDetector gestureDetector;

    /// <summary>
    /// Detektor gestu przybliżania dwoma palcami.
    /// </summary>
    private readonly ScaleGestureDetector scaleDetector;

    /// <summary>
    /// Tworzy widok powiązany z kontrolerem mapy.
    /// </summary>
    /// <param name="context">Kontekst Activity.</param>
    /// <param name="controller">Kontroler mapy.</param>
    public PhoneMapView(Context context, MapController controller) : base(context)
    {
        this.controller = controller;
        gestureDetector = new GestureDetector(context, new PanListener(controller));
        scaleDetector = new ScaleGestureDetector(context, new ScaleListener(controller));
        Holder?.AddCallback(this);
    }

    /// <summary>
    /// Nie wymaga działania, rysowanie rusza po ustaleniu rozmiaru powierzchni.
    /// </summary>
    /// <param name="holder">Uchwyt powierzchni.</param>
    public void SurfaceCreated(ISurfaceHolder holder)
    {
    }

    /// <summary>
    /// Uruchamia rysowanie mapy na powierzchni o ustalonym rozmiarze.
    /// </summary>
    /// <param name="holder">Uchwyt powierzchni.</param>
    /// <param name="format">Format powierzchni.</param>
    /// <param name="width">Szerokość w pikselach.</param>
    /// <param name="height">Wysokość w pikselach.</param>
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
    /// Zatrzymuje rysowanie, gdy powierzchnia jest niszczona.
    /// </summary>
    /// <param name="holder">Uchwyt powierzchni.</param>
    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        controller.Stop();
    }

    /// <summary>
    /// Przekazuje dotyk do detektorów gestów.
    /// </summary>
    /// <param name="e">Zdarzenie dotyku.</param>
    /// <returns>Zawsze true, aby otrzymywać kolejne zdarzenia gestu.</returns>
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
    /// Przekazuje gest przesuwania do kontrolera mapy.
    /// </summary>
    private sealed class PanListener : GestureDetector.SimpleOnGestureListener
    {
        /// <summary>
        /// Kontroler stanu mapy.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Tworzy listener powiązany z kontrolerem mapy.
        /// </summary>
        /// <param name="controller">Kontroler mapy.</param>
        public PanListener(MapController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// Przesuwa mapę o wartość gestu.
        /// </summary>
        /// <param name="e1">Zdarzenie rozpoczynające gest.</param>
        /// <param name="e2">Bieżące zdarzenie.</param>
        /// <param name="distanceX">Przesunięcie w poziomie od poprzedniego zdarzenia.</param>
        /// <param name="distanceY">Przesunięcie w pionie od poprzedniego zdarzenia.</param>
        /// <returns>Zawsze true.</returns>
        public override bool OnScroll(MotionEvent? e1, MotionEvent e2, float distanceX, float distanceY)
        {
            controller.Pan(distanceX, distanceY);

            return true;
        }

        /// <summary>
        /// Przekazuje dotknięcie do kontrolera, który zaznacza samolot w pobliżu palca.
        /// </summary>
        /// <param name="e">Zdarzenie dotknięcia.</param>
        /// <returns>Zawsze true.</returns>
        public override bool OnSingleTapUp(MotionEvent e)
        {
            controller.Tap(e.GetX(), e.GetY());

            return true;
        }

        /// <summary>
        /// Uruchamia bezwładne przesuwanie mapy po szybkim przeciągnięciu.
        /// </summary>
        /// <param name="e1">Zdarzenie rozpoczynające gest.</param>
        /// <param name="e2">Zdarzenie kończące gest.</param>
        /// <param name="velocityX">Prędkość w poziomie w pikselach na sekundę.</param>
        /// <param name="velocityY">Prędkość w pionie w pikselach na sekundę.</param>
        /// <returns>Zawsze true.</returns>
        public override bool OnFling(MotionEvent? e1, MotionEvent e2, float velocityX, float velocityY)
        {
            controller.Fling(velocityX, velocityY);

            return true;
        }
    }

    /// <summary>
    /// Przekazuje gest przybliżania do kontrolera mapy.
    /// </summary>
    private sealed class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        /// <summary>
        /// Kontroler stanu mapy.
        /// </summary>
        private readonly MapController controller;

        /// <summary>
        /// Tworzy listener powiązany z kontrolerem mapy.
        /// </summary>
        /// <param name="controller">Kontroler mapy.</param>
        public ScaleListener(MapController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// Zmienia zoom zgodnie ze skalą gestu.
        /// </summary>
        /// <param name="detector">Detektor gestu.</param>
        /// <returns>Zawsze true.</returns>
        public override bool OnScale(ScaleGestureDetector detector)
        {
            controller.Scale(detector.ScaleFactor);

            return true;
        }
    }
}

using AdsbCarMap.Data;
using Android.Graphics;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map;

/// <summary>
/// Kształty ikon samolotów według rodzaju, w jednostkach niezależnych od gęstości ekranu, skierowane na północ.
/// </summary>
public sealed class AircraftShapes
{
    /// <summary>
    /// Kształt ikony i jego skala względem podstawowego rozmiaru, według rodzaju.
    /// </summary>
    private readonly Dictionary<AircraftKind, (AndroidPath Path, float Scale)> shapes = new Dictionary<AircraftKind, (AndroidPath Path, float Scale)>();

    /// <summary>
    /// Tworzy wszystkie kształty ikon.
    /// </summary>
    public AircraftShapes()
    {
        shapes[AircraftKind.Unknown] = (CreateArrow(), 1.0f);
        shapes[AircraftKind.Airliner] = (CreateSymmetric(new float[] { 0, -14, 1.8f, -10, 1.8f, -3, 13, 5.5f, 13, 8.5f, 1.8f, 4.5f, 1.8f, 9.5f, 5.5f, 12.5f, 5.5f, 14.5f, 1.2f, 13.5f, 0, 15 }), 1.0f);
        shapes[AircraftKind.Heavy] = (CreateSymmetric(new float[] { 0, -15, 2.2f, -11, 2.2f, -3, 14, 6, 14, 9, 2.2f, 5, 2.2f, 10, 6, 13, 6, 15, 1.5f, 14, 0, 16 }), 1.25f);
        shapes[AircraftKind.SmallJet] = (CreateSymmetric(new float[] { 0, -12, 1.5f, -8, 1.5f, -1, 9, 5, 9, 7.5f, 1.5f, 4.5f, 1.5f, 9, 4.5f, 11.5f, 4.5f, 13, 1.2f, 12.5f, 0, 13.5f }), 0.9f);
        shapes[AircraftKind.Turboprop] = (CreateSymmetric(new float[] { 0, -12, 1.8f, -9, 1.8f, -4, 12.5f, -4, 12.5f, 0, 1.8f, 0.5f, 1.8f, 9, 5.5f, 11, 5.5f, 13, 1.2f, 12.5f, 0, 13.5f }), 0.95f);
        shapes[AircraftKind.LightPlane] = (CreateSymmetric(new float[] { 0, -10, 1.4f, -7, 1.4f, -3, 10, -3, 10, 1, 1.4f, 1.5f, 1.4f, 8, 4.5f, 9.5f, 4.5f, 11.5f, 1, 11, 0, 12 }), 0.85f);
        shapes[AircraftKind.Fighter] = (CreateSymmetric(new float[] { 0, -14, 1.3f, -9, 2, -3, 8, 4, 11, 9, 11, 11, 3, 9.5f, 2, 13, 0, 12 }), 0.9f);
        shapes[AircraftKind.MilitaryTransport] = (CreateSymmetric(new float[] { 0, -14, 2.4f, -10, 2.4f, -3, 14.5f, -2.5f, 14.5f, 2, 2.4f, 3, 2.4f, 9, 6.5f, 11, 6.5f, 13.5f, 1.5f, 13, 0, 15 }), 1.1f);
        shapes[AircraftKind.Glider] = (CreateSymmetric(new float[] { 0, -8, 1, -5, 1, -1, 16, -1, 16, 1.5f, 1, 2.5f, 1, 10, 4.5f, 11, 4.5f, 12.5f, 0.8f, 12, 0, 13 }), 0.9f);
        shapes[AircraftKind.Helicopter] = (CreateHelicopter(), 0.95f);
        shapes[AircraftKind.Balloon] = (CreateBalloon(), 0.9f);
        shapes[AircraftKind.Drone] = (CreateDrone(), 0.8f);
        shapes[AircraftKind.Ground] = (CreateGround(), 0.7f);
    }

    /// <summary>
    /// Zwraca kształt ikony i jego skalę dla rodzaju samolotu.
    /// </summary>
    /// <param name="kind">Rodzaj samolotu.</param>
    /// <returns>Ścieżka ikony skierowanej na północ i skala względem podstawowego rozmiaru.</returns>
    public (AndroidPath Path, float Scale) Get(AircraftKind kind)
    {
        return shapes[kind];
    }

    /// <summary>
    /// Buduje ogólną strzałkę używaną, gdy rodzaju nie da się ustalić.
    /// </summary>
    /// <returns>Ścieżka strzałki.</returns>
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
    /// Buduje symetryczną sylwetkę z punktów prawej strony, od dziobu do ogona. Pierwszy i ostatni punkt leżą na osi.
    /// </summary>
    /// <param name="rightSide">Współrzędne X i Y kolejnych punktów prawej strony.</param>
    /// <returns>Zamknięta ścieżka sylwetki.</returns>
    private static AndroidPath CreateSymmetric(float[] rightSide)
    {
        int count = rightSide.Length / 2;
        AndroidPath path = new AndroidPath();
        path.MoveTo(rightSide[0], rightSide[1]);

        for (int index = 1; index < count; index++)
        {
            path.LineTo(rightSide[2 * index], rightSide[2 * index + 1]);
        }

        for (int index = count - 2; index >= 1; index--)
        {
            path.LineTo(-rightSide[2 * index], rightSide[2 * index + 1]);
        }

        path.Close();

        return path;
    }

    /// <summary>
    /// Buduje śmigłowiec: korpus, belkę ogonową ze śmigłem ogonowym i dwie skrzyżowane łopaty wirnika.
    /// </summary>
    /// <returns>Ścieżka śmigłowca.</returns>
    private static AndroidPath CreateHelicopter()
    {
        AndroidPath path = new AndroidPath();
        path.AddOval(new RectF(-3f, -6f, 3f, 5f), AndroidPath.Direction.Cw!);
        path.AddRect(-0.9f, 4f, 0.9f, 14f, AndroidPath.Direction.Cw!);
        path.AddRect(-3f, 12.5f, 3f, 14f, AndroidPath.Direction.Cw!);

        AndroidPath blade = new AndroidPath();
        blade.AddRect(-12f, -0.9f, 12f, 0.9f, AndroidPath.Direction.Cw!);

        Matrix first = new Matrix();
        first.SetRotate(35f);
        path.AddPath(blade, first);

        Matrix second = new Matrix();
        second.SetRotate(125f);
        path.AddPath(blade, second);

        return path;
    }

    /// <summary>
    /// Buduje balon: czaszę i koszyk.
    /// </summary>
    /// <returns>Ścieżka balonu.</returns>
    private static AndroidPath CreateBalloon()
    {
        AndroidPath path = new AndroidPath();
        path.AddCircle(0f, -3f, 7f, AndroidPath.Direction.Cw!);
        path.AddRect(-2f, 6f, 2f, 10f, AndroidPath.Direction.Cw!);

        return path;
    }

    /// <summary>
    /// Buduje drona: cztery ramiona skrzyżowane pod kątem prostym i środek.
    /// </summary>
    /// <returns>Ścieżka drona.</returns>
    private static AndroidPath CreateDrone()
    {
        AndroidPath path = new AndroidPath();
        path.AddCircle(0f, 0f, 3.5f, AndroidPath.Direction.Cw!);

        AndroidPath arm = new AndroidPath();
        arm.AddRect(-10f, -1.2f, 10f, 1.2f, AndroidPath.Direction.Cw!);

        Matrix first = new Matrix();
        first.SetRotate(45f);
        path.AddPath(arm, first);

        Matrix second = new Matrix();
        second.SetRotate(-45f);
        path.AddPath(arm, second);

        return path;
    }

    /// <summary>
    /// Buduje znacznik obiektu naziemnego jako mały kwadrat.
    /// </summary>
    /// <returns>Ścieżka kwadratu.</returns>
    private static AndroidPath CreateGround()
    {
        AndroidPath path = new AndroidPath();
        path.AddRect(-5f, -5f, 5f, 5f, AndroidPath.Direction.Cw!);

        return path;
    }
}

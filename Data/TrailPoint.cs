namespace AdsbCarMap.Data
{
    /// <summary>
    /// Punkt śladu samolotu zapamiętany z jednej z wcześniejszych odpowiedzi API.
    /// </summary>
    /// <param name="Latitude">Szerokość geograficzna.</param>
    /// <param name="Longitude">Długość geograficzna.</param>
    /// <param name="AltitudeFeet">Wysokość barometryczna w stopach lub null, gdy nieznana.</param>
    /// <param name="OnGround">Informacja, czy samolot był na ziemi.</param>
    /// <param name="TimestampUtc">Moment pomiaru pozycji, w UTC.</param>
    public readonly record struct TrailPoint(double Latitude, double Longitude, int? AltitudeFeet, bool OnGround, DateTime TimestampUtc);
}

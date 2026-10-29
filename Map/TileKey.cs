namespace AdsbCarMap.Map
{
    /// <summary>
    /// Identyfikator kafelka mapy w schemacie XYZ.
    /// </summary>
    /// <param name="Zoom">Poziom przybliżenia.</param>
    /// <param name="X">Numer kolumny, w zakresie od 0 do 2^Zoom - 1.</param>
    /// <param name="Y">Numer wiersza, w zakresie od 0 do 2^Zoom - 1.</param>
    public readonly record struct TileKey(int Zoom, int X, int Y);
}

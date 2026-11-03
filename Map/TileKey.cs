namespace AdsbCarMap.Map
{
    /// <summary>
    /// Map tile identifier in the XYZ scheme.
    /// </summary>
    /// <param name="Zoom">Zoom level.</param>
    /// <param name="X">Column number, from 0 to 2^Zoom - 1.</param>
    /// <param name="Y">Row number, from 0 to 2^Zoom - 1.</param>
    public readonly record struct TileKey(int Zoom, int X, int Y);
}

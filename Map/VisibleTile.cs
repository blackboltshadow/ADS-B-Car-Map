namespace AdsbCarMap.Map
{
    /// <summary>
    /// Tile visible on the screen together with the position of its top left corner.
    /// </summary>
    /// <param name="Key">Tile identifier.</param>
    /// <param name="ScreenX">Position of the left edge on the screen in pixels.</param>
    /// <param name="ScreenY">Position of the top edge on the screen in pixels.</param>
    public readonly record struct VisibleTile(TileKey Key, float ScreenX, float ScreenY);
}

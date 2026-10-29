namespace AdsbCarMap.Map;

/// <summary>
/// Kafelek widoczny na ekranie wraz z pozycją jego lewego górnego rogu.
/// </summary>
/// <param name="Key">Identyfikator kafelka.</param>
/// <param name="ScreenX">Pozycja lewej krawędzi na ekranie w pikselach.</param>
/// <param name="ScreenY">Pozycja górnej krawędzi na ekranie w pikselach.</param>
public readonly record struct VisibleTile(TileKey Key, float ScreenX, float ScreenY);

namespace Gondwana.Scenes;

/// <summary>
/// Tiles -> draw from refresh queue dirty rectangles
/// All -> redraw layer
/// </summary>
public enum SceneRefreshType
{
    Tiles,
    All
}
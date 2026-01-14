namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Identifies the layout and math rules used to map tiles to world pixels
/// and world pixels back to grid coordinates. Each option defines a different
/// tile geometry.
/// Used by SceneLayer to choose the correct coordinate math for a given map.
/// </summary>
public enum CoordinateSystemTypes
{
    /// <summary>
    /// Square tiles laid out in a simple row/column grid.
    /// </summary>
    Orthographic = 0,

    /// <summary>
    /// Diamond-shaped isometric grid (axis-aligned diamonds).
    /// </summary>
    DiagIso_DiagMatrix = 1,

    /// <summary>
    /// Square-based isometric stepping (half-width diagonal steps).
    /// </summary>
    DiagIso_SquareMatrix = 2,

    /// <summary>
    /// Hexagonal tiles with flat tops (horizontal edges).
    /// </summary>
    HexFlatTop = 3,

    /// <summary>
    /// Hexagonal tiles with pointed tops (vertical edges).
    /// </summary>
    HexPointedTop = 4
}

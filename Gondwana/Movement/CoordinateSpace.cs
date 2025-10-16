namespace Gondwana.Movement;

/// <summary>
/// Specifies the coordinate system used for movement calculations and updates.
/// </summary>
public enum CoordinateSpace
{
    /// <summary>
    /// Movement is in the scene's grid/tile coordinate system.
    /// </summary>
    Grid,

    /// <summary>
    /// Movement is in pixel coordinates relative to the rendering layer.
    /// </summary>
    Pixel
}

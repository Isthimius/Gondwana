namespace Gondwana.Movement;

public enum MovementSpace
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

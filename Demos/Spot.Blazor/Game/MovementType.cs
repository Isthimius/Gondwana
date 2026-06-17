namespace Gondwana.Demos.SpotBlazor.Game;

internal enum MovementType
{
    /// <summary>
    /// out of bounds, or greater than 2 cells away
    /// </summary>
    Illegal,

    /// <summary>
    /// in bounds, 1 cell away
    /// </summary>
    Clone,

    /// <summary>
    /// in bounds, 2 cells away
    /// </summary>
    Jump
}

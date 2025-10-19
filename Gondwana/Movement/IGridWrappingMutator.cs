namespace Gondwana.Movement;

/// <summary>
/// Grid-space only: enables toroidal wrapping on X/Y.
/// Implement only for movers whose MovementState uses Grid space.
/// </summary>
public interface IGridWrappingMutator
{
    MovementState MovementState { get; }

    void SetWrapX(bool enabled);
    void SetWrapY(bool enabled);
    void SetWrap(bool wrapX, bool wrapY);
}

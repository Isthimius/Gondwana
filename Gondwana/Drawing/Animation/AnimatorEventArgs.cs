namespace Gondwana.Drawing.Animation;

/// <summary>
/// Provides data for animator-related events, containing references to the tile and animator involved in the event.
/// </summary>
public class AnimatorEventArgs : EventArgs
{
    /// <summary>
    /// The tile associated with the animator event.
    /// </summary>
    public Tile Tile;

    /// <summary>
    /// The animator that raised the event.
    /// </summary>
    public Animator Animator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatorEventArgs"/> class with the specified tile and animator.
    /// </summary>
    /// <param name="tile">The tile associated with the event.</param>
    /// <param name="animator">The animator that raised the event.</param>
    protected internal AnimatorEventArgs(Tile tile, Animator animator)
    {
        Tile = tile;
        Animator = animator;
    }
}

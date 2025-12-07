namespace Gondwana.Drawing.Animation;

public class AnimatorEventArgs : EventArgs
{
    public Tile Tile;
    public Animator Animator;

    protected internal AnimatorEventArgs(Tile tile, Animator animator)
    {
        Tile = tile;
        Animator = animator;
    }
}

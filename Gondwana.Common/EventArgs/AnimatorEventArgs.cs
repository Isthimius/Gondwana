using Gondwana.Common.Drawing.Animation;

namespace Gondwana.Common.EventArgs
{
    public delegate void AnimatorEventHandler(AnimatorEventArgs e);

    public class AnimatorEventArgs : System.EventArgs
    {
        public Tile tile;
        public Animator animator;

        protected internal AnimatorEventArgs(Tile _tile, Animator _animator)
        {
            tile = _tile;
            animator = _animator;
        }
    }
}
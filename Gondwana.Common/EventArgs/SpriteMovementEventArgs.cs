using Gondwana.Common.Drawing.Sprites;

namespace Gondwana.Common.EventArgs
{
    public delegate void SpriteMovementEventHandler(SpriteMovementEventArgs e);

    public class SpriteMovementEventArgs : System.EventArgs
    {
        public Sprite sprite;
        public Movement movement;

        protected internal SpriteMovementEventArgs(Sprite _sprite, Movement _movement)
        {
            sprite = _sprite;
            movement = _movement;
        }
    }
}

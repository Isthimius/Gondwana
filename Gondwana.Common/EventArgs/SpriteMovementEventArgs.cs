using Gondwana.Drawing.Sprites;

namespace Gondwana.EventArgs;

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

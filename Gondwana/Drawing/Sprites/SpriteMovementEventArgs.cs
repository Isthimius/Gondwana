namespace Gondwana.Drawing.Sprites;

public delegate void SpriteMovementEventHandler(SpriteMovementEventArgs e);

public class SpriteMovementEventArgs : EventArgs
{
    public Sprite sprite;
    public Movement movement;

    protected internal SpriteMovementEventArgs(Sprite _sprite, Movement _movement)
    {
        sprite = _sprite;
        movement = _movement;
    }
}

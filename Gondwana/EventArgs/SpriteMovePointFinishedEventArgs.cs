using Gondwana.Drawing.Sprites;

namespace Gondwana.EventArgs;

public delegate void SpriteMovePointFinishedHandler(SpriteMovePointFinishedEventArgs e);

public class SpriteMovePointFinishedEventArgs : System.EventArgs
{
    public Sprite sprite;
    public Movement movement;
    public MovePoint movePoint;

    protected internal SpriteMovePointFinishedEventArgs(Sprite _sprite, Movement _movement, MovePoint _movePoint)
    {
        sprite = _sprite;
        movement = _movement;
        movePoint = _movePoint;
    }
}

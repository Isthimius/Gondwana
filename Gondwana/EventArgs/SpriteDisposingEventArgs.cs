using Gondwana.Drawing.Sprites;

namespace Gondwana.EventArgs;

public delegate void SpriteDisposingEventHandler(SpriteDisposingEventArgs e);

public class SpriteDisposingEventArgs : System.EventArgs
{
    public Sprite sprite;

    protected internal SpriteDisposingEventArgs(Sprite _sprite)
    {
        sprite = _sprite;
    }
}

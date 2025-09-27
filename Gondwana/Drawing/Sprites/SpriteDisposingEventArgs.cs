namespace Gondwana.Drawing.Sprites;

public delegate void SpriteDisposingEventHandler(SpriteDisposingEventArgs e);

public class SpriteDisposingEventArgs : EventArgs
{
    public Sprite sprite;

    protected internal SpriteDisposingEventArgs(Sprite _sprite)
    {
        sprite = _sprite;
    }
}
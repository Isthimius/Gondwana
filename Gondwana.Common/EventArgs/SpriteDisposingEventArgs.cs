using Gondwana.Common.Drawing.Sprites;

namespace Gondwana.Common.EventArgs
{
    public delegate void SpriteDisposingEventHandler(SpriteDisposingEventArgs e);

    public class SpriteDisposingEventArgs : System.EventArgs
    {
        public Sprite sprite;

        protected internal SpriteDisposingEventArgs(Sprite _sprite)
        {
            sprite = _sprite;
        }
    }
}

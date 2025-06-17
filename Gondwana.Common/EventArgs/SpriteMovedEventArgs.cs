using Gondwana.Drawing.Sprites;
using Gondwana.Grid;
using System.Drawing;

namespace Gondwana.Common.EventArgs
{
    public delegate void SpriteMovedEventHandler(SpriteMovedEventArgs e);

    public class SpriteMovedEventArgs : System.EventArgs
    {
        public Sprite sprite;
        public PointF oldPt;
        public PointF newPt;

        protected internal SpriteMovedEventArgs(Sprite _sprite, PointF _oldPt, PointF _newPt)
        {
            sprite = _sprite;
            oldPt = _oldPt;
            newPt = _newPt;
        }
    }
}

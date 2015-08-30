using System.Drawing;
using System.Windows.Forms;

namespace Gondwana.Common.Drawing.Direct
{
    public class Text : DirectDrawing
    {
        #region private fields
        private string _text;
        private Font _font;
        private Color _foreColor;
        private Color _backColor;
        private TextFormatFlags _flags;
        #endregion

        #region constructors / finalizer
        public Text(VisibleSurfaceBase surface, string text, Font font,
            Rectangle bounds, Color foreColor, Color backColor)
            : base(surface, bounds)
        {
            InstantiateNew(text, font, foreColor, backColor, TextFormatFlags.Default);
        }

        public Text(VisibleSurfaceBase surface, string text, Font font, Rectangle bounds,
            Color foreColor, Color backColor, TextFormatFlags flags)
            : base(surface, bounds)
        {
            InstantiateNew(text, font, foreColor, backColor, flags);
        }

        ~Text()
        {
            Dispose();
        }
        #endregion

        #region public properties
        public string TextDisplay
        {
            get { return _text; }
            set
            {
                _text = value;
                ForceRefresh();
            }
        }
        #endregion

        #region private methods
        private void InstantiateNew(string text, Font font,
            Color foreColor, Color backColor, TextFormatFlags flags)
        {
            _text = text;
            _font = font;
            _foreColor = foreColor;
            _backColor = backColor;
            _flags = flags;
        }
        #endregion

        #region inherited from DirectDrawing
        protected internal override void Render()
        {
            TextRenderer.DrawText(_surface.Buffer.DC, _text, _font, _bounds, _foreColor, _backColor, _flags);
        }
        #endregion
    }
}

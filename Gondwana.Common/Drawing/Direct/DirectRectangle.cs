using System.Drawing;

namespace Gondwana.Common.Drawing.Direct
{
    public class DirectRectangle : DirectDrawing
    {
        #region private fields

        private bool _isExplicitAlphaBlended;
        private int _width;
        private Color _color;
        private int _alpha;
        private bool _isFilled;

        #endregion private fields

        #region constructors / finalizer

        public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, int width)
            : base(surface, bounds)
        {
            _isExplicitAlphaBlended = false;
            _width = width;
            _color = color;
            _alpha = color.A;
            _isFilled = false;
        }

        public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, bool isFilled)
            : base(surface, bounds)
        {
            _isExplicitAlphaBlended = false;
            _width = 1;
            _color = color;
            _alpha = color.A;
            _isFilled = isFilled;
        }

        public DirectRectangle(VisibleSurfaceBase surface, Rectangle bounds, Color color, bool isFilled, int alpha)
            : base(surface, bounds)
        {
            _isExplicitAlphaBlended = true;
            _width = 1;
            _color = color;
            _alpha = alpha;
            _isFilled = true;
        }

        #endregion constructors / finalizer

        #region public properties

        public bool IsAlphaBlended
        {
            get { return ((_color.A != 255) || (_isExplicitAlphaBlended)); }
        }

        public Color BaseColor
        {
            get { return _color; }
        }

        public int Width
        {
            get { return _width; }
        }

        public int Alpha
        {
            get { return _alpha; }
        }

        public bool IsFilled
        {
            get { return _isFilled; }
        }

        #endregion public properties

        #region inherited from DirectDrawing

        protected internal override void Render()
        {
            if (_isExplicitAlphaBlended)
                _color = Color.FromArgb(_alpha, _color.R, _color.G, _color.B);

            if (_isFilled)
                _surface.Buffer.DC.FillRectangle(new SolidBrush(_color), _bounds);
            else
                _surface.Buffer.DC.DrawRectangle(new Pen(_color, _width), _bounds);
        }

        #endregion inherited from DirectDrawing
    }
}
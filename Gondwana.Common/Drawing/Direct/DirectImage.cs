using Gondwana.Common;
using Gondwana.Common.Win32;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

public class DirectImage : DirectDrawing
{
    #region private fields
    private Tilesheet _tilesheet;
    private TernaryRasterOperations _rasterOp;
    #endregion

    #region constructors
    public DirectImage(VisibleSurfaceBase surface, Rectangle bounds, Tilesheet bmp)
        : base(surface, bounds)
    {
        _tilesheet = bmp;
        _rasterOp = TernaryRasterOperations.SRCCOPY;
    }

    public DirectImage(VisibleSurfaceBase surface, Rectangle bounds, Tilesheet bmp,
        TernaryRasterOperations rasterOp)
        : base(surface, bounds)
    {
        _tilesheet = bmp;
        _rasterOp = rasterOp;
    }
    #endregion

    #region public properties
    public Tilesheet Tilesheet
    {
        get { return _tilesheet; }
    }

    public TernaryRasterOperations RasterOp
    {
        get { return _rasterOp; }
    }
    #endregion

    #region inherited from DirectDrawing
    protected internal override void Render()
    {
        // obtain exclusive lock on backbuffer dc for GDI blit
        IntPtr hDC = _surface.Buffer.DC.GetHdc();

        // if the tilesheet has a mask...
        if (_tilesheet.Mask != null)
        {
            // AND the mask
            Win32Support.DrawBitmap(hDC, _bounds,
                _tilesheet.Mask.hDC, new Rectangle(new Point(), _tilesheet.Bmp.Size),
                TernaryRasterOperations.SRCAND);

            // PAINT the primary
            Win32Support.DrawBitmap(hDC, _bounds,
                _tilesheet.hDC, new Rectangle(new Point(), _tilesheet.Bmp.Size),
                TernaryRasterOperations.SRCPAINT);
        }
        else
        {
            // straight COPY the primary
            Win32Support.DrawBitmap(hDC, _bounds,
                _tilesheet.hDC, new Rectangle(new Point(), _tilesheet.Bmp.Size),
                _rasterOp);
        }

        // release lock on backbuffer dc
        _surface.Buffer.DC.ReleaseHdc(hDC);
    }
    #endregion
}

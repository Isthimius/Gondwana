using Gondwana.Common;
using Gondwana.Drawing.Direct;
using Gondwana.EventArgs;
using Gondwana.Grid;
using Gondwana.Common.Win32;
using System.Drawing;
using System.Windows.Forms;

namespace Gondwana.Rendering;

public class VisibleSurface : VisibleSurfaceBase
{
    #region private delegates
    private delegate void RenderFromBackbufferDel();
    private RenderFromBackbufferDel RenderFromBackbuffer;
    #endregion

    public event VisibleSurfaceBindEventHandler VisibleSurfaceBind;

    #region constructors / finalizer
    public VisibleSurface(Graphics graphics, int wdth, int hght)
        : base(wdth, hght)
    {
        base.DC = graphics;
        base.Buffer = new Backbuffer(this);
        this.RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Graphics graphics, int wdth, int hght, GridPointMatrixes drawSource)
        : base(wdth, hght)
    {
        base.DC = graphics;
        base.Buffer = new Backbuffer(this)
            {
                DrawSource = drawSource
            };
        this.RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface)
        : base(surface.Width, surface.Height)
    {
        base.DC = surface.CreateGraphics();
        base.Buffer = new Backbuffer(this);
        this.RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface, GridPointMatrixes drawSource)
        : base(surface.Width, surface.Height)
    {
        base.DC = surface.CreateGraphics();
        base.Buffer = new Backbuffer(this)
            {
                DrawSource = drawSource
            };
        this.RedrawDirtyRectangleOnly = true;
    }

    ~VisibleSurface()
    {
        Dispose();
    }
    #endregion

    #region public properties
    public virtual new bool RedrawDirtyRectangleOnly
    {
        get { return base.RedrawDirtyRectangleOnly; }
        set
        {
            base.RedrawDirtyRectangleOnly = value;
            if (base.RedrawDirtyRectangleOnly)
                RenderFromBackbuffer = new RenderFromBackbufferDel(RenderBackbufferRect);
            else
                RenderFromBackbuffer = new RenderFromBackbufferDel(RenderBackbufferAll);

            ((Backbuffer)Buffer).DirtyRectangle = new Rectangle(0, 0, Buffer.Width, Buffer.Height);
        }
    }
    #endregion

    #region public / protected methods
    public override void Erase()
    {
        IntPtr hDC = DC.GetHdc();
        Win32Support.DrawBitmap(hDC, 0, 0, Width, Height, hDC, 0, 0, Width, Height, TernaryRasterOperations.BLACKNESS);
        DC.ReleaseHdc(hDC);
    }

    public override void Bind(GridPointMatrixes layers)
    {
        GridPointMatrixes oldBind = Buffer.DrawSource;
        ((Backbuffer)Buffer).DrawSource = layers;

        if (VisibleSurfaceBind != null)
            VisibleSurfaceBind(new VisibleSufaceBindEventArgs(this, oldBind, layers));
    }

    protected internal void RenderBackbuffer()
    {
        RenderFromBackbuffer();
    }

    public override void RenderBackbuffer(bool onlyDirtyRectangle)
    {
        if (onlyDirtyRectangle)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();
    }
    #endregion

    #region private methods
    private void RenderBackbufferAll()
    {
        IntPtr hDC = DC.GetHdc();
        IntPtr hDCBuffer = Buffer.DC.GetHdc();

        Win32Support.DrawBitmap(hDC, 0, 0, Width, Height, hDCBuffer, 0, 0, Width, Height, TernaryRasterOperations.SRCCOPY);

        DC.ReleaseHdc(hDC);
        Buffer.DC.ReleaseHdc(hDCBuffer);
    }

    private void RenderBackbufferRect()
    {
        if (!Buffer.DirtyRectangle.IsEmpty)
        {
            IntPtr hDC = DC.GetHdc();
            IntPtr hDCBuffer = Buffer.DC.GetHdc();

            Win32Support.DrawBitmap(hDC, Buffer.DirtyRectangle, hDCBuffer, Buffer.DirtyRectangle, TernaryRasterOperations.SRCCOPY);

            DC.ReleaseHdc(hDC);
            Buffer.DC.ReleaseHdc(hDCBuffer);

            // dirty rectangle drawn, so clear it out for next cycle
            ((Backbuffer)Buffer).DirtyRectangle = new Rectangle();
        }
    }
    #endregion

    #region IDisposable Members
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);

        base.Dispose();
        VisibleSurfaceBind = null;

        DirectDrawing[] drawings = new DirectDrawing[DirectDrawing.Count];
        DirectDrawing.AllDirectDrawings.CopyTo(drawings, 0);

        for (int i = 0; i < drawings.GetLength(0); i++)
        {
            if (drawings[i].Surface == this)
                drawings[i].Dispose();
        }
    }
    #endregion
}

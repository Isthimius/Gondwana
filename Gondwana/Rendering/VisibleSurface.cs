using Gondwana.Common;
using Gondwana.Common.Win32;
using Gondwana.Grid;
using Gondwana.Rendering.Direct;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gondwana.Rendering;

public class VisibleSurface : VisibleSurfaceBase
{
    private Action RenderFromBackbuffer = () => { };

    public event EventHandler<VisibleSufaceBindEventArgs>? VisibleSurfaceBind;

    public VisibleSurface(Graphics graphics, int width, int height)
        : base(width, height)
    {
        DC = graphics;
        Buffer = new Backbuffer(this);
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Graphics graphics, int width, int height, GridPointMatrixes drawSource)
        : base(width, height)
    {
        DC = graphics;
        Buffer = new Backbuffer(this)
        {
            DrawSource = drawSource
        };
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface)
        : base(surface.Width, surface.Height)
    {
        DC = surface.CreateGraphics();
        Buffer = new Backbuffer(this);
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface, GridPointMatrixes drawSource)
        : base(surface.Width, surface.Height)
    {
        DC = surface.CreateGraphics();
        Buffer = new Backbuffer(this)
        {
            DrawSource = drawSource
        };
        RedrawDirtyRectangleOnly = true;
    }

    public override bool RedrawDirtyRectangleOnly
    {
        get => base.RedrawDirtyRectangleOnly;
        protected internal set
        {
            base.RedrawDirtyRectangleOnly = value;
            RenderFromBackbuffer = value ? RenderBackbufferRect : RenderBackbufferAll;

            if (Buffer is Backbuffer backbuffer)
            {
                backbuffer.DirtyRectangle = new Rectangle(0, 0, Buffer.Width, Buffer.Height);
            }
        }
    }

    public override void Erase()
    {
        var hdc = DC.GetHdc();
        Win32Support.DrawBitmap(hdc, 0, 0, Width, Height, hdc, 0, 0, Width, Height, TernaryRasterOperations.BLACKNESS);
        DC.ReleaseHdc(hdc);
    }

    public override void Bind(GridPointMatrixes layers)
    {
        if (Buffer is not Backbuffer backbuffer)
            return;

        var oldBind = backbuffer.DrawSource;
        backbuffer.DrawSource = layers;

        VisibleSurfaceBind?.Invoke(this, new VisibleSufaceBindEventArgs(this, oldBind, layers));
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

    private void RenderBackbufferAll()
    {
        var hdc = DC.GetHdc();
        var hdcBuffer = Buffer.DC.GetHdc();

        Win32Support.DrawBitmap(hdc, 0, 0, Width, Height, hdcBuffer, 0, 0, Width, Height, TernaryRasterOperations.SRCCOPY);

        DC.ReleaseHdc(hdc);
        Buffer.DC.ReleaseHdc(hdcBuffer);
    }

    private void RenderBackbufferRect()
    {
        if (Buffer is not Backbuffer backbuffer || backbuffer.DirtyRectangle.IsEmpty)
            return;

        var hdc = DC.GetHdc();
        var hdcBuffer = Buffer.DC.GetHdc();

        Win32Support.DrawBitmap(hdc, backbuffer.DirtyRectangle, hdcBuffer, backbuffer.DirtyRectangle, TernaryRasterOperations.SRCCOPY);

        DC.ReleaseHdc(hdc);
        Buffer.DC.ReleaseHdc(hdcBuffer);

        backbuffer.DirtyRectangle = new Rectangle();
    }

    public override void Dispose()
    {
        base.Dispose();
        VisibleSurfaceBind = null;

        var drawings = new DirectDrawing[DirectDrawing.Count];
        DirectDrawing.AllDirectDrawings.CopyTo(drawings, 0);

        foreach (var drawing in drawings)
        {
            if (drawing.Surface == this)
                drawing.Dispose();
        }
    }
}

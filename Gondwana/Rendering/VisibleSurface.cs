using Gondwana.Common.Win32;
using Gondwana.Grid;
using Gondwana.Rendering.Direct;
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
        Canvas = graphics;
        Buffer = new Backbuffer(this);
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Graphics graphics, int width, int height, GridPointMatrixes drawSource)
        : base(width, height)
    {
        Canvas = graphics;
        Buffer = new Backbuffer(this)
        {
            DrawSource = drawSource
        };
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface)
        : base(surface.Width, surface.Height)
    {
        Canvas = surface.CreateGraphics();
        Buffer = new Backbuffer(this);
        RedrawDirtyRectangleOnly = true;
    }

    public VisibleSurface(Control surface, GridPointMatrixes drawSource)
        : base(surface.Width, surface.Height)
    {
        Canvas = surface.CreateGraphics();
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
        var hCanvas = Canvas.GetHCanvas();
        Win32Support.DrawBitmap(hCanvas, 0, 0, Width, Height, hCanvas, 0, 0, Width, Height, TernaryRasterOperations.BLACKNESS);
        Canvas.ReleaseHCanvas(hCanvas);
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
        var hCanvas = Canvas.GetHCanvas();
        var hCanvasBuffer = Buffer.Canvas.GetHCanvas();

        Win32Support.DrawBitmap(hCanvas, 0, 0, Width, Height, hCanvasBuffer, 0, 0, Width, Height, TernaryRasterOperations.SRCCOPY);

        Canvas.ReleaseHCanvas(hCanvas);
        Buffer.Canvas.ReleaseHCanvas(hCanvasBuffer);
    }

    private void RenderBackbufferRect()
    {
        if (Buffer is not Backbuffer backbuffer || backbuffer.DirtyRectangle.IsEmpty)
            return;

        var hCanvas = Canvas.GetHCanvas();
        var hCanvasBuffer = Buffer.Canvas.GetHCanvas();

        Win32Support.DrawBitmap(hCanvas, backbuffer.DirtyRectangle, hCanvasBuffer, backbuffer.DirtyRectangle, TernaryRasterOperations.SRCCOPY);

        Canvas.ReleaseHCanvas(hCanvas);
        Buffer.Canvas.ReleaseHCanvas(hCanvasBuffer);

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

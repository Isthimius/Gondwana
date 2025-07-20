using Gondwana.Grid;
using SkiaSharp;

namespace Gondwana.Rendering;

public class VisibleSurface : VisibleSurfaceBase
{
    private Action RenderFromBackbuffer = () => { };

    public event EventHandler<VisibleSurfaceBindEventArgs>? VisibleSurfaceBind;

    public VisibleSurface(int width, int height)
        : base(width, height)
    {
        Surface = SKSurface.Create(new SKImageInfo(width, height));
        Canvas = Surface.Canvas;

        Buffer = BackbufferFactory.Create(width, height);
        RedrawDirtyRectangleOnly = true;
    }

    /// <summary>
    /// Backing surface for visible rendering target.
    /// </summary>
    public SKSurface Surface { get; }

    /// <summary>
    /// Render target canvas exposed for external drawing.
    /// </summary>
    public SKCanvas Canvas { get; }

    public override bool RedrawDirtyRectangleOnly
    {
        get => base.RedrawDirtyRectangleOnly;
        protected internal set
        {
            base.RedrawDirtyRectangleOnly = value;
            RenderFromBackbuffer = value ? RenderBackbufferRect : RenderBackbufferAll;
            Buffer.DirtyRectangle = new System.Drawing.Rectangle(0, 0, Buffer.Width, Buffer.Height);
        }
    }

    public override void Erase()
    {
        Buffer.Erase();
    }

    public override void Bind(GridPointMatrixes layers)
    {
        var oldBind = Buffer.DrawSource;
        Buffer.DrawSource = layers;

        VisibleSurfaceBind?.Invoke(this, new VisibleSurfaceBindEventArgs(this, oldBind, layers));
    }

    public override void RenderBackbuffer(bool resetDirtyRegion = true)
    {
        RenderFromBackbuffer();

        if (resetDirtyRegion)
        {
            Buffer.DirtyRectangle = System.Drawing.Rectangle.Empty;
        }
    }

    private void RenderBackbufferAll()
    {
        using var snapshot = Buffer.Snapshot();
        Canvas.DrawImage(snapshot, new SKPoint(0, 0));
    }

    private void RenderBackbufferRect()
    {
        var dirty = Buffer.DirtyRectangle;
        if (dirty.IsEmpty)
            return;

        using var snapshot = Buffer.Snapshot();
        var skRect = dirty.ToSKRect();
        Canvas.DrawImage(snapshot, skRect, skRect);
    }
}

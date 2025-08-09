using Gondwana.Drawing;
using Gondwana.Scenes;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public class GpuBackbuffer : BackbufferBase
{
    private readonly GRContext _grContext;
    private readonly GRBackendRenderTarget _renderTarget;
    private readonly SKSurface _surface;
    
    public GpuBackbuffer(int width, int height)
        : base(width, height)
    {
        _grContext = GRContext.CreateGl() ?? throw new InvalidOperationException("No active OpenGL context.");

        var glInfo = new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat());

        _renderTarget = new GRBackendRenderTarget(
            width, height,
            sampleCount: 0,
            stencilBits: 8,
            glInfo: glInfo);

        _surface = SKSurface.Create(
            _grContext,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888,
            (SKColorSpace?)null                    // color space (null means sRGB)
        ) ?? throw new InvalidOperationException("Could not create GPU surface.");
    }

    public override SKCanvas Canvas => _surface.Canvas;
    public override void DrawTileFrame(Tile tile)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image != null)
            Canvas.DrawImage(image, tile.DrawLocation.ToSKRect());
    }
    public override SKImage Snapshot() => _surface.Snapshot();

    public override void Dispose()
    {
        base.Dispose();

        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
    }
}

using System.Drawing;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Engine-owned command recording destination. It never allocates a GPU surface,
/// performs a readback, or accesses the GL-owned backbuffer canvas.
/// </summary>
internal sealed class RecordingBackbuffer : BackbufferBase
{
    private readonly SKPictureRecorder _recorder = new();
    private SKCanvas? _canvas;

    internal RecordingBackbuffer() : base(1, 1) { }

    public override SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("No recording is active.");
    public override bool IsGlThreadRendered => true;

    internal void Start(BackbufferBase source)
    {
        UpdateSize(source.Width, source.Height);
        ClearColor = source.ClearColor;
        FogPaint.Dispose();
        GridLinePaint.Dispose();
        CollisionBoxPaint.Dispose();
        FogPaint = source.FogPaint.Clone();
        GridLinePaint = source.GridLinePaint.Clone();
        CollisionBoxPaint = source.CollisionBoxPaint.Clone();
        _canvas = _recorder.BeginRecording(new SKRect(0, 0, Width, Height));
    }

    internal SKPicture Complete()
    {
        var picture = _recorder.EndRecording();
        _canvas = null;
        return picture;
    }

    internal void Cancel()
    {
        if (_canvas is not null) Complete().Dispose();
    }

    protected internal override void BeginFrame() { }
    protected internal override void EndFrame() { }
    protected internal override SKImage Snapshot() =>
        throw new NotSupportedException("A recording has commands, not pixels. Read pixels on the GL thread after replay.");

    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image is not null) Canvas.DrawImage(image, destRectScreen.ToSKRect());
    }

    public override void Dispose()
    {
        Cancel();
        _recorder.Dispose();
        base.Dispose();
    }
}

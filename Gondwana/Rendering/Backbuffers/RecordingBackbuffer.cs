using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Engine-owned command recording destination. It never allocates a GPU surface,
/// performs a readback, or accesses the GL-owned backbuffer canvas.
/// </summary>
internal sealed class RecordingBackbuffer : BackbufferBase
{
    private const int MaxAtlasBatchSize = 64;

    private sealed class AtlasBatchBuffer(int size)
    {
        internal SKRect[] Sprites { get; } = new SKRect[size];
        internal SKRotationScaleMatrix[] Transforms { get; } = new SKRotationScaleMatrix[size];
    }

    private readonly SKPictureRecorder _recorder = new();
    private readonly SKRect[] _pendingSprites = new SKRect[MaxAtlasBatchSize];
    private readonly SKRotationScaleMatrix[] _pendingTransforms =
        new SKRotationScaleMatrix[MaxAtlasBatchSize];
    private readonly SKImage?[] _pendingFrameImages = new SKImage?[MaxAtlasBatchSize];
    private readonly SKRect[] _pendingDestinations = new SKRect[MaxAtlasBatchSize];
    private readonly Dictionary<int, AtlasBatchBuffer> _partialBatchBuffers = new()
    {
        [2] = new(2),
        [4] = new(4),
        [8] = new(8),
        [16] = new(16),
        [32] = new(32)
    };

    private SKCanvas? _canvas;
    private SKImage? _pendingAtlas;
    private int _pendingCount;

    internal int AtlasBatchCount { get; private set; }
    internal int AtlasBatchedTileCount { get; private set; }

    internal RecordingBackbuffer() : base(1, 1) { }

    public override SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("No recording is active.");
    public override bool IsGlThreadRendered => true;

    internal void Start(BackbufferBase source)
    {
        ResetPendingBatch();
        AtlasBatchCount = 0;
        AtlasBatchedTileCount = 0;

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
        FlushTileBatch();
        var picture = _recorder.EndRecording();
        _canvas = null;
        ResetPendingBatch();
        return picture;
    }

    internal void Cancel()
    {
        ResetPendingBatch();

        if (_canvas is null)
            return;

        var picture = _recorder.EndRecording();
        _canvas = null;
        picture.Dispose();
    }

    internal bool TryQueueTile(SceneLayerTile tile, RectangleF destination)
    {
        if (tile.Transform != TileTransform.Identity)
            return false;

        var frame = tile.CurrentFrame;
        var frameImage = frame.SkImage;
        var atlas = frame.AtlasImage;
        var source = frame.AtlasSourceBounds;

        if (frameImage is null ||
            atlas is null ||
            source.IsEmpty ||
            destination.Width <= 0 ||
            destination.Height <= 0)
        {
            return false;
        }

        float scaleX = destination.Width / source.Width;
        float scaleY = destination.Height / source.Height;
        float tolerance = MathF.Max(0.0001f, MathF.Abs(scaleX) * 0.0001f);

        if (!float.IsFinite(scaleX) ||
            !float.IsFinite(scaleY) ||
            MathF.Abs(scaleX - scaleY) > tolerance)
        {
            return false;
        }

        if (_pendingAtlas is not null && !ReferenceEquals(_pendingAtlas, atlas))
            FlushTileBatch();

        _pendingAtlas = atlas;
        int index = _pendingCount++;
        _pendingSprites[index] = source.ToSKRect();
        _pendingTransforms[index] = new SKRotationScaleMatrix(
            scaleX,
            0f,
            destination.Left,
            destination.Top);
        _pendingFrameImages[index] = frameImage;
        _pendingDestinations[index] = destination.ToSKRect();

        if (_pendingCount == MaxAtlasBatchSize)
            FlushFullTileBatch();

        return true;
    }

    internal void FlushTileBatch()
    {
        if (_pendingCount == 0)
        {
            _pendingAtlas = null;
            return;
        }

        if (_pendingAtlas is null)
            throw new InvalidOperationException("Atlas batch has queued tiles without an atlas image.");

        int offset = 0;
        int remaining = _pendingCount;

        foreach (int size in new[] { 32, 16, 8, 4, 2 })
        {
            while (remaining >= size)
            {
                var buffers = _partialBatchBuffers[size];
                Array.Copy(_pendingSprites, offset, buffers.Sprites, 0, size);
                Array.Copy(_pendingTransforms, offset, buffers.Transforms, 0, size);

                Canvas.DrawAtlas(
                    _pendingAtlas,
                    buffers.Sprites,
                    buffers.Transforms,
                    SKSamplingOptions.Default);

                AtlasBatchCount++;
                AtlasBatchedTileCount += size;
                offset += size;
                remaining -= size;
            }
        }

        if (remaining == 1)
        {
            var image = _pendingFrameImages[offset];
            if (image is not null)
                Canvas.DrawImage(image, _pendingDestinations[offset]);
        }

        ResetPendingBatch();
    }

    private void FlushFullTileBatch()
    {
        if (_pendingAtlas is null || _pendingCount != MaxAtlasBatchSize)
            return;

        Canvas.DrawAtlas(
            _pendingAtlas,
            _pendingSprites,
            _pendingTransforms,
            SKSamplingOptions.Default);

        AtlasBatchCount++;
        AtlasBatchedTileCount += MaxAtlasBatchSize;
        _pendingCount = 0;
        Array.Clear(_pendingFrameImages);
    }

    private void ResetPendingBatch()
    {
        _pendingAtlas = null;
        _pendingCount = 0;
        Array.Clear(_pendingFrameImages);
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

using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering.Views;
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
    private const int MaxAtlasBatchSize = 256;
    private static readonly int[] PartialBatchSizes = [128, 64, 32, 16, 8, 4, 2, 1];

    private sealed class AtlasBatchBuffer(int size)
    {
        internal SKRect[] Sprites { get; } = new SKRect[size];
        internal SKRotationScaleMatrix[] Transforms { get; } = new SKRotationScaleMatrix[size];
    }

    private readonly record struct FrameAtlasKey(
        Tilesheet Tilesheet,
        string RegionName,
        int XTile,
        int YTile);

    private readonly record struct FrameAtlasInfo(
        SKImage Atlas,
        SKRect Source,
        Spacing Overhang);

    internal readonly record struct FixedGridRenderPlan(
        SceneLayer Layer,
        Rectangle WorldRect);

    private readonly record struct LayerScreenMap(
        float OffsetX,
        float OffsetY,
        float ScaleX,
        float ScaleY)
    {
        internal static LayerScreenMap Create(View view, SceneLayer layer)
        {
            RectangleF unit = view.WorldRectToScreenRect(
                layer,
                new RectangleF(0f, 0f, 1f, 1f));

            return new(unit.Left, unit.Top, unit.Width, unit.Height);
        }

        internal RectangleF Map(Rectangle world) =>
            new(
                OffsetX + world.Left * ScaleX,
                OffsetY + world.Top * ScaleY,
                world.Width * ScaleX,
                world.Height * ScaleY);
    }

    private readonly SKPictureRecorder _recorder = new();
    private readonly SKRect[] _pendingSprites = new SKRect[MaxAtlasBatchSize];
    private readonly SKRotationScaleMatrix[] _pendingTransforms =
        new SKRotationScaleMatrix[MaxAtlasBatchSize];
    private readonly Dictionary<FrameAtlasKey, FrameAtlasInfo> _frameAtlasCache = [];
    private readonly Dictionary<int, AtlasBatchBuffer> _partialBatchBuffers = new()
    {
        [1] = new(1),
        [2] = new(2),
        [4] = new(4),
        [8] = new(8),
        [16] = new(16),
        [32] = new(32),
        [64] = new(64),
        [128] = new(128)
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
        _frameAtlasCache.Clear();
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

    internal bool TryPrepareFixedGridLayer(
        SceneLayer layer,
        Rectangle worldRect,
        out FixedGridRenderPlan plan)
    {
        plan = default;

        if (layer.WrapHorizontally ||
            layer.WrapVertically ||
            layer.TransformedTiles.Count != 0 ||
            layer.ShowGridLines ||
            layer.ShowCollisionBoxes ||
            !layer.IsFixedGridSnapshotFastPathEligible ||
            !RenderTileQuery.IsRenderOrdered(layer))
        {
            return false;
        }

        var queryRect = worldRect;
        queryRect.Inflate(layer.TileWidth, layer.TileHeight);
        queryRect.Inflate(1, 1);

        if (SpriteManager.Instance.HasVisibleSpriteInWorldRect(queryRect, layer) ||
            DirectDrawingManager.Instance.HasVisibleDrawingForLayer(layer, worldRect))
        {
            return false;
        }

        plan = new(layer, worldRect);
        return true;
    }

    internal int DrawFixedGridLayer(
        View view,
        FixedGridRenderPlan plan,
        Rectangle clipRect)
    {
        var layer = plan.Layer;
        LayerScreenMap screen = LayerScreenMap.Create(view, layer);
        int tileCount = 0;

        Canvas.Save();
        Canvas.ClipRect(clipRect.ToSKRect());

        try
        {
            RenderTileQuery.VisitCandidates(
                layer.CoordinateSystem,
                layer,
                plan.WorldRect,
                includeOverhang: true,
                tile =>
                {
                    if (!tile.Visible || tile.CurrentFrame.Tilesheet is null)
                        return true;

                    tileCount++;
                    DrawFixedGridTile(view, layer, screen, tile);
                    return true;
                });

            FlushTileBatch();
            return tileCount;
        }
        finally
        {
            Canvas.Restore();
        }
    }

    internal bool TryDrawFixedGridDrawables(
        View view,
        IReadOnlyList<IDrawable> drawables,
        Rectangle clipRect)
    {
        if (drawables.Count == 0)
            return true;

        SceneLayer? layer = null;

        // This path deliberately handles only the engine's fixed tile type and no
        // post-tile debug/fog overlays. Anything richer retains the generic renderer.
        for (int i = 0; i < drawables.Count; i++)
        {
            if (drawables[i].GetType() != typeof(SceneLayerTile) ||
                drawables[i] is not SceneLayerTile tile ||
                tile.EnableFog)
            {
                return false;
            }

            layer ??= tile.SceneLayer;
            if (!ReferenceEquals(layer, tile.SceneLayer))
                return false;
        }

        if (layer is null ||
            layer.ShowGridLines ||
            layer.ShowCollisionBoxes)
        {
            return false;
        }

        LayerScreenMap screen = LayerScreenMap.Create(view, layer);

        Canvas.Save();
        Canvas.ClipRect(clipRect.ToSKRect());

        try
        {
            for (int i = 0; i < drawables.Count; i++)
                DrawFixedGridTile(view, layer, screen, (SceneLayerTile)drawables[i]);

            FlushTileBatch();
            return true;
        }
        finally
        {
            Canvas.Restore();
        }
    }

    private void DrawFixedGridTile(
        View view,
        SceneLayer layer,
        LayerScreenMap screen,
        SceneLayerTile tile)
    {
        if (tile.Transform != TileTransform.Identity)
        {
            FlushTileBatch();
            tile.Draw(this, tile.GetDrawLocationScreen(view));
            return;
        }

        if (!TryGetFrameAtlasInfo(tile.CurrentFrame, out var frame))
        {
            FlushTileBatch();
            tile.Draw(this, tile.GetDrawLocationScreen(view));
            return;
        }

        Rectangle world = RenderTileQuery.GetFixedTileCellBounds(layer, tile);
        if (!frame.Overhang.IsEmpty)
        {
            world = Rectangle.FromLTRB(
                world.Left - frame.Overhang.Left,
                world.Top - frame.Overhang.Top,
                world.Right + frame.Overhang.Right,
                world.Bottom + frame.Overhang.Bottom);
        }

        RectangleF destination = screen.Map(world);

        if (!TryQueueAtlas(frame.Atlas, frame.Source, destination))
        {
            FlushTileBatch();
            tile.Draw(this, destination);
        }
    }

    internal bool TryQueueTile(SceneLayerTile tile, RectangleF destination)
    {
        if (tile.Transform != TileTransform.Identity ||
            !TryGetFrameAtlasInfo(tile.CurrentFrame, out var frame))
        {
            return false;
        }

        return TryQueueAtlas(frame.Atlas, frame.Source, destination);
    }

    private bool TryGetFrameAtlasInfo(Frame frame, out FrameAtlasInfo info)
    {
        if (frame.Tilesheet is null)
        {
            info = default;
            return false;
        }

        var key = new FrameAtlasKey(
            frame.Tilesheet,
            frame.RegionName ?? string.Empty,
            frame.XTile,
            frame.YTile);

        if (_frameAtlasCache.TryGetValue(key, out info))
            return true;

        var atlas = frame.AtlasImage;
        Rectangle source = frame.AtlasSourceBounds;
        if (atlas is null || source.IsEmpty)
        {
            info = default;
            return false;
        }

        info = new(
            atlas,
            source.ToSKRect(),
            frame.Overhang);
        _frameAtlasCache.Add(key, info);
        return true;
    }

    private bool TryQueueAtlas(
        SKImage atlas,
        SKRect source,
        RectangleF destination)
    {
        if (source.Width <= 0 ||
            source.Height <= 0 ||
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
        _pendingSprites[index] = source;
        _pendingTransforms[index] = new SKRotationScaleMatrix(
            scaleX,
            0f,
            destination.Left,
            destination.Top);

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

        foreach (int size in PartialBatchSizes)
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
    }

    private void ResetPendingBatch()
    {
        _pendingAtlas = null;
        _pendingCount = 0;
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

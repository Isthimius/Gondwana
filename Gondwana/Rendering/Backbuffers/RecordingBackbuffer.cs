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
    private const int MaxSnappedVertexBatchSize = 4096;
    private static readonly int[] PartialBatchSizes = [128, 64, 32, 16, 8, 4, 2, 1];
    private static readonly int[] PartialSnappedVertexBatchSizes =
        [2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1];

    private sealed class AtlasBatchBuffer(int size)
    {
        /// <summary>
        /// Gets the sprites.
        /// </summary>
        internal SKRect[] Sprites { get; } = new SKRect[size];
        /// <summary>
        /// Gets the transforms.
        /// </summary>
        internal SKRotationScaleMatrix[] Transforms { get; } = new SKRotationScaleMatrix[size];
    }

    private sealed class SnappedVertexBatchBuffer(int tileCount)
    {
        /// <summary>
        /// Gets the positions.
        /// </summary>
        internal SKPoint[] Positions { get; } = new SKPoint[tileCount * 4];
        /// <summary>
        /// Gets the texture coordinates.
        /// </summary>
        internal SKPoint[] TextureCoordinates { get; } = new SKPoint[tileCount * 4];
        /// <summary>
        /// Gets the indices.
        /// </summary>
        internal ushort[] Indices { get; } = CreateQuadIndices(tileCount);
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

    /// <summary>
    /// Represents fixed grid render plan.
    /// </summary>
    /// <param name="Layer">The layer.</param>
    /// <param name="WorldRect">The world rect.</param>
    internal readonly record struct FixedGridRenderPlan(
        SceneLayer Layer,
        Rectangle WorldRect);

    private readonly record struct LayerScreenMap(
        float OffsetX,
        float OffsetY,
        float ScaleX,
        float ScaleY)
    {
        /// <summary>
        /// Performs the create operation.
        /// </summary>
        /// <param name="view">The view used for the operation.</param>
        /// <param name="layer">The scene layer used for the operation.</param>
        /// <returns>The resulting value.</returns>
        internal static LayerScreenMap Create(View view, SceneLayer layer)
        {
            RectangleF unit = view.WorldRectToScreenRect(
                layer,
                new RectangleF(0f, 0f, 1f, 1f));

            return new(unit.Left, unit.Top, unit.Width, unit.Height);
        }

        /// <summary>
        /// Performs the map operation.
        /// </summary>
        /// <param name="world">The world.</param>
        /// <returns>The resulting value.</returns>
        internal RectangleF Map(Rectangle world) =>
            new(
                OffsetX + world.Left * ScaleX,
                OffsetY + world.Top * ScaleY,
                world.Width * ScaleX,
                world.Height * ScaleY);

        /// <summary>
        /// Performs the map pixel snapped operation.
        /// </summary>
        /// <param name="world">The world.</param>
        /// <returns>The resulting value.</returns>
        internal RectangleF MapPixelSnapped(Rectangle world)
        {
            float left = Snap(OffsetX + world.Left * ScaleX);
            float top = Snap(OffsetY + world.Top * ScaleY);
            float right = Snap(OffsetX + world.Right * ScaleX);
            float bottom = Snap(OffsetY + world.Bottom * ScaleY);

            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        private static float Snap(float value) =>
            MathF.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Creates world to screen matrix.
        /// </summary>
        /// <returns>The resulting value.</returns>
        internal SKMatrix CreateWorldToScreenMatrix() =>
            SKMatrix.CreateScaleTranslation(
                ScaleX,
                ScaleY,
                OffsetX,
                OffsetY);
    }

    private readonly SKPictureRecorder _recorder = new();
    private readonly SKRect[] _pendingSprites = new SKRect[MaxAtlasBatchSize];
    private readonly SKRotationScaleMatrix[] _pendingTransforms =
        new SKRotationScaleMatrix[MaxAtlasBatchSize];
    private readonly SKPoint[] _pendingSnappedPositions =
        new SKPoint[MaxSnappedVertexBatchSize * 4];
    private readonly SKPoint[] _pendingSnappedTextureCoordinates =
        new SKPoint[MaxSnappedVertexBatchSize * 4];
    private readonly ushort[] _fullSnappedVertexIndices =
        CreateQuadIndices(MaxSnappedVertexBatchSize);
    private readonly Dictionary<FrameAtlasKey, FrameAtlasInfo> _frameAtlasCache = [];
    private readonly Dictionary<SKImage, SKShader> _atlasShaderCache = [];
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
    private readonly Dictionary<int, SnappedVertexBatchBuffer> _partialSnappedVertexBuffers =
        PartialSnappedVertexBatchSizes.ToDictionary(
            size => size,
            size => new SnappedVertexBatchBuffer(size));
    private readonly SKPaint _snappedVertexPaint = new()
    {
        IsAntialias = false,
        BlendMode = SKBlendMode.SrcOver
    };

    private SKCanvas? _canvas;
    private SKImage? _pendingAtlas;
    private SKImage? _pendingSnappedVertexAtlas;
    private int _pendingCount;
    private int _pendingSnappedVertexCount;

    /// <summary>
    /// Gets or sets the atlas batch count.
    /// </summary>
    internal int AtlasBatchCount { get; private set; }
    /// <summary>
    /// Gets or sets the atlas batched tile count.
    /// </summary>
    internal int AtlasBatchedTileCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="RecordingBackbuffer"/>.
    /// </summary>
    internal RecordingBackbuffer() : base(1, 1) { }

    /// <inheritdoc/>
    public override SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("No recording is active.");
    /// <inheritdoc/>
    public override bool IsGlThreadRendered => true;

    /// <summary>
    /// Performs the start operation.
    /// </summary>
    /// <param name="source">The source backbuffer.</param>
    internal void Start(BackbufferBase source)
    {
        ResetPendingBatch();
        ResetSnappedVertexBatch();
        DisposeAtlasShaders();
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

    /// <summary>
    /// Performs the complete operation.
    /// </summary>
    /// <returns>The resulting value.</returns>
    internal SKPicture Complete()
    {
        FlushTileBatch();
        FlushSnappedVertexBatch();
        var picture = _recorder.EndRecording();
        _canvas = null;
        ResetPendingBatch();
        ResetSnappedVertexBatch();
        DisposeAtlasShaders();
        return picture;
    }

    /// <summary>
    /// Performs the cancel operation.
    /// </summary>
    internal void Cancel()
    {
        ResetPendingBatch();
        ResetSnappedVertexBatch();

        if (_canvas is null)
        {
            DisposeAtlasShaders();
            return;
        }

        var picture = _recorder.EndRecording();
        _canvas = null;
        picture.Dispose();
        DisposeAtlasShaders();
    }

    /// <summary>
    /// Attempts to prepare fixed grid layer.
    /// </summary>
    /// <param name="layer">The scene layer used for the operation.</param>
    /// <param name="worldRect">The world rect.</param>
    /// <param name="plan">The plan.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Draws fixed grid layer.
    /// </summary>
    /// <param name="view">The view used for the operation.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="clipRect">The clipping rectangle.</param>
    /// <returns>The resulting value.</returns>
    internal int DrawFixedGridLayer(
        View view,
        FixedGridRenderPlan plan,
        Rectangle clipRect)
    {
        var layer = plan.Layer;
        LayerScreenMap screen = LayerScreenMap.Create(view, layer);
        bool snapOrthogonalEdges = layer.CoordinateSystem is OrthogonalCoordinates;
        int tileCount = 0;

        Canvas.Save();
        Canvas.ClipRect(clipRect.ToSKRect());

        if (!snapOrthogonalEdges)
        {
            var worldToScreen = screen.CreateWorldToScreenMatrix();
            Canvas.Concat(ref worldToScreen);
        }

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

                    if (snapOrthogonalEdges)
                        DrawOrthogonalFixedGridTileSnapped(layer, screen, tile);
                    else
                        DrawFixedGridTileInWorldSpace(layer, tile);

                    return true;
                });

            if (snapOrthogonalEdges)
                FlushSnappedVertexBatch();
            else
                FlushTileBatch();

            return tileCount;
        }
        finally
        {
            Canvas.Restore();
        }
    }

    /// <summary>
    /// Attempts to draw fixed grid drawables.
    /// </summary>
    /// <param name="view">The view used for the operation.</param>
    /// <param name="drawables">The drawables to render.</param>
    /// <param name="clipRect">The clipping rectangle.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
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

        if (layer.CoordinateSystem is OrthogonalCoordinates)
        {
            try
            {
                for (int i = 0; i < drawables.Count; i++)
                    DrawOrthogonalFixedGridTileSnapped(
                        layer,
                        screen,
                        (SceneLayerTile)drawables[i]);

                FlushSnappedVertexBatch();
                return true;
            }
            finally
            {
                Canvas.Restore();
            }
        }

        var worldToScreen = screen.CreateWorldToScreenMatrix();
        bool worldSpaceActive = false;

        try
        {
            for (int i = 0; i < drawables.Count; i++)
            {
                var tile = (SceneLayerTile)drawables[i];

                if (tile.Transform == TileTransform.Identity &&
                    TryGetFrameAtlasInfo(tile.CurrentFrame, out _))
                {
                    if (!worldSpaceActive)
                    {
                        Canvas.Save();
                        Canvas.Concat(ref worldToScreen);
                        worldSpaceActive = true;
                    }

                    DrawFixedGridTileInWorldSpace(layer, tile);
                    continue;
                }

                if (worldSpaceActive)
                {
                    FlushTileBatch();
                    Canvas.Restore();
                    worldSpaceActive = false;
                }

                DrawFixedGridTile(view, layer, screen, tile);
            }

            if (worldSpaceActive)
            {
                FlushTileBatch();
                Canvas.Restore();
                worldSpaceActive = false;
            }

            return true;
        }
        finally
        {
            if (worldSpaceActive)
            {
                FlushTileBatch();
                Canvas.Restore();
            }

            Canvas.Restore();
        }
    }

    private void DrawOrthogonalFixedGridTileSnapped(
        SceneLayer layer,
        LayerScreenMap screen,
        SceneLayerTile tile)
    {
        if (tile.Transform == TileTransform.Identity &&
            TryGetFrameAtlasInfo(tile.CurrentFrame, out var frame))
        {
            Rectangle world = RenderTileQuery.GetFixedTileCellBounds(layer, tile);
            if (!frame.Overhang.IsEmpty)
            {
                world = Rectangle.FromLTRB(
                    world.Left - frame.Overhang.Left,
                    world.Top - frame.Overhang.Top,
                    world.Right + frame.Overhang.Right,
                    world.Bottom + frame.Overhang.Bottom);
            }

            RectangleF destination = screen.MapPixelSnapped(world);
            if (destination.Width <= 0f || destination.Height <= 0f)
                return;

            QueueSnappedVertexAtlas(
                frame.Atlas,
                frame.Source,
                destination);
            return;
        }

        FlushSnappedVertexBatch();

        RectangleF transformedDestination =
            screen.MapPixelSnapped(tile.DrawLocationWorld);
        if (transformedDestination.Width > 0f &&
            transformedDestination.Height > 0f)
        {
            tile.Draw(this, transformedDestination);
        }
    }

    private void DrawFixedGridTileInWorldSpace(
        SceneLayer layer,
        SceneLayerTile tile)
    {
        if (!TryGetFrameAtlasInfo(tile.CurrentFrame, out var frame))
            return;

        Rectangle world = RenderTileQuery.GetFixedTileCellBounds(layer, tile);
        if (!frame.Overhang.IsEmpty)
        {
            world = Rectangle.FromLTRB(
                world.Left - frame.Overhang.Left,
                world.Top - frame.Overhang.Top,
                world.Right + frame.Overhang.Right,
                world.Bottom + frame.Overhang.Bottom);
        }

        // Record atlas entries in integer world coordinates. A single canvas transform
        // scales/translates the complete fixed-grid layer to screen space, so adjacent
        // tile edges are transformed from the same world boundary instead of from
        // independently rounded fractional destination rectangles.
        if (TryQueueAtlas(frame.Atlas, frame.Source, world))
            return;

        FlushTileBatch();
        Canvas.DrawImage(
            frame.Atlas,
            frame.Source,
            world.ToSKRect(),
            SKSamplingOptions.Default);
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

    private void QueueSnappedVertexAtlas(
        SKImage atlas,
        SKRect source,
        RectangleF destination)
    {
        if (_pendingSnappedVertexAtlas is not null &&
            !ReferenceEquals(_pendingSnappedVertexAtlas, atlas))
        {
            FlushSnappedVertexBatch();
        }

        _pendingSnappedVertexAtlas = atlas;

        int tileIndex = _pendingSnappedVertexCount++;
        int vertexIndex = tileIndex * 4;

        float left = destination.Left;
        float top = destination.Top;
        float right = destination.Right;
        float bottom = destination.Bottom;

        _pendingSnappedPositions[vertexIndex] = new(left, top);
        _pendingSnappedPositions[vertexIndex + 1] = new(right, top);
        _pendingSnappedPositions[vertexIndex + 2] = new(right, bottom);
        _pendingSnappedPositions[vertexIndex + 3] = new(left, bottom);

        _pendingSnappedTextureCoordinates[vertexIndex] =
            new(source.Left, source.Top);
        _pendingSnappedTextureCoordinates[vertexIndex + 1] =
            new(source.Right, source.Top);
        _pendingSnappedTextureCoordinates[vertexIndex + 2] =
            new(source.Right, source.Bottom);
        _pendingSnappedTextureCoordinates[vertexIndex + 3] =
            new(source.Left, source.Bottom);

        if (_pendingSnappedVertexCount == MaxSnappedVertexBatchSize)
            FlushFullSnappedVertexBatch();
    }

    private void FlushSnappedVertexBatch()
    {
        if (_pendingSnappedVertexCount == 0)
        {
            _pendingSnappedVertexAtlas = null;
            return;
        }

        if (_pendingSnappedVertexAtlas is null)
        {
            throw new InvalidOperationException(
                "Snapped vertex batch has queued tiles without an atlas image.");
        }

        int offset = 0;
        int remaining = _pendingSnappedVertexCount;

        foreach (int size in PartialSnappedVertexBatchSizes)
        {
            while (remaining >= size)
            {
                var buffers = _partialSnappedVertexBuffers[size];
                Array.Copy(
                    _pendingSnappedPositions,
                    offset * 4,
                    buffers.Positions,
                    0,
                    size * 4);
                Array.Copy(
                    _pendingSnappedTextureCoordinates,
                    offset * 4,
                    buffers.TextureCoordinates,
                    0,
                    size * 4);

                DrawSnappedVertexBatch(
                    _pendingSnappedVertexAtlas,
                    buffers.Positions,
                    buffers.TextureCoordinates,
                    buffers.Indices,
                    size);

                offset += size;
                remaining -= size;
            }
        }

        ResetSnappedVertexBatch();
    }

    private void FlushFullSnappedVertexBatch()
    {
        if (_pendingSnappedVertexAtlas is null ||
            _pendingSnappedVertexCount != MaxSnappedVertexBatchSize)
        {
            return;
        }

        DrawSnappedVertexBatch(
            _pendingSnappedVertexAtlas,
            _pendingSnappedPositions,
            _pendingSnappedTextureCoordinates,
            _fullSnappedVertexIndices,
            MaxSnappedVertexBatchSize);

        _pendingSnappedVertexCount = 0;
    }

    private void DrawSnappedVertexBatch(
        SKImage atlas,
        SKPoint[] positions,
        SKPoint[] textureCoordinates,
        ushort[] indices,
        int tileCount)
    {
        using var vertices = SKVertices.CreateCopy(
            SKVertexMode.Triangles,
            positions,
            textureCoordinates,
            null,
            indices);

        if (!_atlasShaderCache.TryGetValue(atlas, out var shader))
        {
            shader = atlas.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                new SKSamplingOptions(
                    SKFilterMode.Nearest,
                    SKMipmapMode.None));
            _atlasShaderCache.Add(atlas, shader);
        }

        _snappedVertexPaint.Shader = shader;
        Canvas.DrawVertices(
            vertices,
            SKBlendMode.Modulate,
            _snappedVertexPaint);

        AtlasBatchCount++;
        AtlasBatchedTileCount += tileCount;
    }

    private static ushort[] CreateQuadIndices(int tileCount)
    {
        var indices = new ushort[tileCount * 6];

        for (int tile = 0; tile < tileCount; tile++)
        {
            int vertex = tile * 4;
            int index = tile * 6;

            indices[index] = (ushort)vertex;
            indices[index + 1] = (ushort)(vertex + 1);
            indices[index + 2] = (ushort)(vertex + 2);
            indices[index + 3] = (ushort)vertex;
            indices[index + 4] = (ushort)(vertex + 2);
            indices[index + 5] = (ushort)(vertex + 3);
        }

        return indices;
    }

    private void ResetSnappedVertexBatch()
    {
        _pendingSnappedVertexAtlas = null;
        _pendingSnappedVertexCount = 0;
    }

    private void DisposeAtlasShaders()
    {
        _snappedVertexPaint.Shader = null;

        foreach (var shader in _atlasShaderCache.Values)
            shader.Dispose();

        _atlasShaderCache.Clear();
    }

    /// <summary>
    /// Attempts to queue tile.
    /// </summary>
    /// <param name="tile">The tile to process.</param>
    /// <param name="destination">The destination rectangle.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Flushes tile batch.
    /// </summary>
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

    /// <inheritdoc/>
    protected internal override void BeginFrame() { }
    /// <inheritdoc/>
    protected internal override void EndFrame() { }
    /// <inheritdoc/>
    protected internal override SKImage Snapshot() =>
        throw new NotSupportedException("A recording has commands, not pixels. Read pixels on the GL thread after replay.");

    /// <inheritdoc/>
    protected internal override void DrawTileFrame(Tile tile, RectangleF destRectScreen)
    {
        var image = tile.CurrentFrame.SkImage;
        if (image is not null) Canvas.DrawImage(image, destRectScreen.ToSKRect());
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        Cancel();
        _snappedVertexPaint.Dispose();
        _recorder.Dispose();
        base.Dispose();
    }
}

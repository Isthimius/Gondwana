namespace Gondwana.Rendering;

/// <summary>
/// Captures the measured work performed for one scene layer during a full-frame GPU render.
/// </summary>
/// <param name="LayerIndex">The layer index.</param>
/// <param name="LayerId">The layer id.</param>
/// <param name="ZOrder">The z order.</param>
/// <param name="DrawableCount">The drawable count.</param>
/// <param name="TileCount">The tile count.</param>
/// <param name="TransformedTileCount">The transformed tile count.</param>
/// <param name="TileWidth">The tile width.</param>
/// <param name="TileHeight">The tile height.</param>
/// <param name="QueryAndSortMilliseconds">The query and sort milliseconds.</param>
/// <param name="DrawMilliseconds">The draw milliseconds.</param>
public sealed record GpuLayerRenderDiagnostics(
    int LayerIndex,
    string LayerId,
    int ZOrder,
    int DrawableCount,
    int TileCount,
    int TransformedTileCount,
    int TileWidth,
    int TileHeight,
    double QueryAndSortMilliseconds,
    double DrawMilliseconds);

/// <summary>
/// Captures measured scene-render work for one full-frame GPU render.
/// </summary>
/// <param name="TotalRenderMilliseconds">The total render milliseconds.</param>
/// <param name="QueryAndSortMilliseconds">The query and sort milliseconds.</param>
/// <param name="DrawMilliseconds">The draw milliseconds.</param>
/// <param name="OverlayMilliseconds">The overlay milliseconds.</param>
/// <param name="DrawableCount">The drawable count.</param>
/// <param name="TileCount">The tile count.</param>
/// <param name="Layers">The layers.</param>
public sealed record GpuRenderFrameDiagnostics(
    double TotalRenderMilliseconds,
    double QueryAndSortMilliseconds,
    double DrawMilliseconds,
    double OverlayMilliseconds,
    int DrawableCount,
    int TileCount,
    IReadOnlyList<GpuLayerRenderDiagnostics> Layers)
{
    /// <summary>Time gathering visible drawable candidates before sorting.</summary>
    public double QueryMilliseconds { get; init; }

    /// <summary>Time validating existing order or sorting the gathered drawable lists.</summary>
    public double SortMilliseconds { get; init; }

    /// <summary>Number of DrawAtlas operations recorded for fixed-grid tiles.</summary>
    public int AtlasBatchCount { get; init; }

    /// <summary>Number of fixed-grid tiles represented by DrawAtlas operations.</summary>
    public int AtlasBatchedTileCount { get; init; }
}


/// <summary>
/// Captures time spent waiting for and holding the shared live render-state synchronization gate.
/// </summary>
/// <param name="LockWaitMilliseconds">The lock wait milliseconds.</param>
/// <param name="LockHeldMilliseconds">The lock held milliseconds.</param>
public sealed record GpuRenderSynchronizationDiagnostics(
    double LockWaitMilliseconds,
    double LockHeldMilliseconds)
{
    /// <summary>CPU time replaying completed commands and flushing the backbuffer.</summary>
    public double ReplayMilliseconds { get; init; }

    /// <summary>CPU time spent replaying the recorded SKPicture onto the GPU backbuffer canvas.</summary>
    public double PictureReplayMilliseconds { get; init; }

    /// <summary>CPU time spent flushing the GPU backbuffer after picture replay.</summary>
    public double BackbufferFlushMilliseconds { get; init; }

    /// <summary>CPU time spent creating the lightweight GPU-backed snapshot used for presentation.</summary>
    public double SnapshotMilliseconds { get; init; }

    /// <summary>Age of the completed snapshot when acquired by GL.</summary>
    public double SnapshotAgeMilliseconds { get; init; }
    /// <summary>Total snapshots published by this surface.</summary>
    public long PublishedSnapshots { get; init; }
    /// <summary>Total published snapshots replaced before acquisition.</summary>
    public long DroppedSnapshots { get; init; }
    /// <summary>Occupied slots at acquisition, including the rendering slot (maximum three).</summary>
    public int SnapshotSlotsInUse { get; init; }
    /// <summary>Approximate number of native Skia operations in the acquired recording.</summary>
    public int SnapshotCommandCount { get; init; }
}

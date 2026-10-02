namespace Gondwana.Rendering;

/// <summary>
/// Captures the measured work performed for one scene layer during a full-frame GPU render.
/// </summary>
public sealed record GpuLayerRenderDiagnostics(
    int LayerIndex,
    string LayerId,
    int ZOrder,
    int DrawableCount,
    int TileCount,
    double QueryAndSortMilliseconds,
    double DrawMilliseconds);

/// <summary>
/// Captures measured scene-render work for one full-frame GPU render.
/// </summary>
public sealed record GpuRenderFrameDiagnostics(
    double TotalRenderMilliseconds,
    double QueryAndSortMilliseconds,
    double DrawMilliseconds,
    double OverlayMilliseconds,
    int DrawableCount,
    int TileCount,
    IReadOnlyList<GpuLayerRenderDiagnostics> Layers);

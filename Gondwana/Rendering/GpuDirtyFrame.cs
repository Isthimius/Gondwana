using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// An immutable snapshot of per-layer world-space dirty regions for one GPU frame.
/// </summary>
/// <remarks>
/// <para>
/// Produced on the engine thread by <see cref="RenderSurfaceHostBase.CommitGpuDirtyFrame"/> and
/// consumed on the GL thread inside
/// <see cref="RenderSurfaceHostBase.GlRenderAndSnapshot"/>.
/// Using an immutable snapshot avoids the race conditions that arise when
/// <see cref="RefreshQueue"/> is read and cleared from different threads.
/// </para>
/// <para>
/// When the engine runs faster than the GPU can render, successive published frames are
/// <em>merged</em> by <see cref="GpuBackbuffer.PublishDirtyFrame"/> so that every dirty
/// rectangle is eventually rendered without loss.
/// </para>
/// </remarks>
internal sealed class GpuDirtyFrame
{
    /// <summary>
    /// Sentinel value representing an empty (no pending work) frame.
    /// Used as the initial and post-consume value in <see cref="GpuBackbuffer"/>.
    /// </summary>
    internal static readonly GpuDirtyFrame Empty = new(0, false, Array.Empty<GpuLayerDirtyRects>());

    /// <summary>
    /// Monotonically increasing counter.  Incremented each time a non-empty frame is
    /// published by the engine thread.  Used by adapters to detect new work.
    /// </summary>
    internal long Revision { get; }

    /// <summary>
    /// When <see langword="true"/> the entire surface must be redrawn on the GL thread
    /// (camera or zoom change, scene bound change, resize, initial frame, etc.).
    /// </summary>
    internal bool ForceFullRedraw { get; }

    /// <summary>
    /// Per-layer world-space pixel rectangles that need to be re-rendered.
    /// </summary>
    internal IReadOnlyList<GpuLayerDirtyRects> LayerRects { get; }

    /// <summary>
    /// <see langword="true"/> when there is no rendering work to perform.
    /// </summary>
    internal bool IsEmpty => !ForceFullRedraw && LayerRects.Count == 0;

    internal GpuDirtyFrame(long revision, bool forceFullRedraw, IReadOnlyList<GpuLayerDirtyRects> layerRects)
    {
        Revision = revision;
        ForceFullRedraw = forceFullRedraw;
        LayerRects = layerRects;
    }

    /// <summary>
    /// Returns a new <see cref="GpuDirtyFrame"/> that is the union of <c>this</c> and
    /// <paramref name="other"/>.  The resulting <see cref="Revision"/> is taken from
    /// <paramref name="other"/>; <see cref="ForceFullRedraw"/> is the logical-OR of both.
    /// </summary>
    /// <remarks>
    /// Called on the engine thread by <see cref="GpuBackbuffer.PublishDirtyFrame"/> when a
    /// new snapshot arrives before the GL thread has consumed the previous one.
    /// </remarks>
    internal GpuDirtyFrame MergeWith(GpuDirtyFrame other)
    {
        if (other.IsEmpty) return this;
        if (this.IsEmpty) return other;

        bool forceFullRedraw = this.ForceFullRedraw || other.ForceFullRedraw;

        var merged = new Dictionary<SceneLayer, List<Rectangle>>();

        static void AddLayerRects(Dictionary<SceneLayer, List<Rectangle>> dict,
                                   IReadOnlyList<GpuLayerDirtyRects> src)
        {
            foreach (var lr in src)
            {
                if (!dict.TryGetValue(lr.Layer, out var list))
                    dict[lr.Layer] = list = new List<Rectangle>(lr.WorldRects.Length);
                list.AddRange(lr.WorldRects);
            }
        }

        AddLayerRects(merged, this.LayerRects);
        AddLayerRects(merged, other.LayerRects);

        var result = merged
            .Select(kv => new GpuLayerDirtyRects(kv.Key, kv.Value.ToArray()))
            .ToList<GpuLayerDirtyRects>();

        return new GpuDirtyFrame(other.Revision, forceFullRedraw, result);
    }
}

/// <summary>
/// Pairs a <see cref="SceneLayer"/> with the world-space pixel rectangles that are dirty
/// for that layer in the current <see cref="GpuDirtyFrame"/>.
/// </summary>
internal sealed class GpuLayerDirtyRects
{
    internal SceneLayer Layer { get; }
    internal Rectangle[] WorldRects { get; }

    internal GpuLayerDirtyRects(SceneLayer layer, Rectangle[] worldRects)
    {
        Layer = layer;
        WorldRects = worldRects;
    }
}

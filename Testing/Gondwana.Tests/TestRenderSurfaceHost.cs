using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Minimal render-surface host used by direct-drawing and widget unit tests.
/// </summary>
internal sealed class TestRenderSurfaceHost : RenderSurfaceHostBase
{
    internal TestRenderSurfaceHost()
    {
        Scene = new Scene();
        ViewManager = new ViewManager(this);
    }

    /// <inheritdoc/>
    public override BackbufferBase Backbuffer =>
        throw new NotSupportedException(
            "The test host does not render.");

    /// <inheritdoc/>
    public override Scene Scene { get; }

    /// <inheritdoc/>
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter =>
        null;

    /// <inheritdoc/>
    public override ViewManager ViewManager { get; }

    internal override void RenderToBackbuffer(long tick)
    {
    }

    internal override void PresentBackbufferToAdapter()
    {
    }
}

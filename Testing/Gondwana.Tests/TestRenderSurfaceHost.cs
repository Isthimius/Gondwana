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

    public override BackbufferBase Backbuffer =>
        throw new NotSupportedException(
            "The test host does not render.");

    public override Scene Scene { get; }

    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter =>
        null;

    public override ViewManager ViewManager { get; }

    internal override void RenderToBackbuffer(long tick)
    {
    }

    internal override void PresentBackbufferToAdapter()
    {
    }
}

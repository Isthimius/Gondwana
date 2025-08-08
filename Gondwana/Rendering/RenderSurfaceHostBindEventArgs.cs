using Gondwana.Grid;

namespace Gondwana.Rendering;

public class RenderSurfaceHostBindEventArgs : EventArgs
{
    public GridPointMatrixes? OldScene { get; }
    public GridPointMatrixes? NewScene { get; }

    private RenderSurfaceHostBindEventArgs() : this(null, null) { }

    public RenderSurfaceHostBindEventArgs(GridPointMatrixes? oldScene, GridPointMatrixes? newScene)
    {
        OldScene = oldScene;
        NewScene = newScene;
    }
}

using Gondwana.Grid;

namespace Gondwana.Rendering;

public class VisibleSurfaceBindEventArgs : EventArgs
{
    public VisibleSurfaceBase Surface { get; }
    public GridPointMatrixes OldBindValue { get; }
    public GridPointMatrixes NewBindValue { get; }

    public VisibleSurfaceBindEventArgs(
        VisibleSurfaceBase surface,
        GridPointMatrixes oldBind,
        GridPointMatrixes newBind)
    {
        Surface = surface;
        OldBindValue = oldBind;
        NewBindValue = newBind;
    }
}

using Gondwana.Grid;

namespace Gondwana.Rendering;

public class VisibleSufaceBindEventArgs : EventArgs
{
    public VisibleSurfaceBase Surface { get; }
    public GridPointMatrixes OldBindValue { get; }
    public GridPointMatrixes NewBindValue { get; }

    public VisibleSufaceBindEventArgs(
        VisibleSurfaceBase surface,
        GridPointMatrixes oldBind,
        GridPointMatrixes newBind)
    {
        Surface = surface;
        OldBindValue = oldBind;
        NewBindValue = newBind;
    }
}

namespace Gondwana.Rendering;

public class VisibleSurfaceBindEventArgs : EventArgs
{
    public VisibleSurfaceBase Surface { get; }
    public BackbufferBase OldBindValue { get; }
    public BackbufferBase NewBindValue { get; }

    public VisibleSurfaceBindEventArgs(
        VisibleSurfaceBase surface,
        BackbufferBase oldBind,
        BackbufferBase newBind)
    {
        Surface = surface;
        OldBindValue = oldBind;
        NewBindValue = newBind;
    }
}

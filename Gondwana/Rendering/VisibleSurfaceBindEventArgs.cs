namespace Gondwana.Rendering;

public class VisibleSurfaceBindEventArgs : EventArgs
{
    public BackbufferBase? OldBackbuffer { get; }
    public BackbufferBase? NewBackbuffer { get; }

    public VisibleSurfaceBindEventArgs(BackbufferBase? oldBuffer, BackbufferBase? newBuffer)
    {
        OldBackbuffer = oldBuffer;
        NewBackbuffer = newBuffer;
    }
}

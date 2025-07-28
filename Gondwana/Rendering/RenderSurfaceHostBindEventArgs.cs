namespace Gondwana.Rendering;

public class RenderSurfaceHostBindEventArgs : EventArgs
{
    public BackbufferBase? OldBackbuffer { get; }
    public BackbufferBase? NewBackbuffer { get; }

    public RenderSurfaceHostBindEventArgs(BackbufferBase? oldBuffer, BackbufferBase? newBuffer)
    {
        OldBackbuffer = oldBuffer;
        NewBackbuffer = newBuffer;
    }
}

namespace Gondwana.Rendering;

public class RenderSurfaceAdapterResizedEventArgs
{
    public RenderSurfaceAdapterBase RenderSurfaceAdapter { get; }
    public int OldWidth { get; }
    public int OldHeight { get; }
    public int NewWidth { get; }
    public int NewHeight { get; }

    public RenderSurfaceAdapterResizedEventArgs(RenderSurfaceAdapterBase renderSurfaceAdapter, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        RenderSurfaceAdapter = renderSurfaceAdapter;
        OldWidth = oldWidth;
        OldHeight = oldHeight;
        NewWidth = newWidth;
        NewHeight = newHeight;
    }
}

using System.Drawing;

namespace Gondwana.Rendering;

public class ViewportZoomChangedEventArgs
{
    public Viewport Viewport { get; }
    public float OldZoom { get; }
    public float NewZoom { get; }

    public ViewportZoomChangedEventArgs(Viewport viewport, float oldZoom, float newZoom)
    {
        Viewport = viewport;
        OldZoom = oldZoom;
        NewZoom = newZoom;
    }
}

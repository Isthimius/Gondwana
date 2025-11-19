using System.Drawing;

namespace Gondwana.Rendering;

public class ViewportResizedEventArgs
{
    public Viewport Viewport { get; }
    public Rectangle OldRect { get; }
    public Rectangle NewRect { get; }

    public ViewportResizedEventArgs(Viewport viewport, Rectangle oldRect, Rectangle newRect)
    {
        Viewport = viewport;
        OldRect = oldRect;
        NewRect = newRect;
    }
}

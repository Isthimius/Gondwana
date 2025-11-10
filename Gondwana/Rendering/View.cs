namespace Gondwana.Rendering;

public sealed class View
{
    public Camera Camera { get; }
    public Viewport Viewport { get; }

    public View(Camera cam, Viewport vp)
    {
        Camera = cam;
        Viewport = vp;
        // Let camera clamp against THIS viewport’s visible world size.
        Camera.GetVisibleWorldSizePx = () => Viewport.VisibleWorldSizePx;
    }
}

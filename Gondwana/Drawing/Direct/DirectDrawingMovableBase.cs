using Gondwana.Movement;
using Gondwana.Rendering;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable
{
    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds) { }

    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

    public Vector2 GetPosition() => new(Bounds.X, Bounds.Y);

    public void SetPosition(Vector2 p) => Bounds = new Rectangle((int)p.X, (int)p.Y, Bounds.Width, Bounds.Height);
}
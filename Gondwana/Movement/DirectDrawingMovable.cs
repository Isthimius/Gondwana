using System.Drawing;
using System.Numerics;
using System.Xml.Linq;
using Gondwana.Drawing.Direct;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class DirectDrawingMovable : IMovable
{
    private readonly DirectDrawingBase _d;
    public DirectDrawingMovable(DirectDrawingBase d) => _d = d;

    public MovementSpace PositionSpace => MovementSpace.Pixel;

    public Vector2 GetPosition() => new(_d.Bounds.X, _d.Bounds.Y);

    public void SetPosition(Vector2 p)
    {
        _d.Bounds = new Rectangle((int)p.X, (int)p.Y, _d.Bounds.Width, _d.Bounds.Height);
    }
}


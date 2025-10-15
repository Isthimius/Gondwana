using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class DirectDrawingMovable : IMovable
{
    private readonly SceneLayer _sceneLayer;
    private readonly DirectDrawingBase _node; // your drawable with PixelOffset (x,y)

    public DirectDrawingMovable(SceneLayer sceneLayer, DirectDrawingBase node)
    {
        _sceneLayer = sceneLayer;
        _node = node;
    }

    public Vector2 GetGridPosition()
    {
        // invert pixel→grid via the layer’s coordinate system:
        var px = new Point(_node.Bounds.X, _node.Bounds.Y);
        var gp = _sceneLayer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(_sceneLayer, px);
        return new(gp.X, gp.Y);
    }

    public void SetGridPosition(Vector2 p)
    {
        var pt = _sceneLayer.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(
            _sceneLayer, new System.Drawing.PointF(p.X, p.Y));
        _node.Bounds = new Rectangle(pt.X, pt.Y, _node.Bounds.Width, _node.Bounds.Height);
    }
}

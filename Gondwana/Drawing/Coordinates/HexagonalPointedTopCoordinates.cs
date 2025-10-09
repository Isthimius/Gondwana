using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public class HexagonalPointedTopCoordinates : ISceneLayerCoordinates
{
    public Point GetSrcPixelAtLayerPoint(SceneLayer matrix, PointF gridCoord)
    {
        throw new NotImplementedException();
    }

    public PointF GetLayerPointAtPixel(SceneLayer matrix, Point pixelPt)
    {
        throw new NotImplementedException();
    }

    public List<SceneLayerPoint> GetLayerPointListInPixelRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPixelRangeAtLayerPoint(Tile tile, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPixelRangeAtLayerPointList(List<Tile> tileList, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public SceneLayerPoint GetAdjacentLayerPoint(SceneLayerPoint gridPt, CardinalDirections direction)
    {
        throw new NotImplementedException();
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        throw new NotImplementedException();
    }
}
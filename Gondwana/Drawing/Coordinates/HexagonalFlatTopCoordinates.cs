using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public class HexagonalFlatTopCoordinates : IGridCoordinates
{
    public Point GetSrcPxlAtGridPt(SceneLayer matrix, PointF gridCoord)
    {
        throw new NotImplementedException();
    }

    public PointF GetGridPtAtPxl(SceneLayer matrix, Point pixelPt)
    {
        throw new NotImplementedException();
    }

    public List<SceneLayerPoint> GetGridPtListInPxlRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPt(Tile tile, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public SceneLayerPoint GetAdjGridPt(SceneLayerPoint gridPt, CardinalDirections direction)
    {
        throw new NotImplementedException();
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        throw new NotImplementedException();
    }

    public PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        throw new NotImplementedException();
    }
}
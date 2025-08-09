using Gondwana.Drawing;
using System.Drawing;

using Gondwana.Scenes.Coordinates;
using Gondwana.Scenes;

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

    public List<SceneLayerPoint> GetGridPtListInPxlRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverlaps)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPt(Tile tile, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public SceneLayerPoint GetAdjGridPt(SceneLayerPoint gridPt, CardinalDirections direction)
    {
        throw new NotImplementedException();
    }

    public Point[] GetPolygonPts(Tile tile, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        throw new NotImplementedException();
    }
}

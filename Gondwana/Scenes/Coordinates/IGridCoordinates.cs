using System.Drawing;
using Gondwana.Drawing;

namespace Gondwana.Scenes.Coordinates;

public interface IGridCoordinates
{
    Point GetSrcPxlAtGridPt(SceneLayer matrix, PointF gridCoord);

    PointF GetGridPtAtPxl(SceneLayer matrix, Point pixelPt);

    List<SceneLayerPoint> GetGridPtListInPxlRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverlaps);

    Rectangle GetPxlRangeAtGridPt(Tile tile, bool inclOverlaps);

    Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool inclOverlaps);

    SceneLayerPoint GetAdjGridPt(SceneLayerPoint gridPt, CardinalDirections direction);

    Point[] GetPolygonPts(Tile tile, bool inclOverlaps);

    PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound);
}
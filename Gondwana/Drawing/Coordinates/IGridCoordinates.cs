using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public interface IGridCoordinates
{
    Point GetSrcPxlAtGridPt(SceneLayer matrix, PointF gridCoord);

    PointF GetGridPtAtPxl(SceneLayer matrix, Point pixelPt);

    List<SceneLayerPoint> GetGridPtListInPxlRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverhang);

    Rectangle GetPxlRangeAtGridPt(Tile tile, bool includeOverhang);

    Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool includeOverhang);

    SceneLayerPoint GetAdjGridPt(SceneLayerPoint gridPt, CardinalDirections direction);

    Point[] GetPolygonPts(Tile tile, bool includeOverhang);

    PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound);
}
using Gondwana.Common.Enums;
using Gondwana.Common.Grid;
using System.Collections.Generic;
using System.Drawing;

namespace Gondwana.Common
{
    public interface IGridCoordinates
    {
        Point GetSrcPxlAtGridPt(GridPointMatrix matrix, PointF gridCoord);
        PointF GetGridPtAtPxl(GridPointMatrix matrix, Point pixelPt);
        List<GridPoint> GetGridPtListInPxlRange(GridPointMatrix matrix, Rectangle pixelRange, bool includeOverlaps);
        Rectangle GetPxlRangeAtGridPt(Tile tile, bool inclOverlaps);
        Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool inclOverlaps);
        GridPoint GetAdjGridPt(GridPoint gridPt, CardinalDirections direction);
        Point[] GetPolygonPts(Tile tile, bool inclOverlaps);
        PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound);
    }
}

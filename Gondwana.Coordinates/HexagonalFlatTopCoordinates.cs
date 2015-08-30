using Gondwana.Common;
using Gondwana.Common.Enums;
using Gondwana.Common.Grid;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Gondwana.Coordinates
{
    public class HexagonalFlatTopCoordinates : IGridCoordinates
    {
        public Point GetSrcPxlAtGridPt(GridPointMatrix matrix, PointF gridCoord)
        {
            throw new NotImplementedException();
        }

        public PointF GetGridPtAtPxl(GridPointMatrix matrix, Point pixelPt)
        {
            throw new NotImplementedException();
        }

        public List<GridPoint> GetGridPtListInPxlRange(GridPointMatrix matrix, Rectangle pixelRange, bool includeOverlaps)
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

        public GridPoint GetAdjGridPt(GridPoint gridPt, CardinalDirections direction)
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
}

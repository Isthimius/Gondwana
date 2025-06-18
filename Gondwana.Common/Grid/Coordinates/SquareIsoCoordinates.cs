using Gondwana.Common;
using Gondwana.Drawing;
using Gondwana.Common.Enums;
using Gondwana.Grid;
using System.Drawing;

namespace Gondwana.Grid.Coordinates;

public class SquareIsoCoordinates : IGridCoordinates
{
    public Point GetSrcPxlAtGridPt(GridPointMatrix matrix, PointF gridCoord)
    {
        Point retVal = new Point();

        retVal.X = (int)(matrix.GridPointWidth * (gridCoord.X - matrix.SourceGridPoint.X));
        retVal.Y = (int)(matrix.GridPointHeight * (gridCoord.Y - matrix.SourceGridPoint.Y));

        return retVal;
    }

    public PointF GetGridPtAtPxl(GridPointMatrix matrix, Point pixelPt)
    {
        PointF retPt = new PointF();

        retPt.X = (pixelPt.X - matrix.GridPointZeroPixel.X) / (float)matrix.GridPointWidth;
        retPt.Y = (pixelPt.Y - matrix.GridPointZeroPixel.Y) / (float)matrix.GridPointHeight;

        return retPt;
    }

    public List<GridPoint> GetGridPtListInPxlRange(GridPointMatrix matrix, Rectangle pixelRange, bool includeOverlaps)
    {
        List<GridPoint> retVal = new List<GridPoint>();

        // find upper-left and bottom-right X and Y grid coordinates
        PointF ptUL = GetGridPtAtPxl(matrix, new Point(pixelRange.Left, pixelRange.Top));
        PointF ptBR = GetGridPtAtPxl(matrix, new Point(pixelRange.Right - 1, pixelRange.Bottom - 1));
        
        // loop through all coordinates and add to return value
        for (int y = (int)Math.Floor(ptUL.Y); y <= (int)ptBR.Y; y++)
        {
            for (int x = (int)Math.Floor(ptUL.X); x <= (int)ptBR.X; x++)
            {
                var gPt = matrix[x, y];
                if (gPt != null)
                    retVal.Add(gPt);
            }
        }
        
        // check for overlaps if required
        if (includeOverlaps)
        {
            if (Tilesheet.MaxExtraTopSpaceRatio > 0)
            {
                foreach (GridPoint grPt in GetGridPtListInPxlRange(matrix,
                    new Rectangle(pixelRange.Left, pixelRange.Bottom,
                    pixelRange.Width,
                    (int)Math.Ceiling(Tilesheet.MaxExtraTopSpaceRatio * matrix.GridPointHeight)),
                    false))
                {
                    if (grPt != null)
                    {
                        if (GetPxlRangeAtGridPt(grPt, true).IntersectsWith(pixelRange))
                        {
                            if (retVal.IndexOf(grPt) == -1)
                                retVal.Add(grPt);
                        }
                    }
                }
            }
        }
        
        return retVal;
    }

    public Rectangle GetPxlRangeAtGridPt(Tile tile, bool inclOverlaps)
    {
        Rectangle retVal = new Rectangle();

        retVal.X = (int)(tile.ParentGrid.GridPointWidth * tile.GridCoordinates.X) + tile.ParentGrid.GridPointZeroPixel.X;
        retVal.Y = (int)(tile.ParentGrid.GridPointHeight * tile.GridCoordinates.Y) + tile.ParentGrid.GridPointZeroPixel.Y;

        retVal.Width = tile.ParentGrid.GridPointWidth;
        retVal.Height = tile.ParentGrid.GridPointHeight;
        

        if (inclOverlaps)
        {
            // if the Bmp has overlapping pixels at the top (defined in Tilesheet),
            // move the rectangle up (subtract from Y) and increase Height
            if (tile.CurrentFrame.Tilesheet != null && tile.CurrentFrame.Tilesheet.ExtraTopSpace != 0)
            {
                retVal.Y -= tile.OverlappingPixels;
                retVal.Height += tile.OverlappingPixels;
            }
        }

        return retVal;
    }

    public Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool inclOverlaps)
    {
        Rectangle retVal = new Rectangle();

        foreach (Tile tile in tileList)
        {
            if (retVal.IsEmpty)
                retVal = GetPxlRangeAtGridPt(tile, inclOverlaps);
            else
                retVal = Rectangle.Union(retVal, GetPxlRangeAtGridPt(tile, inclOverlaps));
        }

        return retVal;
    }

    public GridPoint GetAdjGridPt(GridPoint gridPt, CardinalDirections direction)
    {
        GridPointMatrix matrix = gridPt.ParentGrid;

        switch (direction)
        {
            case CardinalDirections.N:
                return matrix[gridPt.GridCoordinatesAbs.X, gridPt.GridCoordinatesAbs.Y - 1];
            case CardinalDirections.NE:
                return matrix[gridPt.GridCoordinatesAbs.X - 1, gridPt.GridCoordinatesAbs.Y - 1];
            case CardinalDirections.E:
                return matrix[gridPt.GridCoordinatesAbs.X + 1, gridPt.GridCoordinatesAbs.Y];
            case CardinalDirections.SE:
                return matrix[gridPt.GridCoordinatesAbs.X + 1, gridPt.GridCoordinatesAbs.Y + 1];
            case CardinalDirections.S:
                return matrix[gridPt.GridCoordinatesAbs.X, gridPt.GridCoordinatesAbs.Y + 1];
            case CardinalDirections.SW:
                return matrix[gridPt.GridCoordinatesAbs.X - 1, gridPt.GridCoordinatesAbs.Y + 1];
            case CardinalDirections.W:
                return matrix[gridPt.GridCoordinatesAbs.X - 1, gridPt.GridCoordinatesAbs.Y];
            case CardinalDirections.NW:
                return matrix[gridPt.GridCoordinatesAbs.X - 1, gridPt.GridCoordinatesAbs.Y - 1];
            default:
                return null;
        }
    }

    public Point[] GetPolygonPts(Tile tile, bool inclOverlaps)
    {
        Point[] ret = new Point[4];
        Rectangle outline = GetPxlRangeAtGridPt(tile, false);

        ret[0] = new Point(outline.Location.X, outline.Location.Y);
        ret[1] = new Point(outline.Location.X + outline.Width, outline.Location.Y);
        ret[2] = new Point(outline.Location.X + outline.Width, outline.Location.Y + outline.Height);
        ret[3] = new Point(outline.Location.X, outline.Location.Y + outline.Height);

        return ret;
    }

    public PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);

        if (modX < 0)
            modX += xUpperBound + 1;

        if (modY < 0)
            modY += yUpperBound + 1;

        return new PointF(modX, modY);
    }
}

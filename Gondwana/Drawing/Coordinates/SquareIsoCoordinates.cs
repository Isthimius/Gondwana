using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public class SquareIsoCoordinates : ISceneLayerCoordinates
{
    public Point GetSrcPixelAtLayerPoint(SceneLayer matrix, PointF gridCoord)
    {
        Point retVal = new Point();

        retVal.X = (int)(matrix.GridPointWidth * (gridCoord.X - matrix.SourceGridPoint.X));
        retVal.Y = (int)(matrix.GridPointHeight * (gridCoord.Y - matrix.SourceGridPoint.Y));

        return retVal;
    }

    public PointF GetLayerPointAtPixel(SceneLayer matrix, Point pixelPt)
    {
        PointF retPt = new PointF();

        retPt.X = (pixelPt.X - matrix.GridPointZeroPixel.X) / (float)matrix.GridPointWidth;
        retPt.Y = (pixelPt.Y - matrix.GridPointZeroPixel.Y) / (float)matrix.GridPointHeight;

        return retPt;
    }

    // Updated to properly consider overhang in all directions
    public List<SceneLayerPoint> GetLayerPointListInPixelRange(SceneLayer matrix, Rectangle pixelRange, bool includeOverhang)
    {
        var retVal = new List<SceneLayerPoint>();

        // 1) Find coarse grid bounds via inverse transform (unchanged)
        PointF ptUL = GetLayerPointAtPixel(matrix, new Point(pixelRange.Left, pixelRange.Top));
        PointF ptBR = GetLayerPointAtPixel(matrix, new Point(pixelRange.Right - 1, pixelRange.Bottom - 1));

        int minY = (int)Math.Floor(ptUL.Y) - 1;
        int maxY = (int)Math.Ceiling(ptBR.Y) + 1;
        int minX = (int)Math.Floor(ptUL.X) - 1;
        int maxX = (int)Math.Ceiling(ptBR.X) + 1;

        // 2) Scan candidate grid cells and include if their overhang-aware rect intersects
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gPt = matrix[x, y];
                if (gPt == null) continue;

                // Overhang-aware pixel rect
                var rect = GetPixelRangeAtLayerPoint(gPt, includeOverhang);
                if (rect.IntersectsWith(pixelRange))
                    retVal.Add(gPt);
            }
        }

        return retVal;
    }

    public Rectangle GetPixelRangeAtLayerPoint(Tile tile, bool includeOverhang)
    {
        // Base rect (unchanged)
        var baseRect = new Rectangle
        {
            X = (int)(tile.ParentGrid.GridPointWidth * tile.GridCoordinates.X) + tile.ParentGrid.GridPointZeroPixel.X,
            Y = (int)(tile.ParentGrid.GridPointHeight * tile.GridCoordinates.Y) + tile.ParentGrid.GridPointZeroPixel.Y,
            Width = tile.ParentGrid.GridPointWidth,
            Height = tile.ParentGrid.GridPointHeight
        };

        // Apply full overhang (Left/Top/Right/Bottom)
        return TileBounds.ApplyOverhang(baseRect, tile.OverhangPixels, includeOverhang);
    }

    public Rectangle GetPixelRangeAtLayerPointList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle retVal = Rectangle.Empty;

        foreach (Tile tile in tileList)
        {
            var rect = GetPixelRangeAtLayerPoint(tile, includeOverhang);
            retVal = retVal.IsEmpty ? rect : Rectangle.Union(retVal, rect);
        }

        return retVal;
    }

    public SceneLayerPoint GetAdjacentLayerPoint(SceneLayerPoint gridPt, CardinalDirections direction)
    {
        SceneLayer matrix = gridPt.ParentGrid;

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

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        // Square polygon using the overhang-aware rect
        var r = GetPixelRangeAtLayerPoint(tile, includeOverhang);
        return new[]
        {
                new Point(r.Left,  r.Top),
                new Point(r.Right, r.Top),
                new Point(r.Right, r.Bottom),
                new Point(r.Left,  r.Bottom)
            };
    }

    public PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound)
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
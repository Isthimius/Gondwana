using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public class SquareIsoCoordinates : ISceneLayerCoordinates
{
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF layerPoint)
    {
        Point retVal = new Point();

        retVal.X = (int)(sceneLayer.SceneLayerTileWidth * (layerPoint.X - sceneLayer.RenderSurfaceOriginCoordinates.X));
        retVal.Y = (int)(sceneLayer.SceneLayerTileHeight * (layerPoint.Y - sceneLayer.RenderSurfaceOriginCoordinates.Y));

        return retVal;
    }

    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        PointF retPt = new PointF();

        retPt.X = (pixelPt.X - sceneLayer.RenderSurfaceOriginPx.X) / (float)sceneLayer.SceneLayerTileWidth;
        retPt.Y = (pixelPt.Y - sceneLayer.RenderSurfaceOriginPx.Y) / (float)sceneLayer.SceneLayerTileHeight;

        return retPt;
    }

    // Updated to properly consider overhang in all directions
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle pixelRange, bool includeOverhang)
    {
        var retVal = new List<SceneLayerTile>();

        // 1) Find coarse grid bounds via inverse transform (unchanged)
        PointF ptUL = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Left, pixelRange.Top));
        PointF ptBR = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Right - 1, pixelRange.Bottom - 1));

        int minY = (int)Math.Floor(ptUL.Y) - 1;
        int maxY = (int)Math.Ceiling(ptBR.Y) + 1;
        int minX = (int)Math.Floor(ptUL.X) - 1;
        int maxX = (int)Math.Ceiling(ptBR.X) + 1;

        // 2) Scan candidate grid cells and include if their overhang-aware rect intersects
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gPt = sceneLayer[x, y];
                if (gPt == null) continue;

                // Overhang-aware pixel rect
                var rect = GetPixelRangeForTile(gPt, includeOverhang);
                if (rect.IntersectsWith(pixelRange))
                    retVal.Add(gPt);
            }
        }

        return retVal;
    }

    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        // Base rect (unchanged)
        var baseRect = new Rectangle
        {
            X = (int)(tile.SceneLayer.SceneLayerTileWidth * tile.SceneLayerCoordinates.X) + tile.SceneLayer.RenderSurfaceOriginPx.X,
            Y = (int)(tile.SceneLayer.SceneLayerTileHeight * tile.SceneLayerCoordinates.Y) + tile.SceneLayer.RenderSurfaceOriginPx.Y,
            Width = tile.SceneLayer.SceneLayerTileWidth,
            Height = tile.SceneLayer.SceneLayerTileHeight
        };

        // Apply full overhang (Left/Top/Right/Bottom)
        return TileBounds.ApplyOverhang(baseRect, tile.OverhangPixels, includeOverhang);
    }

    public Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle retVal = Rectangle.Empty;

        foreach (Tile tile in tileList)
        {
            var rect = GetPixelRangeForTile(tile, includeOverhang);
            retVal = retVal.IsEmpty ? rect : Rectangle.Union(retVal, rect);
        }

        return retVal;
    }

    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile layerPoint, CardinalDirections direction)
    {
        SceneLayer sceneLayer = layerPoint.SceneLayer;

        switch (direction)
        {
            case CardinalDirections.N:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X, layerPoint.GridCoordinatesAbs.Y - 1];

            case CardinalDirections.NE:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y - 1];

            case CardinalDirections.E:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y];

            case CardinalDirections.SE:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.S:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.SW:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.W:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y];

            case CardinalDirections.NW:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y - 1];

            default:
                return null;
        }
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        // Square polygon using the overhang-aware rect
        var r = GetPixelRangeForTile(tile, includeOverhang);
        return new[]
        {
                new Point(r.Left,  r.Top),
                new Point(r.Right, r.Top),
                new Point(r.Right, r.Bottom),
                new Point(r.Left,  r.Bottom)
            };
    }

    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
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
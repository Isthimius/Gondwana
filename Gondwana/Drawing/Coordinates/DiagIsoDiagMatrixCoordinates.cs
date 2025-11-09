using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Diagonal-Isometric (Diagonal Matrix) – same diamond projection
/// but kept separate for alternative adjacency/rounding if needed.
/// Uses identical math for now (clean, predictable behavior).
/// </summary>
public class DiagIsoDiagMatrixCoordinates : ISceneLayerCoordinates
{
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        int W =  sceneLayer.SceneLayerTileWidth; int H =  sceneLayer.SceneLayerTileHeight;
        float dx = gp.X -  sceneLayer.SourceSceneLayerTile.X;
        float dy = gp.Y -  sceneLayer.SourceSceneLayerTile.Y;
        float px =  sceneLayer.ZeroPixel.X + (dx - dy) * (W / 2f);
        float py =  sceneLayer.ZeroPixel.Y + (dx + dy) * (H / 2f);
        return new Point((int)Math.Floor(px), (int)Math.Floor(py));
    }

    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        int W =  sceneLayer.SceneLayerTileWidth; int H =  sceneLayer.SceneLayerTileHeight;
        float a = (pixelPt.X -  sceneLayer.ZeroPixel.X) / (W / 2f);
        float b = (pixelPt.Y -  sceneLayer.ZeroPixel.Y) / (H / 2f);
        float dx = (a + b) / 2f;
        float dy = (b - a) / 2f;
        return new PointF( sceneLayer.SourceSceneLayerTile.X + dx,  sceneLayer.SourceSceneLayerTile.Y + dy);
    }

    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer  sceneLayer, Rectangle pixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        var ul = GetSceneLayerCoordinatesAtPixel( sceneLayer, new PointF(pixelRange.Left, pixelRange.Top));
        var ur = GetSceneLayerCoordinatesAtPixel( sceneLayer, new PointF(pixelRange.Right, pixelRange.Top));
        var ll = GetSceneLayerCoordinatesAtPixel( sceneLayer, new PointF(pixelRange.Left, pixelRange.Bottom));
        var lr = GetSceneLayerCoordinatesAtPixel( sceneLayer, new PointF(pixelRange.Right, pixelRange.Bottom));

        int minX = (int)Math.Floor(new[] { ul.X, ur.X, ll.X, lr.X }.Min()) - 1;
        int maxX = (int)Math.Ceiling(new[] { ul.X, ur.X, ll.X, lr.X }.Max()) + 1;
        int minY = (int)Math.Floor(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Min()) - 1;
        int maxY = (int)Math.Ceiling(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Max()) + 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gp =  sceneLayer[x, y];
                if (gp == null) continue;
                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(pixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.SceneLayerTileWidth; int H = tile.SceneLayer.SceneLayerTileHeight;
        var rect = new Rectangle(top.X - W / 2, top.Y, W, H);
        return TileBounds.ApplyOverhang(rect, tile.OverhangPixels, includeOverhang);
    }

    public Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle ret = Rectangle.Empty;
        foreach (var t in tileList)
        {
            var r = GetPixelRangeForTile(t, includeOverhang);
            ret = ret.IsEmpty ? r : Rectangle.Union(ret, r);
        }
        return ret;
    }

    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile gp, CardinalDirections dir)
    {
        var m = gp.SceneLayer; int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        return dir switch
        {
            CardinalDirections.N => m[x, y - 1],
            CardinalDirections.S => m[x, y + 1],
            CardinalDirections.E => m[x + 1, y],
            CardinalDirections.W => m[x - 1, y],
            CardinalDirections.NE => m[x + 1, y - 1],
            CardinalDirections.NW => m[x - 1, y - 1],
            CardinalDirections.SE => m[x + 1, y + 1],
            CardinalDirections.SW => m[x - 1, y + 1],
            _ => null
        };
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.SceneLayerTileWidth; int H = tile.SceneLayer.SceneLayerTileHeight;
        var oh = includeOverhang ? tile.OverhangPixels : Overhang.None;

        return new[]
        {
                new Point(top.X, top.Y - oh.Top),
                new Point(top.X + W/2 + oh.Right, top.Y + H/2),
                new Point(top.X, top.Y + H + oh.Bottom),
                new Point(top.X - W/2 - oh.Left, top.Y + H/2)
            };
    }

    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }
}

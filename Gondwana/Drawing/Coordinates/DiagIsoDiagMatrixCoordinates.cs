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
    public Point GetSrcPixelAtLayerPoint(SceneLayer layer, PointF gp)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        float dx = gp.X - layer.SourceGridPoint.X;
        float dy = gp.Y - layer.SourceGridPoint.Y;
        float px = layer.GridPointZeroPixel.X + (dx - dy) * (W / 2f);
        float py = layer.GridPointZeroPixel.Y + (dx + dy) * (H / 2f);
        return new Point((int)Math.Floor(px), (int)Math.Floor(py));
    }

    public PointF GetLayerPointAtPixel(SceneLayer layer, Point pixelPt)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        float a = (pixelPt.X - layer.GridPointZeroPixel.X) / (W / 2f);
        float b = (pixelPt.Y - layer.GridPointZeroPixel.Y) / (H / 2f);
        float dx = (a + b) / 2f;
        float dy = (b - a) / 2f;
        return new PointF(layer.SourceGridPoint.X + dx, layer.SourceGridPoint.Y + dy);
    }

    public List<SceneLayerPoint> GetLayerPointListInPixelRange(SceneLayer layer, Rectangle pixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerPoint>();
        var ul = GetLayerPointAtPixel(layer, new Point(pixelRange.Left, pixelRange.Top));
        var ur = GetLayerPointAtPixel(layer, new Point(pixelRange.Right, pixelRange.Top));
        var ll = GetLayerPointAtPixel(layer, new Point(pixelRange.Left, pixelRange.Bottom));
        var lr = GetLayerPointAtPixel(layer, new Point(pixelRange.Right, pixelRange.Bottom));

        int minX = (int)Math.Floor(new[] { ul.X, ur.X, ll.X, lr.X }.Min()) - 1;
        int maxX = (int)Math.Ceiling(new[] { ul.X, ur.X, ll.X, lr.X }.Max()) + 1;
        int minY = (int)Math.Floor(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Min()) - 1;
        int maxY = (int)Math.Ceiling(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Max()) + 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gp = layer[x, y];
                if (gp == null) continue;
                var r = GetPixelRangeAtLayerPoint(gp, includeOverhang);
                if (r.IntersectsWith(pixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    public Rectangle GetPixelRangeAtLayerPoint(Tile tile, bool includeOverhang)
    {
        var top = GetSrcPixelAtLayerPoint(tile.ParentGrid, tile.GridCoordinates);
        int W = tile.ParentGrid.GridPointWidth; int H = tile.ParentGrid.GridPointHeight;
        var rect = new Rectangle(top.X - W / 2, top.Y, W, H);
        return TileBounds.ApplyOverhang(rect, tile.OverhangPixels, includeOverhang);
    }

    public Rectangle GetPixelRangeAtLayerPointList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle ret = Rectangle.Empty;
        foreach (var t in tileList)
        {
            var r = GetPixelRangeAtLayerPoint(t, includeOverhang);
            ret = ret.IsEmpty ? r : Rectangle.Union(ret, r);
        }
        return ret;
    }

    public SceneLayerPoint GetAdjacentLayerPoint(SceneLayerPoint gp, CardinalDirections dir)
    {
        var m = gp.ParentGrid; int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
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
        var top = GetSrcPixelAtLayerPoint(tile.ParentGrid, tile.GridCoordinates);
        int W = tile.ParentGrid.GridPointWidth; int H = tile.ParentGrid.GridPointHeight;
        var oh = includeOverhang ? tile.OverhangPixels : Overhang.None;

        return new[]
        {
                new Point(top.X, top.Y - oh.Top),
                new Point(top.X + W/2 + oh.Right, top.Y + H/2),
                new Point(top.X, top.Y + H + oh.Bottom),
                new Point(top.X - W/2 - oh.Left, top.Y + H/2)
            };
    }

    public PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }
}

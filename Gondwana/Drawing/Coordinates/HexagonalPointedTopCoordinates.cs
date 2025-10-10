using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Hexagonal – Pointed-Top layout (even-r horizontal layout)
/// Bounding rectangle is (W x H). Centers advance by (W/2 per row parity, 0.75*H vertically)
/// </summary>
public class HexagonalPointedTopCoordinates : ISceneLayerCoordinates
{
    public Point GetSrcPixelAtLayerPoint(SceneLayer layer, PointF gp)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        int col = (int)Math.Round(gp.X); int row = (int)Math.Round(gp.Y);

        int x = layer.GridPointZeroPixel.X + col * W + ((row & 1) == 0 ? 0 : W / 2);
        int y = layer.GridPointZeroPixel.Y + (int)Math.Floor(row * (H * 0.75f));
        return new Point(x, y);
    }

    public PointF GetLayerPointAtPixel(SceneLayer layer, Point pixelPt)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        float fy = (pixelPt.Y - layer.GridPointZeroPixel.Y) / (H * 0.75f);
        int approxRow = (int)Math.Round(fy);
        int baseX = layer.GridPointZeroPixel.X + ((approxRow & 1) == 0 ? 0 : W / 2);
        float fx = (pixelPt.X - baseX) / (float)W;
        int approxCol = (int)Math.Round(fx);

        var best = new Point(approxCol, approxRow); float bestDist = float.MaxValue;
        foreach (var cand in NeighborsPointedTop(approxCol, approxRow, includeSelf: true))
        {
            var poly = HexPolygonPointedTop(layer, cand.X, cand.Y, includeOverhang: false);
            if (PointInPolygon(poly, pixelPt)) return new PointF(cand.X, cand.Y);
            float cx = layer.GridPointZeroPixel.X + cand.X * W + ((cand.Y & 1) == 0 ? 0 : W / 2f) + W / 2f;
            float cy = layer.GridPointZeroPixel.Y + cand.Y * (H * 0.75f) + H / 2f;
            float d = (cx - pixelPt.X) * (cx - pixelPt.X) + (cy - pixelPt.Y) * (cy - pixelPt.Y);
            if (d < bestDist) { bestDist = d; best = cand; }
        }
        return new PointF(best.X, best.Y);
    }

    public List<SceneLayerPoint> GetLayerPointListInPixelRange(SceneLayer layer, Rectangle pixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerPoint>();
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;

        int minRow = (int)Math.Floor((pixelRange.Top - layer.GridPointZeroPixel.Y) / (H * 0.75f)) - 2;
        int maxRow = (int)Math.Ceiling((pixelRange.Bottom - layer.GridPointZeroPixel.Y) / (H * 0.75f)) + 2;

        for (int row = minRow; row <= maxRow; row++)
        {
            int xOffset = ((row & 1) == 0 ? 0 : W / 2);
            int minCol = (int)Math.Floor((pixelRange.Left - layer.GridPointZeroPixel.X - xOffset) / (float)W) - 2;
            int maxCol = (int)Math.Ceiling((pixelRange.Right - layer.GridPointZeroPixel.X - xOffset) / (float)W) + 2;

            for (int col = minCol; col <= maxCol; col++)
            {
                var gp = layer[col, row]; if (gp == null) continue;
                var r = GetPixelRangeAtLayerPoint(gp, includeOverhang);
                if (r.IntersectsWith(pixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    public Rectangle GetPixelRangeAtLayerPoint(Tile tile, bool includeOverhang)
    {
        var p = GetSrcPixelAtLayerPoint(tile.ParentGrid, tile.GridCoordinates);
        int W = tile.ParentGrid.GridPointWidth; int H = tile.ParentGrid.GridPointHeight;
        var rect = new Rectangle(p.X, p.Y, W, H);
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
        int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        bool even = (y & 1) == 0; var m = gp.ParentGrid;
        return dir switch
        {
            CardinalDirections.N => m[x, y - 1],
            CardinalDirections.S => m[x, y + 1],
            CardinalDirections.NE => m[x + (even ? 0 : 1), y - 1],
            CardinalDirections.NW => m[x - (even ? 1 : 0), y - 1],
            CardinalDirections.SE => m[x + (even ? 0 : 1), y + 1],
            CardinalDirections.SW => m[x - (even ? 1 : 0), y + 1],
            CardinalDirections.E => null,
            CardinalDirections.W => null,
            _ => null
        };
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        return HexPolygonPointedTop(tile.ParentGrid, (int)tile.GridCoordinates.X, (int)tile.GridCoordinates.Y, includeOverhang);
    }

    public PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }

    // Helpers ---------------------------------------------------
    private static IEnumerable<Point> NeighborsPointedTop(int col, int row, bool includeSelf)
    {
        if (includeSelf) yield return new Point(col, row);
        bool even = (row & 1) == 0;
        yield return new Point(col, row - 1); // N
        yield return new Point(col, row + 1); // S
        yield return new Point(col + (even ? 0 : 1), row - 1); // NE
        yield return new Point(col - (even ? 1 : 0), row - 1); // NW
        yield return new Point(col + (even ? 0 : 1), row + 1); // SE
        yield return new Point(col - (even ? 1 : 0), row + 1); // SW
    }

    private static Point[] HexPolygonPointedTop(SceneLayer layer, int col, int row, bool includeOverhang)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        var p = new Point(
            layer.GridPointZeroPixel.X + col * W + ((row & 1) == 0 ? 0 : W / 2),
            layer.GridPointZeroPixel.Y + (int)Math.Floor(row * (H * 0.75f)));

        var rect = new Rectangle(p.X, p.Y, W, H);
        var ohRect = TileBounds.ApplyOverhang(rect, includeOverhang ? new Overhang(0, 0, 0, 0) : Overhang.None, includeOverhang);
        int x = ohRect.X; int y = ohRect.Y;

        // Pointed-top vertices from bounding box
        return new[]
        {
                new Point(x + W/2, y),
                new Point(x + W, y + H/4),
                new Point(x + W, y + 3*H/4),
                new Point(x + W/2, y + H),
                new Point(x, y + 3*H/4),
                new Point(x, y + H/4)
            };
    }

    private static bool PointInPolygon(Point[] poly, Point p)
    {
        bool inside = false; int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            bool intersect = ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (float)(poly[j].Y - poly[i].Y) + poly[i].X);
            if (intersect) inside = !inside; j = i;
        }
        return inside;
    }
}

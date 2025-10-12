using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Hexagonal – Flat-Top layout (even-q vertical layout)
/// Bounding rectangle is (W x H). Centers advance by (0.75*W, H/2 per column parity)
/// </summary>
public class HexagonalFlatTopCoordinates : ISceneLayerCoordinates
{
    public Point GetSrcPixelAtLayerPoint(SceneLayer layer, PointF gp)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        int col = (int)Math.Round(gp.X); int row = (int)Math.Round(gp.Y);

        int x = layer.GridPointZeroPixel.X + (int)Math.Floor(col * (W * 0.75f));
        int y = layer.GridPointZeroPixel.Y + row * H + ((col & 1) == 0 ? 0 : H / 2);

        return new Point(x, y);
    }

    public PointF GetLayerPointAtPixel(SceneLayer layer, Point pixelPt)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        float fx = (pixelPt.X - layer.GridPointZeroPixel.X) / (W * 0.75f);
        int approxCol = (int)Math.Round(fx);
        int baseY = layer.GridPointZeroPixel.Y + ((approxCol & 1) == 0 ? 0 : H / 2);
        float fy = (pixelPt.Y - baseY) / (float)H;
        int approxRow = (int)Math.Round(fy);

        // refine by checking which nearby hex actually contains the pixel (up to 6 neighbors)
        var best = new Point(approxCol, approxRow);
        float bestDist = float.MaxValue;
        foreach (var cand in NeighborsFlatTop(approxCol, approxRow, includeSelf: true))
        {
            var poly = HexPolygonFlatTop(layer, cand.X, cand.Y, includeOverhang: false);
            if (PointInPolygon(poly, pixelPt)) return new PointF(cand.X, cand.Y);
            float cx = layer.GridPointZeroPixel.X + cand.X * (W * 0.75f) + W / 2f;
            float cy = layer.GridPointZeroPixel.Y + cand.Y * H + ((cand.X & 1) == 0 ? 0 : H / 2) + H / 2f;
            float d = (cx - pixelPt.X) * (cx - pixelPt.X) + (cy - pixelPt.Y) * (cy - pixelPt.Y);
            if (d < bestDist) { bestDist = d; best = cand; }
        }
        return new PointF(best.X, best.Y);
    }

    public List<SceneLayerTile> GetLayerPointListInPixelRange(SceneLayer layer, Rectangle pixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;

        int minCol = (int)Math.Floor((pixelRange.Left - layer.GridPointZeroPixel.X) / (W * 0.75f)) - 2;
        int maxCol = (int)Math.Ceiling((pixelRange.Right - layer.GridPointZeroPixel.X) / (W * 0.75f)) + 2;

        for (int col = minCol; col <= maxCol; col++)
        {
            int yOffset = ((col & 1) == 0 ? 0 : H / 2);
            int minRow = (int)Math.Floor((pixelRange.Top - layer.GridPointZeroPixel.Y - yOffset) / (float)H) - 2;
            int maxRow = (int)Math.Ceiling((pixelRange.Bottom - layer.GridPointZeroPixel.Y - yOffset) / (float)H) + 2;

            for (int row = minRow; row <= maxRow; row++)
            {
                var gp = layer[col, row];
                if (gp == null) continue;
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
        var rect = new Rectangle(p.X, p.Y, W, H); // hex image fits W x H box
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

    public SceneLayerTile GetAdjacentLayerPoint(SceneLayerTile gp, CardinalDirections dir)
    {
        int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        bool even = (x & 1) == 0;
        var m = gp.ParentGrid;
        return dir switch
        {
            CardinalDirections.E => m[x + 1, y],
            CardinalDirections.W => m[x - 1, y],
            CardinalDirections.NE => m[x + 1, y - (even ? 1 : 0)],
            CardinalDirections.SE => m[x + 1, y + (even ? 0 : 1)],
            CardinalDirections.NW => m[x - 1, y - (even ? 1 : 0)],
            CardinalDirections.SW => m[x - 1, y + (even ? 0 : 1)],
            CardinalDirections.N => null, // not a direct neighbor in hex grid
            CardinalDirections.S => null,
            _ => null
        };
    }

    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        return HexPolygonFlatTop(tile.ParentGrid, (int)tile.GridCoordinates.X, (int)tile.GridCoordinates.Y, includeOverhang);
    }

    public PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        // wrap like square; hex maps are usually finite with wrap disabled, but keep parity sane
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }

    // Helpers ---------------------------------------------------
    private static IEnumerable<Point> NeighborsFlatTop(int col, int row, bool includeSelf)
    {
        if (includeSelf) yield return new Point(col, row);
        bool even = (col & 1) == 0;
        yield return new Point(col + 1, row);
        yield return new Point(col - 1, row);
        yield return new Point(col + 1, row + (even ? 0 : 1));
        yield return new Point(col + 1, row - (even ? 1 : 0));
        yield return new Point(col - 1, row + (even ? 0 : 1));
        yield return new Point(col - 1, row - (even ? 1 : 0));
    }

    private static Point[] HexPolygonFlatTop(SceneLayer layer, int col, int row, bool includeOverhang)
    {
        int W = layer.GridPointWidth; int H = layer.GridPointHeight;
        var p = new Point(
            layer.GridPointZeroPixel.X + (int)Math.Floor(col * (W * 0.75f)),
            layer.GridPointZeroPixel.Y + row * H + ((col & 1) == 0 ? 0 : H / 2));

        var rect = new Rectangle(p.X, p.Y, W, H);
        var ohRect = TileBounds.ApplyOverhang(rect, includeOverhang ? new Overhang(0, 0, 0, 0) : Overhang.None, includeOverhang); // polygon uses base box; overhang impacts range, not shape

        int x = ohRect.X; int y = ohRect.Y;
        // Flat-top vertices from bounding box
        return new[]
        {
                new Point(x + W/4, y),
                new Point(x + 3*W/4, y),
                new Point(x + W, y + H/2),
                new Point(x + 3*W/4, y + H),
                new Point(x + W/4, y + H),
                new Point(x, y + H/2)
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
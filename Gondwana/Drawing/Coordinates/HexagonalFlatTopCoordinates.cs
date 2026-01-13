using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Hexagonal – Flat-Top layout (even-q vertical layout)
/// Bounding rectangle is (W x H). Centers advance by (0.75*W, H/2 per column parity)
/// </summary>
internal sealed class HexagonalFlatTopCoordinates : ISceneLayerCoordinates
{
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        int W = sceneLayer.SceneLayerTileWidth;
        int H = sceneLayer.SceneLayerTileHeight;
        int col = (int)Math.Round(gp.X);
        int row = (int)Math.Round(gp.Y);

        var origin = sceneLayer.OriginPx;
        int x = (int)Math.Floor(-origin.X + col * (W * 0.75f));
        int y = (int)Math.Floor(-origin.Y + row * (double)H + ((col & 1) == 0 ? 0 : H / 2));

        return new Point(x, y);
    }

    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        int W = sceneLayer.SceneLayerTileWidth;
        int H = sceneLayer.SceneLayerTileHeight;

        float originX = sceneLayer.OriginPx.X;
        float originY = sceneLayer.OriginPx.Y;

        // --- 1) Coarse column estimate using centers, not box top-left ---

        // Center of column 0 is at originX + W/2
        float dxFromCol0Center = pixelPt.X - (-originX + W * 0.5f);

        // Columns advance by 0.75 * W horizontally
        float colF = dxFromCol0Center / (W * 0.75f);
        int approxCol = (int)Math.Round(colF);

        // --- 2) Coarse row estimate using centers and parity ---

        // Center Y of row 0 in this column:
        //   even col: originY + H/2
        //   odd  col: originY + H/2 + H/2 = originY + H
        float baseCenterY =
            -originY + H * 0.5f
            + ((approxCol & 1) == 0 ? 0f : H * 0.5f);

        float dyFromRow0Center = pixelPt.Y - baseCenterY;
        float rowF = dyFromRow0Center / H;
        int approxRow = (int)Math.Round(rowF);

        var best = new Point(approxCol, approxRow);
        float bestDist = float.MaxValue;

        // --- 3) Refine via polygon hit-test + nearest center ---

        foreach (var cand in NeighborsFlatTop(approxCol, approxRow, includeSelf: true))
        {
            var poly = HexPolygonFlatTop(sceneLayer, cand.X, cand.Y, includeOverhang: false);

            var pInt = new Point((int)Math.Round(pixelPt.X), (int)Math.Round(pixelPt.Y));
            if (PointInPolygon(poly, pInt))
                return new PointF(cand.X, cand.Y);

            // Center of candidate hex
            float cx = -originX + cand.X * (W * 0.75f) + W / 2f;
            float cy = -originY + cand.Y * (float)H
                       + ((cand.X & 1) == 0 ? 0 : H / 2) + H / 2f;

            float dx = cx - pixelPt.X;
            float dy = cy - pixelPt.Y;
            float d = dx * dx + dy * dy;

            if (d < bestDist)
            {
                bestDist = d;
                best = cand;
            }
        }

        return new PointF(best.X, best.Y);
    }

    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle worldPixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        int W = sceneLayer.SceneLayerTileWidth;
        int H = sceneLayer.SceneLayerTileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        int minCol = (int)Math.Floor((worldPixelRange.Left + originX) / (W * 0.75f)) - 2;
        int maxCol = (int)Math.Ceiling((worldPixelRange.Right + originX) / (W * 0.75f)) + 2;

        for (int col = minCol; col <= maxCol; col++)
        {
            int yOffset = ((col & 1) == 0 ? 0 : H / 2);
            int minRow = (int)Math.Floor((worldPixelRange.Top + originY - yOffset) / (float)H) - 2;
            int maxRow = (int)Math.Ceiling((worldPixelRange.Bottom + originY - yOffset) / (float)H) + 2;

            for (int row = minRow; row <= maxRow; row++)
            {
                var gp = sceneLayer[col, row];
                if (gp == null) continue;
                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(worldPixelRange))
                    result.Add(gp);
            }
        }

        return result;
    }

    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var p = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.SceneLayerTileWidth; int H = tile.SceneLayer.SceneLayerTileHeight;
        var rect = new Rectangle(p.X, p.Y, W, H); // hex image fits W x H box
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
        int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        bool even = (x & 1) == 0;
        var m = gp.SceneLayer;
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
        return HexPolygonFlatTop(tile.SceneLayer, (int)tile.SceneLayerCoordinates.X, (int)tile.SceneLayerCoordinates.Y, includeOverhang);
    }

    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        // wrap like square; hex maps are usually finite with wrap disabled, but keep parity sane
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }

    #region helpers

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

    private static Point[] HexPolygonFlatTop(SceneLayer sceneLayer, int col, int row, bool includeOverhang)
    {
        int W = sceneLayer.SceneLayerTileWidth;
        int H = sceneLayer.SceneLayerTileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        var p = new Point(
            -originX + (int)Math.Floor(col * (W * 0.75f)),
            -originY + row * H + ((col & 1) == 0 ? 0 : H / 2));

        var rect = new Rectangle(p.X, p.Y, W, H);
        var ohRect = TileBounds.ApplyOverhang(
            rect,
            includeOverhang ? new Overhang(0, 0, 0, 0) : Overhang.None,
            includeOverhang); // polygon uses base box; overhang impacts range, not shape

        int x = ohRect.X;
        int y = ohRect.Y;

        // Flat-top vertices from bounding box
        return new[]
        {
            new Point(x + W/4,    y),
            new Point(x + 3*W/4,  y),
            new Point(x + W,      y + H/2),
            new Point(x + 3*W/4,  y + H),
            new Point(x + W/4,    y + H),
            new Point(x,          y + H/2)
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

    #endregion helpers
}
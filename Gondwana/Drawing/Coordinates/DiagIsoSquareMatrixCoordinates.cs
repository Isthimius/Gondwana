using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Diagonal-Isometric (Square Matrix, **no 45° rotation**)
/// - World/layout is axis-aligned (rectangular bounds, no dx/dy mixing)
/// - Each grid cell renders a diamond inside its W×H footprint
/// - Column centers advance by (W/2, 0); row centers by (0, H/2)
///   (i.e., tight diamond packing without rotating the world axes)
/// Pixel anchor = TOP vertex of the diamond
/// </summary>
public class DiagIsoSquareMatrixCoordinates : ISceneLayerCoordinates
{
    // Precompute half sizes repeatedly used
    private static void WH(SceneLayer m, out int W, out int H, out float halfW, out float halfH)
    {
        W = m.SceneLayerTileWidth;
        H = m.SceneLayerTileHeight;
        halfW = W * 0.5f;
        halfH = H * 0.5f;
    }

    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Axis-aligned layout: gx only affects X; gy only affects Y.
        float gx = gp.X - sceneLayer.SourceSceneLayerTile.X;
        float gy = gp.Y - sceneLayer.SourceSceneLayerTile.Y;

        // STEP BY FULL TILE SIZE (W, H) — not half
        float px = sceneLayer.SceneLayerTileZeroPixel.X + gx * W;
        float py = sceneLayer.SceneLayerTileZeroPixel.Y + gy * H;

        return new Point((int)Math.Floor(px), (int)Math.Floor(py));
    }

    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Inverse for full-tile stepping
        float gxF = (pixelPt.X - sceneLayer.SceneLayerTileZeroPixel.X) / W;
        float gyF = (pixelPt.Y - sceneLayer.SceneLayerTileZeroPixel.Y) / H;

        return new PointF(sceneLayer.SourceSceneLayerTile.X + gxF,
                          sceneLayer.SourceSceneLayerTile.Y + gyF);
    }

    public List<SceneLayerTile> GetSceneLayerTileListInPixelRange(SceneLayer sceneLayer, Rectangle pixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Corner → coarse grid bounds (continuous)
        var ul = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Left, pixelRange.Top));
        var ur = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Right, pixelRange.Top));
        var ll = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Left, pixelRange.Bottom));
        var lr = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(pixelRange.Right, pixelRange.Bottom));

        int minX = (int)System.Math.Floor(System.Math.Min(System.Math.Min(ul.X, ur.X), System.Math.Min(ll.X, lr.X))) - 2;
        int maxX = (int)System.Math.Ceiling(System.Math.Max(System.Math.Max(ul.X, ur.X), System.Math.Max(ll.X, lr.X))) + 2;
        int minY = (int)System.Math.Floor(System.Math.Min(System.Math.Min(ul.Y, ur.Y), System.Math.Min(ll.Y, lr.Y))) - 2;
        int maxY = (int)System.Math.Ceiling(System.Math.Max(System.Math.Max(ul.Y, ur.Y), System.Math.Max(ll.Y, lr.Y))) + 2;

        // Clamp to layer bounds (no wrapping)
        int cols = sceneLayer.GridColumnCount;
        int rows = sceneLayer.GridRowCount;

        int xStart = System.Math.Max(minX, 0);
        int xEnd = System.Math.Min(maxX, cols - 1);
        int yStart = System.Math.Max(minY, 0);
        int yEnd = System.Math.Min(maxY, rows - 1);
        if (xStart > xEnd || yStart > yEnd) return result;

        for (int y = yStart; y <= yEnd; y++)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                var gp = sceneLayer[x, y];
                if (gp == null) continue;

                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(pixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        WH(tile.SceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Top vertex of the diamond
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);

        // Diamond fits exactly in W×H box whose top-left is (top.X - W/2, top.Y)
        var rect = new Rectangle(top.X - (int)halfW, top.Y, W, H);
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
        // Square-like adjacency over the rectangular index space
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
        WH(tile.SceneLayer, out int W, out int H, out _, out _);
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        var oh = includeOverhang ? tile.OverhangPixels : Overhang.None;

        // Diamond vertices (top, right, bottom, left)
        return new[]
        {
            new Point(top.X,                   top.Y - oh.Top),
            new Point(top.X + W/2 + oh.Right, top.Y + H/2),
            new Point(top.X,                   top.Y + H + oh.Bottom),
            new Point(top.X - W/2 - oh.Left,  top.Y + H/2)
        };
    }

    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        // For now, keep the simple torus mapping; if you prefer no wrap, clamp upstream.
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }
}

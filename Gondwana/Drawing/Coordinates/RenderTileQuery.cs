using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Conservative render-query result. <see cref="Tiles"/> may over-select a small viewport
/// fringe that is clipped by the renderer. <see cref="IsRenderOrdered"/> means the fixed-grid
/// stream already matches Gondwana's depth ordering when all returned tiles share Z-order.
/// </summary>
/// <param name="Tiles">The conservative list of render candidate tiles.</param>
/// <param name="IsRenderOrdered">Whether the candidate list already follows projection depth order.</param>
internal readonly record struct RenderTileCandidates(
    List<SceneLayerTile> Tiles,
    bool IsRenderOrdered);

/// <summary>
/// Projection-specific fast traversal used only by full-frame rendering. Exact public
/// intersection queries remain unchanged for gameplay, hit testing, and editor tooling.
/// </summary>
internal static class RenderTileQuery
{
    internal static RenderTileCandidates GetCandidates(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle worldPixelRange,
        bool includeOverhang)
    {
        var tiles = new List<SceneLayerTile>();

        VisitCandidates(
            coordinates,
            layer,
            worldPixelRange,
            includeOverhang,
            tile =>
            {
                tiles.Add(tile);
                return true;
            });

        return new(tiles, IsRenderOrdered(layer));
    }

    /// <summary>
    /// Visits conservative render candidates in projection depth order without materializing
    /// an intermediate tile list. Returning false from <paramref name="visitor"/> stops traversal.
    /// </summary>
    /// <param name="coordinates">The layer coordinate-system implementation.</param>
    /// <param name="layer">The scene layer whose geometry is queried.</param>
    /// <param name="worldPixelRange">The query or dirty rectangle in world pixels.</param>
    /// <param name="includeOverhang">Whether to include tile visuals extending beyond their grid cells.</param>
    /// <param name="visitor">The callback invoked for each candidate; returning false stops traversal.</param>
    /// <returns>True if traversal completed; false if the visitor stopped traversal early.</returns>
    internal static bool VisitCandidates(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle worldPixelRange,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return coordinates switch
        {
            OrthogonalCoordinates =>
                VisitOrthogonal(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            IsometricRhombicCoordinates =>
                VisitIsometricRhombic(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            IsometricAxialCoordinates =>
                VisitIsometricAxial(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            HexAxialFlatTopCoordinates =>
                VisitHexFlat(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            HexAxialPointedTop =>
                VisitHexPointed(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            ObliqueRightCoordinates or ObliqueLeftCoordinates =>
                VisitOblique(coordinates, layer, worldPixelRange, includeOverhang, visitor),
            _ => VisitExactFallback(coordinates, layer, worldPixelRange, includeOverhang, visitor)
        };
    }

    internal static bool IsRenderOrdered(SceneLayer layer) =>
        layer.TransformedTiles.Count == 0 ||
        layer.TileWidth == layer.TileHeight;

    internal static Rectangle GetFixedTileCellBounds(
        SceneLayer layer,
        SceneLayerTile tile)
    {
        Point grid = tile.GridCoordinatesAbs;
        int width = layer.TileWidth;
        int height = layer.TileHeight;

        if (layer.CoordinateSystem is OrthogonalCoordinates)
        {
            return new Rectangle(
                grid.X * width - layer.OriginPx.X,
                grid.Y * height - layer.OriginPx.Y,
                width,
                height);
        }

        Point anchor = layer.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(
            layer,
            grid);

        if (layer.CoordinateSystem is IsometricRhombicCoordinates or IsometricAxialCoordinates)
            return new Rectangle(anchor.X - width / 2, anchor.Y, width, height);

        return new Rectangle(anchor.X, anchor.Y, width, height);
    }

    private static bool VisitOrthogonal(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        var candidate = TileBounds.GetTransformedTileCandidateRange(layer, query, includeOverhang);
        var ul = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Left, candidate.Top));
        var br = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Right - 1, candidate.Bottom - 1));

        int minX = Math.Max(0, (int)Math.Floor(ul.X) - 1);
        int maxX = Math.Min(layer.GridColumnCount - 1, (int)Math.Ceiling(br.X) + 1);
        int minY = Math.Max(0, (int)Math.Floor(ul.Y) - 1);
        int maxY = Math.Min(layer.GridRowCount - 1, (int)Math.Ceiling(br.Y) + 1);

        return VisitRowMajor(layer, minX, maxX, minY, maxY, visitor);
    }

    private static bool VisitIsometricRhombic(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        GetCornerBounds(
            coordinates,
            layer,
            query,
            includeOverhang,
            1,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY);

        if (!Clamp(layer, ref minX, ref maxX, ref minY, ref maxY))
            return true;

        for (int depth = minX + minY; depth <= maxX + maxY; depth++)
        {
            int xStart = Math.Max(minX, depth - maxY);
            int xEnd = Math.Min(maxX, depth - minY);

            for (int x = xStart; x <= xEnd; x++)
            {
                int y = depth - x;
                if (layer[x, y] is { } tile && !visitor(tile))
                    return false;
            }
        }

        return true;
    }

    private static bool VisitIsometricAxial(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        GetCornerBounds(
            coordinates,
            layer,
            query,
            includeOverhang,
            2,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY);

        return VisitRowMajorClamped(layer, minX, maxX, minY, maxY, visitor);
    }

    private static bool VisitHexPointed(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        var candidate = TileBounds.GetTransformedTileCandidateRange(layer, query, includeOverhang);
        int width = layer.TileWidth;
        int height = layer.TileHeight;

        int minRow = Math.Max(
            0,
            (int)Math.Floor((candidate.Top + layer.OriginPx.Y) / (height * 0.75f)) - 2);
        int maxRow = Math.Min(
            layer.GridRowCount - 1,
            (int)Math.Ceiling((candidate.Bottom + layer.OriginPx.Y) / (height * 0.75f)) + 2);

        if (minRow > maxRow)
            return true;

        for (int row = minRow; row <= maxRow; row++)
        {
            int xOffset = (row & 1) == 0 ? 0 : width / 2;
            int minCol = Math.Max(
                0,
                (int)Math.Floor(
                    (candidate.Left + layer.OriginPx.X - xOffset) / (float)width) - 2);
            int maxCol = Math.Min(
                layer.GridColumnCount - 1,
                (int)Math.Ceiling(
                    (candidate.Right + layer.OriginPx.X - xOffset) / (float)width) + 2);

            for (int col = minCol; col <= maxCol; col++)
            {
                if (layer[col, row] is { } tile && !visitor(tile))
                    return false;
            }
        }

        return true;
    }

    private static bool VisitHexFlat(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        var candidate = TileBounds.GetTransformedTileCandidateRange(layer, query, includeOverhang);
        int width = layer.TileWidth;
        int height = layer.TileHeight;
        int halfHeight = height / 2;

        int minCol = Math.Max(
            0,
            (int)Math.Floor((candidate.Left + layer.OriginPx.X) / (width * 0.75f)) - 2);
        int maxCol = Math.Min(
            layer.GridColumnCount - 1,
            (int)Math.Ceiling((candidate.Right + layer.OriginPx.X) / (width * 0.75f)) + 2);

        if (minCol > maxCol)
            return true;

        int minEven =
            (int)Math.Floor((candidate.Top + layer.OriginPx.Y) / (float)height) - 2;
        int maxEven =
            (int)Math.Ceiling((candidate.Bottom + layer.OriginPx.Y) / (float)height) + 2;
        int minOdd =
            (int)Math.Floor(
                (candidate.Top + layer.OriginPx.Y - halfHeight) / (float)height) - 2;
        int maxOdd =
            (int)Math.Ceiling(
                (candidate.Bottom + layer.OriginPx.Y - halfHeight) / (float)height) + 2;

        int minRow = Math.Max(0, Math.Min(minEven, minOdd));
        int maxRow = Math.Min(layer.GridRowCount - 1, Math.Max(maxEven, maxOdd));
        if (minRow > maxRow)
            return true;

        for (int depth = minRow * 2; depth <= maxRow * 2 + 1; depth++)
        {
            for (int col = minCol; col <= maxCol; col++)
            {
                int numerator = depth - (col & 1);
                if ((numerator & 1) != 0)
                    continue;

                int row = numerator / 2;
                if (row < minRow || row > maxRow)
                    continue;

                if (layer[col, row] is { } tile && !visitor(tile))
                    return false;
            }
        }

        return true;
    }

    private static bool VisitOblique(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        GetCornerBounds(
            coordinates,
            layer,
            query,
            includeOverhang,
            1,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY);

        return VisitRowMajorClamped(layer, minX, maxX, minY, maxY, visitor);
    }

    private static bool VisitExactFallback(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        Func<SceneLayerTile, bool> visitor)
    {
        var exact = coordinates.GetSceneLayerTilesInPixelRange(
            layer,
            query,
            includeOverhang);

        for (int i = 0; i < exact.Count; i++)
        {
            if (!visitor(exact[i]))
                return false;
        }

        return true;
    }

    private static void GetCornerBounds(
        ISceneLayerCoordinates coordinates,
        SceneLayer layer,
        Rectangle query,
        bool includeOverhang,
        int margin,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        var candidate = TileBounds.GetTransformedTileCandidateRange(layer, query, includeOverhang);
        var ul = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Left, candidate.Top));
        var ur = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Right, candidate.Top));
        var ll = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Left, candidate.Bottom));
        var lr = coordinates.GetSceneLayerCoordinatesAtPixel(
            layer,
            new PointF(candidate.Right, candidate.Bottom));

        minX =
            (int)Math.Floor(Math.Min(Math.Min(ul.X, ur.X), Math.Min(ll.X, lr.X))) -
            margin;
        maxX =
            (int)Math.Ceiling(Math.Max(Math.Max(ul.X, ur.X), Math.Max(ll.X, lr.X))) +
            margin;
        minY =
            (int)Math.Floor(Math.Min(Math.Min(ul.Y, ur.Y), Math.Min(ll.Y, lr.Y))) -
            margin;
        maxY =
            (int)Math.Ceiling(Math.Max(Math.Max(ul.Y, ur.Y), Math.Max(ll.Y, lr.Y))) +
            margin;
    }

    private static bool VisitRowMajorClamped(
        SceneLayer layer,
        int minX,
        int maxX,
        int minY,
        int maxY,
        Func<SceneLayerTile, bool> visitor)
    {
        if (!Clamp(layer, ref minX, ref maxX, ref minY, ref maxY))
            return true;

        return VisitRowMajor(layer, minX, maxX, minY, maxY, visitor);
    }

    private static bool VisitRowMajor(
        SceneLayer layer,
        int minX,
        int maxX,
        int minY,
        int maxY,
        Func<SceneLayerTile, bool> visitor)
    {
        if (minX > maxX || minY > maxY)
            return true;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (layer[x, y] is { } tile && !visitor(tile))
                    return false;
            }
        }

        return true;
    }

    private static bool Clamp(
        SceneLayer layer,
        ref int minX,
        ref int maxX,
        ref int minY,
        ref int maxY)
    {
        minX = Math.Max(0, minX);
        maxX = Math.Min(layer.GridColumnCount - 1, maxX);
        minY = Math.Max(0, minY);
        maxY = Math.Min(layer.GridRowCount - 1, maxY);
        return minX <= maxX && minY <= maxY;
    }
}

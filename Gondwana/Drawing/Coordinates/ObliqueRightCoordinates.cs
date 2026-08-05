using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Oblique projection using a right-receding, sheared square lattice.
/// Columns remain horizontal while rows advance down and to the right,
/// producing a parallelogram tile footprint rather than an isometric diamond.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SceneLayer.TileWidth"/> and <see cref="SceneLayer.TileHeight"/>
/// describe the full pixel bounding box of each rendered tile.
/// The horizontal skew is half the tile width; the remaining width forms the
/// tile's horizontal top and bottom edges.
/// </para>
/// <para>
/// Tile artwork should fit the resulting parallelogram footprint, typically
/// using transparency in the unused corners of the tile's bounding box.
/// </para>
/// </remarks>
internal sealed class ObliqueRightCoordinates : ISceneLayerCoordinates
{
    /// <summary>
    /// Gets the anchor pixel position for a tile at the specified scene layer coordinates.
    /// The anchor is the top-left corner of the tile's full pixel bounding box.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing the tile.</param>
    /// <param name="layerPoint">The scene layer coordinates (column, row).</param>
    /// <returns>The pixel position of the tile's bounding-box anchor.</returns>
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF layerPoint)
    {
        GetGeometry(sceneLayer, out _, out int H, out int skewX, out int faceWidth);

        float x = -sceneLayer.OriginPx.X
                  + layerPoint.X * faceWidth
                  + layerPoint.Y * skewX;

        float y = -sceneLayer.OriginPx.Y
                  + layerPoint.Y * H;

        return new Point(
            (int)Math.Floor(x),
            (int)Math.Floor(y));
    }

    /// <summary>
    /// Converts a pixel-space point into continuous oblique grid coordinates.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query.</param>
    /// <param name="pixelPt">The world-space pixel position to convert.</param>
    /// <returns>The corresponding continuous scene layer coordinates.</returns>
    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        GetGeometry(sceneLayer, out _, out int H, out int skewX, out int faceWidth);

        float row = (pixelPt.Y + sceneLayer.OriginPx.Y) / H;
        float col = (pixelPt.X + sceneLayer.OriginPx.X - row * skewX) / faceWidth;

        return new PointF(col, row);
    }

    /// <summary>
    /// Gets all scene layer tiles whose pixel bounds intersect the specified range.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query.</param>
    /// <param name="worldPixelRange">The world-space pixel range to search.</param>
    /// <param name="includeOverhang">Whether tile overhang is included in intersection tests.</param>
    /// <returns>The tiles intersecting the specified pixel range.</returns>
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(
        SceneLayer sceneLayer,
        Rectangle worldPixelRange,
        bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();

        var upperLeft = GetSceneLayerCoordinatesAtPixel(
            sceneLayer,
            new PointF(worldPixelRange.Left, worldPixelRange.Top));

        var upperRight = GetSceneLayerCoordinatesAtPixel(
            sceneLayer,
            new PointF(worldPixelRange.Right, worldPixelRange.Top));

        var lowerLeft = GetSceneLayerCoordinatesAtPixel(
            sceneLayer,
            new PointF(worldPixelRange.Left, worldPixelRange.Bottom));

        var lowerRight = GetSceneLayerCoordinatesAtPixel(
            sceneLayer,
            new PointF(worldPixelRange.Right, worldPixelRange.Bottom));

        int minX = (int)Math.Floor(new[]
        {
            upperLeft.X,
            upperRight.X,
            lowerLeft.X,
            lowerRight.X
        }.Min()) - 1;

        int maxX = (int)Math.Ceiling(new[]
        {
            upperLeft.X,
            upperRight.X,
            lowerLeft.X,
            lowerRight.X
        }.Max()) + 1;

        int minY = (int)Math.Floor(new[]
        {
            upperLeft.Y,
            upperRight.Y,
            lowerLeft.Y,
            lowerRight.Y
        }.Min()) - 1;

        int maxY = (int)Math.Ceiling(new[]
        {
            upperLeft.Y,
            upperRight.Y,
            lowerLeft.Y,
            lowerRight.Y
        }.Max()) + 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = sceneLayer[x, y];
                if (tile is null)
                    continue;

                var tileRange = GetPixelRangeForTile(tile, includeOverhang);
                if (tileRange.IntersectsWith(worldPixelRange))
                    result.Add(tile);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the pixel bounding rectangle for the specified tile.
    /// </summary>
    /// <param name="tile">The tile whose bounds are requested.</param>
    /// <param name="includeOverhang">Whether tile overhang is included in the bounds.</param>
    /// <returns>The tile's pixel bounding rectangle.</returns>
    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var anchor = GetAnchorPixelAtSceneLayerCoordinates(
            tile.SceneLayer,
            tile.SceneLayerCoordinates);

        var baseRect = new Rectangle(
            anchor.X,
            anchor.Y,
            tile.SceneLayer.TileWidth,
            tile.SceneLayer.TileHeight);

        return TileBounds.ApplyOverhang(baseRect, tile.Overhang, includeOverhang);
    }

    /// <summary>
    /// Gets the combined pixel bounding rectangle for the specified tiles.
    /// </summary>
    /// <param name="tileList">The tiles whose combined bounds are requested.</param>
    /// <param name="includeOverhang">Whether tile overhang is included in the bounds.</param>
    /// <returns>The union of all tile bounds, or an empty rectangle when the list is empty.</returns>
    public Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle result = Rectangle.Empty;

        foreach (var tile in tileList)
        {
            var tileRange = GetPixelRangeForTile(tile, includeOverhang);
            result = result.IsEmpty
                ? tileRange
                : Rectangle.Union(result, tileRange);
        }

        return result;
    }

    /// <summary>
    /// Gets the adjacent scene layer tile in the specified cardinal direction.
    /// Oblique projection changes presentation only; topology remains a square grid.
    /// </summary>
    /// <param name="layerPoint">The source tile.</param>
    /// <param name="direction">The direction of the adjacent tile.</param>
    /// <returns>The adjacent tile, or <see langword="null"/> when none exists.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(
        SceneLayerTile layerPoint,
        CardinalDirections direction)
    {
        var sceneLayer = layerPoint.SceneLayer;
        int x = layerPoint.GridCoordinatesAbs.X;
        int y = layerPoint.GridCoordinatesAbs.Y;

        return direction switch
        {
            CardinalDirections.N => sceneLayer[x, y - 1],
            CardinalDirections.NE => sceneLayer[x + 1, y - 1],
            CardinalDirections.E => sceneLayer[x + 1, y],
            CardinalDirections.SE => sceneLayer[x + 1, y + 1],
            CardinalDirections.S => sceneLayer[x, y + 1],
            CardinalDirections.SW => sceneLayer[x - 1, y + 1],
            CardinalDirections.W => sceneLayer[x - 1, y],
            CardinalDirections.NW => sceneLayer[x - 1, y - 1],
            _ => null
        };
    }

    /// <summary>
    /// Gets the four pixel-space vertices of the tile's oblique parallelogram.
    /// </summary>
    /// <param name="tile">The tile whose polygon is requested.</param>
    /// <param name="includeOverhang">Whether tile overhang is reflected in the polygon.</param>
    /// <returns>The parallelogram vertices in clockwise order.</returns>
    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        GetGeometry(
            tile.SceneLayer,
            out int W,
            out int H,
            out int skewX,
            out int faceWidth);

        var anchor = GetAnchorPixelAtSceneLayerCoordinates(
            tile.SceneLayer,
            tile.SceneLayerCoordinates);

        var overhang = includeOverhang
            ? tile.Overhang
            : Spacing.None;

        return new[]
        {
            new Point(
                anchor.X - overhang.Left,
                anchor.Y - overhang.Top),

            new Point(
                anchor.X + faceWidth + overhang.Right,
                anchor.Y - overhang.Top),

            new Point(
                anchor.X + W + overhang.Right,
                anchor.Y + H + overhang.Bottom),

            new Point(
                anchor.X + skewX - overhang.Left,
                anchor.Y + H + overhang.Bottom)
        };
    }

    /// <summary>
    /// Finds equivalent scene layer coordinates within the specified bounds by wrapping values.
    /// </summary>
    /// <param name="valColRow">The input coordinates.</param>
    /// <param name="xUpperBound">The inclusive upper bound for the X coordinate.</param>
    /// <param name="yUpperBound">The inclusive upper bound for the Y coordinate.</param>
    /// <returns>The wrapped coordinates within the supplied bounds.</returns>
    public PointF FindEquivalentSceneLayerCoordinates(
        PointF valColRow,
        int xUpperBound,
        int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);

        if (modX < 0)
            modX += xUpperBound + 1;

        if (modY < 0)
            modY += yUpperBound + 1;

        return new PointF(modX, modY);
    }

    private static void GetGeometry(
        SceneLayer sceneLayer,
        out int width,
        out int height,
        out int skewX,
        out int faceWidth)
    {
        width = sceneLayer.TileWidth;
        height = sceneLayer.TileHeight;

        // The full tile image remains W x H. Half of W is used by the
        // row-wise shear; the remaining width is the horizontal tile face.
        skewX = width / 2;
        faceWidth = width - skewX;
    }
}

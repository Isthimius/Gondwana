using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Defines the coordinate transformation logic for a SceneLayer, providing
/// conversions between grid-space layer points and pixel-space positions,
/// as well as geometric queries such as adjacency, polygon outlines, and
/// wrapping behavior for different coordinate systems (square, isometric, hex, etc.).
/// </summary>
internal interface ISceneLayerCoordinates
{
    /// <summary>
    /// Computes the world-pixel repetition period for the layer coordinate system.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The layer repetition period in world pixels.</returns>
    LayerPeriod GetWrapPeriod(SceneLayer layer) => LayerPeriod.Create(this, layer);

    /// <summary>
    /// Returns the projection-defined world-space pixel anchor of the tile at the
    /// given grid coordinate (col,row) in this SceneLayer.
    /// 
    /// This is the starting pixel used to draw the tile’s image or polygon.
    /// Every tile's shape (square, isometric, hex) is positioned by taking this
    /// anchor pixel and adding its local geometry.
    /// 
    /// For rectangular and oblique tiles this is the top-left corner of the image
    /// bounding box. For isometric diamonds it is the top vertex.
    /// </summary>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="layerPoint">The position in scene-layer grid coordinates.</param>
    /// <returns>The tile anchor in world pixels for the supplied grid position.</returns>
    Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF layerPoint);

    /// <summary>
    /// Converts a pixel-space point into its corresponding grid-space
    /// layer coordinate (column, row) within the specified SceneLayer.
    /// </summary>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="worldPixelPt">The point in world pixels.</param>
    /// <returns>The corresponding position in scene-layer grid coordinates.</returns>
    PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF worldPixelPt);

    /// <summary>
    /// Returns a list of all layer points whose rendered pixel areas intersect
    /// the specified pixel-space rectangle, optionally including tiles with visual
    /// overhang regions (e.g., tall sprites or hexes that extend beyond their cell).
    /// </summary>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="worldPixelRange">The query rectangle in world pixels.</param>
    /// <param name="includeOverhang">Whether to include visual content extending beyond tile bounds.</param>
    /// <returns>The tiles whose rendered pixel areas intersect the query rectangle.</returns>
    List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle worldPixelRange, bool includeOverhang);

    /// <summary>
    /// Returns a conservative tile candidate stream for full-frame rendering.
    /// The stream may include a small clipped fringe, but must include every tile
    /// that the exact intersection query would return.
    /// </summary>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="worldPixelRange">The query rectangle in world pixels.</param>
    /// <param name="includeOverhang">Whether to include visual content extending beyond tile bounds.</param>
    /// <returns>The conservative set of tile candidates for full-frame rendering.</returns>
    RenderTileCandidates GetSceneLayerTilesForRendering(
        SceneLayer sceneLayer,
        Rectangle worldPixelRange,
        bool includeOverhang) =>
        RenderTileQuery.GetCandidates(this, sceneLayer, worldPixelRange, includeOverhang);

    /// <summary>
    /// Gets the pixel-space rectangle occupied by a given tile, optionally
    /// expanding to include any overhang region defined by the tile’s geometry.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="includeOverhang">Whether to include visual content extending beyond tile bounds.</param>
    /// <returns>The tile bounds in world pixels, including overhang when requested.</returns>
    Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang);

    /// <summary>
    /// Computes a bounding pixel-space rectangle that encompasses all tiles
    /// in the specified list, optionally including their overhang areas.
    /// </summary>
    /// <param name="tileList">The tile list.</param>
    /// <param name="includeOverhang">Whether to include visual content extending beyond tile bounds.</param>
    /// <returns>The world-pixel rectangle enclosing the supplied tiles.</returns>
    Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang);

    /// <summary>
    /// Returns the layer point adjacent to the specified one in the given
    /// cardinal direction (up, down, left, right, etc.), according to the
    /// current coordinate system’s topology.
    /// </summary>
    /// <param name="layerPoint">The position in scene-layer grid coordinates.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The neighboring tile in the requested direction.</returns>
    SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile layerPoint, CardinalDirections direction);

    /// <summary>
    /// Returns the polygon vertex positions (in pixel space) defining the
    /// visual shape of the specified tile, optionally including its overhang.
    /// Used for hit-testing, rendering outlines, or debug overlays.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="includeOverhang">Whether to include visual content extending beyond tile bounds.</param>
    /// <returns>The polygon vertices in world pixels.</returns>
    Point[] GetPolygonPts(Tile tile, bool includeOverhang);

    /// <summary>
    /// Maps a given grid-space coordinate into its equivalent position within
    /// the valid layer bounds, performing wrapping (modulo) as needed to keep
    /// the coordinate within the range [0..xUpperBound], [0..yUpperBound].
    /// </summary>
    /// <param name="valColRow">The val col row.</param>
    /// <param name="xUpperBound">The x upper bound.</param>
    /// <param name="yUpperBound">The y upper bound.</param>
    /// <returns>The equivalent grid position within the wrapped layer dimensions.</returns>
    PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound);
}

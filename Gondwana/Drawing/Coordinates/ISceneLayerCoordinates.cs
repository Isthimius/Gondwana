using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Defines the coordinate transformation logic for a SceneLayer, providing
/// conversions between grid-space layer points and pixel-space positions,
/// as well as geometric queries such as adjacency, polygon outlines, and
/// wrapping behavior for different coordinate systems (square, isometric, hex, etc.).
/// </summary>
public interface ISceneLayerCoordinates
{
    /// <summary>
    /// Converts a grid-space layer point (column, row) into its corresponding
    /// top-left pixel position within the specified SceneLayer.
    /// </summary>
    Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF layerPoint);

    /// <summary>
    /// Converts a pixel-space point into its corresponding grid-space
    /// layer coordinate (column, row) within the specified SceneLayer.
    /// </summary>
    PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt);

    /// <summary>
    /// Returns a list of all layer points whose rendered pixel areas intersect
    /// the specified pixel-space rectangle, optionally including tiles with visual
    /// overhang regions (e.g., tall sprites or hexes that extend beyond their cell).
    /// </summary>
    List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle pixelRange, bool includeOverhang);

    /// <summary>
    /// Gets the pixel-space rectangle occupied by a given tile, optionally
    /// expanding to include any overhang region defined by the tile’s geometry.
    /// </summary>
    Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang);

    /// <summary>
    /// Computes a bounding pixel-space rectangle that encompasses all tiles
    /// in the specified list, optionally including their overhang areas.
    /// </summary>
    Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang);

    /// <summary>
    /// Returns the layer point adjacent to the specified one in the given
    /// cardinal direction (up, down, left, right, etc.), according to the
    /// current coordinate system’s topology.
    /// </summary>
    SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile layerPoint, CardinalDirections direction);

    /// <summary>
    /// Returns the polygon vertex positions (in pixel space) defining the
    /// visual shape of the specified tile, optionally including its overhang.
    /// Used for hit-testing, rendering outlines, or debug overlays.
    /// </summary>
    Point[] GetPolygonPts(Tile tile, bool includeOverhang);

    /// <summary>
    /// Maps a given grid-space coordinate into its equivalent position within
    /// the valid layer bounds, performing wrapping (modulo) as needed to keep
    /// the coordinate within the range [0..xUpperBound], [0..yUpperBound].
    /// </summary>
    PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound);
}

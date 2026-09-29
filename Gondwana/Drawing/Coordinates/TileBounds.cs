using System.Drawing;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Provides utility methods for calculating tile boundaries and applying adjustments.
/// </summary>
public static class TileBounds
{
    internal static void IncludeTransformedTiles(Gondwana.Scenes.SceneLayer layer, Rectangle query, bool include, List<Gondwana.Scenes.SceneLayerTile> result)
    {
        if (!include || layer.TransformedTiles.Count == 0) return;
        var present = new HashSet<Gondwana.Scenes.SceneLayerTile>(result);
        foreach (var tile in layer.TransformedTiles)
            if (tile.DrawLocationWorld.IntersectsWith(query) && present.Add(tile)) result.Add(tile);
    }
    /// <summary>Applies placement geometry while preserving untransformed grid-cell bounds.</summary>
    public static Rectangle ApplyTileGeometry(Rectangle cell, Tile tile, bool include)
    {
        if (!include) return cell;
        return tile is Gondwana.Scenes.SceneLayerTile fixedTile
            ? ApplyOverhang(TileTransformGeometry.GetVisualBounds(cell, Spacing.None, fixedTile.Transform), tile.Overhang, true)
            : ApplyOverhang(cell, tile.Overhang, true);
    }
    /// <summary>
    /// Applies an overhang adjustment to a rectangle, expanding its bounds in all directions.
    /// </summary>
    /// <param name="baseRect">The base rectangle to adjust.</param>
    /// <param name="oh">The overhang values to apply to each side of the rectangle.</param>
    /// <param name="include">If <c>true</c>, applies the overhang; if <c>false</c>, returns the original rectangle.</param>
    /// <returns>
    /// A rectangle expanded by the overhang amounts if <paramref name="include"/> is <c>true</c> and the overhang is not empty;
    /// otherwise, the original <paramref name="baseRect"/>.
    /// </returns>
    public static Rectangle ApplyOverhang(Rectangle baseRect, Spacing oh, bool include)
    {
        if (!include || oh.IsEmpty)
            return baseRect;

        return Rectangle.FromLTRB(
            baseRect.Left - oh.Left,
            baseRect.Top - oh.Top,
            baseRect.Right + oh.Right,
            baseRect.Bottom + oh.Bottom
        );
    }
}

using System.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tooling.Tilesheets.Editing;

internal static class FrameGeometry
{
    /// <summary>Finds a frame beneath an image point, preferring the selected region and excluding padding and unused pixels.</summary>
    /// <param name="definition">Definition whose regions are searched.</param>
    /// <param name="selectedRegion">Region to search first if it belongs to the definition, or null.</param>
    /// <param name="imagePoint">Point in unscaled source-image pixels.</param>
    /// <returns>The matching region and zero-based frame column and row, or null when no frame is hit.</returns>
    public static (TilesheetRegionDefinition Region, Point Frame)? HitTest(
        TilesheetDefinition definition, TilesheetRegionDefinition? selectedRegion, PointF imagePoint)
    {
        if (selectedRegion is not null && definition.Regions.Contains(selectedRegion) &&
            HitTestRegion(selectedRegion, imagePoint) is { } selectedFrame)
            return (selectedRegion, selectedFrame);

        foreach (var region in definition.Regions)
            if (region != selectedRegion && HitTestRegion(region, imagePoint) is { } frame)
                return (region, frame);
        return null;
    }

    private static Point? HitTestRegion(TilesheetRegionDefinition region, PointF point)
    {
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        long pitchX = (long)region.TileSize.Width + region.TilePadding.Left + region.TilePadding.Right;
        long pitchY = (long)region.TileSize.Height + region.TilePadding.Top + region.TilePadding.Bottom;
        if (columns <= 0 || rows <= 0 || pitchX <= 0 || pitchY <= 0 ||
            region.TileSize.Width <= 0 || region.TileSize.Height <= 0) return null;

        double px = (double)point.X - region.Area.X - region.RegionMargin.Left;
        double py = (double)point.Y - region.Area.Y - region.RegionMargin.Top;
        if (!double.IsFinite(px) || !double.IsFinite(py) || px < 0 || py < 0) return null;
        double x = Math.Floor(px / pitchX), y = Math.Floor(py / pitchY);
        if (x >= columns || y >= rows || x > int.MaxValue || y > int.MaxValue) return null;

        // Padding and unused region pixels are not frames.
        double localX = px - x * pitchX - region.TilePadding.Left;
        double localY = py - y * pitchY - region.TilePadding.Top;
        return localX >= 0 && localX < region.TileSize.Width && localY >= 0 && localY < region.TileSize.Height
            ? new Point((int)x, (int)y) : null;
    }

    // Matches TilesheetRegion.GetTileBounds. Grid counts come directly from the shared validator.
    /// <summary>Computes frame bounds without checking whether the grid coordinates exist.</summary>
    /// <param name="region">Region supplying image origin, margins, padding, and tile dimensions.</param>
    /// <param name="x">Zero-based frame column.</param>
    /// <param name="y">Zero-based frame row.</param>
    /// <returns>Frame rectangle in source-image pixels.</returns>
    /// <exception cref="OverflowException">The computed frame origin exceeds integer coordinates.</exception>
    public static Rectangle Bounds(TilesheetRegionDefinition region, int x, int y) => new(
        checked((int)((long)region.Area.X + region.RegionMargin.Left + region.TilePadding.Left +
            x * ((long)region.TileSize.Width + region.TilePadding.Left + region.TilePadding.Right))),
        checked((int)((long)region.Area.Y + region.RegionMargin.Top + region.TilePadding.Top +
            y * ((long)region.TileSize.Height + region.TilePadding.Top + region.TilePadding.Bottom))),
        region.TileSize.Width, region.TileSize.Height);

    /// <summary>Computes collision bounds using frame overrides or the region default.</summary>
    /// <param name="region">Region supplying image origin, margins, padding, and tile dimensions.</param>
    /// <param name="x">Zero-based frame column.</param>
    /// <param name="y">Zero-based frame row.</param>
    /// <returns>Collision rectangle in source-image pixels.</returns>
    /// <exception cref="OverflowException">The computed frame origin exceeds integer coordinates.</exception>
    public static Rectangle CollisionBounds(TilesheetRegionDefinition region, int x, int y)
    {
        var frame = region.Frames.FirstOrDefault(f => f is not null && f.XTile == x && f.YTile == y);
        return (frame?.CollisionAdjust ?? region.CollisionAdjust).ApplyTo(Bounds(region, x, y));
    }
}

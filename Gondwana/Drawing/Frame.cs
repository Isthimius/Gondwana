using System.Drawing;
using SkiaSharp;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing;

/// <summary>
/// Represents the source Tilesheet and its coordinates to render on a destination.
/// </summary>
public struct Frame
{
    /// <summary>
    /// The tilesheet that contains the source bitmap for this frame.
    /// </summary>
    public readonly Tilesheet Tilesheet;

    /// <summary>
    /// The tilesheet region that contains the source bitmap for this frame.
    /// </summary>
    public readonly string RegionName;

    /// <summary>
    /// The horizontal tile coordinate (column index) within the tilesheet.
    /// </summary>
    public readonly int XTile;

    /// <summary>
    /// The vertical tile coordinate (row index) within the tilesheet.
    /// </summary>
    public readonly int YTile;

    /// <summary>
    /// Initializes a new instance of the <see cref="Frame"/> struct with the specified tilesheet and tile coordinates.
    /// </summary>
    public Frame(Tilesheet tilesheet, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        RegionName = TilesheetRegion.DefaultRegionName;
        XTile = xTile;
        YTile = yTile;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Frame"/> struct with the specified tilesheet and tile coordinates.
    /// </summary>
    public Frame(Tilesheet tilesheet, string regionName, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        RegionName = regionName;
        XTile = xTile;
        YTile = yTile;
    }

    /// <summary>
    /// Gets the SkiaSharp bitmap for this frame.
    /// </summary>
    public readonly SKBitmap? SkBitmap => Tilesheet?.GetBitmap(RegionName, XTile, YTile);

    /// <summary>
    /// Gets the SkiaSharp image for this frame.
    /// </summary>
    public readonly SKImage? SkImage => Tilesheet?.GetImage(RegionName, XTile, YTile);

    /// <summary>
    /// Gets the base tile size, without overhang.
    /// </summary>
    public readonly Size TileSize => Tilesheet?.GetRegion(RegionName)?.TileSize ?? Size.Empty;

    /// <summary>
    /// Gets the overhang dimensions for this frame.
    /// </summary>
    public readonly Spacing Overhang => Tilesheet?.GetRegion(RegionName)?.Overhang ?? Spacing.None;

    /// <summary>
    /// Gets or sets the collision adjustment associated with this frame's region coordinates.
    /// </summary>
    /// <remarks>
    /// A frame is a lightweight tilesheet reference, so assigning this property updates the
    /// authoritative per-frame metadata owned by <see cref="TilesheetRegion"/> and its cache.
    /// </remarks>
    public CollisionAdjust CollisionAdjust
    {
        readonly get => Tilesheet?.GetRegion(RegionName)?.GetFrameCollisionAdjust(XTile, YTile)
            ?? Gondwana.Physics.Collisions.CollisionAdjust.None;
        set
        {
            var region = Tilesheet?.GetRegion(RegionName)
                ?? throw new InvalidOperationException(
                    $"Tilesheet region '{RegionName}' could not be resolved for this frame.");

            region.SetFrameCollisionAdjust(XTile, YTile, value);
        }
    }

    /// <summary>
    /// Gets whether this frame has an explicit collision adjustment rather than
    /// inheriting its region's <see cref="TilesheetRegion.CollisionAdjust"/>.
    /// </summary>
    public readonly bool HasCollisionAdjustOverride =>
        Tilesheet?.GetRegion(RegionName)?.TryGetFrameCollisionAdjustOverride(
            XTile,
            YTile,
            out _) == true;

    /// <summary>
    /// Removes this frame's explicit collision adjustment so it once again inherits
    /// its region's <see cref="TilesheetRegion.CollisionAdjust"/>.
    /// </summary>
    public readonly bool ClearCollisionAdjustOverride()
    {
        var region = Tilesheet?.GetRegion(RegionName)
            ?? throw new InvalidOperationException(
                $"Tilesheet region '{RegionName}' could not be resolved for this frame.");

        return region.ClearFrameCollisionAdjustOverride(XTile, YTile);
    }

    /// <summary>
    /// Gets the frame-local collision rectangle derived from <see cref="TileSize"/> and
    /// <see cref="CollisionAdjust"/>.
    /// </summary>
    public readonly Rectangle CollisionArea =>
        CollisionAdjust.ApplyTo(new Rectangle(Point.Empty, TileSize));
}

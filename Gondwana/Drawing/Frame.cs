using System.Drawing;
using SkiaSharp;
using Gondwana.Drawing.Tilesheets;

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
    /// <param name="tilesheet">The tilesheet containing the source bitmap.</param>
    /// <param name="xTile">The horizontal tile coordinate (column index) within the tilesheet.</param>
    /// <param name="yTile">The vertical tile coordinate (row index) within the tilesheet.</param>
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
    /// <param name="tilesheet">The tilesheet containing the source bitmap.</param>
    /// <param name="regionName">The tilesheet region containing the source bitmap.</param>
    /// <param name="xTile">The horizontal tile coordinate (column index) within the tilesheet.</param>
    /// <param name="yTile">The vertical tile coordinate (row index) within the tilesheet.</param>
    public Frame(Tilesheet tilesheet, string regionName, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        RegionName = regionName;
        XTile = xTile;
        YTile = yTile;
    }

    /// <summary>
    /// Gets the SkiaSharp bitmap for this frame at the specified tile coordinates.
    /// Returns <see langword="null"/> if the tilesheet is not available.
    /// </summary>
    /// <returns>The frame bitmap, or <see langword="null"/>.</returns>
    public readonly SKBitmap? SkBitmap => Tilesheet?.GetBitmap(RegionName, XTile, YTile);

    /// <summary>
    /// Gets the SkiaSharp image for this frame at the specified tile coordinates.
    /// Returns <see langword="null"/> if the tilesheet is not available.
    /// </summary>
    /// <returns>The frame image, or <see langword="null"/>.</returns>
    public readonly SKImage? SkImage => Tilesheet?.GetImage(RegionName, XTile, YTile);

    /// <summary>
    /// Gets the base tile size (without overhang) from the tilesheet.
    /// Returns <see cref="Size.Empty"/> if the tilesheet is not available.
    /// </summary>
    public readonly Size TileSize => Tilesheet?.GetRegion(RegionName)?.TileSize ?? Size.Empty;

    /// <summary>
    /// Gets the overhang dimensions (in pixels) that extend beyond the base tile boundaries.
    /// Returns <see cref="Spacing.None"/> if the tilesheet is not available.
    /// </summary>
    public readonly Spacing Overhang => Tilesheet?.GetRegion(RegionName)?.Overhang ?? Spacing.None;
}
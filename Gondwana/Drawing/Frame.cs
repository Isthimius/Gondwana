using System.Drawing;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;
using SkiaSharp;

namespace Gondwana.Drawing;

/// <summary>
/// Represents the source Tilesheet and its coordinates to render on a destination.
/// </summary>
public struct Frame
{
    /// <summary>
    /// The tilesheet that contains the source bitmap for this frame.
    /// </summary>
    [JsonProperty]
    public readonly Tilesheet Tilesheet;

    /// <summary>
    /// The horizontal tile coordinate (column index) within the tilesheet.
    /// </summary>
    [JsonProperty]
    public readonly int XTile;

    /// <summary>
    /// The vertical tile coordinate (row index) within the tilesheet.
    /// </summary>
    [JsonProperty]
    public readonly int YTile;

    /// <summary>
    /// The duration in seconds this frame should display. A value of 0 means
    /// the owning <see cref="Gondwana.Drawing.Animation.Cycle"/>'s <c>ThrottleTime</c> is used instead.
    /// </summary>
    public double DurationSeconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="Frame"/> struct with the specified tilesheet and tile coordinates.
    /// </summary>
    /// <param name="tilesheet">The tilesheet containing the source bitmap.</param>
    /// <param name="xTile">The horizontal tile coordinate (column index) within the tilesheet.</param>
    /// <param name="yTile">The vertical tile coordinate (row index) within the tilesheet.</param>
    public Frame(Tilesheet tilesheet, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        XTile = xTile;
        YTile = yTile;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Frame"/> struct with the specified tilesheet, tile coordinates, and per-frame display duration.
    /// </summary>
    /// <param name="tilesheet">The tilesheet containing the source bitmap.</param>
    /// <param name="xTile">The horizontal tile coordinate (column index) within the tilesheet.</param>
    /// <param name="yTile">The vertical tile coordinate (row index) within the tilesheet.</param>
    /// <param name="durationSeconds">How long in seconds this frame should display. 0 defers to the cycle's throttle time.</param>
    public Frame(Tilesheet tilesheet, int xTile, int yTile, double durationSeconds)
    {
        Tilesheet = tilesheet;
        XTile = xTile;
        YTile = yTile;
        DurationSeconds = durationSeconds;
    }

    /// <summary>
    /// Gets the SkiaSharp bitmap for this frame at the specified tile coordinates.
    /// Returns <see langword="null"/> if the tilesheet is not available.
    /// </summary>
    [JsonIgnore]
    public readonly SKBitmap? SkBitmap => Tilesheet?.GetBitmap(XTile, YTile);

    /// <summary>
    /// Gets the SkiaSharp image for this frame at the specified tile coordinates.
    /// Returns <see langword="null"/> if the tilesheet is not available.
    /// </summary>
    [JsonIgnore]
    public readonly SKImage? SkImage => Tilesheet?.GetImage(XTile, YTile);

    /// <summary>
    /// Gets the base tile size (without overhang) from the tilesheet.
    /// Returns <see cref="Size.Empty"/> if the tilesheet is not available.
    /// </summary>
    [JsonIgnore]
    public Size BaseTileSize => Tilesheet?.TileSize ?? Size.Empty;

    /// <summary>
    /// Gets the overhang dimensions (in pixels) that extend beyond the base tile boundaries.
    /// Returns <see cref="Overhang.None"/> if the tilesheet is not available.
    /// </summary>
    [JsonIgnore]
    public Overhang OverhangPixels => Tilesheet?.OverhangPixels ?? Overhang.None;

    /// <summary>
    /// Gets the total tile size including overhang pixels in all directions.
    /// This is calculated as the base tile size plus the left, right, top, and bottom overhang values.
    /// </summary>
    [JsonIgnore]
    public Size TileSizeWithOverhang =>
        new Size(BaseTileSize.Width + OverhangPixels.Left + OverhangPixels.Right,
                 BaseTileSize.Height + OverhangPixels.Top + OverhangPixels.Bottom);
}
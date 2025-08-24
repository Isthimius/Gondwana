using SkiaSharp;
using Gondwana.Drawing;
using System.Drawing;
using Gondwana.Skia;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for managing a graphical backbuffer, and is the in-memory surface where
/// rendering operations are performed before being presented to the display.
/// </summary>
/// <remarks>This abstract class serves as the foundation for backbuffer implementations, offering methods and 
/// properties to facilitate rendering operations, manage graphical state, and interact with graphical  elements such as
/// tiles. Derived classes must implement the <see cref="Canvas"/>, <see cref="DrawTileFrame(Tile)"/>, and <see
/// cref="Snapshot"/> members to define specific rendering behavior.</remarks>
public abstract class BackbufferBase : IDisposable
{
    private static List<BackbufferBase> _allBackbuffers { get; } = new();

    protected readonly Rectangle _range;

    private BackbufferBase() { _allBackbuffers.Add(this); }

    protected BackbufferBase(int width, int height)
        : this()
    {
        _range = new Rectangle(0, 0, width, height);
    }

    public abstract SKCanvas Canvas { get; }
    public abstract void DrawTileFrame(Tile tile);
    public abstract SKImage Snapshot();

    public SKPaint FogPaint { get; set; } = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    public SKPaint GridPaint { get; set; } = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    public int Width => _range.Width;
    public int Height => _range.Height;

    public Rectangle DirtyRectangle { get; set; }
    
    private SKColor _clearColor = SKColors.Black;
    protected readonly SKPaint _fillPaint = new() { IsAntialias = false, BlendMode = SKBlendMode.Src };

    public SKColor ClearColor 
    {
        get => _clearColor;
        set
        {
            _clearColor = value;
            _fillPaint.Color = value;
        }
    }

    /// <summary>
    /// Runs as part of DoBackgroundTasks
    /// </summary>
    internal void DrawTiles(IList<Tile> tiles)
    {
        foreach (var tile in tiles)
        {
            if (!tile.Visible)
                continue;

            DrawTileFrame(tile);

            if (tile.EnableFog)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), FogPaint);

            if (!tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), GridPaint);
        }
    }

    protected void AddToDirtyRectangle(Rectangle area)
    {
        if (area.IsEmpty) return;

        DirtyRectangle = DirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(DirtyRectangle, area);
    }

    public virtual byte[] ToByteArray(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        using var image = Snapshot();
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    public virtual void Dispose()
    {
        _fillPaint.Dispose();
        FogPaint.Dispose();
        GridPaint.Dispose();

        _allBackbuffers.Remove(this);
    }
}

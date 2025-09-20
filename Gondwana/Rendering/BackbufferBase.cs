using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Skia;
using Microsoft.Extensions.Logging;
using SkiaSharp;

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
    private int _width;
    private int _height;

    private BackbufferBase() { }

    protected BackbufferBase(int width, int height)
        : this()
    {
        _width = width;
        _height = height;
    }

    public abstract SKCanvas Canvas { get; }
    public abstract void DrawTileFrame(Tile tile);
    public abstract SKImage Snapshot();
    public abstract void BeginFrame();
    public abstract void EndFrame();

    public SKPaint FogPaint { get; set; } = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    public SKPaint GridPaint { get; set; } = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    public int Width => Volatile.Read(ref _width);
    public int Height => Volatile.Read(ref _height);

    public virtual void RequestResize(int width, int height) { /* no-op by default */ }

    // Let subclasses (render thread only) update the logical size.
    public event Action<int, int>? SizeChanged;

    protected void UpdateSize(int width, int height)
    {
        Volatile.Write(ref _width, width);
        Volatile.Write(ref _height, height);

        Engine.Logger.LogTrace("*** in BackbufferBase.UpdateSize() width: " + width.ToString() + " height: " + height.ToString());

        SizeChanged?.Invoke(width, height);
    }

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
        Engine.Logger.LogTrace("in BackbufferBase.DrawTiles() count: " + tiles.Count.ToString());

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
    }
}

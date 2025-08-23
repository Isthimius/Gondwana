using SkiaSharp;
using Gondwana.Drawing;
using System.Drawing;
using Gondwana.Skia;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

public abstract class BackbufferBase : IDisposable
{
    private static List<BackbufferBase> _allBackbuffers { get; } = new();

    internal static void _resetAllDirtyRectangles()
    {
        foreach (var backbuffer in _allBackbuffers)
        {
            backbuffer.DirtyRectangle = Rectangle.Empty;
        }
    }

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

    private Rectangle _dirtyRectangle = Rectangle.Empty;
    public Rectangle DirtyRectangle
    {
        get => _dirtyRectangle;
        set
        {
            _dirtyRectangle = value;
            Engine.Logger.LogTrace("Backbuffer DirtyRectangle set to: " + _dirtyRectangle);
        }
    }
    
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
        if (tiles?.Count != 3)
            Engine.Logger.LogInformation("Drawing single tile: {Tile}", tiles[0]);

        foreach (var tile in tiles)
        {
            if (!tile.Visible)
                continue;

            //if (!DirtyRectangle.IsEmpty && !DirtyRectangle.IntersectsWith(tile.DrawLocation))
            //    continue;

            DrawTileFrame(tile);

            if (tile.EnableFog)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), FogPaint);

            if (!tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), GridPaint);
        }
    }

    protected void AddToDirtyRectangle(Rectangle area)
    {
        //Engine.Logger.LogTrace($"DirtyRectangle: {DirtyRectangle}");
        //Engine.Logger.LogTrace($"Adding to DirtyRectangle: {area}");

        if (area.IsEmpty) return;

        DirtyRectangle = DirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(DirtyRectangle, area);

        Engine.Logger.LogTrace("New DirtyRectangle: " + DirtyRectangle);
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

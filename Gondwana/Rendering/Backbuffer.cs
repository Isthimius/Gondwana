using SkiaSharp;
using Gondwana.Grid;
using Gondwana.Drawing;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace Gondwana.Rendering;

public class Backbuffer
{
    private readonly SKSurface _surface;
    private readonly Rectangle _range;

    private SKPaint _fogPaint = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    private SKPaint _gridPaint = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    private GridPointMatrixes _source;
    private Rectangle _dirtyRectangle = Rectangle.Empty;

    public Backbuffer(int width, int height)
    {
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _range = new Rectangle(0, 0, width, height);
    }

    public SKCanvas Canvas => _surface.Canvas;

    public GridPointMatrixes DrawSource
    {
        get => _source;
        set
        {
            if (_source != null)
                _source.Disposing -= SourceDisposing;

            _source = value;

            if (_source != null)
            {
                _source.Disposing += SourceDisposing;
                _source.RefreshNeeded = MatrixesRefreshType.All;
            }
        }
    }

    public Rectangle DirtyRectangle
    {
        get => _dirtyRectangle;
        internal set => _dirtyRectangle = value;
    }

    public int Width => _range.Width;
    public int Height => _range.Height;

    public SKImage Snapshot() => _surface.Snapshot();

    public SolidBrush FogBrush
    {
        get => throw new NotSupportedException("Use SKPaint-based customization.");
        set => _fogPaint = new SKPaint { Color = value.Color.ToSKColor(), IsAntialias = true };
    }

    public Pen GridPen
    {
        get => throw new NotSupportedException("Use SKPaint-based customization.");
        set => _gridPaint = new SKPaint
        {
            Color = value.Color.ToSKColor(),
            IsStroke = true,
            StrokeWidth = value.Width
        };
    }

    public void SaveToFile(string file)
    {
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(file);
        data.SaveTo(stream);
    }

    public void Erase() => Erase(_range);

    public void Erase(Rectangle pxlRange)
    {
        var intersect = Rectangle.Intersect(pxlRange, _range);
        if (intersect.IsEmpty) return;

        Canvas.Save();
        Canvas.ClipRect(intersect.ToSKRect());
        Canvas.Clear(SKColors.Black);
        Canvas.Restore();

        AddToDirtyRectangle(intersect);
    }

    public void Erase(IList<Rectangle> areas)
    {
        foreach (var rect in areas)
            Erase(rect);
    }

    public void DrawTiles(IList<Tile> tiles)
    {
        foreach (var tile in tiles)
        {
            if (!tile.Visible)
                continue;

            var destRect = tile.DrawLocation.ToSKRect();
            var frame = tile.CurrentFrame;
            var bmp = frame.GetSkiaBitmap();
            var mask = frame.GetSkiaBitmap();

            if (bmp != null)
            {
                var skBitmap = tile.CurrentFrame.GetSkiaBitmap();
                if (skBitmap != null)
                    Canvas.DrawBitmap(skBitmap, destRect);
            }

            if (tile.EnableFog)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), _fogPaint);

            if (tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), _gridPaint);
        }
    }

    public static SKBitmap CombineBitmapWithMask(SKBitmap color, SKBitmap? mask)
    {
        if (color == null)
            throw new ArgumentNullException(nameof(color));

        int width = color.Width;
        int height = color.Height;

        var output = new SKBitmap(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var colorPixel = color.GetPixel(x, y);
                byte alpha = 255;

                if (mask != null)
                {
                    if (x < mask.Width && y < mask.Height)
                    {
                        var maskPixel = mask.GetPixel(x, y);
                        alpha = (byte)(255 - maskPixel.Red); // assumes white (255) = transparent
                    }
                }

                var finalColor = new SKColor(colorPixel.Red, colorPixel.Green, colorPixel.Blue, alpha);
                output.SetPixel(x, y, finalColor);
            }
        }

        return output;
    }

    public void Dispose()
    {
        _surface.Dispose();
        _fogPaint.Dispose();
        _gridPaint.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SourceDisposing(GridPointMatrixesDisposingEventArgs e) => _source = null;

    private void AddToDirtyRectangle(Rectangle area)
    {
        if (area.IsEmpty) return;

        _dirtyRectangle = _dirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(_dirtyRectangle, area);
    }
}

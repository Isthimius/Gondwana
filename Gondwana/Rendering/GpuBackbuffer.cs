using SkiaSharp;
using Gondwana.Grid;
using Gondwana.Drawing;
using System.Drawing;

namespace Gondwana.Rendering;

public class GpuBackbuffer : IBackbuffer
{
    private readonly GRContext _grContext;
    private GRBackendRenderTarget _renderTarget;
    private SKSurface _surface;
    private readonly Rectangle _range;

    private GridPointMatrixes _source;
    private Rectangle _dirtyRectangle = Rectangle.Empty;

    public GpuBackbuffer(int width, int height)
    {
        _range = new Rectangle(0, 0, width, height);

        // Create GPU context
        _grContext = GRContext.CreateGl();

        // Create a render target for GPU drawing
        var glInfo = new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat());
        _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, glInfo);

        // Create GPU-backed surface
        _surface = SKSurface.Create(
            _grContext,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888);

        if (_surface == null)
            throw new InvalidOperationException("Could not create GPU surface.");
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
        set => _dirtyRectangle = value;
    }

    public int Width => _range.Width;
    public int Height => _range.Height;
    public SKPaint FogPaint { get; set; } = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    public SKPaint GridPaint { get; set; } = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    public SKImage Snapshot() => _surface.Snapshot();

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
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), FogPaint);

            if (tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), GridPaint);
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

    public void SaveToFile(string file)
    {
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(file);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        FogPaint?.Dispose();
        GridPaint?.Dispose();
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

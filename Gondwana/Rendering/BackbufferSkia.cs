using SkiaSharp;
using Gondwana.Grid;
using Gondwana.Drawing;
using System.Drawing;
using static SkiaExtensions;

namespace Gondwana.Rendering;

public class SkiaBackbuffer : IBackbufferSkia
{
    private readonly SKSurface _surface;
    private readonly SKBitmap _bitmap;
    private readonly Rectangle _range;

    private SKPaint _fogPaint = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    private SKPaint _gridPaint = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    private GridPointMatrixes _source;
    private Rectangle _dirtyRectangle = Rectangle.Empty;

    public SkiaBackbuffer(int width, int height)
    {
        _bitmap = new SKBitmap(width, height);
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
            if (!tile.Visible) continue;

            // TODO: Load tile.CurrentFrame.Tilesheet.SKBitmap source
            // and draw with masking if needed
            // Canvas.DrawBitmap(...)

            if (tile.EnableFog)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), _fogPaint);

            if (tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), _gridPaint);
        }
    }

    public void Dispose()
    {
        _surface.Dispose();
        _bitmap.Dispose();
        _fogPaint.Dispose();
        _gridPaint.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SourceDisposing(GridPointMatrixesDisposingEventArgs e) => _source = null;

    private void AddToDirtyRectangle(Rectangle area)
    {
        if (area.IsEmpty) return;

        _dirtyRectangle = _dirtyRectangle.IsEmpty ? area : Rectangle.Union(_dirtyRectangle, area);
    }
}

using SkiaSharp;
using Gondwana.Grid;
using Gondwana.Drawing;
using System.Drawing;
using Gondwana.Skia;

namespace Gondwana.Rendering;

public abstract class BackbufferBase : IDisposable
{
    internal static List<BackbufferBase> _allBackbuffers { get; } = new();

    internal static void _resetAllDirtyRectangles()
    {
        foreach (var backbuffer in _allBackbuffers)
        {
            backbuffer.DirtyRectangle = Rectangle.Empty;
        }
    }

    protected readonly Rectangle _range;

    protected BackbufferBase(int width, int height, GridPointMatrixes? drawSource = null)
    { 
        _range = new Rectangle(0, 0, width, height);

        if (drawSource != null)
            Bind(drawSource);

        _allBackbuffers.Add(this);
    }

    public abstract SKCanvas Canvas { get; }
    public abstract SKImage Snapshot();

    public SKPaint FogPaint { get; set; } = new() { Color = new SKColor(0, 0, 0, 128), IsAntialias = true };
    public SKPaint GridPaint { get; set; } = new() { Color = SKColors.White, IsStroke = true, StrokeWidth = 1 };

    public int Width => _range.Width;
    public int Height => _range.Height;
    public Rectangle DirtyRectangle { get; set; } = Rectangle.Empty;
    public SKColor ClearColor { get; set; } = SKColors.Black;

    public GridPointMatrixes? DrawSource { get; private set; } = null;

    public void Bind(GridPointMatrixes drawSource)
    {
        if (DrawSource != null)
            DrawSource.Disposing -= OnSourceDisposing;

        DrawSource = drawSource;

        if (DrawSource != null)
        {
            DrawSource.Disposing += OnSourceDisposing;
            DrawSource.RefreshNeeded = MatrixesRefreshType.All;
        }
    }

    internal void DrawTiles(IList<Tile> tiles)
    {
        foreach (var tile in tiles)
        {
            if (!tile.Visible) continue;
            if (!DirtyRectangle.IntersectsWith(tile.DrawLocation)) continue;

            var bitmap = tile.CurrentFrame.SkBitmap;
            if (bitmap != null)
                Canvas.DrawBitmap(bitmap, tile.DrawLocation.ToSKRect());

            if (tile.EnableFog)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), FogPaint);

            if (tile.ParentGrid.ShowGridLines && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, tile.OutlinePoints.ToSKPoints(), GridPaint);
        }
    }

    public void Erase() => Erase(_range);

    public void Erase(Rectangle pxlRange)
    {
        var intersect = Rectangle.Intersect(pxlRange, _range);
        if (intersect.IsEmpty) return;

        Canvas.Save();
        Canvas.ClipRect(intersect.ToSKRect());
        Canvas.Clear(ClearColor);
        Canvas.Restore();

        AddToDirtyRectangle(intersect);
    }

    public void Erase(IList<Rectangle> areas)
    {
        foreach (var rect in areas)
            Erase(rect);
    }

    protected void AddToDirtyRectangle(Rectangle area)
    {
        if (area.IsEmpty) return;

        DirtyRectangle = DirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(DirtyRectangle, area);
    }

    protected void OnSourceDisposing(GridPointMatrixesDisposingEventArgs e) => DrawSource = null;

    public virtual byte[] EncodeImage(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        using var image = Snapshot();
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    public virtual void Dispose()
    {
        FogPaint.Dispose();
        GridPaint.Dispose();

        _allBackbuffers.Remove(this);
    }
}

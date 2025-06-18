using Gondwana.Common.Enums;
using Gondwana.Grid;
using Gondwana.EventArgs;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public class BackbufferSkia : IBackbuffer, IDisposable
{
    private SKSurface surface;
    private SKCanvas canvas;
    private SKBitmap bitmap;
    private SKPaint fogPaint;
    private SKPaint gridPaint;
    private Rectangle dirtyRect;
    private GridPointMatrixes? source;

    public int Width { get; }
    public int Height { get; }

    public BackbufferSkia(int width, int height)
    {
        Width = width;
        Height = height;

        var info = new SKImageInfo(width, height);
        surface = SKSurface.Create(info);
        canvas = surface.Canvas;

        fogPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 128),
            Style = SKPaintStyle.Fill
        };

        gridPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        dirtyRect = Rectangle.Empty;
    }

    public Rectangle DirtyRectangle
    {
        get => dirtyRect;
        set => dirtyRect = value;
    }

    public GridPointMatrixes? DrawSource
    {
        get => source;
        set
        {
            if (source != null)
                source.Disposing -= Source_Disposing;

            source = value;
            if (source != null)
            {
                source.Disposing += Source_Disposing;
                source.RefreshNeeded = MatrixesRefreshType.All;
            }
        }
    }

    public SKCanvas Canvas => canvas;

    public Graphics DC => throw new NotImplementedException();

    public SolidBrush FogBrush { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public Pen GridPen { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public void SaveToFile(string path)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    public void Erase()
    {
        canvas.Clear(SKColors.Black);
        dirtyRect = new Rectangle(0, 0, Width, Height);
    }

    public void DrawFog(Tile tile)
    {
        var path = new SKPath();
        foreach (var pt in tile.OutlinePoints)
        {
            path.LineTo(pt.X, pt.Y);
        }
        path.Close();
        canvas.DrawPath(path, fogPaint);
    }

    public void DrawGridLines(Tile tile)
    {
        var path = new SKPath();
        foreach (var pt in tile.OutlinePoints)
        {
            path.LineTo(pt.X, pt.Y);
        }
        path.Close();
        canvas.DrawPath(path, gridPaint);
    }

    public void Dispose()
    {
        fogPaint.Dispose();
        gridPaint.Dispose();
        surface.Dispose();
    }

    private void Source_Disposing(GridPointMatrixesDisposingEventArgs e)
    {
        source = null;
    }

    public void Erase(Rectangle pxlRange)
    {
        throw new NotImplementedException();
    }

    public void Erase(IList<Rectangle> areas)
    {
        throw new NotImplementedException();
    }

    public void DrawTiles(IList<Tile> tiles)
    {
        throw new NotImplementedException();
    }
}

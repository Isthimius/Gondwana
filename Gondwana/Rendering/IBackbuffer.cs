using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Grid;
using SkiaSharp;

namespace Gondwana.Rendering;

public interface IBackbuffer : IDisposable
{
    int Width { get; }
    int Height { get; }

    SKPaint FogPaint { get; set; }
    SKPaint GridPaint { get; set; }
    SKCanvas Canvas { get; }
    GridPointMatrixes DrawSource { get; set; }
    Rectangle DirtyRectangle { get; set; }

    void DrawTiles(IList<Tile> tiles);
    SKImage Snapshot();
    void Erase();
    void Erase(Rectangle pxlRange);
    void Erase(IList<Rectangle> areas);
    void SaveToFile(string file);
}

using Gondwana.Drawing;
using Gondwana.Grid;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public interface IBackbufferSkia : IDisposable
{
    SKCanvas Canvas { get; }
    SolidBrush FogBrush { get; set; }
    Pen GridPen { get; set; }

    GridPointMatrixes DrawSource { get; }
    int Height { get; }
    int Width { get; }
    Rectangle DirtyRectangle { get; }

    void SaveToFile(string file);
    void Erase();
    void Erase(Rectangle pxlRange);
    void Erase(IList<Rectangle> areas);
    void DrawTiles(IList<Tile> tiles);
}
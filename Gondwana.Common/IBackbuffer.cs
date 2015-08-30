using Gondwana.Common.Grid;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Gondwana.Common
{
    public interface IBackbuffer : IDisposable
    {
        Graphics DC { get; }
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
}
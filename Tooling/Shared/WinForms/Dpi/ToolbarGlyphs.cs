using System.Drawing.Drawing2D;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.WinForms;

public static partial class ToolbarGlyphs
{
    public enum DefinitionCommand { Add, Import, Duplicate, Remove, Rename }

    public static Bitmap Studio(string kind, int pixels, ToolingPalette palette)
    {
        var bitmap = new Bitmap(pixels, pixels);
        using (var g = Graphics.FromImage(bitmap))
        using (var pen = new Pen(palette.Foreground, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            ToolbarGlyphs.Prepare(g, pixels, 24);
            void Lines(params PointF[] points) => g.DrawLines(pen, points);
            void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, x1, y1, x2, y2);
            switch (kind)
            {
                case "new":
                    Lines(new(6, 3), new(15, 3), new(19, 7), new(19, 13));
                    Lines(new(15, 3), new(15, 7), new(19, 7));
                    Lines(new(6, 3), new(6, 21), new(13, 21));
                    Line(17, 16, 17, 22);
                    Line(14, 19, 20, 19);
                    break;
                case "open":
                case "folder":
                    Lines(new(2, 8), new(2, 5), new(9, 5), new(11, 8), new(21, 8), new(21, 19), new(3, 19), new(2, 8));
                    if (kind == "open")
                    {
                        Lines(new(5, 12), new(22, 12), new(19, 21), new(3, 21), new(5, 12));
                    }
                    break;
                case "save":
                case "save-all":
                    if (kind == "save-all")
                    {
                        Lines(new(3, 6), new(3, 3), new(17, 3), new(20, 6), new(20, 17));
                        // Leave room for the front sheet stroke and antialiasing at the edge.
                        g.TranslateTransform(1, 1);
                    }
                    Lines(new(4, 3), new(16, 3), new(20, 7), new(20, 21), new(4, 21), new(4, 3));
                    Lines(new(8, 3), new(8, 10), new(16, 10), new(16, 3));
                    Lines(new(8, 21), new(8, 14), new(16, 14), new(16, 21));
                    break;
                case "refresh":
                    g.DrawArc(pen, 4, 4, 16, 16, 35, 285);
                    Lines(new(17, 3), new(21, 5), new(20, 10));
                    break;
                case "preview":
                    Lines(new(8, 4), new(20, 12), new(8, 20), new(8, 4));
                    break;
                case "validate":
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    Lines(new(7, 12), new(11, 16), new(17, 9));
                    break;
            }
        }
        return bitmap;
    }

    public static Bitmap Definition(DefinitionCommand command, int pixels, ToolingPalette palette)
    {
        var image = new Bitmap(pixels, pixels);
        using (var graphics = Graphics.FromImage(image))
        using (var pen = new Pen(palette.Foreground, 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        using (var accent = new Pen(palette.Accent, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            ToolbarGlyphs.Prepare(graphics, pixels, 48);
            switch (command)
            {
                case DefinitionCommand.Add:
                    graphics.DrawLines(pen, new PointF[] { new(10, 7), new(27, 7), new(36, 16), new(36, 27) });
                    graphics.DrawLines(pen, new PointF[] { new(10, 7), new(10, 41), new(25, 41) });
                    graphics.DrawLines(pen, new PointF[] { new(27, 7), new(27, 16), new(36, 16) });
                    graphics.DrawLine(accent, 34, 30, 34, 44);
                    graphics.DrawLine(accent, 27, 37, 41, 37);
                    break;
                case DefinitionCommand.Import:
                    graphics.DrawLines(pen, new PointF[] { new(9, 31), new(9, 41), new(39, 41), new(39, 31) });
                    graphics.DrawLine(accent, 24, 6, 24, 31);
                    graphics.DrawLines(accent, new PointF[] { new(14, 22), new(24, 32), new(34, 22) });
                    break;
                case DefinitionCommand.Duplicate:
                    graphics.DrawRectangle(pen, 8, 7, 25, 29);
                    graphics.DrawRectangle(pen, 16, 15, 25, 29);
                    break;
                case DefinitionCommand.Remove:
                    graphics.DrawLine(pen, 10, 13, 38, 13);
                    graphics.DrawLine(pen, 18, 8, 30, 8);
                    graphics.DrawLines(pen, new PointF[] { new(13, 18), new(16, 41), new(32, 41), new(35, 18) });
                    graphics.DrawLine(pen, 21, 23, 21, 35);
                    graphics.DrawLine(pen, 27, 23, 27, 35);
                    break;
                case DefinitionCommand.Rename:
                    graphics.DrawLines(pen, new PointF[] { new(9, 8), new(30, 8), new(37, 15), new(37, 23) });
                    graphics.DrawLines(pen, new PointF[] { new(9, 8), new(9, 41), new(19, 41) });
                    graphics.DrawLines(pen, new PointF[] { new(30, 8), new(30, 15), new(37, 15) });
                    graphics.DrawLines(accent, new PointF[] { new(22, 35), new(36, 21), new(42, 27), new(28, 41), new(22, 41), new(22, 35) });
                    graphics.DrawLine(accent, 33, 24, 39, 30);
                    break;
            }
        }

        return image;
    }
}

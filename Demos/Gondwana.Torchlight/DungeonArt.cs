using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;
using SkiaSharp;

namespace Gondwana.Torchlight;

/// <summary>Small procedural pixel-art atlas; the demo has no external asset dependency.</summary>
internal static class DungeonArt
{
    internal const int TileSize = 32;
    internal const int Floor = 0;
    internal const int FloorCracked = 1;
    internal const int Wall = 2;
    internal const int Pillar = 3;
    internal const int Sconce = 4;
    internal const int HeroUp = 5;
    internal const int HeroDown = 6;
    internal const int HeroLeft = 7;
    internal const int HeroRight = 8;
    internal const int Rune = 9;
    internal const int Stones = 10;
    private const int FrameCount = 11;
    private static Tilesheet _sheet = null!;

    internal static void Load(TilesheetRegistry registry)
    {
        _sheet = registry.LoadFromBitmap("torchlight-generated-art", CreateBitmap());
        _sheet.DefaultRegion.TileSize = new Size(TileSize, TileSize);
    }

    internal static Frame Get(int id) => _sheet[id, 0];

    private static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(TileSize * FrameCount, TileSize,
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        for (int id = Floor; id <= FloorCracked; id++)
            DrawFloor(canvas, id, id == FloorCracked);
        DrawWall(canvas, Wall, false);
        DrawWall(canvas, Pillar, true);
        DrawSconce(canvas, Sconce);
        for (int id = HeroUp; id <= HeroRight; id++)
            DrawHero(canvas, id);
        DrawRune(canvas, Rune);
        DrawStones(canvas, Stones);

        canvas.Flush();
        return bitmap;
    }

    private static SKPaint Paint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = false,
        Style = SKPaintStyle.Fill
    };

    private static int X(int frame) => frame * TileSize;

    private static void DrawFloor(SKCanvas canvas, int frame, bool cracked)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(69, 73, 84));
        canvas.DrawRect(x, 0, 32, 32, paint);
        paint.Color = new SKColor(57, 60, 72);
        canvas.DrawRect(x, 15, 32, 2, paint);
        canvas.DrawRect(x + 15, 0, 2, 15, paint);
        canvas.DrawRect(x + 7, 17, 2, 15, paint);
        paint.Color = new SKColor(85, 88, 100);
        canvas.DrawRect(x + 2, 3, 9, 2, paint);
        canvas.DrawRect(x + 19, 20, 8, 2, paint);
        if (cracked)
        {
            paint.Color = new SKColor(36, 40, 51);
            canvas.DrawLine(x + 12, 5, x + 22, 12, paint);
            canvas.DrawLine(x + 22, 12, x + 17, 20, paint);
        }
    }

    private static void DrawWall(SKCanvas canvas, int frame, bool pillar)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(31, 35, 47));
        canvas.DrawRect(x, 0, 32, 32, paint);
        paint.Color = new SKColor(90, 94, 105);
        canvas.DrawRect(x + 2, 2, 28, 5, paint);
        paint.Color = new SKColor(59, 63, 76);
        canvas.DrawRect(x + 2, 9, 28, 18, paint);
        paint.Color = new SKColor(44, 48, 60);
        canvas.DrawRect(x + 16, 9, 2, 18, paint);
        canvas.DrawRect(x + 2, 18, 28, 2, paint);
        if (pillar)
        {
            paint.Color = new SKColor(129, 125, 110);
            canvas.DrawRect(x + 4, 4, 24, 4, paint);
            canvas.DrawRect(x + 7, 8, 18, 18, paint);
            paint.Color = new SKColor(61, 59, 68);
            canvas.DrawRect(x + 11, 10, 10, 14, paint);
        }
    }

    private static void DrawSconce(SKCanvas canvas, int frame)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(85, 78, 68));
        canvas.DrawRect(x + 12, 18, 8, 12, paint);
        paint.Color = new SKColor(177, 135, 62);
        canvas.DrawRect(x + 9, 15, 14, 5, paint);
        paint.Color = new SKColor(245, 143, 42);
        canvas.DrawRect(x + 13, 8, 7, 8, paint);
        paint.Color = new SKColor(255, 213, 84);
        canvas.DrawRect(x + 15, 4, 3, 10, paint);
    }

    private static void DrawHero(SKCanvas canvas, int frame)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(20, 23, 29));
        canvas.DrawRect(x + 8, 25, 16, 5, paint);
        paint.Color = new SKColor(37, 107, 102);
        canvas.DrawRect(x + 7, 12, 18, 15, paint);
        paint.Color = new SKColor(70, 150, 138);
        canvas.DrawRect(x + 10, 14, 12, 9, paint);
        paint.Color = new SKColor(217, 172, 127);
        canvas.DrawRect(x + 10, 5, 12, 10, paint);
        paint.Color = new SKColor(73, 49, 45);
        canvas.DrawRect(x + 9, 3, 14, 5, paint);
        canvas.DrawRect(x + 9, 7, 3, 5, paint);
        if (frame != HeroUp)
        {
            paint.Color = new SKColor(28, 39, 45);
            if (frame == HeroLeft)
                canvas.DrawRect(x + 11, 10, 3, 2, paint);
            else if (frame == HeroRight)
                canvas.DrawRect(x + 19, 10, 3, 2, paint);
            else
            {
                canvas.DrawRect(x + 12, 10, 3, 2, paint);
                canvas.DrawRect(x + 19, 10, 3, 2, paint);
            }
        }
        paint.Color = new SKColor(121, 69, 45);
        canvas.DrawRect(x + 25, 15, 3, 12, paint);
        paint.Color = new SKColor(255, 194, 72);
        canvas.DrawRect(x + 24, 11, 5, 6, paint);
        paint.Color = new SKColor(255, 225, 114);
        canvas.DrawRect(x + 26, 9, 2, 5, paint);
    }

    private static void DrawRune(SKCanvas canvas, int frame)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(54, 136, 143));
        canvas.DrawRect(x + 13, 5, 6, 22, paint);
        canvas.DrawRect(x + 6, 12, 20, 5, paint);
        paint.Color = new SKColor(110, 202, 191);
        canvas.DrawRect(x + 15, 8, 2, 16, paint);
    }

    private static void DrawStones(SKCanvas canvas, int frame)
    {
        int x = X(frame);
        using var paint = Paint(new SKColor(105, 101, 102));
        canvas.DrawRect(x + 3, 18, 12, 8, paint);
        canvas.DrawRect(x + 17, 10, 10, 9, paint);
        paint.Color = new SKColor(143, 132, 125);
        canvas.DrawRect(x + 4, 18, 8, 2, paint);
        canvas.DrawRect(x + 18, 10, 6, 2, paint);
    }
}

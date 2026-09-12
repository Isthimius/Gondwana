using SkiaSharp;

namespace Gondwana.Demos.Platformer;

internal static class PlatformerArt
{
    internal const int TileSize = 32;

    internal const int GrassFrame = 0;
    internal const int StoneFrame = 1;
    internal const int SpikeFrame = 2;
    internal const int RelicFrame = 3;
    internal const int GoalFrame = 4;
    internal const int PlayerRightFrame = 5;
    internal const int PlayerLeftFrame = 6;
    internal const int CloudFrame = 7;

    internal const int EnemyWalkFrame = 8;
    internal const int EnemyFlattenedFrame = 10;
    internal const int EnemyFadeFrames = 16;

    private const int FrameCount = EnemyFlattenedFrame + EnemyFadeFrames;

    internal static SKBitmap CreateTilesheetBitmap()
    {
        var bitmap = new SKBitmap(
            TileSize * FrameCount,
            TileSize,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawGrass(canvas, FrameLeft(GrassFrame));
        DrawStone(canvas, FrameLeft(StoneFrame));
        DrawSpikes(canvas, FrameLeft(SpikeFrame));
        DrawRelic(canvas, FrameLeft(RelicFrame));
        DrawGoal(canvas, FrameLeft(GoalFrame));
        DrawPlayer(canvas, FrameLeft(PlayerRightFrame), facingLeft: false);
        DrawPlayer(canvas, FrameLeft(PlayerLeftFrame), facingLeft: true);
        DrawCloud(canvas, FrameLeft(CloudFrame));

        DrawEnemy(canvas, FrameLeft(EnemyWalkFrame), alternateFeet: false);
        DrawEnemy(canvas, FrameLeft(EnemyWalkFrame + 1), alternateFeet: true);
        for (var i = 0; i < EnemyFadeFrames; i++)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(FrameLeft(EnemyFlattenedFrame + i), 0,
                FrameLeft(EnemyFlattenedFrame + i) + TileSize, TileSize));
            canvas.Translate(FrameLeft(EnemyFlattenedFrame + i), 24);
            canvas.Scale(1f, 0.25f);
            using var fade = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)(255 * (EnemyFadeFrames - 1 - i) / (EnemyFadeFrames - 1)))
            };
            canvas.SaveLayer(fade);
            DrawEnemy(canvas, 0, alternateFeet: false);
            canvas.Restore();
            canvas.Restore();
        }

        canvas.Flush();
        return bitmap;
    }

    private static int FrameLeft(int frame) => frame * TileSize;

    private static void DrawGrass(SKCanvas canvas, int x)
    {
        using var paint = PixelPaint(new SKColor(126, 79, 43));
        canvas.DrawRect(x, 0, TileSize, TileSize, paint);

        paint.Color = new SKColor(73, 143, 72);
        canvas.DrawRect(x, 0, TileSize, 8, paint);

        paint.Color = new SKColor(111, 190, 92);
        canvas.DrawRect(x, 0, TileSize, 3, paint);

        paint.Color = new SKColor(95, 58, 35);
        canvas.DrawRect(x + 5, 13, 5, 4, paint);
        canvas.DrawRect(x + 20, 21, 7, 4, paint);
        canvas.DrawRect(x + 12, 28, 4, 3, paint);
    }

    private static void DrawStone(SKCanvas canvas, int x)
    {
        using var paint = PixelPaint(new SKColor(89, 101, 115));
        canvas.DrawRect(x, 0, TileSize, TileSize, paint);

        paint.Color = new SKColor(153, 167, 177);
        canvas.DrawRect(x, 0, TileSize, 4, paint);

        paint.Color = new SKColor(62, 72, 84);
        canvas.DrawRect(x, 14, TileSize, 2, paint);
        canvas.DrawRect(x, 29, TileSize, 3, paint);
        canvas.DrawRect(x + 14, 4, 2, 10, paint);
        canvas.DrawRect(x + 7, 16, 2, 13, paint);
        canvas.DrawRect(x + 25, 16, 2, 13, paint);
    }

    private static void DrawSpikes(SKCanvas canvas, int x)
    {
        using var fill = PixelPaint(new SKColor(224, 229, 233));
        using var edge = PixelPaint(new SKColor(95, 109, 120));

        for (var i = 0; i < 4; i++)
        {
            var left = x + i * 8;
            using var path = new SKPath();
            path.MoveTo(left, 31);
            path.LineTo(left + 4, 8);
            path.LineTo(left + 8, 31);
            path.Close();
            canvas.DrawPath(path, fill);
            canvas.DrawLine(left + 4, 9, left + 8, 31, edge);
        }

        canvas.DrawRect(x, 29, TileSize, 3, edge);
    }

    private static void DrawRelic(SKCanvas canvas, int x)
    {
        using var glow = PixelPaint(new SKColor(255, 238, 111, 100));
        canvas.DrawCircle(x + 16, 16, 13, glow);

        using var fill = PixelPaint(new SKColor(255, 200, 57));
        using var path = new SKPath();
        path.MoveTo(x + 16, 4);
        path.LineTo(x + 26, 13);
        path.LineTo(x + 21, 27);
        path.LineTo(x + 11, 27);
        path.LineTo(x + 6, 13);
        path.Close();
        canvas.DrawPath(path, fill);

        fill.Color = new SKColor(255, 244, 164);
        canvas.DrawRect(x + 13, 8, 6, 13, fill);
    }

    private static void DrawGoal(SKCanvas canvas, int x)
    {
        using var paint = PixelPaint(new SKColor(236, 223, 186));
        canvas.DrawRect(x + 6, 2, 3, 30, paint);

        paint.Color = new SKColor(219, 66, 69);
        canvas.DrawRect(x + 9, 4, 19, 13, paint);
        paint.Color = new SKColor(255, 245, 224);
        canvas.DrawRect(x + 9, 9, 19, 4, paint);

        paint.Color = new SKColor(92, 61, 45);
        canvas.DrawRect(x + 2, 29, 12, 3, paint);
    }

    private static void DrawPlayer(SKCanvas canvas, int x, bool facingLeft)
    {
        using var paint = PixelPaint(new SKColor(51, 63, 72));
        canvas.DrawRect(x + 8, 25, 7, 6, paint);
        canvas.DrawRect(x + 19, 25, 7, 6, paint);

        paint.Color = new SKColor(37, 102, 162);
        canvas.DrawRect(x + 8, 14, 18, 13, paint);

        paint.Color = new SKColor(244, 190, 117);
        canvas.DrawRect(x + 9, 5, 16, 12, paint);

        paint.Color = new SKColor(123, 70, 45);
        canvas.DrawRect(x + 7, 3, 19, 5, paint);
        canvas.DrawRect(x + (facingLeft ? 4 : 24), 7, 5, 3, paint);

        paint.Color = SKColors.White;
        canvas.DrawRect(x + (facingLeft ? 11 : 20), 9, 3, 3, paint);
        paint.Color = new SKColor(35, 42, 49);
        canvas.DrawRect(x + (facingLeft ? 11 : 22), 10, 2, 2, paint);

        paint.Color = new SKColor(247, 221, 88);
        canvas.DrawRect(x + 5, 16, 4, 9, paint);
        canvas.DrawRect(x + 25, 16, 4, 9, paint);
    }

    private static void DrawEnemy(SKCanvas canvas, int x, bool alternateFeet)
    {
        using var paint = PixelPaint(new SKColor(62, 37, 28));
        canvas.DrawOval(new SKRect(x + 2, alternateFeet ? 25 : 28, x + 14, alternateFeet ? 29 : 32), paint);
        canvas.DrawOval(new SKRect(x + 18, alternateFeet ? 28 : 25, x + 30, alternateFeet ? 32 : 29), paint);
        paint.Color = new SKColor(229, 196, 139);
        canvas.DrawOval(new SKRect(x + 9, 17, x + 23, 29), paint);
        paint.Color = new SKColor(100, 48, 30);
        canvas.DrawOval(new SKRect(x + 2, 3, x + 30, 24), paint);
        paint.Color = new SKColor(167, 85, 44);
        canvas.DrawOval(new SKRect(x + 4, 4, x + 28, 21), paint);
        paint.Color = new SKColor(217, 152, 83);
        canvas.DrawRect(x + 8, 7, 5, 3, paint);
        canvas.DrawRect(x + 21, 8, 4, 3, paint);
        paint.Color = SKColors.White;
        canvas.DrawRect(x + 8, 12, 6, 8, paint);
        canvas.DrawRect(x + 18, 12, 6, 8, paint);
        paint.Color = new SKColor(36, 24, 23);
        canvas.DrawRect(x + 11, 14, 3, 5, paint);
        canvas.DrawRect(x + 18, 14, 3, 5, paint);
        paint.StrokeWidth = 3;
        canvas.DrawLine(x + 7, 10, x + 14, 13, paint);
        canvas.DrawLine(x + 18, 13, x + 25, 10, paint);
        canvas.DrawRect(x + 12, 23, 8, 2, paint);
    }

    private static void DrawCloud(SKCanvas canvas, int x)
    {
        using var paint = PixelPaint(new SKColor(255, 255, 255, 210));
        canvas.DrawCircle(x + 10, 18, 7, paint);
        canvas.DrawCircle(x + 17, 13, 9, paint);
        canvas.DrawCircle(x + 24, 19, 6, paint);
        canvas.DrawRect(x + 7, 18, 21, 8, paint);
    }

    private static SKPaint PixelPaint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        StrokeWidth = 1
    };
}

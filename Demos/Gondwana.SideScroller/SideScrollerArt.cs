using SkiaSharp;

namespace Gondwana.Demos.SideScroller;

internal static class SideScrollerArt
{
    internal const int FrameSize = 64;
    internal const int FarStars = 0;
    internal const int Nebula = 1;
    internal const int NearStars = 2;
    internal const int Player = 3;
    internal const int Enemy = 4;
    internal const int PlayerShot = 5;
    internal const int EnemyShot = 6;

    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(FrameSize * 7, FrameSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        DrawStars(canvas, 0, new SKColor(110, 145, 205, 170), 1f);
        DrawNebula(canvas, FrameSize);
        DrawStars(canvas, FrameSize * 2, new SKColor(235, 250, 255, 240), 2f);
        DrawShip(canvas, FrameSize * 3, new SKColor(55, 220, 255), false);
        DrawShip(canvas, FrameSize * 4, new SKColor(255, 92, 110), true);
        DrawShot(canvas, FrameSize * 5, new SKColor(90, 245, 255));
        DrawShot(canvas, FrameSize * 6, new SKColor(255, 90, 120));
        canvas.Flush();
        return bitmap;
    }

    private static void DrawStars(SKCanvas c, int x, SKColor color, float radius)
    {
        using var p = new SKPaint { IsAntialias = true, Color = color };
        c.DrawCircle(x + 8, 12, radius, p); c.DrawCircle(x + 40, 19, radius * .7f, p);
        c.DrawCircle(x + 23, 48, radius * .85f, p); c.DrawCircle(x + 56, 39, radius * .6f, p);
    }

    private static void DrawNebula(SKCanvas c, int x)
    {
        using var p = new SKPaint { IsAntialias = true, Color = new SKColor(75, 38, 145, 60) };
        c.DrawOval(new SKRect(x + 3, 15, x + 61, 51), p);
        p.Color = new SKColor(20, 150, 185, 45); c.DrawOval(new SKRect(x + 17, 5, x + 55, 59), p);
    }

    private static void DrawShip(SKCanvas c, int x, SKColor color, bool facesLeft)
    {
        float direction = facesLeft ? -1f : 1f;
        using var body = new SKPaint { IsAntialias = true, Color = color };
        var path = new SKPath();
        path.MoveTo(x + 32 + direction * 27, 32); path.LineTo(x + 32 - direction * 20, 13);
        path.LineTo(x + 32 - direction * 11, 32); path.LineTo(x + 32 - direction * 20, 51);
        path.Close(); c.DrawPath(path, body);
        body.Color = new SKColor(225, 250, 255); c.DrawCircle(x + 32 + direction * 5, 32, 5, body);
        body.Color = new SKColor(255, 185, 55, 210);
        c.DrawRect(facesLeft ? x + 53 : x + 4, 27, 8, 10, body);
    }

    private static void DrawShot(SKCanvas c, int x, SKColor color)
    {
        using var glow = new SKPaint { IsAntialias = true, Color = color.WithAlpha(85) };
        c.DrawRoundRect(new SKRect(x + 5, 24, x + 59, 40), 8, 8, glow);
        glow.Color = color; c.DrawRoundRect(new SKRect(x + 8, 29, x + 56, 35), 3, 3, glow);
    }
}

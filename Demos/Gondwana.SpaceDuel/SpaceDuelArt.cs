using System.Reflection;
using SkiaSharp;

namespace Gondwana.Demos.SpaceDuel;

internal static class SpaceDuelArt
{
    internal const int ShipFrameSize = 512;
    internal const int EffectsFrameSize = 64;

    internal const int FarStarFrame = 0;
    internal const int NearStarFrame = 1;
    internal const int LaserFrame = 2;

    private const string ShipResourceName =
        "Gondwana.Demos.SpaceDuel.Assets.ships.png";

    internal static SKBitmap LoadShipBitmap()
    {
        Assembly assembly = typeof(SpaceDuelArt).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(ShipResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ship resource '{ShipResourceName}' was not found.");

        return SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("The embedded ship sprite sheet could not be decoded.");
    }

    internal static SKBitmap CreateEffectsBitmap()
    {
        var bitmap = new SKBitmap(
            EffectsFrameSize * 3,
            EffectsFrameSize,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawFarStars(canvas, FrameLeft(FarStarFrame));
        DrawNearStars(canvas, FrameLeft(NearStarFrame));
        DrawLaser(canvas, FrameLeft(LaserFrame));

        canvas.Flush();
        return bitmap;
    }

    private static int FrameLeft(int frame) => frame * EffectsFrameSize;

    private static void DrawFarStars(SKCanvas canvas, int x)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(138, 177, 220, 185)
        };

        canvas.DrawCircle(x + 9, 11, 1f, paint);
        canvas.DrawCircle(x + 47, 22, 0.8f, paint);
        canvas.DrawCircle(x + 29, 51, 1.1f, paint);
    }

    private static void DrawNearStars(SKCanvas canvas, int x)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(245, 249, 255, 235)
        };

        canvas.DrawCircle(x + 14, 17, 1.8f, paint);
        canvas.DrawCircle(x + 51, 43, 1.3f, paint);

        paint.Color = new SKColor(115, 210, 242, 210);
        canvas.DrawCircle(x + 34, 8, 1.5f, paint);
    }

    private static void DrawLaser(SKCanvas canvas, int x)
    {
        using var glow = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(55, 226, 255, 90)
        };

        canvas.DrawRoundRect(
            new SKRect(x + 26, 7, x + 38, 57),
            6,
            6,
            glow);

        using var core = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(210, 252, 255, 255)
        };

        canvas.DrawRoundRect(
            new SKRect(x + 30, 9, x + 34, 55),
            2,
            2,
            core);
    }
}

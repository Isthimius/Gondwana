using SkiaSharp;

namespace Gondwana.Demos.Breakout;

/// <summary>All visuals are procedural, so the demos need no copied image assets.</summary>
internal static class BreakoutArt
{
    internal const int Paddle = 0;
    internal const int Ball = 1;
    internal const int FirstBrick = 2;
    internal const int FrameSize = 64;

    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(FrameSize * 8, FrameSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var body = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var shine = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 90) };
        SKColor[] colors =
        [
            new(249, 103, 122), new(252, 164, 86), new(255, 216, 103),
            new(115, 216, 155), new(87, 198, 242), new(168, 145, 251)
        ];

        // Each frame fills its rendered sprite bounds. The game uses those
        // same bounds for paddle, ball, and brick collisions.
        body.Color = new SKColor(105, 222, 255);
        canvas.DrawRoundRect(new SKRect(0, 0, FrameSize, FrameSize), 5, 5, body);
        canvas.DrawRoundRect(new SKRect(5, 8, 59, 18), 4, 4, shine);

        body.Color = new SKColor(240, 252, 255);
        canvas.DrawCircle(96, 32, FrameSize / 2f, body);
        body.Color = new SKColor(93, 204, 249);
        canvas.DrawCircle(86, 22, 10f, body);

        for (int i = 0; i < colors.Length; i++)
        {
            int x = (FirstBrick + i) * FrameSize;
            body.Color = colors[i];
            canvas.DrawRoundRect(new SKRect(x, 0, x + FrameSize, FrameSize), 5, 5, body);
            canvas.DrawRoundRect(new SKRect(x + 5, 8, x + 59, 19), 3, 3, shine);
            body.Color = new SKColor(4, 14, 34, 65);
            canvas.DrawRoundRect(new SKRect(x + 6, 50, x + 58, 58), 3, 3, body);
        }

        canvas.Flush();
        return bitmap;
    }
}

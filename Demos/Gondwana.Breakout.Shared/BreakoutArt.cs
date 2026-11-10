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

        body.Color = new SKColor(105, 222, 255);
        canvas.DrawRoundRect(new SKRect(2, 19, 62, 45), 10, 10, body);
        canvas.DrawRoundRect(new SKRect(5, 21, 59, 28), 5, 5, shine);

        body.Color = new SKColor(240, 252, 255);
        canvas.DrawCircle(96, 32, 13f, body);
        body.Color = new SKColor(93, 204, 249);
        canvas.DrawCircle(92, 27, 4f, body);

        for (int i = 0; i < colors.Length; i++)
        {
            int x = (FirstBrick + i) * FrameSize;
            body.Color = colors[i];
            canvas.DrawRoundRect(new SKRect(x + 2, 10, x + 62, 54), 8, 8, body);
            canvas.DrawRoundRect(new SKRect(x + 5, 13, x + 59, 20), 3, 3, shine);
            body.Color = new SKColor(4, 14, 34, 65);
            canvas.DrawRoundRect(new SKRect(x + 6, 44, x + 58, 50), 3, 3, body);
        }

        canvas.Flush();
        return bitmap;
    }
}

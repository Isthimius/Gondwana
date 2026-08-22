using SkiaSharp;
namespace Gondwana.Demos.Pong;
internal static class PongArt
{
    internal const int FrameSize = 64;
    internal const int PaddleFrame = 0;
    internal const int BallFrame = 1;
    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(FrameSize * 2, FrameSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var glow = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill,
            Color = new SKColor(70, 220, 255, 65), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f) };
        using var white = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill,
            Color = new SKColor(235, 250, 255) };
        canvas.DrawRoundRect(new SKRect(17, 3, 47, 61), 10, 10, glow);
        canvas.DrawRoundRect(new SKRect(22, 5, 42, 59), 7, 7, white);
        const int ballX = FrameSize + FrameSize / 2;
        const int ballY = FrameSize / 2;
        canvas.DrawCircle(ballX, ballY, 21f, glow);
        canvas.DrawCircle(ballX, ballY, 13f, white);
        canvas.Flush();
        return bitmap;
    }
}

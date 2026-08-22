using SkiaSharp;

namespace Gondwana.Demos.Flappy;

internal static class FlappyArt
{
    internal const int FrameSize = 64;
    internal const int BirdFrame = 0;
    internal const int PipeFrame = 1;
    internal const int CloudFrame = 2;
    internal const int GroundFrame = 3;

    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(
            FrameSize * 4,
            FrameSize,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawBird(canvas, FrameLeft(BirdFrame));
        DrawPipe(canvas, FrameLeft(PipeFrame));
        DrawCloud(canvas, FrameLeft(CloudFrame));
        DrawGround(canvas, FrameLeft(GroundFrame));

        canvas.Flush();
        return bitmap;
    }

    private static int FrameLeft(int frame) => frame * FrameSize;

    private static void DrawBird(SKCanvas canvas, int x)
    {
        using var outline = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = new SKColor(86, 54, 18)
        };

        using var body = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(250, 204, 58)
        };

        var bodyRect = new SKRect(x + 11, 17, x + 50, 48);
        canvas.DrawOval(bodyRect, body);
        canvas.DrawOval(bodyRect, outline);

        body.Color = new SKColor(245, 232, 101);
        canvas.DrawOval(new SKRect(x + 10, 29, x + 31, 47), body);
        canvas.DrawOval(new SKRect(x + 10, 29, x + 31, 47), outline);

        body.Color = SKColors.White;
        canvas.DrawCircle(x + 42, 25, 8f, body);
        canvas.DrawCircle(x + 42, 25, 8f, outline);

        body.Color = new SKColor(35, 35, 35);
        canvas.DrawCircle(x + 45, 25, 3f, body);

        body.Color = new SKColor(240, 94, 47);
        var beak = new SKPath();
        beak.MoveTo(x + 49, 29);
        beak.LineTo(x + 62, 34);
        beak.LineTo(x + 49, 38);
        beak.Close();
        canvas.DrawPath(beak, body);
        canvas.DrawPath(beak, outline);
    }

    private static void DrawPipe(SKCanvas canvas, int x)
    {
        using var fill = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(78, 184, 67)
        };

        using var shadow = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(46, 132, 47)
        };

        using var highlight = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(131, 220, 85)
        };

        using var outline = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = new SKColor(42, 93, 38)
        };

        canvas.DrawRect(new SKRect(x + 12, 0, x + 52, 53), fill);
        canvas.DrawRect(new SKRect(x + 39, 0, x + 52, 53), shadow);
        canvas.DrawRect(new SKRect(x + 17, 0, x + 23, 53), highlight);
        canvas.DrawRect(new SKRect(x + 12, 0, x + 52, 53), outline);

        canvas.DrawRect(new SKRect(x + 5, 49, x + 59, 64), fill);
        canvas.DrawRect(new SKRect(x + 43, 49, x + 59, 64), shadow);
        canvas.DrawRect(new SKRect(x + 11, 49, x + 18, 64), highlight);
        canvas.DrawRect(new SKRect(x + 5, 49, x + 59, 64), outline);
    }

    private static void DrawCloud(SKCanvas canvas, int x)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(255, 255, 255, 210)
        };

        canvas.DrawCircle(x + 20, 37, 11f, paint);
        canvas.DrawCircle(x + 33, 28, 15f, paint);
        canvas.DrawCircle(x + 47, 38, 11f, paint);
        canvas.DrawRoundRect(new SKRect(x + 14, 36, x + 54, 49), 7, 7, paint);
    }

    private static void DrawGround(SKCanvas canvas, int x)
    {
        using var dirt = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(213, 184, 106)
        };

        canvas.DrawRect(new SKRect(x, 0, x + 64, 64), dirt);

        dirt.Color = new SKColor(116, 197, 77);
        canvas.DrawRect(new SKRect(x, 0, x + 64, 10), dirt);

        dirt.Color = new SKColor(239, 220, 137);
        for (int offset = -48; offset < 96; offset += 24)
        {
            var stripe = new SKPath();
            stripe.MoveTo(x + offset, 12);
            stripe.LineTo(x + offset + 12, 12);
            stripe.LineTo(x + offset + 52, 64);
            stripe.LineTo(x + offset + 40, 64);
            stripe.Close();
            canvas.DrawPath(stripe, dirt);
        }
    }
}

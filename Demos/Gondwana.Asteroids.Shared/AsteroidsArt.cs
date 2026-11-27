using SkiaSharp;

namespace Gondwana.Demos.Asteroids;

/// <summary>Original vector-style artwork rasterized into a procedural tilesheet.</summary>
internal static class AsteroidsArt
{
    internal const int FrameSize = 128;

    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(FrameSize * 9, FrameSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var pen = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        for (int frame = 0; frame < 9; frame++)
        {
            canvas.Save();
            canvas.Translate(frame * FrameSize + 64f, 64f);

            switch (frame)
            {
                case 0:
                case 1:
                    Polygon(canvas, pen, new(55, 0), new(-39, -42), new(-23, 0), new(-39, 42));

                    if (frame == 1)
                        Polygon(canvas, pen, new(-30, -18), new(-58, 0), new(-30, 18));

                    break;
                case 2:
                case 3:
                case 4:
                    DrawRock(canvas, pen, frame);
                    break;
                case 6:
                    Polygon(canvas, pen, new(-59, 10), new(-38, -12), new(-21, -12), new(-12, -32),
                        new(12, -32), new(21, -12), new(38, -12), new(59, 10), new(35, 31), new(-35, 31));
                    canvas.DrawLine(-57, 10, 57, 10, pen);
                    canvas.DrawLine(-21, -12, 21, -12, pen);
                    break;
                default:
                    pen.Color = frame == 7 ? new SKColor(255, 125, 125) : SKColors.White;
                    pen.Style = SKPaintStyle.Fill;
                    canvas.DrawCircle(0, 0, 51, pen);
                    pen.Style = SKPaintStyle.Stroke;
                    pen.Color = SKColors.White;
                    break;
            }

            canvas.Restore();
        }

        canvas.Flush();
        return bitmap;
    }

    private static void DrawRock(SKCanvas canvas, SKPaint pen, int frame)
    {
        var random = new Random(frame * 91);
        var points = new SKPoint[11];

        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * MathF.Tau / points.Length;
            float radius = 39f + random.NextSingle() * 20f;
            points[i] = new SKPoint(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }

        Polygon(canvas, pen, points);
    }

    private static void Polygon(SKCanvas canvas, SKPaint pen, params SKPoint[] points)
    {
        using var path = new SKPath();
        path.MoveTo(points[0]);

        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i]);

        path.Close();
        canvas.DrawPath(path, pen);
    }
}

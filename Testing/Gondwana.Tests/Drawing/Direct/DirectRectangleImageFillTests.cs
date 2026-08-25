using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class DirectRectangleImageFillTests
{
    [Fact]
    public void SetFillImage_RepeatTilesImageAcrossRectangle()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 6, 4));
        using var source = CreateTwoColorBitmap();
        using var backbuffer = new BitmapBackbuffer(6, 4);
        using var rectangle = new DirectRectangle(
                Color.White,
                host,
                view,
                new Rectangle(0, 0, 6, 4))
            .SetStrokeWidth(0f)
            .SetFillImage(
                source,
                DirectRectangle.ImageFillMode.Repeat,
                filterQuality: SKFilterQuality.None);

        rectangle.Draw(backbuffer, new RectangleF(0, 0, 6, 4));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap result = SKBitmap.FromImage(snapshot);

        Assert.Equal(SKColors.Red, result.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(1, 0));
        Assert.Equal(SKColors.Red, result.GetPixel(2, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(5, 3));
    }

    [Fact]
    public void SetFillImage_StretchFillsRectangleWithoutRepeating()
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 8, 4));
        using var source = CreateTwoColorBitmap();
        using var backbuffer = new BitmapBackbuffer(8, 4);
        using var rectangle = new DirectRectangle(
                Color.White,
                host,
                view,
                new Rectangle(0, 0, 8, 4))
            .SetStrokeWidth(0f)
            .SetFillImage(
                source,
                DirectRectangle.ImageFillMode.Stretch,
                filterQuality: SKFilterQuality.None);

        rectangle.Draw(backbuffer, new RectangleF(0, 0, 8, 4));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap result = SKBitmap.FromImage(snapshot);

        Assert.Equal(SKColors.Red, result.GetPixel(0, 2));
        Assert.Equal(SKColors.Red, result.GetPixel(3, 2));
        Assert.Equal(SKColors.Blue, result.GetPixel(4, 2));
        Assert.Equal(SKColors.Blue, result.GetPixel(7, 2));
    }

    private static SKBitmap CreateTwoColorBitmap()
    {
        var bitmap = new SKBitmap(
            new SKImageInfo(
                2,
                1,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));

        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Blue);
        return bitmap;
    }

    private static View AddView(
        TestRenderSurfaceHost host,
        Rectangle bounds)
    {
        host.ViewManager.AddView(bounds, zOrder: 0);

        return host.ViewManager.Views.Single(view =>
            view.Viewport.TargetRectPx == bounds);
    }
}

using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Overlays;
using SkiaSharp;

namespace Gondwana.Tests.Widgets;

/// <summary>
/// Contains regression tests for splash screen.
/// </summary>
public sealed class SplashScreenTests
{
    /// <summary>
    /// Verifies viewport resize updates full screen image bounds.
    /// </summary>
    [Fact]
    public void ViewportResize_UpdatesFullScreenImageBounds()
    {
        using var host = new TestRenderSurfaceHost();
        var initialBounds = new Rectangle(0, 0, 1, 1);
        host.ViewManager.AddView(initialBounds, zOrder: 0);
        View view = Assert.Single(host.ViewManager.Views);

        using var imageStream = CreatePngStream();
        using var splash = SplashScreen.TryCreate(
            imageStream,
            host,
            view,
            fadeInSec: 1f,
            holdSec: 1f,
            fadeOutSec: 1f);

        Assert.NotNull(splash);
        Assert.Equal(initialBounds, splash.Image.ScreenBounds);

        var resizedBounds = new Rectangle(0, 0, 769, 801);
        view.Viewport.TargetRectPx = resizedBounds;

        Assert.Equal(resizedBounds, splash.Image.ScreenBounds);
    }

    /// <summary>
    /// Verifies initial bounds use viewport origin.
    /// </summary>
    [Fact]
    public void InitialBounds_UseViewportOrigin()
    {
        using var host = new TestRenderSurfaceHost();
        var viewportBounds = new Rectangle(25, 40, 320, 240);
        host.ViewManager.AddView(viewportBounds, zOrder: 0);
        View view = Assert.Single(host.ViewManager.Views);

        using var imageStream = CreatePngStream();
        using var splash = SplashScreen.TryCreate(
            imageStream,
            host,
            view,
            fadeInSec: 1f,
            holdSec: 1f,
            fadeOutSec: 1f);

        Assert.NotNull(splash);
        Assert.Equal(viewportBounds, splash.Image.ScreenBounds);
    }

    private static MemoryStream CreatePngStream()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray(), writable: false);
    }
}

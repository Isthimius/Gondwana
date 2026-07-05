using Gondwana.Drawing.Direct;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Widgets.Overlays;

public sealed class SplashScreen : WidgetBase
{
    public enum State
    {
        Hidden,
        FadingIn,
        Holding,
        FadingOut
    }

    /// <summary>
    /// The current state of the splash screen.
    /// </summary>
    public State CurrentState { get; private set; } = State.Hidden;

    /// <summary>
    /// The nickname used for the DirectImage overlay that displays the splash.
    /// </summary>
    public static readonly string SplashImageNickname = "__gondwana_splash__";

    private readonly DirectImage _image;
    private Gondwana.Timers.Timer _holdTimer;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the duration of the fade-in animation in seconds.
    /// </summary>
    public float FadeInSec { get; private set; }

    /// <summary>
    /// Gets or sets how long the splash is held at full opacity, in seconds.
    /// </summary>
    public float HoldSec { get; private set; }

    /// <summary>
    /// Gets or sets the duration of the fade-out animation in seconds.
    /// </summary>
    public float FadeOutSec { get; private set; }

    /// <summary>
    /// Raised after the fade-in animation completes.
    /// </summary>
    public event Action<SplashScreen>? FadeInCompleted;

    /// <summary>
    /// Raised after the splash has been held at full opacity for the specified duration.
    /// </summary>
    public event Action<SplashScreen>? HoldCompleted;

    /// <summary>
    /// Raised after the fade-out animation completes.
    /// </summary>
    public event Action<SplashScreen>? FadeOutCompleted;

    private SplashScreen(DirectImage image,
                         float fadeInSec,
                         float holdSec,
                         float fadeOutSec)
        : base(
            image.RenderSurfaceHost,
            DirectDrawingMode.View,
            PointF.Empty,
            SplashImageNickname + Guid.NewGuid().ToString())
    {
        _image = image;
        Add(_image);
    }

    public static SplashScreen? TryCreate(Stream imageStream, 
                                          RenderSurfaceHostBase host,
                                          View view,
                                          float fadeInSec = 0.45f,
                                          float holdSec = 3f,
                                          float fadeOutSec = 0.45f)
    {
        if (host.ViewManager.Views.Count == 0)
            return null;

        using var bitmap = SKBitmap.Decode(imageStream);
        var sourceImage = bitmap == null ? null : SKImage.FromBitmap(bitmap);

        if (sourceImage == null)
            return null;

        var vp = view.Viewport.TargetRectPx;
        var screenBounds = new Rectangle(0, 0, vp.Width, vp.Height);

        var image = new DirectImage(sourceImage, host, view, screenBounds, SplashImageNickname)
            .SetScaleMode(DirectImage.ScaleMode.Fit);

        image.ZOrder = int.MaxValue;
        image.Opacity = 0f;

        var splashScreen = new SplashScreen(image, fadeInSec, holdSec, fadeOutSec);
        return splashScreen;
    }

    /// <summary>
    /// Releases the DirectImage overlay and the loaded image.
    /// </summary>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        base.Dispose();
    }
}

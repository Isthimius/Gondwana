using Gondwana.Drawing.Direct;
using Gondwana.Logging;
using Gondwana.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Widgets.Overlays;

public sealed class SplashScreen : WidgetBase
{
    /// <summary>
    /// The nickname used for the DirectImage overlay that displays the splash.
    /// </summary>
    public static readonly string SplashImageNickname = "__gondwana_splash__";

    private readonly DirectImage _image;
    private readonly SKImage _sourceImage;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the duration of the fade-in animation in seconds.
    /// </summary>
    public float FadeInSec { get; set; } = 0.45f;

    /// <summary>
    /// Gets or sets how long the splash is held at full opacity, in seconds.
    /// </summary>
    public float HoldSec { get; set; } = 0.55f;

    /// <summary>
    /// Gets or sets the duration of the fade-out animation in seconds.
    /// </summary>
    public float FadeOutSec { get; set; } = 0.45f;

    /// <summary>
    /// Gets or sets an optional callback that runs after the fade-in completes and while the
    /// splash is being held on screen.
    /// </summary>
    /// <remarks>
    /// Fade-out will not begin until both <see cref="HoldSec"/> has elapsed and this callback
    /// has completed.
    /// </remarks>
    public Func<SplashScreen, Task>? AfterFadeInAsync { get; set; }

    /// <summary>
    /// Raised after the fade-in animation completes.
    /// </summary>
    public event Action<SplashScreen>? FadeInCompleted;

    /// <summary>
    /// Raised after the fade-out animation completes.
    /// </summary>
    public event Action<SplashScreen>? FadeOutCompleted;

    private SplashScreen(
        RenderSurfaceHostBase host,
        DirectImage image,
        SKImage sourceImage)
        : base(
            host,
            DirectDrawingMode.View,
            PointF.Empty,
            SplashImageNickname)
    {
        _image = image;
        _sourceImage = sourceImage;

        Add(_image);

        // Start hidden. ShowAsync() will make it visible and fade it in.
        _image.Visible = false;
    }

    /// <summary>
    /// Attempts to create a <see cref="SplashScreen"/> from an image file, attached to the
    /// first view of the specified host.
    /// </summary>
    /// <param name="host">The render surface host that will display the splash.</param>
    /// <param name="imagePath">Full path to the image file.</param>
    /// <returns>
    /// A configured <see cref="SplashScreen"/>, or <see langword="null"/> if the host has no
    /// views or the image file cannot be decoded.
    /// </returns>
    public static SplashScreen? TryCreate(RenderSurfaceHostBase host, string imagePath)
    {
        if (host.ViewManager.Views.Count == 0)
            return null;

        if (!File.Exists(imagePath))
        {
            EngineLogger.GetLogger<SplashScreen>().LogWarning(
                "SplashScreen image not found at '{Path}'; splash will be skipped.", imagePath);

            return null;
        }

        using var bitmap = SKBitmap.Decode(imagePath);
        var sourceImage = bitmap == null ? null : SKImage.FromBitmap(bitmap);

        if (sourceImage == null)
        {
            EngineLogger.GetLogger<SplashScreen>().LogWarning(
                "SplashScreen image could not be decoded from '{Path}'; splash will be skipped.", imagePath);

            return null;
        }

        var view = host.ViewManager.Views[0];
        var vp = view.Viewport.TargetRectPx;
        var screenBounds = new Rectangle(0, 0, vp.Width, vp.Height);

        var image = new DirectImage(sourceImage, host, view, screenBounds, SplashImageNickname)
            .SetScaleMode(DirectImage.ScaleMode.Fit);

        image.ZOrder = int.MaxValue;
        image.Opacity = 0f;

        return new SplashScreen(host, image, sourceImage);
    }

    /// <summary>
    /// Fades the splash in to full opacity, then waits for both the hold period and any
    /// <see cref="AfterFadeInAsync"/> callback to complete.
    /// </summary>
    public async Task ShowAsync()
    {
        ThrowIfDisposed();

        Show();

        _image.Opacity = 0f;
        await FadeAndWaitAsync(_image, 1f, FadeInSec);

        FadeInCompleted?.Invoke(this);

        var holdTask = HoldSec > 0f
            ? Task.Delay(TimeSpan.FromSeconds(HoldSec))
            : Task.CompletedTask;

        var callbackTask = InvokeAfterFadeInAsync();

        await Task.WhenAll(holdTask, callbackTask);
    }

    /// <summary>
    /// Fades the splash out to fully transparent.
    /// Returns when the fade-out animation completes.
    /// </summary>
    public async Task HideAsync()
    {
        ThrowIfDisposed();

        await FadeAndWaitAsync(_image, 0f, FadeOutSec);

        FadeOutCompleted?.Invoke(this);

        Hide();
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

        _sourceImage.Dispose();
    }

    private Task InvokeAfterFadeInAsync()
    {
        if (AfterFadeInAsync == null)
            return Task.CompletedTask;

        try
        {
            return AfterFadeInAsync(this) ?? Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SplashScreen));
    }

    private static Task FadeAndWaitAsync(
        DirectDrawingBase drawing,
        float targetOpacity,
        float durationSec)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Cleanup()
        {
            drawing.FadeToCompleted -= OnComplete;
            drawing.Disposing -= OnDisposing;
        }

        void OnComplete(object? sender, DirectDrawingBase _)
        {
            Cleanup();
            tcs.TrySetResult();
        }

        void OnDisposing(object? sender, IDirectDrawable _)
        {
            Cleanup();
            tcs.TrySetCanceled();
        }

        drawing.FadeToCompleted += OnComplete;
        drawing.Disposing += OnDisposing;

        try
        {
            drawing.FadeTo(targetOpacity, durationSec);
        }
        catch
        {
            Cleanup();
            throw;
        }

        return tcs.Task;
    }
}
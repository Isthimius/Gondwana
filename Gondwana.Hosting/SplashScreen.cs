using Gondwana.Drawing.Direct;
using Gondwana.Logging;
using Gondwana.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Hosting;

/// <summary>
/// A platform-agnostic splash screen implemented as a <see cref="DirectImage"/> overlay on
/// the primary render surface, animated with the engine's built-in
/// <see cref="DirectDrawingBase.FadeIn"/> and <see cref="DirectDrawingBase.FadeOut"/> transitions.
/// </summary>
/// <remarks>
/// <para>
/// Create an instance via <see cref="TryCreate"/> after calling
/// <see cref="GameHostBase.InitializeAsync"/>—the engine must already be running so the
/// animation loop can drive the fade.  Call <see cref="ShowAsync"/> to fade the splash in
/// (and hold it), then <see cref="HideAsync"/> to fade it out.  Dispose the instance when
/// done to release the underlying image resources.
/// </para>
/// <para>
/// The overlay is rendered in view-space with <see cref="int.MaxValue"/> Z-order so it always
/// appears on top of other direct drawings.
/// </para>
/// </remarks>
public sealed class SplashScreen : IDisposable
{
    /// <summary>Gets or sets the duration of the fade-in animation in seconds.</summary>
    public float FadeInSec { get; set; } = 0.45f;

    /// <summary>Gets or sets how long the splash is held at full opacity, in seconds.</summary>
    public float HoldSec { get; set; } = 0.55f;

    /// <summary>Gets or sets the duration of the fade-out animation in seconds.</summary>
    public float FadeOutSec { get; set; } = 0.45f;

    private readonly DirectImage _image;
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    private SplashScreen(DirectImage image, SKBitmap bitmap)
    {
        _image = image;
        _bitmap = bitmap;
    }

    /// <summary>
    /// Attempts to create a <see cref="SplashScreen"/> from an image file, attached to the
    /// first view of the specified host.
    /// </summary>
    /// <param name="host">The render surface host that will display the splash.</param>
    /// <param name="imagePath">Full path to the image file (PNG, JPG, etc.).</param>
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

        var bitmap = SKBitmap.Decode(imagePath);
        if (bitmap == null)
        {
            EngineLogger.GetLogger<SplashScreen>().LogWarning(
                "SplashScreen image could not be decoded from '{Path}'; splash will be skipped.", imagePath);
            return null;
        }

        var view = host.ViewManager.Views[0];
        var vp = view.Viewport.TargetRectPx;
        var screenBounds = new Rectangle(0, 0, vp.Width, vp.Height);

        var image = new DirectImage(bitmap, host, view, screenBounds, "__gondwana_splash__")
            .SetScaleMode(DirectImage.ScaleMode.Fit);

        image.ZOrder = int.MaxValue;
        image.Opacity = 0f;

        return new SplashScreen(image, bitmap);
    }

    /// <summary>
    /// Fades the splash in to full opacity, then holds for <see cref="HoldSec"/> seconds.
    /// Returns when the hold period expires.
    /// </summary>
    public async Task ShowAsync()
    {
        await FadeAndWaitAsync(_image, 1f, FadeInSec);
        if (HoldSec > 0f)
            await Task.Delay(TimeSpan.FromSeconds(HoldSec));
    }

    /// <summary>
    /// Fades the splash out to fully transparent.
    /// Returns when the fade-out animation completes.
    /// </summary>
    public Task HideAsync() => FadeAndWaitAsync(_image, 0f, FadeOutSec);

    /// <summary>Releases the DirectImage overlay and the loaded bitmap.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _image.Dispose();
        _bitmap.Dispose();
    }

    // Subscribes to terminal events before starting the fade so no completion/disposal event is
    // missed, then returns a Task that resolves when the transition finishes or is canceled by
    // disposal.
    private static Task FadeAndWaitAsync(DirectDrawingBase drawing, float targetOpacity, float durationSec)
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

        void OnDisposing(object? sender, DirectDrawingBase _)
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

using System.Drawing;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana.Drawing.Direct;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Timers;

using Timer = Gondwana.Timers.Timer;

namespace Gondwana.Widgets.Overlays;

/// <summary>
/// Represents a full-screen splash screen widget that fades in,
/// holds for a duration, and fades out.
/// </summary>
/// <remarks>
/// <para>
/// The splash screen transitions through four states:
/// <see cref="State.Hidden"/>,
/// <see cref="State.FadingIn"/>,
/// <see cref="State.Holding"/>, and
/// <see cref="State.FadingOut"/>.
/// </para>
/// <para>
/// During the holding phase, optional synchronous and asynchronous
/// delegates may be invoked. The splash remains visible until both
/// the configured hold duration has elapsed and all supplied delegate
/// work has completed.
/// </para>
/// </remarks>
public sealed class SplashScreen : WidgetBase
{
    private static readonly ILogger<SplashScreen> Logger =
        EngineLogger.GetLogger<SplashScreen>();

    /// <summary>
    /// Represents the current state of the splash screen animation.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// The splash screen is not visible.
        /// </summary>
        Hidden,

        /// <summary>
        /// The splash screen is fading in from transparent to opaque.
        /// </summary>
        FadingIn,

        /// <summary>
        /// The splash screen is held at full opacity.
        /// </summary>
        Holding,

        /// <summary>
        /// The splash screen is fading out from opaque to transparent.
        /// </summary>
        FadingOut
    }

    /// <summary>
    /// The nickname used for the DirectImage overlay that displays the splash.
    /// </summary>
    public static readonly string SplashImageNickname = "__gondwana_splash__";

    private Timer? _holdTimer;
    private TaskCompletionSource? _holdDurationCompletionSource;
    private bool _disposed;

    #region properties

    /// <summary>
    /// Gets the DirectImage overlay that displays the splash image.
    /// </summary>
    public DirectImage Image { get; private set; }

    /// <summary>
    /// Gets the current state of the splash screen.
    /// </summary>
    public State CurrentState { get; private set; }

    /// <summary>
    /// Gets the duration of the fade-in animation in seconds.
    /// </summary>
    public float FadeInSec { get; private set; }

    /// <summary>
    /// Gets the minimum duration for which the splash remains
    /// at full opacity, in seconds.
    /// </summary>
    public float HoldSec { get; private set; }

    /// <summary>
    /// Gets the duration of the fade-out animation in seconds.
    /// </summary>
    public float FadeOutSec { get; private set; }

    #endregion properties

    #region factory / constructor

    private SplashScreen(DirectImage image,
                         float fadeInSec,
                         float holdSec,
                         float fadeOutSec,
                         Action? onHoldingSync,
                         Func<Task>? onHoldingAsync,
                         Action? onSplashCompleted)
        : base(image.RenderSurfaceHost,
               DirectDrawingMode.View,
               PointF.Empty,
               SplashImageNickname + Guid.NewGuid())
    {
        Image = image;
        CurrentState = State.Hidden;

        FadeInSec = fadeInSec;
        HoldSec = holdSec;
        FadeOutSec = fadeOutSec;

        Add(Image);

        StartFadeIn(
            onHoldingSync,
            onHoldingAsync,
            onSplashCompleted);
    }

    /// <summary>
    /// Attempts to create a new <see cref="SplashScreen"/> instance
    /// from the provided image stream.
    /// </summary>
    /// <param name="imageStream">
    /// The stream containing the splash image to decode.
    /// </param>
    /// <param name="host">
    /// The render surface host that manages rendering for the splash screen.
    /// </param>
    /// <param name="view">
    /// The view to which the splash screen is attached.
    /// </param>
    /// <param name="fadeInSec">
    /// The duration of the fade-in animation in seconds.
    /// The default is 0.45 seconds.
    /// </param>
    /// <param name="holdSec">
    /// The minimum duration for which the splash remains at full opacity.
    /// The default is 3 seconds.
    /// </param>
    /// <param name="fadeOutSec">
    /// The duration of the fade-out animation in seconds.
    /// The default is 0.45 seconds.
    /// </param>
    /// <param name="onHoldingSync">
    /// An optional synchronous delegate invoked on the engine thread
    /// during the holding phase.
    /// </param>
    /// <param name="onHoldingAsync">
    /// An optional asynchronous delegate invoked on the engine thread
    /// during the holding phase.
    /// </param>
    /// <returns>
    /// A new <see cref="SplashScreen"/> instance if creation succeeds;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The holding phase waits for both the configured hold duration
    /// to elapse and all supplied delegate work to complete.
    /// </para>
    /// <para>
    /// If the delegates complete before the hold duration has elapsed,
    /// the splash remains visible for the full duration. If delegate work
    /// takes longer, the splash remains visible until that work completes.
    /// </para>
    /// <para>
    /// The delegates are invoked on the engine thread. Continuations within
    /// the asynchronous delegate follow the synchronization behavior defined
    /// by that delegate and its awaited operations.
    /// </para>
    /// <para>
    /// Exceptions thrown by supplied delegates are logged but do not prevent
    /// the splash sequence from completing.
    /// </para>
    /// <para>
    /// The created splash screen automatically disposes itself after
    /// the fade-out animation completes.
    /// </para>
    /// </remarks>
    public static SplashScreen? TryCreate(Stream imageStream,
                                          RenderSurfaceHostBase host,
                                          View view,
                                          float fadeInSec = 0.45f,
                                          float holdSec = 3f,
                                          float fadeOutSec = 0.45f,
                                          Action? onHoldingSync = null,
                                          Func<Task>? onHoldingAsync = null,
                                          Action? onSplashCompleted = null)
    {
        if (host.ViewManager.Views.Count == 0)
            return null;

        using var bitmap = SKBitmap.Decode(imageStream);

        var sourceImage = bitmap is null
            ? null
            : SKImage.FromBitmap(bitmap);

        if (sourceImage is null)
            return null;

        var viewport = view.Viewport.TargetRectPx;

        var screenBounds = new Rectangle(
            0,
            0,
            viewport.Width,
            viewport.Height);

        var image = new DirectImage(
                sourceImage,
                host,
                view,
                screenBounds,
                SplashImageNickname)
            .SetScaleMode(DirectImage.ScaleMode.Fit);

        // DirectImage does not own the SKImage lifetime.
        image.Disposing += (_, _) => sourceImage.Dispose();

        image.ZOrder = int.MaxValue;
        image.Opacity = 0f;

        return new SplashScreen(
            image,
            fadeInSec,
            holdSec,
            fadeOutSec,
            onHoldingSync,
            onHoldingAsync,
            onSplashCompleted);
    }

    #endregion factory / constructor

    /// <summary>
    /// Releases the splash overlay and associated resources.
    /// </summary>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _holdTimer?.Dispose();
        _holdTimer = null;

        _holdDurationCompletionSource?.TrySetCanceled();
        _holdDurationCompletionSource = null;

        base.Dispose();
    }

    #region splash control

    private void StartFadeIn(Action? onHoldingSync, Func<Task>? onHoldingAsync, Action? onSplashCompleted)
    {
        Logger.LogDebug("Starting splash screen fade-in");

        CurrentState = State.FadingIn;

        EventHandler<DirectDrawingBase>? fadeInHandler = null;

        fadeInHandler = (_, _) =>
        {
            Image.FadeToCompleted -= fadeInHandler;

            if (_disposed)
                return;

            CurrentState = State.Holding;

            _ = RunHoldPhaseAsync(
                onHoldingSync,
                onHoldingAsync,
                onSplashCompleted);
        };

        Image.FadeToCompleted += fadeInHandler;

        Image.FadeIn(FadeInSec);
    }

    private async Task RunHoldPhaseAsync(
        Action? onHoldingSync,
        Func<Task>? onHoldingAsync,
        Action? onSplashCompleted)
    {
        Logger.LogDebug("Starting splash screen hold phase");

        try
        {
            var holdDurationTask = WaitForHoldDurationAsync();

            var delegateTask = ExecuteHoldDelegatesAsync(onHoldingSync, onHoldingAsync);

            await Task.WhenAll(
                holdDurationTask,
                delegateTask);
        }
        catch (OperationCanceledException)
            when (_disposed)
            {
                return;
            }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during SplashScreen hold phase");
        }
        finally
        {
            _holdTimer?.Dispose();
            _holdTimer = null;

            _holdDurationCompletionSource = null;
        }

        if (_disposed)
            return;

        Engine.Instance.EngineDispatcher.Post(() =>
        {
            if (_disposed)
                return;

            if (CurrentState != State.Holding)
                return;

            StartFadeOut(onSplashCompleted);
        });
    }

    private Task WaitForHoldDurationAsync()
    {
        var completionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _holdDurationCompletionSource =
            completionSource;

        var timer = Timer.Add(
            TimerType.PostCycle,
            TimerCycles.Once,
            HoldSec);

        _holdTimer = timer;

        void OnHoldTimerElapsed()
        {
            timer.Tick -= OnHoldTimerElapsed;

            completionSource.TrySetResult();
        }

        timer.Tick += OnHoldTimerElapsed;

        return completionSource.Task;
    }

    private static async Task ExecuteHoldDelegatesAsync(
        Action? onHoldingSync,
        Func<Task>? onHoldingAsync)
    {
        if (onHoldingSync is null &&
            onHoldingAsync is null)
        {
            return;
        }

        var dispatcher =
            Engine.Instance.EngineDispatcher;

        try
        {
            await dispatcher.PostAsync(async () =>
            {
                Task asyncTask =
                    Task.CompletedTask;

                Task syncTask =
                    Task.CompletedTask;

                if (onHoldingAsync is not null)
                {
                    Logger.LogDebug("Calling onHoldingAsync in SplashScreen");

                    try
                    {
                        asyncTask = onHoldingAsync() ?? Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        asyncTask = Task.FromException(ex);
                    }
                }

                if (onHoldingSync is not null)
                {
                    Logger.LogDebug("Calling onHoldingSync in SplashScreen");

                    try
                    {
                        onHoldingSync();
                    }
                    catch (Exception ex)
                    {
                        syncTask = Task.FromException(ex);
                    }
                }

                await Task.WhenAll(asyncTask, syncTask);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing SplashScreen hold delegates");
        }
    }

    private void StartFadeOut(Action? onSplashCompleted)
    {
        Logger.LogDebug("Starting splash screen fade-out");

        CurrentState = State.FadingOut;

        EventHandler<DirectDrawingBase>? fadeOutHandler = null;

        fadeOutHandler = (_, _) =>
        {
            Image.FadeToCompleted -= fadeOutHandler;
            CurrentState = State.Hidden;
            Dispose();
            onSplashCompleted?.Invoke();
        };

        Image.FadeToCompleted += fadeOutHandler;
        Image.FadeOut(FadeOutSec);
    }

    #endregion splash control
}
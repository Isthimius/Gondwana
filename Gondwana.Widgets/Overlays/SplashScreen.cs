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
/// Represents a full-screen splash screen widget that fades in, holds for a duration, and fades out.
/// </summary>
/// <remarks>
/// <para>
/// The splash screen transitions through four states: <see cref="State.Hidden"/>, <see cref="State.FadingIn"/>,
/// <see cref="State.Holding"/>, and <see cref="State.FadingOut"/>. During the holding phase, optional synchronous
/// and asynchronous delegates can be executed on the engine thread.
/// </para>
/// <para>
/// The holding phase waits for both the specified hold duration to elapse AND for all provided delegates to complete
/// before transitioning to the fade-out phase.
/// </para>
/// </remarks>
public sealed class SplashScreen : WidgetBase
{
    private static readonly ILogger<SplashScreen> Logger = EngineLogger.GetLogger<SplashScreen>();

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

    private Timer _holdTimer;
    private bool _disposed;
    private bool _holdTimerExpired;
    private bool _holdDelegatesCompleted;

    #region properties

    /// <summary>
    /// The DirectImage overlay that displays the splash image.
    /// </summary>
    public DirectImage Image { get; private set; }

    /// <summary>
    /// The current state of the splash screen.
    /// </summary>
    public State CurrentState { get; private set; }

    /// <summary>
    /// The duration of the fade-in animation in seconds.
    /// </summary>
    public float FadeInSec { get; private set; }

    /// <summary>
    /// How long the splash is held at full opacity, in seconds.
    /// </summary>
    public float HoldSec { get; private set; }

    /// <summary>
    /// The duration of the fade-out animation in seconds.
    /// </summary>
    public float FadeOutSec { get; private set; }

    #endregion properties

    #region events

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

    #endregion events

    #region factory / constructor

    private SplashScreen(DirectImage image,
                         float fadeInSec,
                         float holdSec,
                         float fadeOutSec,
                         Action? onHoldingSync,
                         Func<Task>? onHoldingAsync)
        : base(
            image.RenderSurfaceHost,
            DirectDrawingMode.View,
            PointF.Empty,
            SplashImageNickname + Guid.NewGuid().ToString())
    {
        Image = image;
        CurrentState = State.Hidden;
        FadeInSec = fadeInSec;
        HoldSec = holdSec;
        FadeOutSec = fadeOutSec;

        Add(Image);
        RunSplashSequence(onHoldingSync, onHoldingAsync);
    }

    /// <summary>
    /// Attempts to create a new <see cref="SplashScreen"/> instance from the provided image stream.
    /// </summary>
    /// <param name="imageStream">The stream containing the splash image to decode.</param>
    /// <param name="host">The render surface host that will manage rendering for the splash screen.</param>
    /// <param name="view">The view to which the splash screen will be attached.</param>
    /// <param name="fadeInSec">The duration of the fade-in animation in seconds. Default is 0.45 seconds.</param>
    /// <param name="holdSec">The minimum duration to hold the splash at full opacity in seconds. Default is 3 seconds.</param>
    /// <param name="fadeOutSec">The duration of the fade-out animation in seconds. Default is 0.45 seconds.</param>
    /// <param name="onHoldingSync">Optional synchronous delegate to execute on the engine thread during the holding phase.</param>
    /// <param name="onHoldingAsync">Optional asynchronous delegate to execute on the engine thread during the holding phase.</param>
    /// <returns>
    /// A new <see cref="SplashScreen"/> instance if creation succeeds; otherwise, <see langword="null"/> if the host has no views
    /// or if the image cannot be decoded.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The holding phase will wait for both <paramref name="holdSec"/> to elapse AND for both delegates (if provided) to complete
    /// before transitioning to the fade-out phase. If the delegates complete before the hold duration, the splash waits for the
    /// full duration. If the delegates take longer than the hold duration, the splash waits for them to finish.
    /// </para>
    /// <para>
    /// Both delegates are guaranteed to execute on the engine thread. Any exceptions thrown by the delegates are logged but
    /// do not prevent the splash sequence from completing.
    /// </para>
    /// <para>
    /// The created splash screen automatically disposes itself after the fade-out completes.
    /// </para>
    /// </remarks>
    public static SplashScreen? TryCreate(Stream imageStream, 
                                          RenderSurfaceHostBase host,
                                          View view,
                                          float fadeInSec = 0.45f,
                                          float holdSec = 3f,
                                          float fadeOutSec = 0.45f,
                                          Action? onHoldingSync = null,
                                          Func<Task>? onHoldingAsync = null)
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

        // DirectImage does not own SKImage lifetime; dispose the decoded image when the drawable is disposed.
        image.Disposing += (_, _) => sourceImage.Dispose();

        image.ZOrder = int.MaxValue;
        image.Opacity = 0f;

        var splashScreen = new SplashScreen(image, fadeInSec, holdSec, fadeOutSec, onHoldingSync, onHoldingAsync);
        return splashScreen;
    }

    #endregion factory / constructor

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

    #region splash control

    private void RunSplashSequence(Action? onHoldingSync, Func<Task>? onHoldingAsync)
    {
        // Start fade-in
        CurrentState = State.FadingIn;

        // Subscribe to fade-in completion
        EventHandler<DirectDrawingBase>? fadeInHandler = null;
        fadeInHandler = (sender, drawable) =>
        {
            Image.FadeToCompleted -= fadeInHandler;
            CurrentState = State.Holding;
            FadeInCompleted?.Invoke(this);
            StartHoldPhase(onHoldingSync, onHoldingAsync);
        };
        Image.FadeToCompleted += fadeInHandler;

        // Start the fade-in animation
        Image.FadeIn(FadeInSec);
    }

    private void StartHoldPhase(Action? onHoldingSync, Func<Task>? onHoldingAsync)
    {
        _holdTimerExpired = false;
        _holdDelegatesCompleted = (onHoldingSync == null && onHoldingAsync == null);

        // Start hold timer
        _holdTimer = Timer.Add(TimerType.PostCycle, TimerCycles.Once, HoldSec);
        _holdTimer.Tick += () =>
        {
            _holdTimerExpired = true;
            CheckHoldCompletion();
        };

        // Execute delegates on engine thread if provided
        if (onHoldingSync != null || onHoldingAsync != null)
        {
            var dispatcher = Engine.Instance.EngineDispatcher;
            dispatcher.Post(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        onHoldingSync?.Invoke();

                        if (onHoldingAsync != null)
                            await onHoldingAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error executing splash screen hold delegates");
                    }
                    finally
                    {
                        // Post back to engine thread to mark completion
                        dispatcher.Post(() =>
                        {
                            _holdDelegatesCompleted = true;
                            CheckHoldCompletion();
                        });
                    }
                });
            });
        }
    }

    private void CheckHoldCompletion()
    {
        if (_holdTimerExpired && _holdDelegatesCompleted && CurrentState == State.Holding)
        {
            _holdTimer?.Dispose();
            HoldCompleted?.Invoke(this);
            StartFadeOut();
        }
    }

    private void StartFadeOut()
    {
        CurrentState = State.FadingOut;

        // Subscribe to fade-out completion
        EventHandler<DirectDrawingBase>? fadeOutHandler = null;
        fadeOutHandler = (sender, drawable) =>
        {
            Image.FadeToCompleted -= fadeOutHandler;
            CurrentState = State.Hidden;
            FadeOutCompleted?.Invoke(this);
            Dispose();
        };
        Image.FadeToCompleted += fadeOutHandler;

        // Start the fade-out animation
        Image.FadeOut(FadeOutSec);
    }

    #endregion
}

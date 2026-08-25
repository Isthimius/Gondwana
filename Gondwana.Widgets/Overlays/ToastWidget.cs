using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Widgets.Overlays;

/// <summary>
/// Defines how a toast enters its view.
/// </summary>
public enum ToastTransition
{
    /// <summary>The toast moves from outside the view to its target bounds.</summary>
    Slide,

    /// <summary>The toast remains at its target bounds and fades from transparent.</summary>
    Fade
}

/// <summary>
/// Defines the view edge from which a sliding toast enters.
/// </summary>
public enum ToastSlideOrigin
{
    /// <summary>The toast enters from above the view.</summary>
    Top,

    /// <summary>The toast enters from the right of the view.</summary>
    Right,

    /// <summary>The toast enters from below the view.</summary>
    Bottom,

    /// <summary>The toast enters from the left of the view.</summary>
    Left
}

/// <summary>
/// Represents the lifecycle state of a <see cref="ToastWidget"/>.
/// </summary>
public enum ToastState
{
    /// <summary>The toast is not being shown.</summary>
    Hidden,

    /// <summary>The toast is moving or fading into view.</summary>
    Entering,

    /// <summary>The toast is resting at its target bounds.</summary>
    Holding,

    /// <summary>The toast is moving or fading out of view.</summary>
    Exiting
}

/// <summary>
/// Displays a text notification inside one <see cref="View"/> with a slide or
/// fade transition and optional automatic dismissal.
/// </summary>
/// <remarks>
/// Screen bounds use absolute render-surface pixels, matching other view-mode
/// direct drawings. Set <see cref="HoldDurationSec"/> to <see langword="null"/>
/// for a toast that remains until <see cref="Dismiss"/> is called or the toast
/// is clicked while <see cref="DismissOnClick"/> is enabled.
/// </remarks>
public sealed class ToastWidget : WidgetBase
{
    private bool _disposed;
    private float _transitionDurationSec = 0.3f;
    private float? _holdDurationSec = 3f;
    private float _phaseElapsedSec;
    private long _lastAnimationTick;
    private Vector2 _phaseStartPosition;
    private Vector2 _phaseEndPosition;
    private float _phaseStartOpacity;
    private float _phaseEndOpacity;

    /// <summary>
    /// Occurs after the toast completes dismissal and is hidden.
    /// </summary>
    public event Action? Dismissed;

    /// <summary>
    /// Initializes a toast with explicit target bounds in absolute screen pixels.
    /// </summary>
    public ToastWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle targetBounds,
        string text,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            targetBounds.Location,
            nickname)
    {
        ValidateBounds(targetBounds);

        TargetBounds = targetBounds;

        Background = new DirectRectangle(
                Color.FromArgb(232, 35, 38, 45),
                renderSurfaceHost,
                view,
                targetBounds,
                $"{Nickname}.background")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(245, 215, 219, 225))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(6f);

        Label = new TextBlock(
                renderSurfaceHost,
                view,
                targetBounds,
                $"{Nickname}.label")
            .SetText(text)
            .SetFont(SKTypeface.Default, 17f, minSize: 11f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(true);

        Label.HorizontalPadding = 12f;
        Label.VerticalPadding = 8f;

        Add(
            Background,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: Vector2.Zero);

        Add(
            Label,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: Vector2.Zero);

        SetToastZOrder(int.MaxValue - 100);
        SetIsVisible(false);
    }

    /// <summary>
    /// Initializes a toast positioned from a standard anchor inside the view.
    /// </summary>
    public ToastWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Size size,
        string text,
        WidgetAnchor targetAnchor = WidgetAnchor.TopRight,
        int marginPx = 16,
        string? nickname = null)
        : this(
            renderSurfaceHost,
            view,
            GetAnchoredBounds(view, size, targetAnchor, marginPx),
            text,
            nickname)
    {
    }

    /// <summary>
    /// Gets the rectangle used for the toast background and border.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the text drawing used for the toast message.
    /// </summary>
    public TextBlock Label { get; }

    /// <summary>
    /// Gets the current toast lifecycle state.
    /// </summary>
    public ToastState CurrentState { get; private set; } = ToastState.Hidden;

    /// <summary>
    /// Gets or sets the entrance and animated-dismissal transition.
    /// </summary>
    public ToastTransition Transition { get; set; } = ToastTransition.Slide;

    /// <summary>
    /// Gets or sets the edge used by slide transitions.
    /// </summary>
    public ToastSlideOrigin SlideOrigin { get; set; } = ToastSlideOrigin.Right;

    /// <summary>
    /// Gets or sets an optional explicit slide source top-left in absolute screen pixels.
    /// When null, the source is calculated just outside <see cref="View.Viewport"/>.
    /// </summary>
    public Point? SourceLocationPx { get; set; }

    /// <summary>
    /// Gets or sets the gap between a calculated slide source and the view edge.
    /// </summary>
    public int SourceOffsetPx { get; set; } = 8;

    /// <summary>
    /// Gets or sets the transition duration in seconds. Zero performs the transition immediately.
    /// </summary>
    public float TransitionDurationSec
    {
        get => _transitionDurationSec;
        set => _transitionDurationSec = value >= 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets how long the toast remains at its target before automatic dismissal.
    /// Null disables automatic dismissal.
    /// </summary>
    public float? HoldDurationSec
    {
        get => _holdDurationSec;
        set => _holdDurationSec = value is null or >= 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the easing used by the entrance transition.
    /// </summary>
    public EasingKind EntranceEasing { get; set; } = EasingKind.EaseOutCubic;

    /// <summary>
    /// Gets or sets the easing used by the dismissal transition.
    /// </summary>
    public EasingKind ExitEasing { get; set; } = EasingKind.EaseInCubic;

    /// <summary>
    /// Gets or sets whether dismissal reverses the configured entrance transition.
    /// </summary>
    public bool AnimateDismissal { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a primary-pointer click dismisses the toast.
    /// </summary>
    public bool DismissOnClick { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the toast disposes itself after dismissal.
    /// </summary>
    public bool DisposeOnDismiss { get; set; } = true;

    /// <summary>
    /// Gets the target bounds at which the toast rests, in absolute screen pixels.
    /// </summary>
    public Rectangle TargetBounds { get; private set; }

    /// <summary>
    /// Shows the toast and begins its entrance transition.
    /// </summary>
    public ToastWidget ShowToast()
    {
        Show();
        return this;
    }

    /// <summary>
    /// Begins dismissal. Repeated calls while exiting or hidden have no effect.
    /// </summary>
    public void Dismiss()
    {
        if (_disposed || CurrentState is ToastState.Hidden or ToastState.Exiting)
            return;

        if (!AnimateDismissal || TransitionDurationSec <= 0f)
        {
            CompleteDismissal();
            return;
        }

        CurrentState = ToastState.Exiting;
        BeginPhase(
            GetPosition(),
            Transition == ToastTransition.Slide
                ? GetSourcePosition()
                : GetPosition(),
            GetCurrentOpacity(),
            Transition == ToastTransition.Fade ? 0f : 1f);
    }

    /// <summary>
    /// Changes the target bounds used the next time the toast is shown.
    /// </summary>
    public ToastWidget SetTargetBounds(Rectangle targetBounds)
    {
        ValidateBounds(targetBounds);

        TargetBounds = targetBounds;
        Background.ScreenBounds = targetBounds;
        Label.ScreenBounds = targetBounds;
        SetPosition(targetBounds.X, targetBounds.Y);

        return this;
    }

    /// <summary>
    /// Sets the toast message.
    /// </summary>
    public ToastWidget SetText(string text)
    {
        Label.SetText(text);
        return this;
    }

    /// <summary>
    /// Sets the background Z-order and places the label immediately above it.
    /// </summary>
    public ToastWidget SetToastZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        Label.ZOrder = zOrder + 1;
        return this;
    }

    /// <inheritdoc/>
    public override void Update(long tick)
    {
        if (_disposed || tick <= _lastAnimationTick)
            return;

        float elapsedSec = HighResTimer.GetDuration(_lastAnimationTick, tick);
        _lastAnimationTick = tick;

        base.Update(tick);

        switch (CurrentState)
        {
            case ToastState.Entering:
                AdvanceTransition(elapsedSec, entering: true);
                break;

            case ToastState.Holding:
                _phaseElapsedSec += elapsedSec;

                if (HoldDurationSec is { } holdSec &&
                    _phaseElapsedSec >= holdSec)
                {
                    Dismiss();
                }
                break;

            case ToastState.Exiting:
                AdvanceTransition(elapsedSec, entering: false);
                break;
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Dismissed = null;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        CurrentState = ToastState.Entering;
        _lastAnimationTick = HighResTimer.GetCurrentTick();

        Vector2 target = new(TargetBounds.X, TargetBounds.Y);
        Vector2 source = Transition == ToastTransition.Slide
            ? GetSourcePosition()
            : target;

        BeginPhase(
            source,
            target,
            Transition == ToastTransition.Fade ? 0f : 1f,
            1f);

        if (TransitionDurationSec <= 0f)
            CompleteEntrance();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        CurrentState = ToastState.Hidden;
        Movement.StopAllMovement();
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!DismissOnClick || !args.IsPrimaryButton)
            return;

        args.Handled = true;
        Dismiss();
    }

    private void BeginPhase(
        Vector2 startPosition,
        Vector2 endPosition,
        float startOpacity,
        float endOpacity)
    {
        _phaseElapsedSec = 0f;
        _phaseStartPosition = startPosition;
        _phaseEndPosition = endPosition;
        _phaseStartOpacity = startOpacity;
        _phaseEndOpacity = endOpacity;

        SetPosition(startPosition);
        SetOpacity(startOpacity);
    }

    private void AdvanceTransition(float elapsedSec, bool entering)
    {
        _phaseElapsedSec += elapsedSec;

        float progress = TransitionDurationSec <= 0f
            ? 1f
            : Math.Clamp(_phaseElapsedSec / TransitionDurationSec, 0f, 1f);

        Func<float, float> easing = EasingFunctions.From(
            entering ? EntranceEasing : ExitEasing);

        float eased = Math.Clamp(easing(progress), 0f, 1f);

        SetPosition(Vector2.Lerp(
            _phaseStartPosition,
            _phaseEndPosition,
            eased));

        SetOpacity(
            _phaseStartOpacity
            + (_phaseEndOpacity - _phaseStartOpacity) * eased);

        if (progress < 1f)
            return;

        if (entering)
            CompleteEntrance();
        else
            CompleteDismissal();
    }

    private void CompleteEntrance()
    {
        SetPosition(TargetBounds.X, TargetBounds.Y);
        SetOpacity(1f);
        CurrentState = ToastState.Holding;
        _phaseElapsedSec = 0f;

        if (HoldDurationSec == 0f)
            Dismiss();
    }

    private void CompleteDismissal()
    {
        Hide();
        Dismissed?.Invoke();

        if (DisposeOnDismiss)
            Dispose();
    }

    private Vector2 GetSourcePosition()
    {
        if (SourceLocationPx is { } explicitSource)
            return new Vector2(explicitSource.X, explicitSource.Y);

        Rectangle viewport = View!.Viewport.TargetRectPx;

        return SlideOrigin switch
        {
            ToastSlideOrigin.Top => new Vector2(
                TargetBounds.X,
                viewport.Top - TargetBounds.Height - SourceOffsetPx),

            ToastSlideOrigin.Right => new Vector2(
                viewport.Right + SourceOffsetPx,
                TargetBounds.Y),

            ToastSlideOrigin.Bottom => new Vector2(
                TargetBounds.X,
                viewport.Bottom + SourceOffsetPx),

            ToastSlideOrigin.Left => new Vector2(
                viewport.Left - TargetBounds.Width - SourceOffsetPx,
                TargetBounds.Y),

            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private float GetCurrentOpacity()
    {
        return Children.Count == 0
            ? 1f
            : Children[0] is DirectDrawingBase drawing
                ? drawing.Opacity
                : 1f;
    }

    private static Rectangle GetAnchoredBounds(
        View view,
        Size size,
        WidgetAnchor anchor,
        int marginPx)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        if (marginPx < 0)
            throw new ArgumentOutOfRangeException(nameof(marginPx));

        Rectangle viewport = view.Viewport.TargetRectPx;

        int x = anchor switch
        {
            WidgetAnchor.TopLeft or WidgetAnchor.CenterLeft or WidgetAnchor.BottomLeft => viewport.Left + marginPx,
            WidgetAnchor.TopCenter or WidgetAnchor.Center or WidgetAnchor.BottomCenter => viewport.Left + (viewport.Width - size.Width) / 2,
            WidgetAnchor.TopRight or WidgetAnchor.CenterRight or WidgetAnchor.BottomRight => viewport.Right - size.Width - marginPx,
            _ => throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null)
        };

        int y = anchor switch
        {
            WidgetAnchor.TopLeft or WidgetAnchor.TopCenter or WidgetAnchor.TopRight => viewport.Top + marginPx,
            WidgetAnchor.CenterLeft or WidgetAnchor.Center or WidgetAnchor.CenterRight => viewport.Top + (viewport.Height - size.Height) / 2,
            WidgetAnchor.BottomLeft or WidgetAnchor.BottomCenter or WidgetAnchor.BottomRight => viewport.Bottom - size.Height - marginPx,
            _ => throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null)
        };

        return new Rectangle(x, y, size.Width, size.Height);
    }

    private static void ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Toast bounds must have a positive width and height.");
    }
}

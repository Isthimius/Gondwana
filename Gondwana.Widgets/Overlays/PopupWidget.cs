using System.Drawing;
using System.Numerics;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Widgets.Overlays;

/// <summary>
/// Displays short-lived text or image content in view or scene-layer coordinates.
/// </summary>
/// <remarks>
/// A scene-layer popup can take its source from a <see cref="Tile"/> or fixed
/// grid cell. The source is resolved when <see cref="ShowPopup"/> is called and
/// the popup then moves independently, which is appropriate for floating damage,
/// score, and status-effect feedback.
/// </remarks>
public sealed class PopupWidget : WidgetBase
{
    private bool _disposed;
    private bool _isActive;
    private float _elapsedSec;
    private float _lifetimeSec = 1f;
    private float _fadeInSec = 0.08f;
    private float _fadeOutSec = 0.25f;
    private long _lastAnimationTick;
    private Func<Vector2> _sourceResolver;

    /// <summary>
    /// Occurs after the popup reaches the end of its configured lifetime.
    /// </summary>
    public event Action? Completed;

    /// <summary>
    /// Initializes a text popup in absolute view/screen coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle screenBounds,
        string text,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            screenBounds.Location,
            nickname)
    {
        ValidateBounds(screenBounds);

        Text = CreateText(
            renderSurfaceHost,
            view,
            screenBounds,
            text,
            $"{Nickname}.text");

        Content = Text;
        _sourceResolver = () => new Vector2(screenBounds.X, screenBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Initializes a text popup in scene-layer world coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer,
        Rectangle worldBounds,
        string text,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            worldBounds.Location,
            nickname)
    {
        ValidateBounds(worldBounds);

        Text = CreateText(
            renderSurfaceHost,
            sceneLayer,
            worldBounds,
            text,
            $"{Nickname}.text");

        Content = Text;
        _sourceResolver = () => new Vector2(worldBounds.X, worldBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Initializes a text popup whose source is resolved from a tile when shown.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        Tile source,
        Size size,
        string text,
        WidgetAnchor sourceAnchor = WidgetAnchor.Center,
        Point? offsetPx = null,
        string? nickname = null)
        : this(
            renderSurfaceHost,
            source?.SceneLayer ?? throw new ArgumentNullException(nameof(source)),
            new Rectangle(Point.Empty, size),
            text,
            nickname)
    {
        BindTo(source, sourceAnchor, offsetPx);
    }

    /// <summary>
    /// Initializes an image popup in absolute view/screen coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle screenBounds,
        SKImage image,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            screenBounds.Location,
            nickname)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateBounds(screenBounds);

        Image = new DirectImage(
            image,
            renderSurfaceHost,
            view,
            screenBounds,
            $"{Nickname}.image");

        Content = Image;
        _sourceResolver = () => new Vector2(screenBounds.X, screenBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Initializes an image popup in scene-layer world coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer,
        Rectangle worldBounds,
        SKImage image,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            worldBounds.Location,
            nickname)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateBounds(worldBounds);

        Image = new DirectImage(
            image,
            renderSurfaceHost,
            sceneLayer,
            worldBounds,
            $"{Nickname}.image");

        Content = Image;
        _sourceResolver = () => new Vector2(worldBounds.X, worldBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Initializes a bitmap popup in absolute view/screen coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle screenBounds,
        SKBitmap bitmap,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            screenBounds.Location,
            nickname)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ValidateBounds(screenBounds);

        Image = new DirectImage(
            bitmap,
            renderSurfaceHost,
            view,
            screenBounds,
            $"{Nickname}.image");

        Content = Image;
        _sourceResolver = () => new Vector2(screenBounds.X, screenBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Initializes a bitmap popup in scene-layer world coordinates.
    /// </summary>
    public PopupWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer,
        Rectangle worldBounds,
        SKBitmap bitmap,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            worldBounds.Location,
            nickname)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ValidateBounds(worldBounds);

        Image = new DirectImage(
            bitmap,
            renderSurfaceHost,
            sceneLayer,
            worldBounds,
            $"{Nickname}.image");

        Content = Image;
        _sourceResolver = () => new Vector2(worldBounds.X, worldBounds.Y);

        CompleteInitialization(Content);
    }

    /// <summary>
    /// Gets the direct drawing displayed by this popup.
    /// </summary>
    public IDirectCompositeChild Content { get; }

    /// <summary>
    /// Gets the popup's text drawing, or null for an image popup.
    /// </summary>
    public TextBlock? Text { get; }

    /// <summary>
    /// Gets the popup's image drawing, or null for a text popup.
    /// </summary>
    public DirectImage? Image { get; }

    /// <summary>
    /// Gets the tile used to resolve the popup source, when one is configured.
    /// </summary>
    public Tile? SourceTile { get; private set; }

    /// <summary>
    /// Gets the fixed grid coordinate used to resolve the popup source, when one is configured.
    /// </summary>
    public Point? SourceGridLocation { get; private set; }

    /// <summary>
    /// Gets or sets the popup lifetime in seconds.
    /// </summary>
    public float LifetimeSec
    {
        get => _lifetimeSec;
        set => _lifetimeSec = value > 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the fade-in duration in seconds.
    /// </summary>
    public float FadeInSec
    {
        get => _fadeInSec;
        set => _fadeInSec = value >= 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the fade-out duration in seconds.
    /// </summary>
    public float FadeOutSec
    {
        get => _fadeOutSec;
        set => _fadeOutSec = value >= 0f
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the initial popup velocity in pixels per second.
    /// Scene-layer popups use world pixels; view popups use screen pixels.
    /// </summary>
    public Vector2 VelocityPxPerSec { get; set; } = new(0f, -48f);

    /// <summary>
    /// Gets or sets popup acceleration in pixels per second squared.
    /// </summary>
    public Vector2 AccelerationPxPerSecSquared { get; set; }

    /// <summary>
    /// Gets or sets whether the popup disposes itself after completion.
    /// </summary>
    public bool DisposeOnComplete { get; set; } = true;

    /// <summary>
    /// Shows the popup, resolves its current source, and starts its movement and fades.
    /// </summary>
    public PopupWidget ShowPopup()
    {
        Show();
        return this;
    }

    /// <summary>
    /// Uses a fixed top-left location as the popup source.
    /// </summary>
    public PopupWidget SetSourceLocation(Point locationPx)
    {
        SourceTile = null;
        SourceGridLocation = null;
        _sourceResolver = () => new Vector2(locationPx.X, locationPx.Y);
        SetPosition(locationPx.X, locationPx.Y);
        return this;
    }

    /// <summary>
    /// Resolves the popup source from an anchor point on a tile when the popup is shown.
    /// </summary>
    public PopupWidget BindTo(
        Tile source,
        WidgetAnchor sourceAnchor = WidgetAnchor.Center,
        Point? offsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Mode != DirectDrawingMode.SceneLayer)
            throw new InvalidOperationException("Only a scene-layer popup can bind to a tile.");

        if (!ReferenceEquals(SceneLayer, source.SceneLayer))
            throw new ArgumentException("The source tile must belong to the popup's SceneLayer.", nameof(source));

        Point offset = offsetPx ?? Point.Empty;

        SourceTile = source;
        SourceGridLocation = source is SceneLayerTile fixedTile
            ? fixedTile.GridCoordinatesAbs
            : null;

        _sourceResolver = () => GetCenteredSourcePosition(
            source.DrawLocationWorld,
            GetContentSize(),
            sourceAnchor,
            offset);

        SetPosition(_sourceResolver());
        return this;
    }

    /// <summary>
    /// Resolves the popup source from a fixed grid cell when the popup is shown.
    /// </summary>
    public PopupWidget BindTo(
        SceneLayer sceneLayer,
        Point gridLocation,
        WidgetAnchor sourceAnchor = WidgetAnchor.Center,
        Point? offsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        SceneLayerTile tile = sceneLayer[gridLocation]
            ?? throw new ArgumentOutOfRangeException(nameof(gridLocation), "The grid location is outside the SceneLayer.");

        BindTo(tile, sourceAnchor, offsetPx);
        SourceGridLocation = gridLocation;
        return this;
    }

    /// <summary>
    /// Sets the popup text. Throws when this is an image popup.
    /// </summary>
    public PopupWidget SetText(string text)
    {
        if (Text is null)
            throw new InvalidOperationException("This popup contains an image, not text.");

        Text.SetText(text);
        return this;
    }

    /// <summary>
    /// Sets the popup font. Throws when this is an image popup.
    /// </summary>
    public PopupWidget SetFont(
        SKTypeface typeface,
        float size,
        float? minSize = null)
    {
        if (Text is null)
            throw new InvalidOperationException("This popup contains an image, not text.");

        Text.SetFont(typeface, size, minSize);
        return this;
    }

    /// <summary>
    /// Sets the popup text color. Throws when this is an image popup.
    /// </summary>
    public PopupWidget SetTextColor(SKColor color)
    {
        if (Text is null)
            throw new InvalidOperationException("This popup contains an image, not text.");

        Text.SetColors(color, SKColors.Transparent);
        return this;
    }

    /// <summary>
    /// Sets the popup Z-order.
    /// </summary>
    public PopupWidget SetPopupZOrder(int zOrder)
    {
        Content.SetZOrder(zOrder);
        return this;
    }

    /// <summary>
    /// Completes and hides the popup immediately.
    /// </summary>
    public void Dismiss()
    {
        if (!_isActive || _disposed)
            return;

        CompletePopup();
    }

    /// <inheritdoc/>
    public override void Update(long tick)
    {
        if (_disposed || tick <= _lastAnimationTick)
            return;

        float elapsedSec = HighResTimer.GetDuration(_lastAnimationTick, tick);
        _lastAnimationTick = tick;

        base.Update(tick);

        if (!_isActive)
            return;

        _elapsedSec += elapsedSec;

        if (_elapsedSec >= LifetimeSec)
        {
            CompletePopup();
            return;
        }

        float opacity = 1f;

        if (FadeInSec > 0f && _elapsedSec < FadeInSec)
            opacity = Math.Clamp(_elapsedSec / FadeInSec, 0f, 1f);

        float remainingSec = LifetimeSec - _elapsedSec;

        if (FadeOutSec > 0f && remainingSec < FadeOutSec)
            opacity = Math.Min(opacity, Math.Clamp(remainingSec / FadeOutSec, 0f, 1f));

        SetOpacity(opacity);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Completed = null;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        _elapsedSec = 0f;
        _lastAnimationTick = HighResTimer.GetCurrentTick();
        _isActive = true;

        Movement.StopAllMovement();
        SetPosition(_sourceResolver());
        SetOpacity(FadeInSec > 0f ? 0f : 1f);
        Movement.SetVelocity(VelocityPxPerSec);
        Movement.SetAcceleration(AccelerationPxPerSecSquared);
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        _isActive = false;
        Movement.StopAllMovement();
        base.ProcessHidden();
    }

    private void CompleteInitialization(IDirectCompositeChild content)
    {
        IsInputEnabled = false;
        IsPointerInputEnabled = false;

        Add(
            content,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: Vector2.Zero);

        SetPopupZOrder(int.MaxValue - 50);
        SetIsVisible(false);
    }

    private void CompletePopup()
    {
        Hide();
        Completed?.Invoke();

        if (DisposeOnComplete)
            Dispose();
    }

    private Size GetContentSize()
    {
        Rectangle bounds = Mode == DirectDrawingMode.View
            ? Content.ScreenBounds
            : Content.WorldBounds;

        return bounds.Size;
    }

    private static Vector2 GetCenteredSourcePosition(
        Rectangle sourceBounds,
        Size contentSize,
        WidgetAnchor sourceAnchor,
        Point offsetPx)
    {
        PointF anchor = sourceAnchor.GetPoint(sourceBounds);

        return new Vector2(
            anchor.X - contentSize.Width / 2f + offsetPx.X,
            anchor.Y - contentSize.Height / 2f + offsetPx.Y);
    }

    private static TextBlock CreateText(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds,
        string text,
        string nickname)
    {
        return new TextBlock(
                host,
                view,
                bounds,
                nickname)
            .SetText(text)
            .SetFont(SKTypeface.Default, 22f, minSize: 10f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow(true)
            .UseOutline(true);
    }

    private static TextBlock CreateText(
        RenderSurfaceHostBase host,
        SceneLayer sceneLayer,
        Rectangle bounds,
        string text,
        string nickname)
    {
        return new TextBlock(
                host,
                sceneLayer,
                view: null,
                worldBounds: bounds,
                nickname: nickname)
            .SetText(text)
            .SetFont(SKTypeface.Default, 22f, minSize: 10f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow(true)
            .UseOutline(true);
    }

    private static void ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Popup bounds must have a positive width and height.");
    }
}

using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Provides the base implementation for direct drawing objects that can be rendered to the backbuffer
/// with support for opacity, fade transitions, reveal animations, and flexible positioning modes.
/// </summary>
/// <remarks>
/// <para>
/// DirectDrawingBase serves as the foundation for all custom drawing operations in the Gondwana engine.
/// Unlike sprites and tiles which are managed by scene layers, direct drawings provide immediate control
/// over rendering with support for advanced visual effects.
/// </para>
/// <para>
/// Direct drawings can operate in two modes:
/// <list type="bullet">
/// <item><description><see cref="DirectDrawingMode.SceneLayer"/> - Positioned in world coordinates relative to a specific scene layer, affected by camera and parallax.</description></item>
/// <item><description><see cref="DirectDrawingMode.View"/> - Positioned in screen coordinates relative to a view, unaffected by camera movement (ideal for UI overlays).</description></item>
/// </list>
/// </para>
/// <para>
/// This class is abstract. Derived classes must implement <see cref="OnDraw"/> to perform the actual
/// drawing operations. The base class handles visibility, opacity, fade transitions, reveal animations,
/// and dirty-rectangle management.
/// </para>
/// <para>
/// Thread safety: This class is not thread-safe. All operations should be performed on the UI thread.
/// </para>
/// </remarks>
public abstract class DirectDrawingBase : IDirectDrawable, IComparable<DirectDrawingBase>
{
    /// <summary>
    /// Occurs when this direct drawing is being disposed.
    /// </summary>
    /// <remarks>
    /// Subscribe to this event to perform cleanup operations when the direct drawing is removed.
    /// The event is raised before the object is fully disposed.
    /// </remarks>
    public event EventHandler<IDirectDrawable>? Disposing;

    /// <summary>
    /// Occurs when a fade transition initiated by <see cref="FadeTo"/>, <see cref="FadeIn"/>,
    /// or <see cref="FadeOut"/> completes.
    /// </summary>
    /// <remarks>
    /// This event is raised on the frame where the fade animation reaches its target opacity.
    /// Use this to chain animations or trigger logic after fade transitions.
    /// </remarks>
    public event EventHandler<DirectDrawingBase>? FadeToCompleted;

    /// <summary>
    /// The render surface host that manages this direct drawing's rendering pipeline.
    /// </summary>
    protected readonly RenderSurfaceHostBase _renderSurfaceHost;

    /// <summary>
    /// The current screen-space bounds for view-mode direct drawings, in pixels.
    /// </summary>
    protected Rectangle _screenBounds;

    /// <summary>
    /// The current world-space bounds for scene-layer-mode direct drawings, in pixels.
    /// </summary>
    protected Rectangle _worldBounds;

    /// <summary>
    /// The Z-order used for draw sorting. Higher values draw later (on top).
    /// </summary>
    protected int _zOrder;

    /// <summary>
    /// Indicates whether this direct drawing is currently visible and should be rendered.
    /// </summary>
    protected bool _visible;

    /// <summary>
    /// The last engine tick value used for delta-time calculations in <see cref="Update"/>.
    /// </summary>
    protected long _lastTick = HighResTimer.GetCurrentTick();

    private bool _disposed = false;

    // Fade/opacity state
    private float _opacity = 1f;                 // 0..1
    private float _fadeFrom, _fadeTo;
    private float _fadeDurationSec, _fadeElapsedSec;
    private bool _isFading;

    /// <summary>
    /// Gets or sets a value indicating whether this direct drawing should be automatically hidden
    /// when its opacity reaches zero during fade transitions.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to automatically hide when fully transparent (default);
    /// <see langword="false"/> to keep the object visible even at zero opacity.
    /// </value>
    /// <remarks>
    /// When enabled, setting <see cref="Opacity"/> to 0 or completing a <see cref="FadeOut"/>
    /// will set <see cref="Visible"/> to <see langword="false"/>. Conversely, increasing opacity
    /// above zero will restore visibility.
    /// </remarks>
    public bool HideWhenFullyTransparent { get; set; } = true;

    // Reveal state
    private float _revealT = 1f;                 // 0 = hidden, 1 = fully shown
    private RevealDirection _revealDir = RevealDirection.LeftToRight;

    // optional tween state
    private bool _revealAnimating;
    private float _revealElapsedSec, _revealDurationSec;
    private Func<float, float>? _revealEasing;
    private float _revealStart = 1f, _revealTarget = 1f;

    /// <summary>
    /// Performs the concrete drawing for this direct drawing to the backbuffer.
    /// </summary>
    /// <param name="backbuffer">The backbuffer providing the canvas and rendering context.</param>
    /// <param name="destRectScreen">
    /// The destination rectangle in screen pixel coordinates where this drawing should be rendered.
    /// For scene-layer mode, this is the world bounds transformed to screen space.
    /// For view mode, this is the screen bounds directly.
    /// </param>
    /// <remarks>
    /// <para>
    /// Override this method in derived classes to implement custom drawing logic using SkiaSharp's
    /// <see cref="SKCanvas"/> (accessible via <c>backbuffer.Canvas</c>).
    /// </para>
    /// <para>
    /// Do not call this method directly. The engine calls it via <see cref="Draw"/> after applying
    /// visibility checks, opacity blending, and reveal clipping. The canvas state is managed by
    /// the base class; derived implementations should not call <c>Save()</c> or <c>Restore()</c>
    /// unless paired within the method.
    /// </para>
    /// <para>
    /// The destination rectangle provided is already transformed to screen space and clipped to
    /// the appropriate viewport or reveal region.
    /// </para>
    /// </remarks>
    protected abstract void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen);

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectDrawingBase"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this direct drawing. Must not be <see langword="null"/>.</param>
    /// <param name="mode">The drawing mode determining how this object is positioned and transformed.</param>
    /// <param name="sceneLayer">The scene layer to which this drawing is attached (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/>).</param>
    /// <param name="view">The view to which this drawing is attached (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/>).</param>
    /// <param name="screenBounds">The screen-space bounds in pixels (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/>).</param>
    /// <param name="worldBounds">The world-space bounds in pixels (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/>).</param>
    /// <param name="nickname">An optional human-readable name for this direct drawing, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description><paramref name="sceneLayer"/> is <see langword="null"/> and <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/></description></item>
    /// <item><description><paramref name="view"/> is <see langword="null"/> and <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/></description></item>
    /// <item><description><paramref name="worldBounds"/> is <see langword="null"/> and <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/></description></item>
    /// <item><description><paramref name="screenBounds"/> is <see langword="null"/> and <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/></description></item>
    /// </list>
    /// </exception>
    /// <remarks>
    /// The constructor automatically registers this direct drawing with the <see cref="DirectDrawingManager"/>
    /// and marks the appropriate regions as dirty to ensure the drawing appears on the next frame.
    /// </remarks>
    protected DirectDrawingBase(RenderSurfaceHostBase renderSurfaceHost,
                                DirectDrawingMode mode,
                                SceneLayer? sceneLayer,
                                View? view,
                                Rectangle? screenBounds,
                                Rectangle? worldBounds,
                                string? nickname = null)
    {
        if (renderSurfaceHost is null)
            throw new ArgumentNullException(nameof(renderSurfaceHost));

        if (mode == DirectDrawingMode.SceneLayer && sceneLayer is null)
            throw new ArgumentException("SceneLayer cannot be null when using DirectDrawingMode.SceneLayer", nameof(sceneLayer));

        if (mode == DirectDrawingMode.View && view is null)
            throw new ArgumentException("View cannot be null when using DirectDrawingMode.View", nameof(view));

        if (mode == DirectDrawingMode.SceneLayer && worldBounds is null)
            throw new ArgumentException("worldBounds cannot be null when using DirectDrawingMode.SceneLayer", nameof(worldBounds));

        if (mode == DirectDrawingMode.View && screenBounds is null)
            throw new ArgumentException("screenBounds cannot be null when using DirectDrawingMode.View", nameof(screenBounds));

        _renderSurfaceHost = renderSurfaceHost;
        _zOrder = 0;
        _visible = true;
        Mode = mode;
        SceneLayer = sceneLayer;
        View = view;
        Nickname = nickname ?? Id.ToString();

        if (mode == DirectDrawingMode.SceneLayer)
        {
            _worldBounds = worldBounds!.Value;
            _screenBounds = Rectangle.Empty;
        }
        else // View
        {
            _worldBounds = Rectangle.Empty;
            _screenBounds = screenBounds!.Value;
            _screenBounds.Intersect(view!.Viewport.TargetRectPx);
        }

        DirectDrawingManager.Instance.AddOrReplace(this);
        ForceRefresh();
    }

    ~DirectDrawingBase() => Dispose(false);

    /// <summary>
    /// Gets the render surface host that manages this direct drawing's rendering pipeline.
    /// </summary>
    /// <value>
    /// The <see cref="RenderSurfaceHostBase"/> instance responsible for coordinating rendering,
    /// view management, and backbuffer operations for this direct drawing.
    /// </value>
    public RenderSurfaceHostBase RenderSurfaceHost => _renderSurfaceHost;

    /// <summary>
    /// Gets the drawing mode that determines how this direct drawing is positioned and transformed.
    /// </summary>
    /// <value>
    /// A <see cref="DirectDrawingMode"/> value indicating whether this drawing uses world coordinates
    /// (scene-layer mode) or screen coordinates (view mode).
    /// </value>
    /// <remarks>
    /// The mode is set during construction and cannot be changed. Scene-layer mode drawings move
    /// with the camera and are affected by parallax, while view mode drawings remain fixed in
    /// screen space, ideal for UI overlays.
    /// </remarks>
    public DirectDrawingMode Mode { get; }

    /// <summary>
    /// Gets the scene layer to which this direct drawing is attached, or <see langword="null"/>
    /// if the drawing is in view mode.
    /// </summary>
    /// <value>
    /// The <see cref="SceneLayer"/> instance for scene-layer mode drawings;
    /// <see langword="null"/> for view mode drawings.
    /// </value>
    /// <remarks>
    /// Scene-layer mode drawings are positioned in the world coordinate space of the specified layer
    /// and are affected by the layer's parallax factor and the view's camera position.
    /// </remarks>
    public SceneLayer? SceneLayer { get; private set; }

    /// <summary>
    /// Gets the view to which this direct drawing is attached, or <see langword="null"/>
    /// if the drawing is in scene-layer mode.
    /// </summary>
    /// <value>
    /// The <see cref="View"/> instance for view mode drawings;
    /// <see langword="null"/> for scene-layer mode drawings.
    /// </value>
    /// <remarks>
    /// View mode drawings are positioned in screen coordinates relative to the specified view's viewport
    /// and remain fixed on screen regardless of camera movement.
    /// </remarks>
    public View? View { get; private set; }

    /// <summary>
    /// Gets or sets the screen-space bounds of this direct drawing in pixels.
    /// </summary>
    /// <value>
    /// A <see cref="Rectangle"/> defining the position and size in screen coordinates.
    /// Returns <see cref="Rectangle.Empty"/> for scene-layer mode drawings.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property is only meaningful for view mode drawings (<see cref="Mode"/> is
    /// <see cref="DirectDrawingMode.View"/>). Setting this property for scene-layer mode
    /// drawings has no effect.
    /// </para>
    /// <para>
    /// When set, the affected screen regions are automatically marked as dirty to ensure
    /// the drawing is rerendered at the new position. The bounds are clipped to the view's
    /// viewport rectangle.
    /// </para>
    /// </remarks>
    public Rectangle ScreenBounds
    {
        get => _screenBounds;
        set
        {
            if (Mode != DirectDrawingMode.View)
                return;

            ForceRefresh();
            _screenBounds = value;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the world-space bounds of this direct drawing in pixels.
    /// </summary>
    /// <value>
    /// A <see cref="Rectangle"/> defining the position and size in world coordinates.
    /// Returns <see cref="Rectangle.Empty"/> for view mode drawings.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property is only meaningful for scene-layer mode drawings (<see cref="Mode"/> is
    /// <see cref="DirectDrawingMode.SceneLayer"/>). Setting this property for view mode
    /// drawings has no effect.
    /// </para>
    /// <para>
    /// When set, the affected world regions are automatically marked as dirty to ensure
    /// the drawing is rerendered at the new position. The bounds are subject to the layer's
    /// parallax factor and the view's camera transformation.
    /// </para>
    /// </remarks>
    public Rectangle WorldBounds
    {
        get => _worldBounds;
        set
        {
            if (Mode != DirectDrawingMode.SceneLayer)
                return;

            ForceRefresh();
            _worldBounds = value;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets the extensible value bag for storing arbitrary key-value data associated with this direct drawing.
    /// </summary>
    /// <value>
    /// A <see cref="TypedValueBag"/> instance for storing custom metadata, state, or configuration.
    /// </value>
    /// <remarks>
    /// Use the value bag to attach game-specific data to direct drawings without modifying the base class.
    /// Values are strongly typed and accessed via <see cref="ValueKey{T}"/> instances.
    /// </remarks>
    public TypedValueBag ValueBag { get; } = new();

    #region IDrawable members

    /// <summary>
    /// Gets the unique identifier for this direct drawing.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this instance across the application lifetime.
    /// </value>
    /// <remarks>
    /// The ID is generated during construction and remains constant throughout the object's lifetime.
    /// Use this for tracking, lookup, or equality comparisons when reference equality is not suitable.
    /// </remarks>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the optional human-readable name for this direct drawing.
    /// </summary>
    /// <value>
    /// A string nickname for debugging and identification purposes, or <see langword="null"/> if not set.
    /// </value>
    /// <remarks>
    /// Nicknames are useful for logging, debugging, and identifying objects in diagnostic output.
    /// They are not required to be unique.
    /// </remarks>
    public string? Nickname { get; private set; }

    /// <summary>
    /// Gets or sets the Z-order (depth) used for sorting this direct drawing relative to other drawables.
    /// </summary>
    /// <value>
    /// An integer Z-order value. Higher values are drawn later and appear on top of lower values.
    /// Default is 0.
    /// </value>
    /// <remarks>
    /// When multiple direct drawings overlap, Z-order determines the draw sequence. Objects with
    /// identical Z-order values are drawn in the order they were added to the manager. Changing
    /// the Z-order marks the affected regions as dirty to ensure correct layering on the next frame.
    /// </remarks>
    public int ZOrder
    {
        get => _zOrder;
        set
        {
            if (_zOrder != value)
            {
                _zOrder = value;
                ForceRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this direct drawing is visible and should be rendered.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if this drawing should be rendered; <see langword="false"/> to skip rendering.
    /// Default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// When set to <see langword="false"/>, <see cref="Draw"/> returns immediately without rendering.
    /// The affected regions are marked as dirty to ensure the area is cleared on the next frame.
    /// </para>
    /// <para>
    /// If <see cref="HideWhenFullyTransparent"/> is enabled, visibility may be automatically managed
    /// during fade transitions based on the <see cref="Opacity"/> value.
    /// </para>
    /// </remarks>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                ForceRefresh();
            }
        }
    }

    /// <summary>
    /// Gets the screen-space drawing location for this direct drawing as seen from the specified view.
    /// </summary>
    /// <param name="view">The view from which to calculate the screen location.</param>
    /// <returns>
    /// A <see cref="RectangleF"/> in screen pixel coordinates indicating where this drawing will be rendered.
    /// For view mode drawings, this returns <see cref="ScreenBounds"/> directly.
    /// For scene-layer mode drawings, this returns the <see cref="WorldBounds"/> transformed to screen space
    /// using the view's camera and the layer's parallax.
    /// </returns>
    /// <remarks>
    /// This method is used internally by the rendering pipeline to determine the destination rectangle
    /// for drawing operations. It accounts for camera position, zoom, and parallax for scene-layer mode drawings.
    /// </remarks>
    public RectangleF GetDrawLocationScreen(View view)
    {
        // View mode already returns _screenBounds
        if (Mode == DirectDrawingMode.View)
            return _screenBounds;

        // translate world bounds to screen via view transform
        return view.WorldRectToScreenRect(SceneLayer!, _worldBounds);
    }

    /// <summary>
    /// Renders this direct drawing to the backbuffer at the specified screen location.
    /// </summary>
    /// <param name="backbuffer">The backbuffer providing the canvas and rendering context.</param>
    /// <param name="destRectScreen">The destination rectangle in screen pixel coordinates.</param>
    /// <remarks>
    /// <para>
    /// This is the engine's entry point for rendering a direct drawing. It handles visibility checks,
    /// opacity blending via <see cref="SKPaint"/> with layer support, and reveal clipping before
    /// delegating to <see cref="OnDraw"/> for the actual drawing logic.
    /// </para>
    /// <para>
    /// Do not call this method directly from game code. The rendering pipeline invokes it automatically
    /// during the render pass. To trigger a redraw, call <see cref="ForceRefresh"/> or modify properties
    /// that affect appearance.
    /// </para>
    /// <para>
    /// The method applies the following operations in order:
    /// <list type="number">
    /// <item><description>Visibility check - returns early if <see cref="Visible"/> is <see langword="false"/>.</description></item>
    /// <item><description>Reveal clipping - applies directional clipping based on <see cref="SetReveal"/> and <see cref="SetRevealDirection"/>.</description></item>
    /// <item><description>Opacity blending - applies <see cref="Opacity"/> using canvas layer if less than 1.0.</description></item>
    /// <item><description>Calls <see cref="OnDraw"/> to perform the actual rendering.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Draw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        if (!Visible)
            return;

        var canvas = backbuffer.Canvas;

        // Compute reveal clip rect (screen pixel space) from bounds
        bool useClip = _revealT < 0.999f;

        if (useClip)
        {
            var r = new SKRect(destRectScreen.Left, destRectScreen.Top, destRectScreen.Right, destRectScreen.Bottom);

            SKRect clipRect = _revealDir switch
            {
                RevealDirection.LeftToRight => new SKRect(r.Left, r.Top, r.Left + r.Width * _revealT, r.Bottom),
                RevealDirection.RightToLeft => new SKRect(r.Right - r.Width * _revealT, r.Top, r.Right, r.Bottom),
                RevealDirection.TopToBottom => new SKRect(r.Left, r.Top, r.Right, r.Top + r.Height * _revealT),
                RevealDirection.BottomToTop => new SKRect(r.Left, r.Bottom - r.Height * _revealT, r.Right, r.Bottom),
                _ => r
            };

            // Early-out if reveal window is empty
            if (clipRect.Width <= 0f || clipRect.Height <= 0f)
                return;

            // Outer save owns the clip lifetime
            canvas.Save();

            // Capture current matrix
            var m = canvas.TotalMatrix;

            canvas.ResetMatrix();
            canvas.ClipRect(clipRect, SKClipOperation.Intersect, antialias: false);

            // Put the prior matrix back so Draw() sees the same transform state
            canvas.SetMatrix(m);
        }

        if (_opacity >= 0.999f)
        {
            OnDraw(backbuffer, destRectScreen);
        }
        else
        {
            using var layerPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, (byte)(_opacity * 255))
            };

            canvas.SaveLayer(destRectScreen.ToSKRect(), layerPaint);
            OnDraw(backbuffer, destRectScreen);
            canvas.Restore(); // end SaveLayer
        }

        if (useClip)
            canvas.Restore(); // end outer clip Save
    }

    #endregion IDrawable members

    /// <summary>
    /// Gets or sets the current opacity of this direct drawing.
    /// </summary>
    /// <value>
    /// A floating-point value between 0.0 (fully transparent) and 1.0 (fully opaque).
    /// Default is 1.0.
    /// </value>
    /// <remarks>
    /// <para>
    /// Setting opacity automatically marks the affected regions as dirty. Values outside the 0..1
    /// range are clamped. Very small changes (less than 0.0001) are ignored to avoid unnecessary redraws.
    /// </para>
    /// <para>
    /// If <see cref="HideWhenFullyTransparent"/> is enabled:
    /// <list type="bullet">
    /// <item><description>Setting opacity to 0 automatically sets <see cref="Visible"/> to <see langword="false"/>.</description></item>
    /// <item><description>Increasing opacity above 0 automatically sets <see cref="Visible"/> to <see langword="true"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For animated opacity transitions, use <see cref="FadeTo"/>, <see cref="FadeIn"/>, or <see cref="FadeOut"/>.
    /// </para>
    /// </remarks>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(clamped - _opacity) < 0.0001f)
                return;

            _opacity = clamped;

            if (HideWhenFullyTransparent && _opacity <= 0f)
                Visible = false;
            else if (_opacity > 0f)
                Visible = true;

            ForceRefresh();
        }
    }

    /// <summary>
    /// Instantly sets the opacity to the specified value without animation.
    /// </summary>
    /// <param name="opacity">The target opacity value (0.0 to 1.0).</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// This is equivalent to setting the <see cref="Opacity"/> property directly. It is provided
    /// for fluent-style API usage and method chaining. The value is clamped to the 0..1 range.
    /// </remarks>
    public DirectDrawingBase SetOpacity(float opacity)
    {
        Opacity = opacity;
        return this;
    }

    /// <summary>
    /// Initiates a smooth fade transition to the specified target opacity over the given duration.
    /// </summary>
    /// <param name="targetOpacity">The target opacity value (0.0 to 1.0).</param>
    /// <param name="durationSec">The duration of the fade in seconds. Minimum value is 0.0001 seconds.</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The fade animation uses linear interpolation and is updated each frame by <see cref="Update"/>.
    /// When the fade completes, the <see cref="FadeToCompleted"/> event is raised.
    /// </para>
    /// <para>
    /// If the target opacity is greater than zero, <see cref="Visible"/> is automatically set to
    /// <see langword="true"/> to ensure the object is rendered during the fade. If
    /// <see cref="HideWhenFullyTransparent"/> is enabled and the target is zero, the object will
    /// be hidden when the fade completes.
    /// </para>
    /// <para>
    /// Starting a new fade cancels any previous fade operation. To cancel a fade without starting
    /// a new one, call <see cref="CancelFade"/>.
    /// </para>
    /// </remarks>
    public DirectDrawingBase FadeTo(float targetOpacity, float durationSec)
    {
        _fadeFrom = _opacity;
        _fadeTo = Math.Clamp(targetOpacity, 0f, 1f);
        _fadeDurationSec = Math.Max(0.0001f, durationSec);
        _fadeElapsedSec = 0f;
        _isFading = true;

        if (_fadeTo > 0f)
            Visible = true; // ensure we draw during fade-in

        ForceRefresh();

        return this;
    }

    /// <summary>
    /// Fades this direct drawing from its current opacity to fully opaque over the specified duration.
    /// </summary>
    /// <param name="durationSec">The duration of the fade-in in seconds.</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// This is a convenience method equivalent to calling <c>FadeTo(1.0f, durationSec)</c>.
    /// If the current opacity is already zero or very close to zero, it is set to exactly 0
    /// before starting the fade to ensure a clean transition from invisible to visible.
    /// </remarks>
    public DirectDrawingBase FadeIn(float durationSec)
    {
        if (_opacity <= 0f)
            Opacity = 0f;

        return FadeTo(1f, durationSec);
    }

    /// <summary>
    /// Fades this direct drawing from its current opacity to fully transparent over the specified duration.
    /// </summary>
    /// <param name="durationSec">The duration of the fade-out in seconds.</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method equivalent to calling <c>FadeTo(0.0f, durationSec)</c>.
    /// If <see cref="HideWhenFullyTransparent"/> is enabled, the object will be automatically
    /// hidden when the fade completes.
    /// </para>
    /// <para>
    /// Use this for smooth disappearance effects. Combine with the <see cref="FadeToCompleted"/>
    /// event to perform cleanup or trigger subsequent logic after the fade-out finishes.
    /// </para>
    /// </remarks>
    public DirectDrawingBase FadeOut(float durationSec)
    {
        return FadeTo(0f, durationSec);
    }

    /// <summary>
    /// Cancels any active fade transition, leaving the opacity at its current value.
    /// </summary>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// After calling this method, the fade animation stops immediately and <see cref="Opacity"/>
    /// remains at its current value. The <see cref="FadeToCompleted"/> event will not be raised
    /// for the cancelled fade.
    /// </remarks>
    public DirectDrawingBase CancelFade()
    {
        _isFading = false;
        return this;
    }

    /// <summary>
    /// Instantly sets the reveal progress to the specified value without animation.
    /// </summary>
    /// <param name="t01">The reveal progress from 0.0 (fully hidden) to 1.0 (fully shown).</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The reveal effect progressively clips the rendering based on the <see cref="SetRevealDirection"/>.
    /// At 0.0, the entire drawing is clipped; at 1.0, the full drawing is visible. Values are clamped
    /// to the 0..1 range.
    /// </para>
    /// <para>
    /// This method sets the reveal progress immediately. For animated reveal transitions, use
    /// <see cref="RevealTo"/>.
    /// </para>
    /// </remarks>
    public DirectDrawingBase SetReveal(float t01)
    {
        _revealT = Math.Clamp(t01, 0f, 1f);
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the direction from which the reveal animation progresses.
    /// </summary>
    /// <param name="dir">The reveal direction (left-to-right, right-to-left, top-to-bottom, or bottom-to-top).</param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// The reveal direction determines how the clip rectangle grows as the reveal progresses from 0 to 1.
    /// For example, <see cref="RevealDirection.LeftToRight"/> reveals the drawing from the left edge first,
    /// progressively showing more to the right as the reveal value increases.
    /// </remarks>
    public DirectDrawingBase SetRevealDirection(RevealDirection dir)
    {
        _revealDir = dir;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Initiates a smooth reveal animation to the specified target progress over the given duration,
    /// optionally using a custom easing function.
    /// </summary>
    /// <param name="t01">The target reveal progress from 0.0 (fully hidden) to 1.0 (fully shown).</param>
    /// <param name="durationSec">The duration of the reveal animation in seconds. Minimum value is 0.0001 seconds.</param>
    /// <param name="easing">
    /// An optional easing function that transforms the linear progress (0..1 input) to the desired
    /// interpolation curve (0..1 output). If <see langword="null"/>, linear interpolation is used.
    /// </param>
    /// <returns>This <see cref="DirectDrawingBase"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The reveal animation is updated each frame by <see cref="Update"/>. The easing function,
    /// if provided, allows custom animation curves such as ease-in, ease-out, or elastic effects.
    /// </para>
    /// <para>
    /// Starting a new reveal animation cancels any previous reveal operation. The animation starts
    /// from the current reveal value and interpolates to the target.
    /// </para>
    /// </remarks>
    public DirectDrawingBase RevealTo(float t01, float durationSec, Func<float, float>? easing = null)
    {
        _revealAnimating = true;
        _revealElapsedSec = 0f;
        _revealDurationSec = Math.Max(0.0001f, durationSec);
        _revealEasing = easing;
        // target is t01; we'll lerp in Update
        _revealTarget = Math.Clamp(t01, 0f, 1f);
        _revealStart = _revealT;
        return this;
    }

    /// <summary>
    /// Marks the regions occupied by this direct drawing as dirty, forcing a redraw on the next frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is called internally when properties affecting appearance change (such as position,
    /// visibility, or opacity). It enqueues the appropriate dirty rectangles based on the drawing mode:
    /// </para>
    /// <para>
    /// <list type="bullet">
    /// <item><description><see cref="DirectDrawingMode.SceneLayer"/> - Adds the <see cref="WorldBounds"/> to the scene layer's refresh queue.</description></item>
    /// <item><description><see cref="DirectDrawingMode.View"/> - Adds the <see cref="ScreenBounds"/> to all scene layers' refresh queues for the associated view.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Call this method explicitly if you modify state that affects rendering outside of the provided
    /// properties (for example, if you change custom data that influences <see cref="OnDraw"/>).
    /// </para>
    /// </remarks>
    protected internal void ForceRefresh()
    {
        switch (Mode)
        {
            case DirectDrawingMode.SceneLayer:
                // bounds is WORLD-space
                // there will only be one SceneLayer per DirectDrawing in this mode
                SceneLayer!.RefreshQueue.AddWorldRect(_worldBounds);
                break;

            case DirectDrawingMode.View:
                // bounds is SCREEN-space
                // need to cycle through all SceneLayers for the View to which this DirectDrawing belongs
                foreach (var sceneLayer in RenderSurfaceHost.Scene.SceneLayers)
                {
                    sceneLayer.RefreshQueue.AddViewScreenRect(View!, sceneLayer, _screenBounds);
                }
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Performs per-frame update logic for this direct drawing, including fade and reveal animations.
    /// </summary>
    /// <param name="tick">The current engine tick value from <see cref="HighResTimer"/>.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically by the <see cref="DirectDrawingManager"/> each frame.
    /// The base implementation advances active fade and reveal animations. Override this method
    /// in derived classes to add custom per-frame logic (such as animation or physics updates).
    /// </para>
    /// <para>
    /// Always call <c>base.Update(tick)</c> from overridden implementations to ensure fade and
    /// reveal animations continue to function correctly.
    /// </para>
    /// <para>
    /// The method calculates delta time using <see cref="HighResTimer"/> and the previous tick
    /// stored in <see cref="_lastTick"/>. If the tick value is less than or equal to the last
    /// tick (which can occur if time is manipulated), the method returns immediately without
    /// updating state.
    /// </para>
    /// </remarks>
    public virtual void Update(long tick)
    {
        if (tick <= _lastTick)
            return;

        // Advance fade tween
        if (_isFading)
        {
            float dt = HighResTimer.GetDuration(_lastTick, tick);

            _fadeElapsedSec += dt;
            float timeElapsed = Math.Clamp(_fadeElapsedSec / _fadeDurationSec, 0f, 1f);
            // linear; swap in easing if you like
            _opacity = _fadeFrom + (_fadeTo - _fadeFrom) * timeElapsed;

            if (HideWhenFullyTransparent)
                Visible = _opacity > 0f; // hides when hit zero

            ForceRefresh();

            if (timeElapsed >= 1f)
            {
                _isFading = false;
                FadeToCompleted?.Invoke(this, this);
            }
        }

        // Advance reveal tween
        if (_revealAnimating)
        {
            float dt = HighResTimer.GetDuration(_lastTick, tick);
            _revealElapsedSec = Math.Min(_revealElapsedSec + dt, _revealDurationSec);

            float u = _revealElapsedSec / _revealDurationSec;
            _revealT = (_revealEasing is null ? u : _revealEasing(u));
            _revealT = _revealStart + (_revealTarget - _revealStart) * _revealT;

            ForceRefresh();

            if (_revealElapsedSec >= _revealDurationSec)
                _revealAnimating = false;
        }

        _lastTick = tick;
    }

    /// <summary>
    /// Compares this direct drawing to another for sorting by <see cref="ZOrder"/>.
    /// </summary>
    /// <param name="other">The other direct drawing to compare, or <see langword="null"/>.</param>
    /// <returns>
    /// A negative value if this drawing's Z-order is less than <paramref name="other"/>;
    /// zero if they are equal; a positive value if this drawing's Z-order is greater.
    /// <see langword="null"/> is considered to have a Z-order of 0.
    /// </returns>
    /// <remarks>
    /// This method is used to sort direct drawings during rendering. Higher Z-order values are
    /// drawn later and appear on top of lower values.
    /// </remarks>
    public int CompareTo(DirectDrawingBase? other) => _zOrder.CompareTo(other?._zOrder ?? 0);

    #region IDisposable members

    /// <summary>
    /// Releases all resources used by this <see cref="DirectDrawingBase"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method unregisters the direct drawing from the <see cref="DirectDrawingManager"/>,
    /// marks affected regions as dirty, and raises the <see cref="Disposing"/> event.
    /// After disposal, the object should not be used.
    /// </para>
    /// <para>
    /// Calling <see cref="Dispose()"/> multiple times is safe and has no additional effect.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this <see cref="DirectDrawingBase"/> instance.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources (called from finalizer).
    /// </param>
    /// <remarks>
    /// <para>
    /// When <paramref name="disposing"/> is <see langword="true"/>, this method:
    /// <list type="bullet">
    /// <item><description>Marks affected regions as dirty by calling <see cref="ForceRefresh"/>.</description></item>
    /// <item><description>Raises the <see cref="Disposing"/> event.</description></item>
    /// <item><description>Clears all event handlers to prevent memory leaks.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Override this method in derived classes to release additional resources. Always call
    /// <c>base.Dispose(disposing)</c> to ensure proper cleanup of the base class.
    /// </para>
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            ForceRefresh();
            Disposing?.Invoke(this, this);
            Disposing = null;
        }

        _disposed = true;
    }

    #endregion IDisposable members

    #region Equality & Operators

    /// <summary>
    /// Determines whether the specified object is equal to the current direct drawing.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    /// <see langword="true"/> if the specified object is the same instance as this direct drawing;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This implementation uses reference equality only. Direct drawings are considered equal
    /// only if they are the exact same object instance.
    /// </remarks>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    /// <summary>
    /// Returns a hash code for this direct drawing.
    /// </summary>
    /// <returns>A hash code based on the <see cref="Nickname"/>.</returns>
    /// <remarks>
    /// The hash code is computed using the nickname. If the nickname is <see langword="null"/>,
    /// it contributes 0 to the hash. This implementation is not suitable for dictionary keys
    /// unless nicknames are unique; use <see cref="Id"/> for unique identification.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(Nickname);

    /// <summary>
    /// Determines whether two direct drawing instances are equal using reference equality.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both references point to the same instance or are both <see langword="null"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);

    /// <summary>
    /// Determines whether two direct drawing instances are not equal using reference equality.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if the references do not point to the same instance;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(DirectDrawingBase? left, DirectDrawingBase? right) => !(left == right);

    /// <summary>
    /// Determines whether the Z-order of the left direct drawing is less than the right.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> has a lower Z-order than <paramref name="right"/>;
    /// otherwise, <see langword="false"/>. <see langword="null"/> is considered to have a Z-order of 0.
    /// </returns>
    public static bool operator <(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether the Z-order of the left direct drawing is less than or equal to the right.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> has a Z-order less than or equal to <paramref name="right"/>;
    /// otherwise, <see langword="false"/>. <see langword="null"/> is considered to have a Z-order of 0.
    /// </returns>
    public static bool operator <=(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether the Z-order of the left direct drawing is greater than the right.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> has a higher Z-order than <paramref name="right"/>;
    /// otherwise, <see langword="false"/>. <see langword="null"/> is considered to have a Z-order of 0.
    /// </returns>
    public static bool operator >(DirectDrawingBase? left, DirectDrawingBase? right) =>
        !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether the Z-order of the left direct drawing is greater than or equal to the right.
    /// </summary>
    /// <param name="left">The first direct drawing to compare.</param>
    /// <param name="right">The second direct drawing to compare.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> has a Z-order greater than or equal to <paramref name="right"/>;
    /// otherwise, <see langword="false"/>. <see langword="null"/> is considered to have a Z-order of 0.
    /// </returns>
    public static bool operator >=(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;

    #endregion Equality & Operators

    /// <summary>
    /// Defines the direction from which a reveal animation progressively displays the direct drawing.
    /// </summary>
    /// <remarks>
    /// The reveal effect uses clipping to progressively show portions of the drawing. The direction
    /// determines which edge starts visible and how the visible region expands as the reveal progresses
    /// from 0.0 to 1.0.
    /// </remarks>
    public enum RevealDirection
    {
        /// <summary>
        /// Reveals the drawing from the left edge toward the right edge.
        /// </summary>
        LeftToRight,

        /// <summary>
        /// Reveals the drawing from the right edge toward the left edge.
        /// </summary>
        RightToLeft,

        /// <summary>
        /// Reveals the drawing from the top edge toward the bottom edge.
        /// </summary>
        TopToBottom,

        /// <summary>
        /// Reveals the drawing from the bottom edge toward the top edge.
        /// </summary>
        BottomToTop
    }
}
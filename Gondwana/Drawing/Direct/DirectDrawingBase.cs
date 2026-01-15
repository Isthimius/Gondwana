using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingBase : IDirectDrawable, IComparable<DirectDrawingBase>
{
    public event EventHandler<IDirectDrawable>? Disposing;
    public event EventHandler<DirectDrawingBase>? FadeToCompleted;

    protected readonly RenderSurfaceHostBase _renderSurfaceHost;
    protected Rectangle _screenBounds;
    protected Rectangle _worldBounds;
    protected int _zOrder;
    protected bool _visible;
    protected long _lastTick = HighResTimer.GetCurrentTick();

    private bool _disposed = false;

    // Fade/opacity state
    private float _opacity = 1f;                 // 0..1
    private float _fadeFrom, _fadeTo;
    private float _fadeDurationSec, _fadeElapsedSec;
    private bool _isFading;

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
    /// Performs the concrete drawing for this DirectDrawing to the Backbuffer.
    /// Called by the engine after routing, visibility, opacity,
    /// and reveal logic have been applied.
    /// </summary>
    /// <remarks>
    /// Override this method in derived classes.
    /// Do not call it directly; the engine calls it via <see cref="Draw"/>.
    /// </remarks>
    protected abstract void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen);

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
        Nickname = nickname;

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

    public RenderSurfaceHostBase RenderSurfaceHost => _renderSurfaceHost;

    public DirectDrawingMode Mode { get; }

    public SceneLayer? SceneLayer { get; private set; }

    public View? View { get; private set; }

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

    public TypedValueBag ValueBag { get; } = new();

    #region IDrawable members

    public Guid Id { get; } = Guid.NewGuid();

    public string? Nickname { get; private set; }

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

    public RectangleF GetDrawLocationScreen(View view)
    {
        // View mode already returns _screenBounds
        if (Mode == DirectDrawingMode.View)
            return _screenBounds;

        // translate world bounds to screen via view transform
        return view.WorldRectToScreenRect(SceneLayer!, _worldBounds);
    }

    /// <summary>
    /// Engine entry point for drawing. destRectScreen must be in SCREEN pixels.
    /// This method handles opacity and reveal clipping before calling OnDraw(),
    /// which draws the actual Canvas.
    /// </summary>
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

    /// <summary>Gets/sets the current opacity (0..1). Setting marks dirty.</summary>
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
            else if
                (_opacity > 0f) Visible = true;

            ForceRefresh();
        }
    }

    /// <summary>Instantly jump to the given opacity (0..1).</summary>
    public DirectDrawingBase SetOpacity(float opacity)
    {
        Opacity = opacity;
        return this;
    }

    /// <summary>Fade to target opacity over duration (seconds). Returns this for chaining.</summary>
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

    /// <summary>Convenience: fade in from 0 to 1 over duration.</summary>
    public DirectDrawingBase FadeIn(float durationSec)
    {
        if (_opacity <= 0f)
            Opacity = 0f;

        return FadeTo(1f, durationSec);
    }

    /// <summary>Convenience: fade out from current to 0 over duration.</summary>
    public DirectDrawingBase FadeOut(float durationSec)
    {
        return FadeTo(0f, durationSec);
    }

    /// <summary>Cancel any active fade (keeps current opacity).</summary>
    public DirectDrawingBase CancelFade()
    {
        _isFading = false;
        return this;
    }

    public DirectDrawingBase SetReveal(float t01)
    {
        _revealT = Math.Clamp(t01, 0f, 1f);
        ForceRefresh();
        return this;
    }

    public DirectDrawingBase SetRevealDirection(RevealDirection dir)
    {
        _revealDir = dir;
        ForceRefresh();
        return this;
    }

    public DirectDrawingBase RevealTo(float t01, float durationSec, Func<float, float>? easing = null)
    {
        _revealAnimating = true;
        _revealElapsedSec = 0f;
        _revealDurationSec = Math.Max(0.0001f, durationSec);
        _revealEasing = easing;
        // target is t01; we’ll lerp in Update
        _revealTarget = Math.Clamp(t01, 0f, 1f);
        _revealStart = _revealT;
        return this;
    }

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
    /// Per-frame update hook. Default: advance scrolling only.
    /// Called from <see cref="DirectDrawingManager.UpdateAll(long)"/>.
    /// </summary>
    /// <param name="tick">Current engine tick from <see cref="HighResTimer"/>.</param>
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

    public int CompareTo(DirectDrawingBase? other) => _zOrder.CompareTo(other?._zOrder ?? 0);

    #region IDisposable members

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => HashCode.Combine(Nickname);

    public static bool operator ==(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);

    public static bool operator !=(DirectDrawingBase? left, DirectDrawingBase? right) => !(left == right);

    public static bool operator <(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

    public static bool operator <=(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

    public static bool operator >(DirectDrawingBase? left, DirectDrawingBase? right) =>
        !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

    public static bool operator >=(DirectDrawingBase? left, DirectDrawingBase? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;

    #endregion Equality & Operators

    public enum RevealDirection
    {
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop
    }
}
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
    protected internal bool _dirty = true;
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
    /// Render the drawable to the current backbuffer.
    /// </summary>
    protected internal abstract void Draw(BackbufferBase backbuffer);

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

        if (mode == DirectDrawingMode.SceneLayer)
        {
            _worldBounds = worldBounds!.Value;
            _screenBounds = Rectangle.Empty;
        }
        else // View
        {
            _worldBounds = Rectangle.Empty;
            _screenBounds = screenBounds!.Value;
        }

        _zOrder = 0;
        _visible = true;
        Mode = mode;
        SceneLayer = sceneLayer;
        View = view;
        Nickname = nickname;

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
            ForceRefresh();
            _worldBounds = value;
            ForceRefresh();
        }
    }

    #region IDrawable members

    public Guid Id { get; private set; }

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

    void IDrawable.Draw(BackbufferBase backbuffer)
    {
        if (Mode == DirectDrawingMode.View)
            RenderViewPass();
        else
            RenderLayerPass();
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
        if (Mode == DirectDrawingMode.SceneLayer)
        {
            // bounds is WORLD-space
            SceneLayer!.RefreshQueue.AddWorldRect(_worldBounds);
        }
        else if (Mode == DirectDrawingMode.View)
        {
            // bounds is SCREEN-space
            RenderSurfaceHost.AddViewOverlayScreenDirty(View!, _screenBounds);
        }

        _dirty = true;
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

            _dirty = true;

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

            _dirty = true;
            if (_revealElapsedSec >= _revealDurationSec)
                _revealAnimating = false;
        }

        _lastTick = tick;
    }

    /// <summary>
    /// Render the DirectDrawing in the the view pass in SCREEN pixels;
    /// called for Mode == View direct drawings.
    /// </summary>
    protected internal virtual void RenderViewPass()
    {
        // this method should only be called for Mode == View
        if (Mode != DirectDrawingMode.View)
            return;

        if (!Visible)
            return;

        var canvas = RenderSurfaceHost.Backbuffer!.Canvas;

        // Compute reveal clip rect (in pixel space) from Bounds
        // If fully revealed, skip the whole clip branch.
        bool useClip = _revealT < 0.999f;

        if (useClip)
        {
            var r = new SKRect(_screenBounds.Left, _screenBounds.Top, _screenBounds.Right, _screenBounds.Bottom);
            SKRect clipRect = _revealDir switch
            {
                RevealDirection.LeftToRight => new SKRect(r.Left, r.Top, r.Left + r.Width * _revealT, r.Bottom),
                RevealDirection.RightToLeft => new SKRect(r.Right - r.Width * _revealT, r.Top, r.Right, r.Bottom),
                RevealDirection.TopToBottom => new SKRect(r.Left, r.Top, r.Right, r.Top + r.Height * _revealT),
                RevealDirection.BottomToTop => new SKRect(r.Left, r.Bottom - r.Height * _revealT, r.Right, r.Bottom),
                _ => r
            };

            // Early-out: if clip is empty, no need to draw at all.
            if (clipRect.Width <= 0f || clipRect.Height <= 0f)
                return;

            canvas.Save();
            canvas.ClipRect(clipRect, SKClipOperation.Intersect, antialias: false);
        }

        if (_opacity >= 0.999f)
        {
            Draw(RenderSurfaceHost.Backbuffer!);
        }
        else
        {
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(_opacity * 255)) };
            canvas.SaveLayer(layerPaint);
            Draw(RenderSurfaceHost.Backbuffer);
            canvas.Restore(); // end SaveLayer
        }

        if (useClip)
            canvas.Restore(); // end Clip Save
    }

    /// <summary>
    /// Render the DirectDrawing in the the scene layer pass in WORLD pixels;
    /// called for Mode == SceneLayer direct drawings.
    /// </summary>
    protected internal void RenderLayerPass()
    {
        if (!Visible)
            return;

        // no reveal clip here (until world bounds exist)
        if (_opacity >= 0.999f)
        {
            Draw(RenderSurfaceHost.Backbuffer!);
        }
        else
        {
            var canvas = RenderSurfaceHost.Backbuffer!.Canvas;
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(_opacity * 255)) };
            canvas.SaveLayer(layerPaint);
            Draw(RenderSurfaceHost.Backbuffer);
            canvas.Restore();
        }
    }

    protected Rectangle ActiveBounds => Mode == DirectDrawingMode.SceneLayer ? _worldBounds : _screenBounds;

    protected SKRect ActiveBoundsSk => ActiveBounds.ToSKRect();

    public int CompareTo(DirectDrawingBase? other) => _zOrder.CompareTo(other?._zOrder ?? 0);

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
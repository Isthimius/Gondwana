using Gondwana.Rendering;
using Gondwana.Timers;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingBase : IDirectDrawable, IComparable<DirectDrawingBase>
{
    public event EventHandler<IDirectDrawable>? Disposing;
    public event EventHandler<DirectDrawingBase>? FadeToCompleted;

    protected readonly RenderSurfaceHostBase _renderSurfaceHost;
    protected Rectangle _bounds;
    protected int _zOrder;
    protected bool _isVisible;
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
    protected internal abstract void Draw();

    protected DirectDrawingBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds, string? name = null)
    {
        _renderSurfaceHost = renderSurfaceHost;
        _bounds = bounds;
        _zOrder = 0;
        _isVisible = true;
        Name = name ?? Guid.NewGuid().ToString();

        DirectDrawingManager.Instance.AddOrReplace(this);
        ForceRefresh();
    }

    ~DirectDrawingBase() => Dispose(false);

    public RenderSurfaceHostBase RenderSurfaceHost => _renderSurfaceHost;

    public string Name { get; private set; }

    public Rectangle Bounds
    {
        get => _bounds;
        set
        {
            ForceRefresh();
            _bounds = value;
            ForceRefresh();
        }
    }

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

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                ForceRefresh();
            }
        }
    }

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
                IsVisible = false;
            else if
                (_opacity > 0f) IsVisible = true;

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
            IsVisible = true; // ensure we draw during fade-in

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

    /// <summary>
    /// Mark the current DirectDrawing as dirty, forcing a redraw on the next RenderAll().
    /// Also adds overlapping area on the <see cref="RenderSurfaceHost.DrawSource" /> to the RefreshQueue.
    /// </summary>
    protected internal void ForceRefresh()
    {
        var scene = RenderSurfaceHost.Scene;

        if (scene?.Count > 0)
            scene[0].RefreshQueue.AddPixelRangeToRefreshQueue(_bounds, true);

        _dirty = true;
    }

    /// <summary>
    /// Per-frame update hook. Default: advance scrolling only.
    /// Called from <see cref="DirectDrawingManager.UpdateAll(long)"/>.
    /// </summary>
    /// <param name="tick">Current engine tick from <see cref="HighResTimer"/>.</param>
    public virtual void Update(long tick)
    {
        if (tick == _lastTick)
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
                IsVisible = _opacity > 0f; // hides when hit zero

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
            if (_revealElapsedSec >= _revealDurationSec) _revealAnimating = false;
        }

        _lastTick = tick;
    }

    protected internal virtual void Render()
    {
        if (!IsVisible)
            return;

        var canvas = RenderSurfaceHost.Backbuffer!.Canvas;

        // Compute reveal clip rect (in pixel space) from Bounds
        // If fully revealed, skip the whole clip branch.
        bool useClip = _revealT < 0.999f;
        SKRect clipRect = default;

        if (useClip)
        {
            var r = new SKRect(_bounds.Left, _bounds.Top, _bounds.Right, _bounds.Bottom);
            clipRect = _revealDir switch
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
            Draw();
        }
        else
        {
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(_opacity * 255)) };
            canvas.SaveLayer(layerPaint);
            Draw();
            canvas.Restore(); // end SaveLayer
        }

        if (useClip)
            canvas.Restore(); // end Clip Save
    }

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

    public override int GetHashCode() => HashCode.Combine(Name);

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
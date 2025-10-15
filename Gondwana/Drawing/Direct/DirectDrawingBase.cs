using Gondwana.Rendering;
using Gondwana.Timers;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingBase : IComparable<DirectDrawingBase>, IDisposable
{
    public event EventHandler<DirectDrawingBase>? Disposing;
    public event EventHandler<DirectDrawingBase>? FadeToCompleted;

    protected readonly RenderSurfaceHostBase _renderSurfaceHost;
    protected Rectangle _bounds;
    protected int _zOrder;
    protected bool _isVisible;
    internal Movement? _movement;
    protected internal bool _dirty = true;
    private bool _disposed = false;
    protected long? _lastTick;

    // Fade/opacity state
    private float _opacity = 1f;                 // 0..1
    private float _fadeFrom, _fadeTo;
    private float _fadeDurationSec, _fadeElapsedSec;
    private bool _isFading;
    public bool HideWhenFullyTransparent { get; set; } = true;

    /// <summary>
    /// Render the drawable to the current backbuffer.
    /// </summary>
    protected internal abstract void Draw();

    protected DirectDrawingBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
    {
        _renderSurfaceHost = renderSurfaceHost;
        _bounds = bounds;
        _zOrder = 0;
        _isVisible = true;
        Name = Guid.NewGuid().ToString();

        DirectDrawingManager.Instance.AddOrReplace(this);
        ForceRefresh();
    }

    ~DirectDrawingBase() => Dispose(false);

    public RenderSurfaceHostBase RenderSurfaceHost => _renderSurfaceHost;

    public string Name { get; set; }

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

    public bool IsScrolling => _movement != null;

    public void ScrollToSourceGridPoint(double totalTime, Rectangle destBounds)
    {
        _movement?.Reset();
        _movement = new Movement(this, totalTime, destBounds);
    }

    public void StopScrolling()
    {
        _movement?.Reset();
        _movement = null;
    }

    internal void MoveNext(long tick)
    {
        if (_movement != null)
        {
            ForceRefresh();

            if (_movement?.MoveNext(tick) == true)
                _movement = null;
        }
    }

    /// <summary>
    /// Marke the current DirectDrawing as dirty, forcing a redraw on the next RenderAll().
    /// Also adds overlapping area on the <see cref="RenderSurfaceHost.DrawSource"> to the RefreshQueue.
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
    protected internal virtual void Update(long tick)
    {
        MoveNext(tick);

        // Initialize clock on first frame to avoid huge dt
        if (!_lastTick.HasValue)
        {
            _lastTick = tick;
            return;
        }

        // Advance fade tween
        if (_isFading)
        {
            long deltaTicks = tick - _lastTick.Value;
            if (deltaTicks < 0) deltaTicks = 0;
            float dt = (float)(deltaTicks / (double)HighResTimer.TicksPerSecond);

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

        _lastTick = tick;
    }

    protected internal virtual void Render()
    {
        if (!IsVisible)
            return;

        // Fast path if fully opaque
        if (_opacity >= 0.999f)
        {
            Draw();
            return;
        }

        var canvas = RenderSurfaceHost.Backbuffer.Canvas;

        // SaveLayer with alpha
        using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(_opacity * 255)) };
        canvas.SaveLayer(layerPaint);
        Draw();
        canvas.Restore();
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

    #region Movement Inner Class

    internal class Movement
    {
        internal DirectDrawingBase? parent;
        private readonly long startTick;
        private readonly long totalTicks;
        private readonly Rectangle startBounds;
        private readonly Rectangle destBounds;

        internal Movement(DirectDrawingBase drawing, double totalTime, Rectangle dest)
        {
            parent = drawing;
            startTick = HighResTimer.GetCurrentTick();
            totalTicks = (long)(totalTime * HighResTimer.TicksPerSecond);
            startBounds = parent.Bounds;
            destBounds = dest;
        }

        internal bool IsFinished(long tick) => tick >= startTick + totalTicks;

        internal bool MoveNext(long tick)
        {
            if (IsFinished(tick))
            {
                parent!.Bounds = destBounds;
                parent = null;
                return true;
            }

            double percent = (tick - startTick) / (double)totalTicks;

            int newX = startBounds.X + (int)((destBounds.X - startBounds.X) * percent);
            int newY = startBounds.Y + (int)((destBounds.Y - startBounds.Y) * percent);
            int newWidth = startBounds.Width + (int)((destBounds.Width - startBounds.Width) * percent);
            int newHeight = startBounds.Height + (int)((destBounds.Height - startBounds.Height) * percent);

            parent!.Bounds = new Rectangle(newX, newY, newWidth, newHeight);
            return false;
        }

        internal void Reset() => parent = null;
    }

    #endregion Movement Inner Class
}
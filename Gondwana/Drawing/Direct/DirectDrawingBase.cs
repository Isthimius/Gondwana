using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingBase : IComparable<DirectDrawingBase>, IDisposable
{
    public event EventHandler<DirectDrawingBase> Disposing;

    protected readonly RenderSurfaceHostBase _renderSurfaceHost;
    protected Rectangle _bounds;
    protected int _zOrder;
    internal Movement? _movement;
    protected internal bool _dirty = true;
    private bool _disposed = false;

    /// <summary>
    /// Render the drawable to the current backbuffer.
    /// </summary>
    protected internal abstract void Render();

    protected DirectDrawingBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
    {
        DirectDrawingManager.Instance.Add(this);
        _renderSurfaceHost = renderSurfaceHost;
        _bounds = bounds;
        _zOrder = 0;
        Name = Guid.NewGuid().ToString();
        ForceRefresh();
    }

    ~DirectDrawingBase() => Dispose(false);

    public RenderSurfaceHostBase RenderSurfaceHost => _renderSurfaceHost;

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
            _zOrder = value;
            ForceRefresh();
        }
    }

    public string Name { get; set; }

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
        if (_movement?.MoveNext(tick) == true)
            _movement = null;
    }

    protected internal void ForceRefresh()
    {
        var scene = RenderSurfaceHost.DrawSource;
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
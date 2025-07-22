using Gondwana.Timers;
using System.Drawing;

namespace Gondwana.Rendering.Direct;

public abstract class DirectDrawing : IComparable<DirectDrawing>, IDisposable
{
    public event EventHandler<DirectDrawing> Disposing;

    protected readonly BackbufferBase _buffer;
    protected Rectangle _bounds;
    protected int _zOrder;
    internal Movement? _movement;
    internal bool _dirty = true;
    private bool _disposed = false;

    protected internal abstract void Render();

    protected DirectDrawing(BackbufferBase buffer, Rectangle bounds)
    {
        DirectDrawingManager.Add(this);
        _buffer = buffer;
        _bounds = bounds;
        _zOrder = 0;
        Name = Guid.NewGuid().ToString();
        ForceRefresh();
    }

    ~DirectDrawing() => Dispose(false);

    public BackbufferBase Buffer => _buffer;

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

    public void ScrollSourceGridPoint(double totalTime, Rectangle destBounds)
    {
        _movement?.Reset();
        _movement = new Movement(this, totalTime, destBounds);
    }

    public void StopScrolling()
    {
        _movement?.Reset();
        _movement = null;
    }

    public void MoveNext(long tick)
    {
        if (_movement?.MoveNext(tick) == true)
            _movement = null;
    }

    public void ForceRefresh()
    {
        //var matrixes = _surface.Buffer.DrawSource;
        //if (matrixes?.Count > 0)
        //    matrixes[0].RefreshQueue.AddPixelRangeToRefreshQueue(_bounds, true);

        //_dirty = true;
    }

    public int CompareTo(DirectDrawing? other) => _zOrder.CompareTo(other?._zOrder ?? 0);

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
            Disposing?.Invoke(this, this);
        }

        _disposed = true;
    }

    #region Equality & Operators

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => HashCode.Combine(Name);

    public static bool operator ==(DirectDrawing? left, DirectDrawing? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);

    public static bool operator !=(DirectDrawing? left, DirectDrawing? right) => !(left == right);

    public static bool operator <(DirectDrawing? left, DirectDrawing? right) =>
        ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

    public static bool operator <=(DirectDrawing? left, DirectDrawing? right) =>
        ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

    public static bool operator >(DirectDrawing? left, DirectDrawing? right) =>
        !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

    public static bool operator >=(DirectDrawing? left, DirectDrawing? right) =>
        ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;

    #endregion

    #region Movement Inner Class
    internal class Movement
    {
        internal DirectDrawing? parent;
        private readonly long startTick;
        private readonly long totalTicks;
        private readonly Rectangle startBounds;
        private readonly Rectangle destBounds;

        internal Movement(DirectDrawing drawing, double totalTime, Rectangle dest)
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
    #endregion
}

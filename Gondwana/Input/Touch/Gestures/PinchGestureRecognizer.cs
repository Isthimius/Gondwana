using System.Drawing;

namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Recognizes two-finger pinch and spread gestures by listening to touch events from an
/// <see cref="ITouchInput"/> source. Raises <see cref="PinchUpdated"/> each time the distance
/// between the two active contact points changes, providing a relative scale delta.
/// </summary>
/// <remarks>
/// <para>
/// Subscribe to the <see cref="PinchUpdated"/> event to receive incremental scale updates as the
/// user moves two fingers together or apart. A <see cref="PinchedEventArgs.ScaleDelta"/> greater
/// than <c>1.0</c> means the fingers spread apart (expand), while a value less than <c>1.0</c>
/// means they moved closer (contract).
/// </para>
/// <para>
/// Only exactly two simultaneous active touches are tracked. If more than two touches are present,
/// only the first two that were registered are used.
/// </para>
/// </remarks>
public sealed class PinchGestureRecognizer : IDisposable
{
    private readonly ITouchInput _touchInput;
    private readonly Dictionary<int, Point> _activePoints = new();
    private double _lastDistance = 0;
    private bool _isDisposed;

    /// <summary>
    /// Occurs whenever the distance between two active touch contact points changes during a pinch
    /// or spread gesture. The event data includes the relative scale delta and current distance.
    /// </summary>
    public event EventHandler<PinchedEventArgs>? PinchUpdated;

    /// <summary>
    /// Initializes a new instance of the <see cref="PinchGestureRecognizer"/> class, subscribing
    /// to touch events from the specified <see cref="ITouchInput"/> source.
    /// </summary>
    /// <param name="touchInput">
    /// The touch input source to monitor for pinch gestures. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="touchInput"/> is <see langword="null"/>.
    /// </exception>
    public PinchGestureRecognizer(ITouchInput touchInput)
    {
        _touchInput = touchInput ?? throw new ArgumentNullException(nameof(touchInput));
        _touchInput.TouchBegan += OnTouchBegan;
        _touchInput.TouchMoved += OnTouchMoved;
        _touchInput.TouchEnded += OnTouchEnded;
    }

    private void OnTouchBegan(object? sender, TouchEventArgs e)
    {
        if (_activePoints.Count >= 2)
            return;

        _activePoints[e.Touch.Id] = e.Touch.Position;

        if (_activePoints.Count == 2)
            _lastDistance = GetCurrentDistance();
    }

    private void OnTouchMoved(object? sender, TouchEventArgs e)
    {
        if (!_activePoints.ContainsKey(e.Touch.Id))
            return;

        _activePoints[e.Touch.Id] = e.Touch.Position;

        if (_activePoints.Count == 2)
        {
            var currentDistance = GetCurrentDistance();

            if (_lastDistance > 0 && currentDistance > 0)
            {
                var scaleDelta = currentDistance / _lastDistance;
                PinchUpdated?.Invoke(this, new PinchedEventArgs(scaleDelta, currentDistance));
            }

            _lastDistance = currentDistance;
        }
    }

    private void OnTouchEnded(object? sender, TouchEventArgs e)
    {
        _activePoints.Remove(e.Touch.Id);

        if (_activePoints.Count < 2)
            _lastDistance = 0;
    }

    private double GetCurrentDistance()
    {
        using var enumerator = _activePoints.Values.GetEnumerator();
        if (!enumerator.MoveNext()) return 0;
        var a = enumerator.Current;
        if (!enumerator.MoveNext()) return 0;
        var b = enumerator.Current;

        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Releases all resources held by this recognizer and unsubscribes from the touch input source.
    /// After disposal, no further <see cref="PinchUpdated"/> events will be raised.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _touchInput.TouchBegan -= OnTouchBegan;
        _touchInput.TouchMoved -= OnTouchMoved;
        _touchInput.TouchEnded -= OnTouchEnded;
    }
}

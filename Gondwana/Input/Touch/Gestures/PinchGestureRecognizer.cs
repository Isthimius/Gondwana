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
/// All simultaneous contacts are tracked. Pinch updates are emitted only when exactly two contacts
/// are active. If a third finger lands while two are tracked, updates pause until one lifts and
/// exactly two remain — at which point the baseline distance is recomputed so there is no jump.
/// </para>
/// </remarks>
public sealed class PinchGestureRecognizer : IDisposable
{
    private readonly ITouchInput _touchInput;
    private readonly Dictionary<int, Point> _activePoints = new();
    private double _startingDistance;
    private double _lastDistance = 0;
    private bool _isDisposed;

    /// <summary>Occurs when exactly two contacts establish a pinch gesture.</summary>
    public event EventHandler<PinchedEventArgs>? PinchStarted;

    /// <summary>
    /// Occurs whenever the distance between two active touch contact points changes during a pinch
    /// or spread gesture. The event data includes the relative scale delta and current distance.
    /// </summary>
    public event EventHandler<PinchedEventArgs>? PinchUpdated;

    /// <summary>Occurs when the current two-contact pinch ends or is interrupted.</summary>
    public event EventHandler<PinchedEventArgs>? PinchEnded;

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
        var prevCount = _activePoints.Count;

        if (prevCount == 2)
            Emit(PinchPhase.Ended, PinchEnded);

        _activePoints[e.Touch.Id] = e.Touch.Position;
        var newCount = _activePoints.Count;

        if (prevCount != 2 && newCount == 2)
            BeginPinch();
        else if (newCount != 2)
            ClearBaseline();
    }

    private void OnTouchMoved(object? sender, TouchEventArgs e)
    {
        if (!_activePoints.ContainsKey(e.Touch.Id))
            return;

        _activePoints[e.Touch.Id] = e.Touch.Position;

        // Only emit updates when exactly two contacts are tracked and a baseline exists.
        if (_activePoints.Count == 2 && _lastDistance > 0)
        {
            var currentDistance = GetCurrentDistance();

            if (currentDistance > 0)
            {
                Emit(PinchPhase.Updated, PinchUpdated, currentDistance);
            }

            _lastDistance = currentDistance;
        }
    }

    private void OnTouchEnded(object? sender, TouchEventArgs e)
    {
        var prevCount = _activePoints.Count;

        if (_activePoints.ContainsKey(e.Touch.Id))
            _activePoints[e.Touch.Id] = e.Touch.Position;

        if (prevCount == 2)
            Emit(PinchPhase.Ended, PinchEnded);

        _activePoints.Remove(e.Touch.Id);
        var newCount = _activePoints.Count;

        if (prevCount != 2 && newCount == 2)
            BeginPinch();
        else if (newCount < 2)
            ClearBaseline();
    }

    private void BeginPinch()
    {
        _startingDistance = GetCurrentDistance();
        _lastDistance = _startingDistance;
        Emit(PinchPhase.Began, PinchStarted, _startingDistance);
    }

    private void ClearBaseline()
    {
        _startingDistance = 0;
        _lastDistance = 0;
    }

    private void Emit(PinchPhase phase,
                      EventHandler<PinchedEventArgs>? handler,
                      double? currentDistance = null)
    {
        if (_activePoints.Count != 2)
            return;

        var pair = _activePoints.OrderBy(kvp => kvp.Key).Take(2).ToArray();
        var a = pair[0].Value;
        var b = pair[1].Value;
        var distance = currentDistance ?? GetDistance(a, b);
        var center = new PointF((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

        handler?.Invoke(this, new PinchedEventArgs(phase,
                                                   new[] { pair[0].Key, pair[1].Key },
                                                   center,
                                                   _startingDistance,
                                                   _lastDistance,
                                                   distance));
    }

    private double GetCurrentDistance()
    {
        using var enumerator = _activePoints.Values.GetEnumerator();
        if (!enumerator.MoveNext())
            return 0;
        
        var a = enumerator.Current;
        
        if (!enumerator.MoveNext())
            return 0;

        var b = enumerator.Current;

        return GetDistance(a, b);
    }

    private static double GetDistance(Point a, Point b)
    {
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
        Reset();
    }

    internal void Reset()
    {
        _activePoints.Clear();
        ClearBaseline();
    }
}

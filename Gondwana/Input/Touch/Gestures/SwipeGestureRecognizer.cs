using System.Drawing;

namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Recognizes swipe gestures by listening to touch events from an <see cref="ITouchInput"/> source.
/// A swipe is a single-finger gesture that ends with sufficient speed and with a clear primary direction.
/// Short or slow drags do not qualify.
/// </summary>
/// <remarks>
/// <para>
/// Subscribe to the <see cref="Swiped"/> event to receive notifications when a qualifying swipe is detected.
/// </para>
/// <para>
/// A swipe is recognized when a touch contact ends and the average speed over the contact duration
/// meets or exceeds <see cref="MinimumSwipeSpeedPixelsPerSecond"/>.
/// </para>
/// </remarks>
public sealed class SwipeGestureRecognizer : IDisposable
{
    private readonly ITouchInput _touchInput;
    private readonly Dictionary<int, SwipeState> _activeSwipes = new();
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets the minimum average speed, in pixels per second, that a touch must reach
    /// when it ends in order to be recognized as a swipe. Slower contacts are not recognized.
    /// Default is <c>200</c> pixels per second.
    /// </summary>
    public double MinimumSwipeSpeedPixelsPerSecond { get; set; } = 200;

    /// <summary>
    /// Occurs when a qualifying swipe gesture is detected.
    /// </summary>
    public event EventHandler<SwipedEventArgs>? Swiped;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwipeGestureRecognizer"/> class, subscribing
    /// to touch events from the specified <see cref="ITouchInput"/> source.
    /// </summary>
    /// <param name="touchInput">
    /// The touch input source to monitor for swipe gestures. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="touchInput"/> is <see langword="null"/>.
    /// </exception>
    public SwipeGestureRecognizer(ITouchInput touchInput)
    {
        _touchInput = touchInput ?? throw new ArgumentNullException(nameof(touchInput));
        _touchInput.TouchBegan += OnTouchBegan;
        _touchInput.TouchEnded += OnTouchEnded;
    }

    private void OnTouchBegan(object? sender, TouchEventArgs e)
    {
        _activeSwipes[e.Touch.Id] = new SwipeState(e.Touch.Position);
    }

    private void OnTouchEnded(object? sender, TouchEventArgs e)
    {
        if (!_activeSwipes.TryGetValue(e.Touch.Id, out var state))
            return;

        _activeSwipes.Remove(e.Touch.Id);

        // A system-cancelled contact must never produce a swipe.
        if (e.Touch.Phase == TouchPhase.Cancelled)
            return;

        var endPos = e.Touch.Position;
        var dx = endPos.X - state.StartPosition.X;
        var dy = endPos.Y - state.StartPosition.Y;

        var distance = Math.Sqrt(dx * dx + dy * dy);
        var elapsed = (DateTime.UtcNow - state.StartTime).TotalSeconds;

        if (elapsed <= 0)
            return;

        var speed = distance / elapsed;

        if (speed < MinimumSwipeSpeedPixelsPerSecond)
            return;

        var direction = Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? SwipeDirection.Right : SwipeDirection.Left)
            : (dy >= 0 ? SwipeDirection.Down : SwipeDirection.Up);

        Swiped?.Invoke(this, new SwipedEventArgs(direction, state.StartPosition, endPos, speed));
    }

    /// <summary>
    /// Releases all resources held by this recognizer and unsubscribes from the touch input source.
    /// After disposal, no further <see cref="Swiped"/> events will be raised.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _touchInput.TouchBegan -= OnTouchBegan;
        _touchInput.TouchEnded -= OnTouchEnded;
    }

    private readonly record struct SwipeState(Point StartPosition, DateTime StartTime)
    {
        /// <summary>
        /// Initializes a new swipe tracking state starting at the specified position and the current UTC time.
        /// </summary>
        /// <param name="startPosition">The position where the swipe began.</param>
        public SwipeState(Point startPosition) : this(startPosition, DateTime.UtcNow) { }
    }
}

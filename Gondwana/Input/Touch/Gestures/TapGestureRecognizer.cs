using System.Drawing;

namespace Gondwana.Input.Touch.Gestures;

/// <summary>
/// Recognizes single-finger tap gestures by listening to touch events from an <see cref="ITouchInput"/> source.
/// A tap is defined as a touch that begins and ends within a configurable maximum duration and without
/// exceeding a configurable maximum movement distance. Long presses and drags are ignored.
/// </summary>
/// <remarks>
/// <para>
/// Subscribe to the <see cref="Tapped"/> event to receive notifications when a qualifying tap is detected.
/// </para>
/// <para>
/// A tap is recognized when:
/// <list type="bullet">
/// <item><description>A single touch contact begins (<see cref="TouchPhase.Began"/>).</description></item>
/// <item><description>The same contact ends without exceeding <see cref="MaxTapMovementPixels"/> of movement.</description></item>
/// <item><description>The total contact duration is less than or equal to <see cref="MaxTapDurationSeconds"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TapGestureRecognizer : IDisposable
{
    private readonly ITouchInput _touchInput;

    // Per-touch tracking: key = touch ID
    private readonly Dictionary<int, TapState> _activeTaps = new();

    private bool _isDisposed;

    /// <summary>
    /// Gets or sets the maximum duration in seconds that a touch may last to still be considered a tap.
    /// Contacts lasting longer than this threshold are treated as long presses and will not raise
    /// <see cref="Tapped"/>. Default is <c>0.3</c> seconds.
    /// </summary>
    public double MaxTapDurationSeconds { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the maximum distance in pixels that a touch may travel from its start position
    /// to still be considered a tap. Contacts that move more than this threshold are treated as drags
    /// and will not raise <see cref="Tapped"/>. Default is <c>20</c> pixels.
    /// </summary>
    public double MaxTapMovementPixels { get; set; } = 20;

    /// <summary>
    /// Occurs when a qualifying single-finger tap gesture is detected.
    /// </summary>
    public event EventHandler<TappedEventArgs>? Tapped;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapGestureRecognizer"/> class, subscribing
    /// to touch events from the specified <see cref="ITouchInput"/> source.
    /// </summary>
    /// <param name="touchInput">
    /// The touch input source to monitor for tap gestures. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="touchInput"/> is <see langword="null"/>.
    /// </exception>
    public TapGestureRecognizer(ITouchInput touchInput)
    {
        _touchInput = touchInput ?? throw new ArgumentNullException(nameof(touchInput));
        _touchInput.TouchBegan += OnTouchBegan;
        _touchInput.TouchMoved += OnTouchMoved;
        _touchInput.TouchEnded += OnTouchEnded;
    }

    private void OnTouchBegan(object? sender, TouchEventArgs e)
    {
        _activeTaps[e.Touch.Id] = new TapState(e.Touch.Position);
    }

    private void OnTouchMoved(object? sender, TouchEventArgs e)
    {
        if (!_activeTaps.TryGetValue(e.Touch.Id, out var state))
            return;

        if (state.Cancelled)
            return;

        var dx = e.Touch.Position.X - state.StartPosition.X;
        var dy = e.Touch.Position.Y - state.StartPosition.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > MaxTapMovementPixels)
            _activeTaps[e.Touch.Id] = state with { Cancelled = true };
    }

    private void OnTouchEnded(object? sender, TouchEventArgs e)
    {
        if (!_activeTaps.TryGetValue(e.Touch.Id, out var state))
            return;

        _activeTaps.Remove(e.Touch.Id);

        // A system-cancelled contact (e.g. incoming call) must never fire Tapped.
        if (state.Cancelled || e.Touch.Phase == TouchPhase.Cancelled)
            return;

        var elapsed = (DateTime.UtcNow - state.StartTime).TotalSeconds;
        if (elapsed <= MaxTapDurationSeconds)
            Tapped?.Invoke(this, new TappedEventArgs(e.Touch.Id, state.StartPosition));
    }

    /// <summary>
    /// Releases all resources held by this recognizer and unsubscribes from the touch input source.
    /// After disposal, no further <see cref="Tapped"/> events will be raised.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _touchInput.TouchBegan -= OnTouchBegan;
        _touchInput.TouchMoved -= OnTouchMoved;
        _touchInput.TouchEnded -= OnTouchEnded;
    }

    private record struct TapState(Point StartPosition, DateTime StartTime, bool Cancelled)
    {
        /// <summary>
        /// Initializes a new tap tracking state starting at the specified position and the current UTC time.
        /// </summary>
        /// <param name="startPosition">The position where the tap began.</param>
        public TapState(Point startPosition) : this(startPosition, DateTime.UtcNow, false) { }
    }
}

using System.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using Gondwana.Input.Touch;
using Microsoft.Extensions.Logging;

namespace Gondwana.Avalonia.Input.Touch;

/// <summary>
/// Provides a passive touch/pointer state adapter for Avalonia applications, implementing
/// <see cref="ITouchAdapter"/> by translating Avalonia <c>PointerPressed</c>,
/// <c>PointerMoved</c>, <c>PointerReleased</c>, and <c>PointerCaptureLost</c> events into
/// Gondwana touch state. Events are not raised directly; the <see cref="TouchEventPoller"/>
/// polls this adapter each engine frame to detect transitions and raise events.
/// </summary>
/// <remarks>
/// <para>
/// On physical touch devices (Android, iOS), each finger contact is tracked by Avalonia's pointer ID and
/// exposed as a unique <see cref="TouchPoint"/>. On desktop platforms where no hardware touch screen is
/// present, mouse pointer events are emulated as a single touch point with <c>Id = 0</c>.
/// </para>
/// <para>
/// Dispose this adapter to unsubscribe from all Avalonia pointer events.
/// </para>
/// </remarks>
public sealed class AvaloniaTouchInputAdapter : ITouchAdapter, IDisposable
{
    private readonly Control _control;
    private readonly Dictionary<int, TouchPoint> _activeTouches = new();
    private TouchPoint[] _activeTouchesSnapshot = Array.Empty<TouchPoint>();
    private readonly List<TouchPoint> _pendingEnds = new();
    private readonly object _pendingEndsLock = new();
    private bool _isDisposed;

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ActiveTouches => _activeTouchesSnapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaTouchInputAdapter"/> class,
    /// attaching pointer event handlers to the specified Avalonia control.
    /// </summary>
    /// <param name="control">
    /// The Avalonia <see cref="Control"/> whose pointer events will be translated into touch events.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="control"/> is <see langword="null"/>.
    /// </exception>
    public AvaloniaTouchInputAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        _control.PointerPressed += OnPointerPressed;
        _control.PointerMoved += OnPointerMoved;
        _control.PointerReleased += OnPointerReleased;
        _control.PointerCaptureLost += OnPointerCaptureLost;

        Engine.Logger.LogInformation("AvaloniaTouchInputAdapter initialized.");
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // For mouse pointers, only emulate touch for the primary (left) button.
        // Right- and middle-clicks should not generate touch events.
        if (e.Pointer.Type == PointerType.Mouse &&
            !e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
            return;

        var id = GetTouchId(e.Pointer);
        var pos = GetPosition(e, sender as Control);
        var point = new TouchPoint(id, pos, TouchPhase.Began);

        _activeTouches[id] = point;
        RebuildSnapshot();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var id = GetTouchId(e.Pointer);

        // Only track movement for contacts that are already active (i.e., button/finger is held).
        // On desktop, PointerMoved fires even without a button down; skip those.
        if (!_activeTouches.ContainsKey(id))
            return;

        var pos = GetPosition(e, sender as Control);
        var point = new TouchPoint(id, pos, TouchPhase.Moved);

        _activeTouches[id] = point;
        RebuildSnapshot();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var id = GetTouchId(e.Pointer);

        if (!_activeTouches.ContainsKey(id))
            return;

        var pos = GetPosition(e, sender as Control);
        var point = new TouchPoint(id, pos, TouchPhase.Ended);

        _activeTouches.Remove(id);
        RebuildSnapshot();
        lock (_pendingEndsLock)
            _pendingEnds.Add(point);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // The system cancelled the pointer (e.g. incoming call on Android).
        var id = GetTouchId(e.Pointer);

        if (!_activeTouches.TryGetValue(id, out var existing))
            return;

        var point = new TouchPoint(existing.Id, existing.Position, TouchPhase.Cancelled);
        _activeTouches.Remove(id);
        RebuildSnapshot();
        lock (_pendingEndsLock)
            _pendingEnds.Add(point);
    }

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ConsumeEndedTouches()
    {
        lock (_pendingEndsLock)
        {
            if (_pendingEnds.Count == 0)
                return Array.Empty<TouchPoint>();

            var snapshot = _pendingEnds.ToArray();
            _pendingEnds.Clear();
            return snapshot;
        }
    }

    private void RebuildSnapshot()
    {
        _activeTouchesSnapshot = _activeTouches.Values.ToArray();
    }

    /// <summary>
    /// Maps an Avalonia <see cref="IPointer"/> to a stable integer touch ID suitable for use in
    /// <see cref="TouchPoint.Id"/>. On desktop (mouse), all contacts map to <c>0</c>.
    /// On touch and stylus devices, the Avalonia pointer ID is used directly.
    /// </summary>
    /// <param name="pointer">The Avalonia pointer to map.</param>
    /// <returns>An integer touch identifier.</returns>
    private static int GetTouchId(IPointer pointer)
        => pointer.Type == PointerType.Mouse ? 0 : (int)pointer.Id;

    private static Point GetPosition(PointerEventArgs e, Control? relativeTo)
    {
        var pos = e.GetPosition(relativeTo);
        return new Point((int)pos.X, (int)pos.Y);
    }

    /// <summary>
    /// Releases all resources held by this adapter, unsubscribing from all Avalonia pointer events.
    /// After disposal, no further touch events will be raised.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _control.PointerPressed -= OnPointerPressed;
        _control.PointerMoved -= OnPointerMoved;
        _control.PointerReleased -= OnPointerReleased;
        _control.PointerCaptureLost -= OnPointerCaptureLost;

        Engine.Logger.LogInformation("AvaloniaTouchInputAdapter disposed.");
    }
}

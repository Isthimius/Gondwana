using System.Collections.Concurrent;
using System.Drawing;
using Gondwana.Blazor.Rendering;
using Gondwana.Input.Touch;
using Microsoft.Extensions.Logging;
using BrowserTouchEventArgs = Microsoft.AspNetCore.Components.Web.TouchEventArgs;
using BrowserTouchPoint = Microsoft.AspNetCore.Components.Web.TouchPoint;
using GondwanaTouchPoint = Gondwana.Input.Touch.TouchPoint;

namespace Gondwana.Blazor.Input.Touch;

/// <summary>
/// Provides a passive touch state adapter for Blazor applications, implementing
/// <see cref="ITouchAdapter"/> by translating Blazor <c>ontouchstart</c>, <c>ontouchmove</c>,
/// <c>ontouchend</c>, and <c>ontouchcancel</c> events into Gondwana touch state.
/// </summary>
/// <remarks>
/// <para>
/// Events are not raised directly; the <see cref="TouchEventPoller"/> polls this adapter each
/// engine frame to detect transitions and raise events.
/// </para>
/// <para>
/// On touch-capable devices each finger contact is tracked by its browser touch identifier.
/// On desktop platforms where touch events are not available, use the mouse adapter for
/// pointer input.
/// </para>
/// </remarks>
public sealed class BlazorTouchAdapter : ITouchAdapter, IDisposable
{
    private readonly BlazorRenderSurfaceComponentBase _component;
    private readonly Dictionary<long, GondwanaTouchPoint> _activeTouches = new();
    private GondwanaTouchPoint[] _activeTouchesSnapshot = Array.Empty<GondwanaTouchPoint>();
    private readonly ConcurrentQueue<GondwanaTouchPoint> _pendingBegins = new();
    private readonly ConcurrentQueue<GondwanaTouchPoint> _pendingEnds = new();
    private bool _isDisposed;

    /// <inheritdoc/>
    public IReadOnlyList<GondwanaTouchPoint> ActiveTouches => _activeTouchesSnapshot;

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorTouchAdapter"/> and attaches it to
    /// the touch events on the specified render surface component.
    /// </summary>
    /// <param name="component">The render surface component to capture touch input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public BlazorTouchAdapter(BlazorBitmapRenderSurfaceComponent component)
        : this((BlazorRenderSurfaceComponentBase)component)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorTouchAdapter"/> for any Gondwana Blazor
    /// render surface.
    /// </summary>
    /// <param name="component">The render surface component to capture touch input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public BlazorTouchAdapter(BlazorRenderSurfaceComponentBase component)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));

        _component.TouchStart += OnTouchStart;
        _component.TouchMove += OnTouchMove;
        _component.TouchEnd += OnTouchEnd;
        _component.TouchCancel += OnTouchCancel;

        Engine.Logger.LogInformation("BlazorTouchAdapter initialized.");
    }

    private void OnTouchStart(BrowserTouchEventArgs e)
    {
        if (_isDisposed) return;
        foreach (var t in e.ChangedTouches)
        {
            var point = new GondwanaTouchPoint((int)t.Identifier, GetPosition(t), TouchPhase.Began);
            _activeTouches[t.Identifier] = point;
            _pendingBegins.Enqueue(point);
        }
        RebuildSnapshot();
    }

    private void OnTouchMove(BrowserTouchEventArgs e)
    {
        if (_isDisposed) return;
        foreach (var t in e.ChangedTouches)
        {
            if (!_activeTouches.ContainsKey(t.Identifier)) continue;
            var point = new GondwanaTouchPoint((int)t.Identifier, GetPosition(t), TouchPhase.Moved);
            _activeTouches[t.Identifier] = point;
        }
        RebuildSnapshot();
    }

    private void OnTouchEnd(BrowserTouchEventArgs e)
    {
        if (_isDisposed) return;
        foreach (var t in e.ChangedTouches)
        {
            if (!_activeTouches.ContainsKey(t.Identifier)) continue;
            var point = new GondwanaTouchPoint((int)t.Identifier, GetPosition(t), TouchPhase.Ended);
            _activeTouches.Remove(t.Identifier);
            _pendingEnds.Enqueue(point);
        }
        RebuildSnapshot();
    }

    private void OnTouchCancel(BrowserTouchEventArgs e)
    {
        if (_isDisposed) return;
        foreach (var t in e.ChangedTouches)
        {
            if (!_activeTouches.TryGetValue(t.Identifier, out var existing)) continue;
            var point = new GondwanaTouchPoint(existing.Id, existing.Position, TouchPhase.Cancelled);
            _activeTouches.Remove(t.Identifier);
            _pendingEnds.Enqueue(point);
        }
        RebuildSnapshot();
    }

    /// <inheritdoc/>
    public IReadOnlyList<GondwanaTouchPoint> ConsumeBeganTouches()
    {
        if (_pendingBegins.IsEmpty)
            return Array.Empty<GondwanaTouchPoint>();

        var snapshot = new List<GondwanaTouchPoint>(_pendingBegins.Count);
        while (_pendingBegins.TryDequeue(out var point))
            snapshot.Add(point);
        return snapshot;
    }

    /// <inheritdoc/>
    public IReadOnlyList<GondwanaTouchPoint> ConsumeEndedTouches()
    {
        if (_pendingEnds.IsEmpty)
            return Array.Empty<GondwanaTouchPoint>();

        var snapshot = new List<GondwanaTouchPoint>(_pendingEnds.Count);
        while (_pendingEnds.TryDequeue(out var point))
            snapshot.Add(point);
        return snapshot;
    }

    private void RebuildSnapshot()
    {
        _activeTouchesSnapshot = _activeTouches.Values.ToArray();
    }

    private static Point GetPosition(BrowserTouchPoint t) =>
        new Point((int)t.ClientX, (int)t.ClientY);

    /// <summary>Releases all resources and removes event handlers registered by this adapter.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _component.TouchStart -= OnTouchStart;
        _component.TouchMove -= OnTouchMove;
        _component.TouchEnd -= OnTouchEnd;
        _component.TouchCancel -= OnTouchCancel;

        Engine.Logger.LogInformation("BlazorTouchAdapter disposed.");
    }
}

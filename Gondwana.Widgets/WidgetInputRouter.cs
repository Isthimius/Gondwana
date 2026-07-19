using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Routes mouse, touch, and keyboard input to registered widgets for a render surface host.
/// </summary>
public sealed class WidgetInputRouter : IDisposable
{
    /// <summary>
    /// Identifies mouse pointer events in routed widget input.
    /// </summary>
    public const int MousePointerId = -1;

    private static readonly MouseButton[] _mouseButtons =
    [
        MouseButton.Left,
        MouseButton.Right,
        MouseButton.Middle
    ];

    private readonly object _syncRoot = new();
    private readonly List<WidgetBase> _widgets = [];
    private readonly Dictionary<int, PointerCapture> _touchCaptures = [];

    private readonly RenderSurfaceHostBase _renderSurfaceHost;
    private readonly KeyboardEventPoller? _keyboardEventPoller;
    private readonly MouseEventPoller? _mouseEventPoller;
    private readonly TouchEventPoller? _touchEventPoller;

    private PointerCapture? _mouseCapture;
    private WidgetHit? _hoveredHit;
    private WidgetBase? _focusedWidget;

    private bool _isStarted;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetInputRouter"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host whose widgets will receive routed input.</param>
    /// <param name="keyboardEventPoller">The keyboard event poller that supplies keyboard input, or <see langword="null"/>.</param>
    /// <param name="mouseEventPoller">The mouse event poller that supplies mouse input, or <see langword="null"/>.</param>
    /// <param name="touchEventPoller">The touch event poller that supplies touch input, or <see langword="null"/>.</param>
    public WidgetInputRouter(
        RenderSurfaceHostBase renderSurfaceHost,
        KeyboardEventPoller? keyboardEventPoller,
        MouseEventPoller? mouseEventPoller,
        TouchEventPoller? touchEventPoller)
    {
        _renderSurfaceHost =
            renderSurfaceHost ??
            throw new ArgumentNullException(nameof(renderSurfaceHost));

        _keyboardEventPoller = keyboardEventPoller;
        _mouseEventPoller = mouseEventPoller;
        _touchEventPoller = touchEventPoller;
    }

    /// <summary>
    /// Gets the widget that currently has keyboard focus.
    /// </summary>
    /// <value>
    /// The focused widget, or <see langword="null"/> if no widget currently has focus.
    /// </value>
    public WidgetBase? FocusedWidget => _focusedWidget;

    /// <summary>
    /// Registers a widget so that it can receive routed input.
    /// </summary>
    /// <param name="widget">The widget to register.</param>
    public void Register(WidgetBase widget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(widget);

        if (!ReferenceEquals(widget.RenderSurfaceHost, _renderSurfaceHost))
        {
            throw new ArgumentException(
                "The widget belongs to a different RenderSurfaceHost.",
                nameof(widget));
        }

        lock (_syncRoot)
        {
            if (_widgets.Contains(widget))
                return;

            _widgets.Add(widget);
            widget.Disposing += OnWidgetDisposing;
        }
    }

    /// <summary>
    /// Unregisters a widget so that it no longer receives routed input.
    /// </summary>
    /// <param name="widget">The widget to unregister.</param>
    public void Unregister(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        Unregister(widget, dispatchPointerUp: true);
    }

    /// <summary>
    /// Moves a registered widget to the front of the input routing order.
    /// </summary>
    /// <param name="widget">The widget to move to the front.</param>
    public void BringToFront(WidgetBase widget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(widget);

        lock (_syncRoot)
        {
            if (!_widgets.Remove(widget))
                throw new InvalidOperationException("The widget is not registered.");

            _widgets.Add(widget);
        }
    }

    /// <summary>
    /// Assigns keyboard focus to the specified widget.
    /// </summary>
    /// <param name="widget">The widget to focus, or <see langword="null"/> to clear focus.</param>
    public void Focus(WidgetBase? widget)
    {
        if (ReferenceEquals(_focusedWidget, widget))
            return;

        if (widget is not null && !CanReceiveKeyboardInput(widget))
            return;

        WidgetBase? previous = _focusedWidget;
        _focusedWidget = widget;

        previous?.DispatchFocusLost();
        widget?.DispatchFocusGained();
    }

    /// <summary>
    /// Clears the current keyboard focus.
    /// </summary>
    public void ClearFocus()
    {
        Focus(null);
    }

    /// <summary>
    /// Starts routing input events from the configured pollers to registered widgets.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isStarted)
            return;

        if (_keyboardEventPoller is not null)
            _keyboardEventPoller.KeyDown += OnKeyDown;

        if (_mouseEventPoller is not null)
            _mouseEventPoller.MouseEvent += OnMouseEvent;

        if (_touchEventPoller is not null)
        {
            _touchEventPoller.TouchBegan += OnTouchBegan;
            _touchEventPoller.TouchMoved += OnTouchMoved;
            _touchEventPoller.TouchEnded += OnTouchEnded;
        }

        WidgetInputRouterRegistry.Attach(
            _renderSurfaceHost,
            this);

        _isStarted = true;
    }

    /// <summary>
    /// Stops routing input events and releases current pointer and focus state.
    /// </summary>
    public void Stop()
    {
        if (!_isStarted)
            return;

        WidgetInputRouterRegistry.Detach(
            _renderSurfaceHost,
            this);

        if (_keyboardEventPoller is not null)
            _keyboardEventPoller.KeyDown -= OnKeyDown;

        if (_mouseEventPoller is not null)
            _mouseEventPoller.MouseEvent -= OnMouseEvent;

        if (_touchEventPoller is not null)
        {
            _touchEventPoller.TouchBegan -= OnTouchBegan;
            _touchEventPoller.TouchMoved -= OnTouchMoved;
            _touchEventPoller.TouchEnded -= OnTouchEnded;
        }

        ClearFocus();

        _mouseCapture = null;
        _touchCaptures.Clear();
        _hoveredHit = null;
        _isStarted = false;
    }

    /// <summary>
    /// Releases the resources used by the router and unregisters all widgets.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();

        WidgetBase[] widgets;

        lock (_syncRoot)
        {
            widgets = [.. _widgets];
            _widgets.Clear();
        }

        foreach (WidgetBase widget in widgets)
            widget.Disposing -= OnWidgetDisposing;

        _disposed = true;
    }

    internal void NotifyWidgetHidden(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ReleaseInputState(widget, dispatchPointerUp: true);
    }

    internal void NotifyWidgetInputDisabled(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ReleaseInputState(widget, dispatchPointerUp: true);
    }

    internal void NotifyWidgetPointerInputDisabled(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ReleasePointerState(widget, dispatchPointerUp: true);
    }

    internal void NotifyWidgetKeyboardFocusDisabled(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ReleaseKeyboardFocus(widget);
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        WidgetBase? target = _focusedWidget;

        if (target is null)
            return;

        if (!CanReceiveKeyboardInput(target))
        {
            ClearFocus();
            return;
        }

        if (!int.TryParse(args.KeyConfig.Key, out int key))
            return;

        target.DispatchKeyboardInput(
            new WidgetKeyboardEventArgs(
                target,
                key,
                args.KeyAction,
                args.Modifiers,
                tick: 0));
    }

    private void OnMouseEvent(MouseEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        Point currentPosition = args.CurrentPosition;
        WidgetHit? initialHit = HitTest(currentPosition);

        UpdateMouseHover(
            initialHit,
            currentPosition,
            args.Tick);

        foreach (MouseButton mouseButton in _mouseButtons)
        {
            if (args.IsButtonJustPressed(mouseButton))
            {
                ProcessMouseDown(
                    initialHit,
                    currentPosition,
                    mouseButton,
                    args.Tick);
            }
        }

        if (args.PreviousPosition != currentPosition)
        {
            ProcessMouseMove(
                args.PreviousPosition,
                currentPosition,
                args.Tick);
        }

        foreach (MouseButton mouseButton in _mouseButtons)
        {
            if (args.IsButtonJustReleased(mouseButton))
            {
                ProcessMouseUp(
                    currentPosition,
                    mouseButton,
                    args.Tick);
            }
        }

        UpdateMouseHover(
            HitTest(currentPosition),
            currentPosition,
            args.Tick);
    }

    private void ProcessMouseDown(
        WidgetHit? hit,
        Point position,
        MouseButton mouseButton,
        long tick)
    {
        if (_mouseCapture is not null || hit is null)
            return;

        FocusFromPointer(hit.Widget);

        WidgetPointerButtonEnum widgetButton =
            MapMouseButton(mouseButton);

        hit.Widget.DispatchPointerDown(
            CreatePointerArgs(
                hit.Widget,
                hit.View,
                position,
                widgetButton,
                tick,
                MousePointerId));

        _mouseCapture = new PointerCapture(
            hit.Widget,
            hit.View,
            widgetButton,
            position);
    }

    private void ProcessMouseMove(
        Point previousPosition,
        Point currentPosition,
        long tick)
    {
        PointerCapture? capture =
            GetValidMouseCapture(tick);

        WidgetHit? hit =
            capture is null
                ? HitTest(currentPosition)
                : null;

        WidgetBase? recipient =
            capture?.Widget ?? hit?.Widget;

        View? view =
            capture?.View ?? hit?.View;

        if (recipient is null || view is null)
            return;

        var delta = new Vector2(
            currentPosition.X - previousPosition.X,
            currentPosition.Y - previousPosition.Y);

        WidgetPointerButtonEnum button =
            capture?.Button ??
            WidgetPointerButtonEnum.None;

        recipient.DispatchPointerMove(
            CreatePointerArgs(
                recipient,
                view,
                currentPosition,
                button,
                tick,
                MousePointerId,
                deltaPx: delta));

        if (capture is not null)
            capture.LastPosition = currentPosition;
    }

    private void ProcessMouseUp(
        Point position,
        MouseButton mouseButton,
        long tick)
    {
        WidgetPointerButtonEnum releasedButton =
            MapMouseButton(mouseButton);

        PointerCapture? capture =
            GetValidMouseCapture(tick);

        if (capture is null ||
            capture.Button != releasedButton)
        {
            return;
        }

        WidgetBase recipient = capture.Widget;

        recipient.DispatchPointerUp(
            CreatePointerArgs(
                recipient,
                capture.View,
                position,
                releasedButton,
                tick,
                MousePointerId));

        WidgetHit? releaseHit = HitTest(position);

        if (IsSameHit(
            releaseHit,
            recipient,
            capture.View))
        {
            recipient.DispatchPointerClick(
                CreatePointerArgs(
                    recipient,
                    capture.View,
                    position,
                    releasedButton,
                    tick,
                    MousePointerId,
                    clickCount: 1));
        }

        _mouseCapture = null;
    }

    private void UpdateMouseHover(
        WidgetHit? hit,
        Point position,
        long tick)
    {
        if (IsSameHit(
            _hoveredHit,
            hit))
        {
            return;
        }

        WidgetHit? previous = _hoveredHit;
        _hoveredHit = hit;

        if (previous is not null)
        {
            previous.Widget.DispatchPointerLeave(
                CreatePointerArgs(
                    previous.Widget,
                    previous.View,
                    position,
                    WidgetPointerButtonEnum.None,
                    tick,
                    MousePointerId));
        }

        if (hit is not null)
        {
            hit.Widget.DispatchPointerEnter(
                CreatePointerArgs(
                    hit.Widget,
                    hit.View,
                    position,
                    WidgetPointerButtonEnum.None,
                    tick,
                    MousePointerId));
        }
    }

    private PointerCapture? GetValidMouseCapture(
        long tick)
    {
        if (_mouseCapture is null)
            return null;

        if (IsPointerRoutable(
            _mouseCapture.Widget,
            _mouseCapture.View))
        {
            return _mouseCapture;
        }

        _mouseCapture.Widget.DispatchPointerUp(
            CreatePointerArgs(
                _mouseCapture.Widget,
                _mouseCapture.View,
                _mouseCapture.LastPosition,
                _mouseCapture.Button,
                tick,
                MousePointerId));

        _mouseCapture = null;
        return null;
    }

    private void OnTouchBegan(
        object? sender,
        TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (_touchCaptures.ContainsKey(touch.Id))
            return;

        WidgetHit? hit =
            HitTest(touch.Position);

        if (hit is null)
            return;

        FocusFromPointer(hit.Widget);

        hit.Widget.DispatchPointerDown(
            CreatePointerArgs(
                hit.Widget,
                hit.View,
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id));

        _touchCaptures[touch.Id] =
            new PointerCapture(
                hit.Widget,
                hit.View,
                WidgetPointerButtonEnum.Touch,
                touch.Position);
    }

    private void OnTouchMoved(
        object? sender,
        TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (!_touchCaptures.TryGetValue(
            touch.Id,
            out PointerCapture? capture))
        {
            return;
        }

        if (!IsPointerRoutable(
            capture.Widget,
            capture.View))
        {
            capture.Widget.DispatchPointerUp(
                CreatePointerArgs(
                    capture.Widget,
                    capture.View,
                    capture.LastPosition,
                    WidgetPointerButtonEnum.Touch,
                    args.Tick,
                    touch.Id));

            _touchCaptures.Remove(touch.Id);
            return;
        }

        var delta = new Vector2(
            touch.Position.X - capture.LastPosition.X,
            touch.Position.Y - capture.LastPosition.Y);

        capture.Widget.DispatchPointerMove(
            CreatePointerArgs(
                capture.Widget,
                capture.View,
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id,
                deltaPx: delta));

        capture.LastPosition = touch.Position;
    }

    private void OnTouchEnded(
        object? sender,
        TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (!_touchCaptures.Remove(
            touch.Id,
            out PointerCapture? capture))
        {
            return;
        }

        WidgetBase recipient = capture.Widget;

        recipient.DispatchPointerUp(
            CreatePointerArgs(
                recipient,
                capture.View,
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id));

        WidgetHit? releaseHit =
            HitTest(touch.Position);

        if (touch.Phase != TouchPhase.Cancelled &&
            IsSameHit(
                releaseHit,
                recipient,
                capture.View))
        {
            recipient.DispatchPointerClick(
                CreatePointerArgs(
                    recipient,
                    capture.View,
                    touch.Position,
                    WidgetPointerButtonEnum.Touch,
                    args.Tick,
                    touch.Id,
                    clickCount: 1));
        }
    }

    private WidgetHit? HitTest(
        Point screenPositionPx)
    {
        View? view =
            GetTopmostViewAt(screenPositionPx);

        if (view is null)
            return null;

        WidgetBase[] snapshot;

        lock (_syncRoot)
            snapshot = [.. _widgets];

        for (int index = snapshot.Length - 1;
             index >= 0;
             index--)
        {
            WidgetBase widget = snapshot[index];

            if (widget.HitTest(
                view,
                screenPositionPx))
            {
                return new WidgetHit(
                    widget,
                    view);
            }
        }

        return null;
    }

    private View? GetTopmostViewAt(
        Point screenPositionPx)
    {
        var views =
            _renderSurfaceHost.ViewManager.Views;

        for (int index = views.Count - 1;
             index >= 0;
             index--)
        {
            View view = views[index];

            if (view.Viewport.TargetRectPx.Contains(
                screenPositionPx))
            {
                return view;
            }
        }

        return null;
    }

    private bool IsRegistered(
        WidgetBase widget)
    {
        lock (_syncRoot)
            return _widgets.Contains(widget);
    }

    private bool IsManagedView(
        View view)
    {
        return _renderSurfaceHost
            .ViewManager
            .Views
            .Contains(view);
    }

    private bool HasValidTarget(
        WidgetBase widget,
        View? routedView = null)
    {
        if (widget.Mode == DirectDrawingMode.SceneLayer)
            return widget.SceneLayer is not null;

        if (widget.View is null ||
            !IsManagedView(widget.View))
        {
            return false;
        }

        return routedView is null ||
               ReferenceEquals(
                   widget.View,
                   routedView);
    }

    private bool IsPointerRoutable(
        WidgetBase widget,
        View view)
    {
        return IsRegistered(widget) &&
               IsManagedView(view) &&
               ReferenceEquals(
                   widget.RenderSurfaceHost,
                   _renderSurfaceHost) &&
               HasValidTarget(
                   widget,
                   view) &&
               widget.IsInputEnabled &&
               widget.IsPointerInputEnabled &&
               widget.Visible;
    }

    private bool CanReceiveKeyboardInput(
        WidgetBase widget)
    {
        return IsRegistered(widget) &&
               ReferenceEquals(
                   widget.RenderSurfaceHost,
                   _renderSurfaceHost) &&
               HasValidTarget(widget) &&
               widget.Visible &&
               widget.IsInputEnabled &&
               widget.IsKeyboardInputEnabled &&
               widget.CanReceiveFocus;
    }

    private void FocusFromPointer(
        WidgetBase widget)
    {
        if (widget.IsInputEnabled &&
            widget.IsKeyboardInputEnabled &&
            widget.CanReceiveFocus)
        {
            Focus(widget);
        }
    }

    private void ReleaseInputState(
        WidgetBase widget,
        bool dispatchPointerUp)
    {
        ReleasePointerState(
            widget,
            dispatchPointerUp);

        ReleaseKeyboardFocus(widget);
    }

    private void ReleasePointerState(
        WidgetBase widget,
        bool dispatchPointerUp)
    {
        if (ReferenceEquals(
            _hoveredHit?.Widget,
            widget))
        {
            _hoveredHit = null;
        }

        if (ReferenceEquals(
            _mouseCapture?.Widget,
            widget))
        {
            PointerCapture capture =
                _mouseCapture;

            if (dispatchPointerUp)
            {
                widget.DispatchPointerUp(
                    CreatePointerArgs(
                        widget,
                        capture.View,
                        capture.LastPosition,
                        capture.Button,
                        tick: 0,
                        pointerId: MousePointerId));
            }

            _mouseCapture = null;
        }

        int[] touchIds = _touchCaptures
            .Where(x =>
                ReferenceEquals(
                    x.Value.Widget,
                    widget))
            .Select(x => x.Key)
            .ToArray();

        foreach (int touchId in touchIds)
        {
            PointerCapture capture =
                _touchCaptures[touchId];

            if (dispatchPointerUp)
            {
                widget.DispatchPointerUp(
                    CreatePointerArgs(
                        widget,
                        capture.View,
                        capture.LastPosition,
                        WidgetPointerButtonEnum.Touch,
                        tick: 0,
                        pointerId: touchId));
            }

            _touchCaptures.Remove(touchId);
        }
    }

    private void ReleaseKeyboardFocus(
        WidgetBase widget)
    {
        if (ReferenceEquals(
            _focusedWidget,
            widget))
        {
            Focus(null);
        }
    }

    private static bool IsSameHit(
        WidgetHit? left,
        WidgetHit? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return ReferenceEquals(
                   left.Widget,
                   right.Widget) &&
               ReferenceEquals(
                   left.View,
                   right.View);
    }

    private static bool IsSameHit(
        WidgetHit? hit,
        WidgetBase widget,
        View view)
    {
        return hit is not null &&
               ReferenceEquals(
                   hit.Widget,
                   widget) &&
               ReferenceEquals(
                   hit.View,
                   view);
    }

    private static WidgetPointerEventArgs CreatePointerArgs(
        WidgetBase widget,
        View view,
        Point position,
        WidgetPointerButtonEnum button,
        long tick,
        int pointerId,
        int clickCount = 0,
        Vector2 deltaPx = default)
    {
        return new WidgetPointerEventArgs(
            widget,
            view,
            new PointF(
                position.X,
                position.Y),
            button,
            clickCount,
            deltaPx,
            tick,
            pointerId);
    }

    private static WidgetPointerButtonEnum MapMouseButton(
        MouseButton button)
    {
        return button switch
        {
            MouseButton.Left =>
                WidgetPointerButtonEnum.Left,

            MouseButton.Right =>
                WidgetPointerButtonEnum.Right,

            MouseButton.Middle =>
                WidgetPointerButtonEnum.Middle,

            _ =>
                WidgetPointerButtonEnum.None
        };
    }

    private void OnWidgetDisposing(
        object? sender,
        IDirectDrawable drawing)
    {
        if (drawing is WidgetBase widget)
        {
            Unregister(
                widget,
                dispatchPointerUp: false);
        }
    }

    private void Unregister(
        WidgetBase widget,
        bool dispatchPointerUp)
    {
        bool removed;

        lock (_syncRoot)
            removed = _widgets.Remove(widget);

        if (!removed)
            return;

        widget.Disposing -= OnWidgetDisposing;

        ReleaseInputState(
            widget,
            dispatchPointerUp);
    }

    private sealed class WidgetHit
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WidgetHit"/> class.
        /// </summary>
        /// <param name="widget">The widget that was hit.</param>
        /// <param name="view">The view in which the widget was hit.</param>
        public WidgetHit(
            WidgetBase widget,
            View view)
        {
            Widget = widget;
            View = view;
        }

        /// <summary>
        /// Gets the widget that was hit.
        /// </summary>
        public WidgetBase Widget { get; }

        /// <summary>
        /// Gets the view in which the widget was hit.
        /// </summary>
        public View View { get; }
    }

    private sealed class PointerCapture
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PointerCapture"/> class.
        /// </summary>
        /// <param name="widget">The widget that captured the pointer.</param>
        /// <param name="view">The view associated with the captured pointer event.</param>
        /// <param name="button">The button associated with the captured pointer.</param>
        /// <param name="lastPosition">The last known pointer position.</param>
        public PointerCapture(
            WidgetBase widget,
            View view,
            WidgetPointerButtonEnum button,
            Point lastPosition)
        {
            Widget = widget;
            View = view;
            Button = button;
            LastPosition = lastPosition;
        }

        /// <summary>
        /// Gets the widget that captured the pointer.
        /// </summary>
        public WidgetBase Widget { get; }

        /// <summary>
        /// Gets the view associated with the captured pointer event.
        /// </summary>
        public View View { get; }

        /// <summary>
        /// Gets the button associated with the captured pointer.
        /// </summary>
        public WidgetPointerButtonEnum Button { get; }

        /// <summary>
        /// Gets or sets the last known pointer position.
        /// </summary>
        /// <value>
        /// The last known pointer position, in pixels.
        /// </value>
        public Point LastPosition { get; set; }
    }
}
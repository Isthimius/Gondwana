using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;
using Gondwana.Rendering;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

public sealed class WidgetInputRouter : IDisposable
{
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
    private WidgetBase? _hoveredWidget;
    private WidgetBase? _focusedWidget;

    private bool _isStarted;
    private bool _disposed;

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

    public WidgetBase? FocusedWidget => _focusedWidget;

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

    public void Unregister(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        Unregister(widget, dispatchPointerUp: true);
    }

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

    public void ClearFocus()
    {
        Focus(null);
    }

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

        WidgetInputRouterRegistry.Attach(_renderSurfaceHost, this);
        _isStarted = true;
    }

    public void Stop()
    {
        if (!_isStarted)
            return;

        WidgetInputRouterRegistry.Detach(_renderSurfaceHost, this);

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
        _hoveredWidget = null;
        _isStarted = false;
    }

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
                KeyboardModifierState.None,
                tick: 0));
    }

    private void OnMouseEvent(MouseEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        Point currentPosition = args.CurrentPosition;
        WidgetBase? initialHit = HitTest(currentPosition);

        UpdateMouseHover(initialHit, currentPosition, args.Tick);

        foreach (MouseButton mouseButton in _mouseButtons)
        {
            if (args.IsButtonJustPressed(mouseButton))
                ProcessMouseDown(initialHit, currentPosition, mouseButton, args.Tick);
        }

        if (args.PreviousPosition != currentPosition)
            ProcessMouseMove(args.PreviousPosition, currentPosition, args.Tick);

        foreach (MouseButton mouseButton in _mouseButtons)
        {
            if (args.IsButtonJustReleased(mouseButton))
                ProcessMouseUp(currentPosition, mouseButton, args.Tick);
        }

        UpdateMouseHover(HitTest(currentPosition), currentPosition, args.Tick);
    }

    private void ProcessMouseDown(
        WidgetBase? hitWidget,
        Point position,
        MouseButton mouseButton,
        long tick)
    {
        if (_mouseCapture is not null || hitWidget is null)
            return;

        FocusFromPointer(hitWidget);

        WidgetPointerButtonEnum widgetButton = MapMouseButton(mouseButton);

        hitWidget.DispatchPointerDown(
            CreatePointerArgs(
                hitWidget,
                position,
                widgetButton,
                tick,
                MousePointerId));

        _mouseCapture = new PointerCapture(
            hitWidget,
            widgetButton,
            position);
    }

    private void ProcessMouseMove(
        Point previousPosition,
        Point currentPosition,
        long tick)
    {
        PointerCapture? capture = GetValidMouseCapture(tick);
        WidgetBase? recipient = capture?.Widget ?? HitTest(currentPosition);

        if (recipient is null)
            return;

        var delta = new Vector2(
            currentPosition.X - previousPosition.X,
            currentPosition.Y - previousPosition.Y);

        WidgetPointerButtonEnum button =
            capture?.Button ?? WidgetPointerButtonEnum.None;

        recipient.DispatchPointerMove(
            CreatePointerArgs(
                recipient,
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
        WidgetPointerButtonEnum releasedButton = MapMouseButton(mouseButton);
        PointerCapture? capture = GetValidMouseCapture(tick);

        if (capture is null || capture.Button != releasedButton)
            return;

        WidgetBase recipient = capture.Widget;

        recipient.DispatchPointerUp(
            CreatePointerArgs(
                recipient,
                position,
                releasedButton,
                tick,
                MousePointerId));

        if (ReferenceEquals(HitTest(position), recipient))
        {
            recipient.DispatchPointerClick(
                CreatePointerArgs(
                    recipient,
                    position,
                    releasedButton,
                    tick,
                    MousePointerId,
                    clickCount: 1));
        }

        _mouseCapture = null;
    }

    private void UpdateMouseHover(
        WidgetBase? hitWidget,
        Point position,
        long tick)
    {
        if (ReferenceEquals(_hoveredWidget, hitWidget))
            return;

        WidgetBase? previous = _hoveredWidget;
        _hoveredWidget = hitWidget;

        if (previous is not null)
        {
            previous.DispatchPointerLeave(
                CreatePointerArgs(
                    previous,
                    position,
                    WidgetPointerButtonEnum.None,
                    tick,
                    MousePointerId));
        }

        if (hitWidget is not null)
        {
            hitWidget.DispatchPointerEnter(
                CreatePointerArgs(
                    hitWidget,
                    position,
                    WidgetPointerButtonEnum.None,
                    tick,
                    MousePointerId));
        }
    }

    private PointerCapture? GetValidMouseCapture(long tick)
    {
        if (_mouseCapture is null)
            return null;

        if (IsPointerRoutable(_mouseCapture.Widget))
            return _mouseCapture;

        _mouseCapture.Widget.DispatchPointerUp(
            CreatePointerArgs(
                _mouseCapture.Widget,
                _mouseCapture.LastPosition,
                _mouseCapture.Button,
                tick,
                MousePointerId));

        _mouseCapture = null;
        return null;
    }

    private void OnTouchBegan(object? sender, TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (_touchCaptures.ContainsKey(touch.Id))
            return;

        WidgetBase? target = HitTest(touch.Position);

        if (target is null)
            return;

        FocusFromPointer(target);

        target.DispatchPointerDown(
            CreatePointerArgs(
                target,
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id));

        _touchCaptures[touch.Id] = new PointerCapture(
            target,
            WidgetPointerButtonEnum.Touch,
            touch.Position);
    }

    private void OnTouchMoved(object? sender, TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (!_touchCaptures.TryGetValue(touch.Id, out PointerCapture? capture))
            return;

        if (!IsPointerRoutable(capture.Widget))
        {
            capture.Widget.DispatchPointerUp(
                CreatePointerArgs(
                    capture.Widget,
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
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id,
                deltaPx: delta));

        capture.LastPosition = touch.Position;
    }

    private void OnTouchEnded(object? sender, TouchEventArgs args)
    {
        if (!_isStarted || _disposed)
            return;

        TouchPoint touch = args.Touch;

        if (!_touchCaptures.Remove(touch.Id, out PointerCapture? capture))
            return;

        WidgetBase recipient = capture.Widget;

        recipient.DispatchPointerUp(
            CreatePointerArgs(
                recipient,
                touch.Position,
                WidgetPointerButtonEnum.Touch,
                args.Tick,
                touch.Id));

        if (touch.Phase != TouchPhase.Cancelled &&
            ReferenceEquals(HitTest(touch.Position), recipient))
        {
            recipient.DispatchPointerClick(
                CreatePointerArgs(
                    recipient,
                    touch.Position,
                    WidgetPointerButtonEnum.Touch,
                    args.Tick,
                    touch.Id,
                    clickCount: 1));
        }
    }

    private WidgetBase? HitTest(Point screenPositionPx)
    {
        WidgetBase[] snapshot;

        lock (_syncRoot)
            snapshot = [.. _widgets];

        for (int index = snapshot.Length - 1; index >= 0; index--)
        {
            WidgetBase widget = snapshot[index];

            if (widget.HitTest(screenPositionPx))
                return widget;
        }

        return null;
    }

    private bool IsRegistered(WidgetBase widget)
    {
        lock (_syncRoot)
            return _widgets.Contains(widget);
    }

    private bool IsPointerRoutable(WidgetBase widget)
    {
        return IsRegistered(widget) &&
               ReferenceEquals(widget.RenderSurfaceHost, _renderSurfaceHost) &&
               widget.IsInputEnabled &&
               widget.IsPointerInputEnabled &&
               widget.Visible;
    }

    private bool CanReceiveKeyboardInput(WidgetBase widget)
    {
        return IsRegistered(widget) &&
               ReferenceEquals(widget.RenderSurfaceHost, _renderSurfaceHost) &&
               widget.Visible &&
               widget.IsInputEnabled &&
               widget.IsKeyboardInputEnabled &&
               widget.CanReceiveFocus;
    }

    private void FocusFromPointer(WidgetBase widget)
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
        ReleasePointerState(widget, dispatchPointerUp);
        ReleaseKeyboardFocus(widget);
    }

    private void ReleasePointerState(
        WidgetBase widget,
        bool dispatchPointerUp)
    {
        if (ReferenceEquals(_hoveredWidget, widget))
            _hoveredWidget = null;

        if (ReferenceEquals(_mouseCapture?.Widget, widget))
        {
            PointerCapture capture = _mouseCapture;

            if (dispatchPointerUp)
            {
                widget.DispatchPointerUp(
                    CreatePointerArgs(
                        widget,
                        capture.LastPosition,
                        capture.Button,
                        tick: 0,
                        pointerId: MousePointerId));
            }

            _mouseCapture = null;
        }

        int[] touchIds = _touchCaptures
            .Where(x => ReferenceEquals(x.Value.Widget, widget))
            .Select(x => x.Key)
            .ToArray();

        foreach (int touchId in touchIds)
        {
            PointerCapture capture = _touchCaptures[touchId];

            if (dispatchPointerUp)
            {
                widget.DispatchPointerUp(
                    CreatePointerArgs(
                        widget,
                        capture.LastPosition,
                        WidgetPointerButtonEnum.Touch,
                        tick: 0,
                        pointerId: touchId));
            }

            _touchCaptures.Remove(touchId);
        }
    }

    private void ReleaseKeyboardFocus(WidgetBase widget)
    {
        if (ReferenceEquals(_focusedWidget, widget))
            Focus(null);
    }

    private static WidgetPointerEventArgs CreatePointerArgs(
        WidgetBase widget,
        Point position,
        WidgetPointerButtonEnum button,
        long tick,
        int pointerId,
        int clickCount = 0,
        Vector2 deltaPx = default)
    {
        return new WidgetPointerEventArgs(
            widget,
            new PointF(position.X, position.Y),
            button,
            clickCount,
            deltaPx,
            tick,
            pointerId);
    }

    private static WidgetPointerButtonEnum MapMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => WidgetPointerButtonEnum.Left,
            MouseButton.Right => WidgetPointerButtonEnum.Right,
            MouseButton.Middle => WidgetPointerButtonEnum.Middle,
            _ => WidgetPointerButtonEnum.None
        };
    }

    private void OnWidgetDisposing(
        object? sender,
        IDirectDrawable drawing)
    {
        if (drawing is WidgetBase widget)
            Unregister(widget, dispatchPointerUp: false);
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

        ReleaseInputState(widget, dispatchPointerUp);
    }

    private sealed class PointerCapture
    {
        public PointerCapture(
            WidgetBase widget,
            WidgetPointerButtonEnum button,
            Point lastPosition)
        {
            Widget = widget;
            Button = button;
            LastPosition = lastPosition;
        }

        public WidgetBase Widget { get; }
        public WidgetPointerButtonEnum Button { get; }
        public Point LastPosition { get; set; }
    }
}
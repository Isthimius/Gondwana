using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Widgets;

public abstract class WidgetBase : DirectComposite
{
    private bool _isInputEnabled = true;
    private bool _isPointerInputEnabled = true;
    private bool _isKeyboardInputEnabled;
    private bool _canReceiveFocus;

    public event Action? Shown;
    public event Action? Hidden;
    public event Action? Activated;
    public event Action? Cancelled;

    public event Action<WidgetPointerEventArgs>? PointerEnter;
    public event Action<WidgetPointerEventArgs>? PointerLeave;
    public event Action<WidgetPointerEventArgs>? PointerDown;
    public event Action<WidgetPointerEventArgs>? PointerMove;
    public event Action<WidgetPointerEventArgs>? PointerUp;
    public event Action<WidgetPointerEventArgs>? PointerClick;

    public event Action? FocusGained;
    public event Action? FocusLost;
    public event Action<WidgetKeyboardEventArgs>? KeyboardInput;

    protected WidgetBase(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    public bool IsInputEnabled
    {
        get => _isInputEnabled;
        set
        {
            if (_isInputEnabled == value)
                return;

            _isInputEnabled = value;

            if (!value)
                WidgetInputRouterRegistry.NotifyInputDisabled(this);
        }
    }

    public bool IsPointerInputEnabled
    {
        get => _isPointerInputEnabled;
        set
        {
            if (_isPointerInputEnabled == value)
                return;

            _isPointerInputEnabled = value;

            if (!value)
                WidgetInputRouterRegistry.NotifyPointerInputDisabled(this);
        }
    }

    public bool IsKeyboardInputEnabled
    {
        get => _isKeyboardInputEnabled;
        set
        {
            if (_isKeyboardInputEnabled == value)
                return;

            _isKeyboardInputEnabled = value;

            if (!value)
                WidgetInputRouterRegistry.NotifyKeyboardFocusDisabled(this);
        }
    }

    public bool CanReceiveFocus
    {
        get => _canReceiveFocus;
        set
        {
            if (_canReceiveFocus == value)
                return;

            _canReceiveFocus = value;

            if (!value)
                WidgetInputRouterRegistry.NotifyKeyboardFocusDisabled(this);
        }
    }

    public bool IsFocused { get; internal set; }

    public virtual bool HitTest(Point screenPositionPx)
    {
        return IsInputEnabled &&
               IsPointerInputEnabled &&
               Visible &&
               ScreenBounds.Contains(screenPositionPx);
    }

    public WidgetBase Show()
    {
        WidgetInputRouterRegistry.TryRegister(this);

        SetIsVisible(true);

        ProcessShown();
        OnShown();
        Shown?.Invoke();

        return this;
    }

    public WidgetBase Hide()
    {
        WidgetInputRouterRegistry.NotifyHidden(this);

        SetIsVisible(false);

        ProcessHidden();
        OnHidden();
        Hidden?.Invoke();

        return this;
    }

    public WidgetBase Activate()
    {
        WidgetInputRouterRegistry.TryBringToFront(this);

        ProcessActivated();
        OnActivated();
        Activated?.Invoke();

        return this;
    }

    public WidgetBase Cancel()
    {
        ProcessCancelled();
        OnCancelled();
        Cancelled?.Invoke();

        return this;
    }

    protected internal void DispatchPointerEnter(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerEnter(args);
        OnPointerEnter(args);
        PointerEnter?.Invoke(args);
    }

    protected internal void DispatchPointerLeave(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerLeave(args);
        OnPointerLeave(args);
        PointerLeave?.Invoke(args);
    }

    protected internal void DispatchPointerDown(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerDown(args);
        OnPointerDown(args);
        PointerDown?.Invoke(args);
    }

    protected internal void DispatchPointerMove(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerMove(args);
        OnPointerMove(args);
        PointerMove?.Invoke(args);
    }

    protected internal void DispatchPointerUp(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerUp(args);
        OnPointerUp(args);
        PointerUp?.Invoke(args);
    }

    protected internal void DispatchPointerClick(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!ShouldDispatchPointerClick(args))
            return;

        OnPointerClick(args);
        PointerClick?.Invoke(args);
    }

    protected internal void DispatchFocusGained()
    {
        IsFocused = true;

        ProcessFocusGained();
        OnFocusGained();
        FocusGained?.Invoke();
    }

    protected internal void DispatchFocusLost()
    {
        IsFocused = false;

        ProcessFocusLost();
        OnFocusLost();
        FocusLost?.Invoke();
    }

    protected internal void DispatchKeyboardInput(WidgetKeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessKeyboardInput(args);
        OnKeyboardInput(args);
        KeyboardInput?.Invoke(args);
    }

    protected virtual void ProcessShown() { }
    protected virtual void ProcessHidden() { }
    protected virtual void ProcessActivated() { }
    protected virtual void ProcessCancelled() { }

    protected virtual void ProcessPointerEnter(WidgetPointerEventArgs args) { }
    protected virtual void ProcessPointerLeave(WidgetPointerEventArgs args) { }
    protected virtual void ProcessPointerDown(WidgetPointerEventArgs args) { }
    protected virtual void ProcessPointerMove(WidgetPointerEventArgs args) { }
    protected virtual void ProcessPointerUp(WidgetPointerEventArgs args) { }

    protected virtual void ProcessFocusGained() { }
    protected virtual void ProcessFocusLost() { }
    protected virtual void ProcessKeyboardInput(WidgetKeyboardEventArgs args) { }

    protected virtual bool ShouldDispatchPointerClick(WidgetPointerEventArgs args)
    {
        return true;
    }

    protected virtual void OnShown() { }
    protected virtual void OnHidden() { }
    protected virtual void OnActivated() { }
    protected virtual void OnCancelled() { }

    protected virtual void OnPointerEnter(WidgetPointerEventArgs args) { }
    protected virtual void OnPointerLeave(WidgetPointerEventArgs args) { }
    protected virtual void OnPointerDown(WidgetPointerEventArgs args) { }
    protected virtual void OnPointerMove(WidgetPointerEventArgs args) { }
    protected virtual void OnPointerUp(WidgetPointerEventArgs args) { }
    protected virtual void OnPointerClick(WidgetPointerEventArgs args) { }

    protected virtual void OnFocusGained() { }
    protected virtual void OnFocusLost() { }
    protected virtual void OnKeyboardInput(WidgetKeyboardEventArgs args) { }
}
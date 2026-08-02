using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets;

/// <summary>
/// Provides the base implementation for widgets that can be drawn and receive pointer, keyboard, and focus input.
/// </summary>
public abstract class WidgetBase : DirectComposite
{
    private bool _isInputEnabled = true;
    private bool _isPointerInputEnabled = true;
    private bool _isKeyboardInputEnabled;
    private bool _canReceiveFocus;

    /// <summary>
    /// Occurs when the widget is shown.
    /// </summary>
    public event Action? Shown;

    /// <summary>
    /// Occurs when the widget is hidden.
    /// </summary>
    public event Action? Hidden;

    /// <summary>
    /// Occurs when the widget is activated.
    /// </summary>
    public event Action? Activated;

    /// <summary>
    /// Occurs when the widget is cancelled.
    /// </summary>
    public event Action? Cancelled;

    /// <summary>
    /// Occurs when a pointer enters the widget bounds.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerEnter;

    /// <summary>
    /// Occurs when a pointer leaves the widget bounds.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerLeave;

    /// <summary>
    /// Occurs when a pointer button or touch press begins on the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerDown;

    /// <summary>
    /// Occurs when a pointer moves while routed to the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerMove;

    /// <summary>
    /// Occurs when a pointer button or touch press is released for the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerUp;

    /// <summary>
    /// Occurs when a pointer press and release completes as a click on the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerClick;

    /// <summary>
    /// Occurs when the widget gains keyboard focus.
    /// </summary>
    public event Action? FocusGained;

    /// <summary>
    /// Occurs when the widget loses keyboard focus.
    /// </summary>
    public event Action? FocusLost;

    /// <summary>
    /// Occurs when keyboard input is routed to the widget.
    /// </summary>
    public event Action<WidgetKeyboardEventArgs>? KeyboardInput;

    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetBase"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the widget.</param>
    /// <param name="mode">The drawing mode used to render the widget.</param>
    /// <param name="anchor">The anchor position used by the widget.</param>
    /// <param name="nickname">The optional nickname assigned to the widget.</param>
    protected WidgetBase(RenderSurfaceHostBase renderSurfaceHost,
                         DirectDrawingMode mode,
                         PointF anchor = default,
                         string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether the widget can participate in input routing.
    /// </summary>
    /// <value>
    /// <c>true</c> if input is enabled for the widget; otherwise, <c>false</c>.
    /// </value>
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

    /// <summary>
    /// Gets or sets a value indicating whether the widget can receive pointer input.
    /// </summary>
    /// <value>
    /// <c>true</c> if pointer input is enabled for the widget; otherwise, <c>false</c>.
    /// </value>
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

    /// <summary>
    /// Gets or sets a value indicating whether the widget can receive keyboard input.
    /// </summary>
    /// <value>
    /// <c>true</c> if keyboard input is enabled for the widget; otherwise, <c>false</c>.
    /// </value>
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

    /// <summary>
    /// Gets or sets a value indicating whether the widget can receive keyboard focus.
    /// </summary>
    /// <value>
    /// <c>true</c> if the widget can receive focus; otherwise, <c>false</c>.
    /// </value>
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

    /// <summary>
    /// Gets a value indicating whether the widget currently has keyboard focus.
    /// </summary>
    /// <value>
    /// <c>true</c> if the widget is focused; otherwise, <c>false</c>.
    /// </value>
    public bool IsFocused { get; internal set; }

    /// <summary>
    /// Determines whether the widget can be hit at the specified screen position within the specified view.
    /// </summary>
    /// <param name="view">The view to evaluate the hit test against.</param>
    /// <param name="screenPositionPx">The screen position, in pixels, to test.</param>
    /// <returns>
    /// <c>true</c> if the widget contains the specified screen position; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool HitTest(View view,
                                Point screenPositionPx)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!IsInputEnabled ||
            !IsPointerInputEnabled ||
            !Visible)
        {
            return false;
        }

        if (Mode == DirectDrawingMode.View
                 && !ReferenceEquals(View, view))
        {
            return false;
        }

        RectangleF screenBounds = GetDrawLocationScreen(view);

        return !screenBounds.IsEmpty
            && screenBounds.Contains(screenPositionPx.X, screenPositionPx.Y);
    }

    /// <summary>
    /// Determines whether the widget can be hit at the specified screen position.
    /// </summary>
    /// <param name="screenPositionPx">The screen position, in pixels, to test.</param>
    /// <returns>
    /// <c>true</c> if the widget contains the specified screen position; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool HitTest(Point screenPositionPx)
    {
        var views = RenderSurfaceHost.ViewManager.Views;

        for (int index = views.Count - 1; index >= 0; index--)
        {
            View view = views[index];

            if (!view.Viewport.TargetRectPx.Contains(screenPositionPx))
            {
                continue;
            }

            return HitTest(view, screenPositionPx);
        }

        return false;
    }

    /// <summary>
    /// Shows the widget and registers it for input routing.
    /// </summary>
    /// <returns>The current widget instance.</returns>
    public WidgetBase Show()
    {
        WidgetInputRouterRegistry.TryRegister(this);

        SetIsVisible(true);

        ProcessShown();
        OnShown();
        Shown?.Invoke();

        return this;
    }

    /// <summary>
    /// Hides the widget and releases any routed input state.
    /// </summary>
    /// <returns>The current widget instance.</returns>
    public WidgetBase Hide()
    {
        WidgetInputRouterRegistry.NotifyHidden(this);

        SetIsVisible(false);

        ProcessHidden();
        OnHidden();
        Hidden?.Invoke();

        return this;
    }

    /// <summary>
    /// Activates the widget and brings it to the front of the input order.
    /// </summary>
    /// <returns>The current widget instance.</returns>
    public WidgetBase Activate()
    {
        WidgetInputRouterRegistry.TryBringToFront(this);

        ProcessActivated();
        OnActivated();
        Activated?.Invoke();

        return this;
    }

    /// <summary>
    /// Cancels the widget.
    /// </summary>
    /// <returns>The current widget instance.</returns>
    public WidgetBase Cancel()
    {
        ProcessCancelled();
        OnCancelled();
        Cancelled?.Invoke();

        return this;
    }

    /// <summary>
    /// Dispatches a pointer-enter event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerEnter(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerEnter(args);
        OnPointerEnter(args);
        PointerEnter?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-leave event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerLeave(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerLeave(args);
        OnPointerLeave(args);
        PointerLeave?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-down event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerDown(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerDown(args);
        OnPointerDown(args);
        PointerDown?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-move event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerMove(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerMove(args);
        OnPointerMove(args);
        PointerMove?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-up event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerUp(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerUp(args);
        OnPointerUp(args);
        PointerUp?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-click event to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected internal void DispatchPointerClick(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!ShouldDispatchPointerClick(args))
            return;

        OnPointerClick(args);
        PointerClick?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a focus-gained event to the widget.
    /// </summary>
    protected internal void DispatchFocusGained()
    {
        IsFocused = true;

        ProcessFocusGained();
        OnFocusGained();
        FocusGained?.Invoke();
    }

    /// <summary>
    /// Dispatches a focus-lost event to the widget.
    /// </summary>
    protected internal void DispatchFocusLost()
    {
        IsFocused = false;

        ProcessFocusLost();
        OnFocusLost();
        FocusLost?.Invoke();
    }

    /// <summary>
    /// Dispatches keyboard input to the widget.
    /// </summary>
    /// <param name="args">The keyboard event data.</param>
    protected internal void DispatchKeyboardInput(WidgetKeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessKeyboardInput(args);
        OnKeyboardInput(args);
        KeyboardInput?.Invoke(args);
    }

    /// <summary>
    /// Performs processing when the widget is shown before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessShown() { }

    /// <summary>
    /// Performs processing when the widget is hidden before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessHidden() { }

    /// <summary>
    /// Performs processing when the widget is activated before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessActivated() { }

    /// <summary>
    /// Performs processing when the widget is cancelled before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessCancelled() { }

    /// <summary>
    /// Performs processing when a pointer enters the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void ProcessPointerEnter(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Performs processing when a pointer leaves the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void ProcessPointerLeave(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Performs processing when a pointer press begins on the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void ProcessPointerDown(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Performs processing when pointer movement is routed to the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void ProcessPointerMove(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Performs processing when a pointer press is released for the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void ProcessPointerUp(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Performs processing when the widget gains focus before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessFocusGained() { }

    /// <summary>
    /// Performs processing when the widget loses focus before notification callbacks are invoked.
    /// </summary>
    protected virtual void ProcessFocusLost() { }

    /// <summary>
    /// Performs processing when keyboard input is routed to the widget before notification callbacks are invoked.
    /// </summary>
    /// <param name="args">The keyboard event data.</param>
    protected virtual void ProcessKeyboardInput(WidgetKeyboardEventArgs args) { }

    /// <summary>
    /// Determines whether a pointer-click event should be dispatched for the specified pointer event data.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    /// <returns>
    /// <c>true</c> if the pointer-click event should be dispatched; otherwise, <c>false</c>.
    /// </returns>
    protected virtual bool ShouldDispatchPointerClick(WidgetPointerEventArgs args)
    {
        return true;
    }

    /// <summary>
    /// Called when the widget is shown.
    /// </summary>
    protected virtual void OnShown() { }

    /// <summary>
    /// Called when the widget is hidden.
    /// </summary>
    protected virtual void OnHidden() { }

    /// <summary>
    /// Called when the widget is activated.
    /// </summary>
    protected virtual void OnActivated() { }

    /// <summary>
    /// Called when the widget is cancelled.
    /// </summary>
    protected virtual void OnCancelled() { }

    /// <summary>
    /// Called when a pointer enters the widget bounds.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerEnter(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when a pointer leaves the widget bounds.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerLeave(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when a pointer press begins on the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerDown(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when pointer movement is routed to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerMove(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when a pointer press is released for the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerUp(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when a pointer click is dispatched to the widget.
    /// </summary>
    /// <param name="args">The pointer event data.</param>
    protected virtual void OnPointerClick(WidgetPointerEventArgs args) { }

    /// <summary>
    /// Called when the widget gains focus.
    /// </summary>
    protected virtual void OnFocusGained() { }

    /// <summary>
    /// Called when the widget loses focus.
    /// </summary>
    protected virtual void OnFocusLost() { }

    /// <summary>
    /// Called when keyboard input is routed to the widget.
    /// </summary>
    /// <param name="args">The keyboard event data.</param>
    protected virtual void OnKeyboardInput(WidgetKeyboardEventArgs args) { }
}
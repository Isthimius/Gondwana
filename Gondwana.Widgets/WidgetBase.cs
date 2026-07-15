using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Widgets;

/// <summary>
/// Base type for reusable Gondwana widgets built on top of <see cref="DirectComposite"/>.
/// </summary>
public abstract class WidgetBase : DirectComposite
{
    #region Events

    /// <summary>
    /// Raised when the widget becomes visible.
    /// </summary>
    public event Action? Shown;

    /// <summary>
    /// Raised when the widget becomes hidden.
    /// </summary>
    public event Action? Hidden;

    /// <summary>
    /// Raised when the widget is activated.
    /// </summary>
    public event Action? Activated;

    /// <summary>
    /// Raised when the widget is cancelled.
    /// </summary>
    public event Action? Cancelled;

    /// <summary>
    /// Raised when a pointer enters the widget's bounds.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerEnter;

    /// <summary>
    /// Raised when a pointer leaves the widget's bounds.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerLeave;

    /// <summary>
    /// Raised when a pointer button is pressed down within the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerDown;

    /// <summary>
    /// Raised when the pointer moves over the widget or while the widget owns pointer capture.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerMove;

    /// <summary>
    /// Raised when a pointer button is released within the widget or while the widget owns pointer capture.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerUp;

    /// <summary>
    /// Raised when a pointer click is completed within the widget.
    /// </summary>
    public event Action<WidgetPointerEventArgs>? PointerClick;

    #endregion Events

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetBase"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host for drawing operations.</param>
    /// <param name="mode">The drawing mode (world or screen space).</param>
    /// <param name="anchor">The anchor point for the widget in pixels. Default is (0, 0).</param>
    /// <param name="nickname">Optional friendly name for the widget.</param>
    protected WidgetBase(RenderSurfaceHostBase renderSurfaceHost,
                         DirectDrawingMode mode,
                         PointF anchor = default,
                         string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Input Handling

    /// <summary>
    /// Gets or sets whether this widget participates in pointer input.
    /// </summary>
    public bool IsPointerInputEnabled { get; set; } = true;

    /// <summary>
    /// Determines whether the supplied screen position intersects this widget.
    /// </summary>
    public virtual bool HitTest(Point screenPositionPx)
    {
        return IsPointerInputEnabled &&
               Visible &&
               ScreenBounds.Contains(screenPositionPx);
    }

    #endregion

    #region Visibility / Activation

    /// <summary>
    /// Makes the widget visible and raises the <see cref="Shown"/> event.
    /// </summary>
    /// <returns>This <see cref="WidgetBase"/> instance for method chaining.</returns>
    public WidgetBase Show()
    {
        SetIsVisible(true);

        ProcessShown();
        OnShown();
        Shown?.Invoke();

        return this;
    }

    /// <summary>
    /// Hides the widget and raises the <see cref="Hidden"/> event.
    /// </summary>
    /// <returns>This <see cref="WidgetBase"/> instance for method chaining.</returns>
    public WidgetBase Hide()
    {
        SetIsVisible(false);

        ProcessHidden();
        OnHidden();
        Hidden?.Invoke();

        return this;
    }

    /// <summary>
    /// Activates the widget and raises the <see cref="Activated"/> event.
    /// </summary>
    /// <returns>This <see cref="WidgetBase"/> instance for method chaining.</returns>
    public WidgetBase Activate()
    {
        ProcessActivated();
        OnActivated();
        Activated?.Invoke();

        return this;
    }

    /// <summary>
    /// Cancels the widget and raises the <see cref="Cancelled"/> event.
    /// </summary>
    /// <returns>This <see cref="WidgetBase"/> instance for method chaining.</returns>
    public WidgetBase Cancel()
    {
        ProcessCancelled();
        OnCancelled();
        Cancelled?.Invoke();

        return this;
    }

    #endregion Visibility / Activation

    #region Pointer Dispatch

    /// <summary>
    /// Dispatches a pointer-enter event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerEnter(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerEnter(args);
        OnPointerEnter(args);
        PointerEnter?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-leave event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerLeave(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerLeave(args);
        OnPointerLeave(args);
        PointerLeave?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-down event, first running required framework behavior,
    /// then calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerDown(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerDown(args);
        OnPointerDown(args);
        PointerDown?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-move event, first running required framework behavior,
    /// then calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerMove(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerMove(args);
        OnPointerMove(args);
        PointerMove?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-up event, first running required framework behavior,
    /// then calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerUp(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProcessPointerUp(args);
        OnPointerUp(args);
        PointerUp?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer-click event, calling the virtual hook and raising the public event
    /// unless required framework behavior suppresses the click.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected internal void DispatchPointerClick(WidgetPointerEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!ShouldDispatchPointerClick(args))
            return;

        OnPointerClick(args);
        PointerClick?.Invoke(args);
    }

    #endregion Pointer Dispatch

    #region Framework Processing

    /// <summary>
    /// Runs required framework behavior before <see cref="OnShown"/> and <see cref="Shown"/>.
    /// </summary>
    protected virtual void ProcessShown()
    {
    }

    /// <summary>
    /// Runs required framework behavior before <see cref="OnHidden"/> and <see cref="Hidden"/>.
    /// </summary>
    protected virtual void ProcessHidden()
    {
    }

    /// <summary>
    /// Runs required framework behavior before <see cref="OnActivated"/> and <see cref="Activated"/>.
    /// </summary>
    protected virtual void ProcessActivated()
    {
    }

    /// <summary>
    /// Runs required framework behavior before <see cref="OnCancelled"/> and <see cref="Cancelled"/>.
    /// </summary>
    protected virtual void ProcessCancelled()
    {
    }

    /// <summary>
    /// Runs required framework behavior before pointer-enter customization and notification.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void ProcessPointerEnter(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Runs required framework behavior before pointer-leave customization and notification.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void ProcessPointerLeave(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Runs required framework behavior before pointer-down customization and notification.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void ProcessPointerDown(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Runs required framework behavior before pointer-move customization and notification.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void ProcessPointerMove(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Runs required framework behavior before pointer-up customization and notification.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void ProcessPointerUp(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Determines whether a pointer click should reach the protected hook and public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    /// <returns><see langword="true"/> to dispatch the click; otherwise, <see langword="false"/>.</returns>
    protected virtual bool ShouldDispatchPointerClick(WidgetPointerEventArgs args)
    {
        return true;
    }

    #endregion Framework Processing

    #region Protected Lifecycle Hooks

    /// <summary>
    /// Called when the widget is shown. Override to customize behavior.
    /// </summary>
    protected virtual void OnShown()
    {
    }

    /// <summary>
    /// Called when the widget is hidden. Override to customize behavior.
    /// </summary>
    protected virtual void OnHidden()
    {
    }

    /// <summary>
    /// Called when the widget is activated. Override to customize behavior.
    /// </summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>
    /// Called when the widget is cancelled. Override to customize behavior.
    /// </summary>
    protected virtual void OnCancelled()
    {
    }

    /// <summary>
    /// Called when a pointer enters the widget's bounds. Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerEnter(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Called when a pointer leaves the widget's bounds. Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerLeave(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Called when a pointer button is pressed down within the widget. Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerDown(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Called when the pointer moves over the widget or while the widget owns pointer capture.
    /// Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerMove(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Called when a pointer button is released within the widget or while the widget owns pointer capture.
    /// Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerUp(WidgetPointerEventArgs args)
    {
    }

    /// <summary>
    /// Called when a pointer click is completed within the widget. Override to customize behavior.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected virtual void OnPointerClick(WidgetPointerEventArgs args)
    {
    }

    #endregion Protected Lifecycle Hooks
}
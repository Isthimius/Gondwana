using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Widgets;

/// <summary>
/// Base type for reusable Gondwana widgets built on top of DirectComposite.
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
    /// Raised when a pointer button is released within the widget.
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
    protected WidgetBase(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Visibility / Activation

    /// <summary>
    /// Makes the widget visible and raises the <see cref="Shown"/> event.
    /// </summary>
    /// <returns>This <see cref="WidgetBase"/> instance for method chaining.</returns>
    public WidgetBase Show()
    {
        SetIsVisible(true);

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
        OnCancelled();
        Cancelled?.Invoke();

        return this;
    }

    #endregion Visibility / Activation

    #region Pointer Dispatch

    /// <summary>
    /// Dispatches a pointer enter event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected void DispatchPointerEnter(WidgetPointerEventArgs args)
    {
        OnPointerEnter(args);
        PointerEnter?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer leave event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected void DispatchPointerLeave(WidgetPointerEventArgs args)
    {
        OnPointerLeave(args);
        PointerLeave?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer down event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected void DispatchPointerDown(WidgetPointerEventArgs args)
    {
        OnPointerDown(args);
        PointerDown?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer up event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected void DispatchPointerUp(WidgetPointerEventArgs args)
    {
        OnPointerUp(args);
        PointerUp?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a pointer click event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The pointer event arguments.</param>
    protected void DispatchPointerClick(WidgetPointerEventArgs args)
    {
        OnPointerClick(args);
        PointerClick?.Invoke(args);
    }

    #endregion Pointer Dispatch

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
    /// Called when a pointer button is released within the widget. Override to customize behavior.
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
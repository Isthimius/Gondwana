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

    public event Action? Shown;
    public event Action? Hidden;
    public event Action? Activated;
    public event Action? Cancelled;

    public event Action<WidgetPointerEventArgs>? PointerEnter;
    public event Action<WidgetPointerEventArgs>? PointerLeave;
    public event Action<WidgetPointerEventArgs>? PointerDown;
    public event Action<WidgetPointerEventArgs>? PointerUp;
    public event Action<WidgetPointerEventArgs>? PointerClick;

    #endregion Events

    #region Constructor

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

    public WidgetBase Show()
    {
        SetIsVisible(true);

        OnShown();
        Shown?.Invoke();

        return this;
    }

    public WidgetBase Hide()
    {
        SetIsVisible(false);

        OnHidden();
        Hidden?.Invoke();

        return this;
    }

    public WidgetBase Activate()
    {
        OnActivated();
        Activated?.Invoke();

        return this;
    }

    public WidgetBase Cancel()
    {
        OnCancelled();
        Cancelled?.Invoke();

        return this;
    }

    #endregion Visibility / Activation

    #region Pointer Dispatch

    protected void DispatchPointerEnter(WidgetPointerEventArgs args)
    {
        OnPointerEnter(args);
        PointerEnter?.Invoke(args);
    }

    protected void DispatchPointerLeave(WidgetPointerEventArgs args)
    {
        OnPointerLeave(args);
        PointerLeave?.Invoke(args);
    }

    protected void DispatchPointerDown(WidgetPointerEventArgs args)
    {
        OnPointerDown(args);
        PointerDown?.Invoke(args);
    }

    protected void DispatchPointerUp(WidgetPointerEventArgs args)
    {
        OnPointerUp(args);
        PointerUp?.Invoke(args);
    }

    protected void DispatchPointerClick(WidgetPointerEventArgs args)
    {
        OnPointerClick(args);
        PointerClick?.Invoke(args);
    }

    #endregion Pointer Dispatch

    #region Protected Lifecycle Hooks

    protected virtual void OnShown()
    {
    }

    protected virtual void OnHidden()
    {
    }

    protected virtual void OnActivated()
    {
    }

    protected virtual void OnCancelled()
    {
    }

    protected virtual void OnPointerEnter(WidgetPointerEventArgs args)
    {
    }

    protected virtual void OnPointerLeave(WidgetPointerEventArgs args)
    {
    }

    protected virtual void OnPointerDown(WidgetPointerEventArgs args)
    {
    }

    protected virtual void OnPointerUp(WidgetPointerEventArgs args)
    {
    }

    protected virtual void OnPointerClick(WidgetPointerEventArgs args)
    {
    }

    #endregion Protected Lifecycle Hooks
}
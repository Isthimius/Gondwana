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
        return this;
    }

    public WidgetBase Hide()
    {
        SetIsVisible(false);
        OnHidden();
        return this;
    }

    public WidgetBase Activate()
    {
        OnActivated();
        return this;
    }

    public WidgetBase Cancel()
    {
        OnCancelled();
        return this;
    }

    #endregion Visibility / Activation

    #region Protected Event Raisers

    protected virtual void OnShown()
    {
        Shown?.Invoke();
    }

    protected virtual void OnHidden()
    {
        Hidden?.Invoke();
    }

    protected virtual void OnActivated()
    {
        Activated?.Invoke();
    }

    protected virtual void OnCancelled()
    {
        Cancelled?.Invoke();
    }

    protected virtual void OnPointerEnter(WidgetPointerEventArgs args)
    {
        PointerEnter?.Invoke(args);
    }

    protected virtual void OnPointerLeave(WidgetPointerEventArgs args)
    {
        PointerLeave?.Invoke(args);
    }

    protected virtual void OnPointerDown(WidgetPointerEventArgs args)
    {
        PointerDown?.Invoke(args);
    }

    protected virtual void OnPointerUp(WidgetPointerEventArgs args)
    {
        PointerUp?.Invoke(args);
    }

    protected virtual void OnPointerClick(WidgetPointerEventArgs args)
    {
        PointerClick?.Invoke(args);
    }

    #endregion Protected Event Raisers
}
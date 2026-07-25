using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Provides a widget that can own other widgets through the inherited
/// <see cref="DirectComposite"/> child system.
/// </summary>
/// <remarks>
/// Direct-drawing children and widget children share the same composite anchor,
/// offset storage, target validation, movement, and disposal ownership.
/// </remarks>
public abstract class ContainerWidget : WidgetBase
{
    private bool _isShown;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerWidget"/> class.
    /// </summary>
    protected ContainerWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            mode,
            anchor,
            nickname)
    {
    }

    /// <summary>
    /// Gets a snapshot of the child widgets directly owned by this container.
    /// </summary>
    public IReadOnlyList<WidgetBase> ChildWidgets =>
        Children
            .OfType<WidgetBase>()
            .ToArray();

    /// <summary>
    /// Adds a child widget at a local offset from this container's anchor.
    /// </summary>
    /// <typeparam name="TWidget">The child widget type.</typeparam>
    /// <param name="widget">The widget to add.</param>
    /// <param name="localOffsetPx">The local offset from the container anchor.</param>
    /// <returns>The added widget.</returns>
    protected TWidget AddChild<TWidget>(
        TWidget widget,
        Vector2 localOffsetPx)
        where TWidget : WidgetBase
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (Children.Contains(widget))
            return widget;

        Add(
            widget,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: localOffsetPx);

        widget.KeyboardInput +=
            OnChildKeyboardInput;

        widget.Disposing +=
            OnChildDisposing;

        if (_isShown)
            widget.Show();

        return widget;
    }

    /// <summary>
    /// Removes a child widget from the container.
    /// </summary>
    /// <param name="widget">The widget to remove.</param>
    /// <param name="dispose">
    /// <see langword="true"/> to dispose the child after detaching it.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the widget was a direct child; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    protected bool RemoveChild(
        WidgetBase widget,
        bool dispose = false)
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (!Children.Contains(widget))
            return false;

        if (_isShown)
            widget.Hide();

        widget.KeyboardInput -=
            OnChildKeyboardInput;

        widget.Disposing -=
            OnChildDisposing;

        Remove(widget);

        if (dispose)
            widget.Dispose();

        return true;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        _isShown = true;

        foreach (WidgetBase child in GetChildWidgetSnapshot())
            child.Show();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        foreach (WidgetBase child in GetChildWidgetSnapshot())
            child.Hide();

        _isShown = false;

        base.ProcessHidden();
    }

    /// <inheritdoc/>
    protected override void ProcessActivated()
    {
        base.ProcessActivated();

        // Children are activated after their parent so they are later in the
        // router's hit-test order.
        foreach (WidgetBase child in GetChildWidgetSnapshot())
            child.Activate();
    }

    /// <inheritdoc/>
    protected override void ProcessCancelled()
    {
        foreach (WidgetBase child in GetChildWidgetSnapshot())
            child.Cancel();

        base.ProcessCancelled();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        foreach (WidgetBase child in GetChildWidgetSnapshot())
        {
            child.KeyboardInput -=
                OnChildKeyboardInput;

            child.Disposing -=
                OnChildDisposing;
        }

        base.Dispose();
    }

    private void OnChildKeyboardInput(
        WidgetKeyboardEventArgs args)
    {
        if (args.Handled)
            return;

        var parentArgs =
            new WidgetKeyboardEventArgs(
                this,
                args.Key,
                args.KeyAction,
                args.Modifiers,
                args.Tick);

        DispatchKeyboardInput(parentArgs);

        args.Handled =
            parentArgs.Handled;
    }

    private void OnChildDisposing(
        object? sender,
        IDirectDrawable child)
    {
        if (child is not WidgetBase widget)
            return;

        widget.KeyboardInput -=
            OnChildKeyboardInput;

        widget.Disposing -=
            OnChildDisposing;
    }

    private WidgetBase[] GetChildWidgetSnapshot() =>
        Children
            .OfType<WidgetBase>()
            .ToArray();
}

using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;

namespace Gondwana.Widgets;

/// <summary>
/// Provides a widget that can own other widgets through the inherited
/// <see cref="DirectComposite"/> child system.
/// </summary>
/// <remarks>
/// Direct-drawing children and widget children share the same composite
/// anchor, offset storage, target validation, movement, and disposal
/// ownership. Widget children additionally participate in lifecycle
/// propagation and keyboard-input bubbling.
/// </remarks>
public abstract class ContainerWidget : WidgetBase
{
    private bool _isShown;

    #region constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerWidget"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">
    /// The render surface host that owns the widget.
    /// </param>
    /// <param name="mode">
    /// The drawing mode used by the widget and its children.
    /// </param>
    /// <param name="anchor">
    /// The widget anchor in mode-appropriate pixels.
    /// </param>
    /// <param name="nickname">
    /// An optional diagnostic nickname.
    /// </param>
    protected ContainerWidget(RenderSurfaceHostBase renderSurfaceHost,
                              DirectDrawingMode mode,
                              PointF anchor = default,
                              string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion constructors

    #region public properties

    /// <summary>
    /// Gets a snapshot of the child widgets directly owned by this container.
    /// </summary>
    public IReadOnlyList<WidgetBase> ChildWidgets => Children.OfType<WidgetBase>().ToArray();

    #endregion public properties

    #region public methods

    /// <inheritdoc/>
    public override DirectComposite Add(IDirectCompositeChild child,
                                        bool keepCurrentOffset = true,
                                        Vector2? explicitLocalOffsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        bool alreadyAdded =
            Children.Contains(child);

        base.Add(
            child,
            keepCurrentOffset,
            explicitLocalOffsetPx);

        if (!alreadyAdded &&
            _isShown &&
            child is WidgetBase widget)
        {
            widget.Show();
        }

        return this;
    }

    /// <inheritdoc/>
    public override DirectComposite Remove(
        IDirectCompositeChild child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_isShown &&
            child is WidgetBase widget &&
            Children.Contains(child))
        {
            widget.Hide();
        }

        return base.Remove(child);
    }

    /// <inheritdoc/>
    public override DirectComposite Clear()
    {
        if (_isShown)
        {
            foreach (WidgetBase child in GetChildWidgetSnapshot())
                child.Hide();
        }

        return base.Clear();
    }

    #endregion public methods

    #region protected methods

    /// <summary>
    /// Adds a child widget at a local offset from this container's anchor.
    /// </summary>
    /// <typeparam name="TWidget">
    /// The child widget type.
    /// </typeparam>
    /// <param name="widget">
    /// The widget to add.
    /// </param>
    /// <param name="localOffsetPx">
    /// The local offset from the container anchor.
    /// </param>
    /// <returns>The added widget.</returns>
    protected TWidget Add<TWidget>(
        TWidget widget,
        Vector2 localOffsetPx)
        where TWidget : WidgetBase
    {
        ArgumentNullException.ThrowIfNull(widget);

        Add(
            (IDirectCompositeChild)widget,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: localOffsetPx);

        return widget;
    }

    /// <summary>
    /// Removes a child widget from the container.
    /// </summary>
    /// <param name="widget">
    /// The widget to remove.
    /// </param>
    /// <param name="dispose">
    /// <see langword="true"/> to dispose the child after detaching it;
    /// otherwise, <see langword="false"/>.
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

        Remove(widget);

        if (dispose)
            widget.Dispose();

        return true;
    }

    /// <inheritdoc/>
    protected override void OnChildAdded(
        IDirectCompositeChild child)
    {
        base.OnChildAdded(child);

        if (child is WidgetBase widget)
            widget.KeyboardInput += OnChildKeyboardInput;
    }

    /// <inheritdoc/>
    protected override void OnChildRemoved(
        IDirectCompositeChild child)
    {
        if (child is WidgetBase widget)
            widget.KeyboardInput -= OnChildKeyboardInput;

        base.OnChildRemoved(child);
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

        // Children are activated after their parent so they appear later in
        // the router's hit-test order.
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

    #endregion protected methods

    #region private methods

    private void OnChildKeyboardInput(
        WidgetKeyboardEventArgs args)
    {
        if (args.Handled)
            return;

        var parentArgs = new WidgetKeyboardEventArgs(
            this,
            args.Key,
            args.KeyAction,
            args.Modifiers,
            args.Tick);

        DispatchKeyboardInput(parentArgs);

        args.Handled = parentArgs.Handled;
    }

    private WidgetBase[] GetChildWidgetSnapshot()
    {
        return Children
            .OfType<WidgetBase>()
            .ToArray();
    }

    #endregion private methods
}
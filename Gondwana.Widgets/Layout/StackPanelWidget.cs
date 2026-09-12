using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;

namespace Gondwana.Widgets.Layout;

/// <summary>
/// Arranges child widgets sequentially in a horizontal or vertical stack.
/// </summary>
public sealed class StackPanelWidget : ContainerWidget
{
    private WidgetOrientation _orientation;
    private float _spacing;
    private SizeF _contentSize;

    /// <summary>
    /// Creates a stack panel in the supplied drawing mode.
    /// The first child establishes the view or scene-layer target.
    /// </summary>
    public StackPanelWidget(RenderSurfaceHostBase renderSurfaceHost,
                            DirectDrawingMode mode,
                            PointF anchor = default,
                            WidgetOrientation orientation = WidgetOrientation.Vertical,
                            float spacing = 0f,
                            string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
        _orientation = orientation;
        Spacing = spacing;

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;
    }

    /// <summary>
    /// Gets or sets the stacking direction.
    /// </summary>
    public WidgetOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;

            _orientation = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets or sets the number of pixels placed between adjacent child widgets.
    /// </summary>
    public float Spacing
    {
        get => _spacing;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value));

            _spacing = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets the current laid-out content size.
    /// </summary>
    public SizeF ContentSize => _contentSize;

    /// <summary>
    /// Adds a child widget and immediately recalculates layout.
    /// </summary>
    public StackPanelWidget AddWidget(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        Add(widget, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        return this;
    }

    /// <summary>
    /// Removes a child widget and immediately recalculates layout.
    /// </summary>
    public StackPanelWidget RemoveWidget(WidgetBase widget, bool dispose = false)
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (RemoveChild(widget, dispose))
            Relayout();

        return this;
    }

    /// <summary>
    /// Recalculates every child offset from the current orientation and spacing.
    /// </summary>
    public StackPanelWidget Relayout()
    {
        WidgetBase[] widgets = ChildWidgets.ToArray();
        float cursor = 0f;
        float cross = 0f;

        for (int index = 0; index < widgets.Length; index++)
        {
            WidgetBase widget = widgets[index];
            Rectangle bounds = Mode == DirectDrawingMode.View
                ? widget.ScreenBounds
                : widget.WorldBounds;

            Vector2 offset = _orientation == WidgetOrientation.Vertical
                ? new Vector2(0f, cursor)
                : new Vector2(cursor, 0f);

            SetLocalOffset(widget, offset);

            if (_orientation == WidgetOrientation.Vertical)
            {
                cursor += bounds.Height;
                cross = Math.Max(cross, bounds.Width);
            }
            else
            {
                cursor += bounds.Width;
                cross = Math.Max(cross, bounds.Height);
            }

            if (index < widgets.Length - 1)
                cursor += _spacing;
        }

        _contentSize = _orientation == WidgetOrientation.Vertical
            ? new SizeF(cross, cursor)
            : new SizeF(cursor, cross);

        return this;
    }

    /// <inheritdoc/>
    public override DirectComposite Add(IDirectCompositeChild child,
                                        bool keepCurrentOffset = true,
                                        Vector2? explicitLocalOffsetPx = null)
    {
        if (child is not WidgetBase)
            throw new ArgumentException("StackPanelWidget accepts widget children only.", nameof(child));

        DirectComposite result = base.Add(child, keepCurrentOffset, explicitLocalOffsetPx);
        Relayout();
        return result;
    }

    /// <inheritdoc/>
    public override DirectComposite Remove(IDirectCompositeChild child)
    {
        DirectComposite result = base.Remove(child);
        Relayout();
        return result;
    }

    /// <inheritdoc/>
    public override DirectComposite Clear()
    {
        DirectComposite result = base.Clear();
        Relayout();
        return result;
    }
}

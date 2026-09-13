using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Coordinates mutually exclusive <see cref="RadioButtonWidget"/> instances.
/// </summary>
public sealed class RadioButtonGroup
{
    private readonly HashSet<RadioButtonWidget> _buttons = [];

    /// <summary>
    /// Gets the currently selected radio button, or <see langword="null"/>.
    /// </summary>
    public RadioButtonWidget? SelectedButton => _buttons.FirstOrDefault(static button => button.IsSelected);

    internal void Register(RadioButtonWidget button)
    {
        ArgumentNullException.ThrowIfNull(button);
        _buttons.Add(button);

        if (button.IsSelected)
            Select(button);
    }

    internal void Unregister(RadioButtonWidget button)
    {
        ArgumentNullException.ThrowIfNull(button);
        _buttons.Remove(button);
    }

    internal void Select(RadioButtonWidget selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        foreach (RadioButtonWidget button in _buttons.ToArray())
            button.SetSelectedFromGroup(ReferenceEquals(button, selected));
    }
}

/// <summary>
/// Provides a radio-button control with optional mutually exclusive grouping.
/// </summary>
public sealed class RadioButtonWidget : WidgetBase
{
    private const int DefaultIndicatorSize = 20;
    private const int DefaultLabelGap = 8;
    private const int DotInset = 6;

    private Color _normalColor = Color.FromArgb(255, 48, 48, 56);
    private Color _hoverColor = Color.FromArgb(255, 66, 66, 76);
    private Color _dotColor = Color.FromArgb(255, 235, 235, 242);
    private RadioButtonGroup? _group;
    private bool _isSelected;
    private bool _disposed;

    /// <summary>
    /// Occurs when <see cref="IsSelected"/> changes.
    /// </summary>
    public event Action<bool>? SelectionChanged;

    /// <summary>
    /// Creates a view-level radio button.
    /// </summary>
    public RadioButtonWidget(RenderSurfaceHostBase renderSurfaceHost,
                             View view,
                             Rectangle bounds,
                             string text,
                             RadioButtonGroup? group = null,
                             bool isSelected = false,
                             string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        Rectangle indicatorBounds = GetIndicatorBounds(bounds);
        Rectangle dotBounds = GetDotBounds(indicatorBounds);
        Rectangle labelBounds = GetLabelBounds(bounds, indicatorBounds.Width);

        Ring = CreateRing(renderSurfaceHost, view, indicatorBounds);
        Dot = CreateDot(renderSurfaceHost, view, dotBounds);
        Label = CreateLabel(renderSurfaceHost, view, labelBounds, text);

        Add(Ring);
        Add(Dot);
        Add(Label);

        CompleteInitialization(group, isSelected);
    }

    /// <summary>
    /// Creates a scene-layer radio button.
    /// </summary>
    public RadioButtonWidget(RenderSurfaceHostBase renderSurfaceHost,
                             SceneLayer sceneLayer,
                             Rectangle bounds,
                             string text,
                             RadioButtonGroup? group = null,
                             bool isSelected = false,
                             string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        Rectangle indicatorBounds = GetIndicatorBounds(bounds);
        Rectangle dotBounds = GetDotBounds(indicatorBounds);
        Rectangle labelBounds = GetLabelBounds(bounds, indicatorBounds.Width);

        Ring = CreateRing(renderSurfaceHost, sceneLayer, indicatorBounds);
        Dot = CreateDot(renderSurfaceHost, sceneLayer, dotBounds);
        Label = CreateLabel(renderSurfaceHost, sceneLayer, labelBounds, text);

        Add(Ring);
        Add(Dot);
        Add(Label);

        CompleteInitialization(group, isSelected);
    }

    /// <summary>
    /// Gets the outer radio-button ring.
    /// </summary>
    public DirectRectangle Ring { get; }

    /// <summary>
    /// Gets the inner selection dot.
    /// </summary>
    public DirectRectangle Dot { get; }

    /// <summary>
    /// Gets the label displayed beside the radio button.
    /// </summary>
    public TextBlock Label { get; }

    /// <summary>
    /// Gets or sets the group that coordinates this radio button.
    /// </summary>
    public RadioButtonGroup? Group
    {
        get => _group;
        set
        {
            if (ReferenceEquals(_group, value))
                return;

            _group?.Unregister(this);
            _group = value;
            _group?.Register(this);
        }
    }

    /// <summary>
    /// Gets or sets whether the radio button is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetSelected(value);
    }

    /// <summary>
    /// Sets the selection state.
    /// </summary>
    public RadioButtonWidget SetSelected(bool isSelected)
    {
        if (isSelected && Group is not null)
        {
            Group.Select(this);
            return this;
        }

        SetSelectedFromGroup(isSelected);
        return this;
    }

    /// <summary>
    /// Selects this button and clears any other selection in its group.
    /// </summary>
    public RadioButtonWidget Select()
    {
        return SetSelected(true);
    }

    /// <summary>
    /// Changes the label text.
    /// </summary>
    public RadioButtonWidget SetText(string text)
    {
        Label.SetText(text ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Sets the normal, hover, and selected-dot colors.
    /// </summary>
    public RadioButtonWidget SetColors(Color normal,
                                       Color hover,
                                       Color dot)
    {
        _normalColor = normal;
        _hoverColor = hover;
        _dotColor = dot;

        UpdateRingColor(_normalColor);
        Dot.SetColor(_dotColor);
        Refresh(Dot);
        return this;
    }

    /// <summary>
    /// Sets the label text color.
    /// </summary>
    public RadioButtonWidget SetTextColor(SKColor color)
    {
        Label.SetColors(color, SKColors.Transparent);
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the radio-button visuals.
    /// </summary>
    public RadioButtonWidget SetRadioButtonZOrder(int zOrder)
    {
        Ring.ZOrder = zOrder;
        Dot.ZOrder = zOrder + 1;
        Label.ZOrder = zOrder + 1;
        return this;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Group = null;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        Dot.Visible = IsSelected;
    }

    /// <inheritdoc/>
    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        UpdateRingColor(_hoverColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        UpdateRingColor(_normalColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        Select();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed || args.Key != 32)
            return;

        args.Handled = true;
        Select();
    }

    internal void SetSelectedFromGroup(bool isSelected)
    {
        if (_isSelected == isSelected)
            return;

        _isSelected = isSelected;
        Dot.Visible = isSelected;
        SelectionChanged?.Invoke(isSelected);
    }

    private void CompleteInitialization(RadioButtonGroup? group, bool isSelected)
    {
        _isSelected = isSelected;
        Dot.Visible = isSelected;
        SetRadioButtonZOrder(0);

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;

        Group = group;
    }

    private void UpdateRingColor(Color color)
    {
        Ring.SetColor(color);
        Refresh(Ring);
    }

    private static void Refresh(DirectRectangle rectangle)
    {
        rectangle.SetPosition(rectangle.GetPosition());
    }

    private DirectRectangle CreateRing(RenderSurfaceHostBase host,
                                       View view,
                                       Rectangle bounds)
    {
        return ConfigureRing(new DirectRectangle(_normalColor, host, view, bounds, $"{Nickname}.ring"), bounds.Width);
    }

    private DirectRectangle CreateRing(RenderSurfaceHostBase host,
                                       SceneLayer layer,
                                       Rectangle bounds)
    {
        return ConfigureRing(new DirectRectangle(_normalColor, host, layer, bounds, $"{Nickname}.ring"), bounds.Width);
    }

    private static DirectRectangle ConfigureRing(DirectRectangle ring, int size)
    {
        return ring.SetFilled(true)
                   .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                   .SetStrokeWidth(1.5f)
                   .SetCornerRadius(size / 2f);
    }

    private DirectRectangle CreateDot(RenderSurfaceHostBase host,
                                      View view,
                                      Rectangle bounds)
    {
        return ConfigureDot(new DirectRectangle(_dotColor, host, view, bounds, $"{Nickname}.dot"), bounds.Width);
    }

    private DirectRectangle CreateDot(RenderSurfaceHostBase host,
                                      SceneLayer layer,
                                      Rectangle bounds)
    {
        return ConfigureDot(new DirectRectangle(_dotColor, host, layer, bounds, $"{Nickname}.dot"), bounds.Width);
    }

    private static DirectRectangle ConfigureDot(DirectRectangle dot, int size)
    {
        return dot.SetFilled(true)
                  .SetStrokeWidth(0f)
                  .SetCornerRadius(size / 2f);
    }

    private static TextBlock CreateLabel(RenderSurfaceHostBase host,
                                         View view,
                                         Rectangle bounds,
                                         string text)
    {
        return ConfigureLabel(new TextBlock(host, view, bounds), text);
    }

    private static TextBlock CreateLabel(RenderSurfaceHostBase host,
                                         SceneLayer layer,
                                         Rectangle bounds,
                                         string text)
    {
        return ConfigureLabel(new TextBlock(host, layer, view: null, worldBounds: bounds), text);
    }

    private static TextBlock ConfigureLabel(TextBlock label, string text)
    {
        return label.SetText(text ?? string.Empty)
                    .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                    .SetColors(SKColors.White, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
                    .EnableWrapping(false);
    }

    private static Rectangle GetIndicatorBounds(Rectangle bounds)
    {
        int size = Math.Min(DefaultIndicatorSize, Math.Max(10, bounds.Height - 2));
        return new Rectangle(bounds.X,
                             bounds.Y + (bounds.Height - size) / 2,
                             size,
                             size);
    }

    private static Rectangle GetDotBounds(Rectangle indicatorBounds)
    {
        int inset = Math.Min(DotInset, Math.Max(2, indicatorBounds.Width / 3));
        return Rectangle.Inflate(indicatorBounds, -inset, -inset);
    }

    private static Rectangle GetLabelBounds(Rectangle bounds, int indicatorWidth)
    {
        int left = bounds.X + indicatorWidth + DefaultLabelGap;
        return new Rectangle(left,
                             bounds.Y,
                             Math.Max(1, bounds.Right - left),
                             bounds.Height);
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width < 40 || bounds.Height < 12)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Radio-button bounds are too small.");

        return bounds;
    }
}

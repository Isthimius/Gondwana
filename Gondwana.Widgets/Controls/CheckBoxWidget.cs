using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a two-state checkbox with a text label.
/// </summary>
public sealed class CheckBoxWidget : WidgetBase
{
    private const int DefaultIndicatorSize = 20;
    private const int DefaultLabelGap = 8;
    private const int MarkInset = 5;

    private Color _normalColor = Color.FromArgb(255, 48, 48, 56);
    private Color _hoverColor = Color.FromArgb(255, 66, 66, 76);
    private Color _markColor = Color.FromArgb(255, 235, 235, 242);
    private bool _isChecked;

    /// <summary>
    /// Occurs when <see cref="IsChecked"/> changes.
    /// </summary>
    public event Action<bool>? CheckedChanged;

    /// <summary>
    /// Creates a view-level checkbox.
    /// </summary>
    public CheckBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                          View view,
                          Rectangle bounds,
                          string text,
                          bool isChecked = false,
                          string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        Rectangle indicatorBounds = GetIndicatorBounds(bounds);
        Rectangle markBounds = GetMarkBounds(indicatorBounds);
        Rectangle labelBounds = GetLabelBounds(bounds, indicatorBounds.Width);

        Box = CreateBox(renderSurfaceHost, view, indicatorBounds);
        Mark = CreateMark(renderSurfaceHost, view, markBounds);
        Label = CreateLabel(renderSurfaceHost, view, labelBounds, text);

        Add(Box);
        Add(Mark);
        Add(Label);

        CompleteInitialization(isChecked);
    }

    /// <summary>
    /// Creates a scene-layer checkbox.
    /// </summary>
    public CheckBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                          SceneLayer sceneLayer,
                          Rectangle bounds,
                          string text,
                          bool isChecked = false,
                          string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        Rectangle indicatorBounds = GetIndicatorBounds(bounds);
        Rectangle markBounds = GetMarkBounds(indicatorBounds);
        Rectangle labelBounds = GetLabelBounds(bounds, indicatorBounds.Width);

        Box = CreateBox(renderSurfaceHost, sceneLayer, indicatorBounds);
        Mark = CreateMark(renderSurfaceHost, sceneLayer, markBounds);
        Label = CreateLabel(renderSurfaceHost, sceneLayer, labelBounds, text);

        Add(Box);
        Add(Mark);
        Add(Label);

        CompleteInitialization(isChecked);
    }

    /// <summary>
    /// Gets the outer checkbox indicator.
    /// </summary>
    public DirectRectangle Box { get; }

    /// <summary>
    /// Gets the filled mark displayed while the checkbox is checked.
    /// </summary>
    public DirectRectangle Mark { get; }

    /// <summary>
    /// Gets the text label displayed beside the checkbox.
    /// </summary>
    public TextBlock Label { get; }

    /// <summary>
    /// Gets or sets whether the checkbox is checked.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetChecked(value);
    }

    /// <summary>
    /// Gets the checkbox bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            Rectangle boxBounds = Mode == DirectDrawingMode.View ? Box.ScreenBounds : Box.WorldBounds;
            Rectangle labelBounds = Mode == DirectDrawingMode.View ? Label.ScreenBounds : Label.WorldBounds;
            return Rectangle.Union(boxBounds, labelBounds);
        }
    }

    /// <summary>
    /// Sets the checkbox state.
    /// </summary>
    public CheckBoxWidget SetChecked(bool isChecked)
    {
        if (_isChecked == isChecked)
            return this;

        _isChecked = isChecked;
        Mark.Visible = isChecked;
        CheckedChanged?.Invoke(isChecked);
        return this;
    }

    /// <summary>
    /// Toggles the checkbox state.
    /// </summary>
    public CheckBoxWidget Toggle()
    {
        return SetChecked(!IsChecked);
    }

    /// <summary>
    /// Changes the checkbox label text.
    /// </summary>
    public CheckBoxWidget SetText(string text)
    {
        Label.SetText(text ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Sets the normal, hover, and checked-mark colors.
    /// </summary>
    public CheckBoxWidget SetColors(Color normal,
                                    Color hover,
                                    Color mark)
    {
        _normalColor = normal;
        _hoverColor = hover;
        _markColor = mark;

        UpdateBoxColor(IsFocused ? _hoverColor : _normalColor);
        Mark.SetColor(_markColor);
        Refresh(Mark);
        return this;
    }

    /// <summary>
    /// Sets the label text color.
    /// </summary>
    public CheckBoxWidget SetTextColor(SKColor color)
    {
        Label.SetColors(color, SKColors.Transparent);
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the checkbox visuals.
    /// </summary>
    public CheckBoxWidget SetCheckBoxZOrder(int zOrder)
    {
        Box.ZOrder = zOrder;
        Mark.ZOrder = zOrder + 1;
        Label.ZOrder = zOrder + 1;
        return this;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        Mark.Visible = IsChecked;
    }

    /// <inheritdoc/>
    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        UpdateBoxColor(_hoverColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        UpdateBoxColor(_normalColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        Toggle();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed || args.Key != 32)
            return;

        args.Handled = true;
        Toggle();
    }

    private void CompleteInitialization(bool isChecked)
    {
        _isChecked = isChecked;
        Mark.Visible = isChecked;
        SetCheckBoxZOrder(0);

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
    }

    private void UpdateBoxColor(Color color)
    {
        Box.SetColor(color);
        Refresh(Box);
    }

    private static void Refresh(DirectRectangle rectangle)
    {
        rectangle.SetPosition(rectangle.GetPosition());
    }

    private DirectRectangle CreateBox(RenderSurfaceHostBase host,
                                      View view,
                                      Rectangle bounds)
    {
        return ConfigureBox(new DirectRectangle(_normalColor, host, view, bounds, $"{Nickname}.box"));
    }

    private DirectRectangle CreateBox(RenderSurfaceHostBase host,
                                      SceneLayer layer,
                                      Rectangle bounds)
    {
        return ConfigureBox(new DirectRectangle(_normalColor, host, layer, bounds, $"{Nickname}.box"));
    }

    private static DirectRectangle ConfigureBox(DirectRectangle box)
    {
        return box.SetFilled(true)
                  .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                  .SetStrokeWidth(1.5f)
                  .SetCornerRadius(3f);
    }

    private DirectRectangle CreateMark(RenderSurfaceHostBase host,
                                       View view,
                                       Rectangle bounds)
    {
        return ConfigureMark(new DirectRectangle(_markColor, host, view, bounds, $"{Nickname}.mark"));
    }

    private DirectRectangle CreateMark(RenderSurfaceHostBase host,
                                       SceneLayer layer,
                                       Rectangle bounds)
    {
        return ConfigureMark(new DirectRectangle(_markColor, host, layer, bounds, $"{Nickname}.mark"));
    }

    private static DirectRectangle ConfigureMark(DirectRectangle mark)
    {
        return mark.SetFilled(true)
                   .SetStrokeWidth(0f)
                   .SetCornerRadius(2f);
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

    private static Rectangle GetMarkBounds(Rectangle indicatorBounds)
    {
        int inset = Math.Min(MarkInset, Math.Max(2, indicatorBounds.Width / 4));
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
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Checkbox bounds are too small.");

        return bounds;
    }
}

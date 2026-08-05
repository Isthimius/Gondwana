using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Represents one actionable command in a menu dropdown.
/// </summary>
public sealed class MenuItemWidget : WidgetBase
{
    private readonly MenuBarTheme _theme;
    private readonly Action? _action;
    private bool _isPointerOver;
    private bool _isSelected;
    private bool _isEnabled = true;

    internal event Action<MenuItemWidget>? Hovered;

    /// <summary>Occurs when the menu item is activated.</summary>
    public event Action<MenuItemWidget>? Invoked;

    internal MenuItemWidget(RenderSurfaceHostBase host,
                            View view,
                            Rectangle bounds,
                            string text,
                            string? shortcutText,
                            Action? action,
                            MenuBarTheme theme,
                            string? nickname = null)
        : base(host, DirectDrawingMode.View, bounds.Location, nickname)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _action = action;

        Text = text ?? throw new ArgumentNullException(nameof(text));
        ShortcutText = shortcutText ?? string.Empty;

        Background = new DirectRectangle(
                _theme.ItemNormalColor,
                host,
                view,
                bounds,
                $"{Nickname}.background")
            .SetFilled(true)
            .SetStrokeWidth(0f);

        Label = new TextBlock(
                host,
                view,
                bounds,
                $"{Nickname}.label")
            .SetText(Text)
            .SetFont(SKTypeface.Default, _theme.FontSize, _theme.MinimumFontSize)
            .SetColors(ToSkColor(_theme.TextColor), SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        Label.HorizontalPadding = _theme.ItemHorizontalPadding;

        ShortcutLabel = new TextBlock(
                host,
                view,
                bounds,
                $"{Nickname}.shortcut")
            .SetText(ShortcutText)
            .SetFont(SKTypeface.Default, _theme.FontSize, _theme.MinimumFontSize)
            .SetColors(ToSkColor(_theme.ShortcutTextColor), SKColors.Transparent)
            .SetAlignment(SKTextAlign.Right, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        ShortcutLabel.HorizontalPadding = _theme.ItemHorizontalPadding;

        Add(Background);
        Add(Label);
        Add(ShortcutLabel);

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        IsPointerInputEnabled = true;
    }

    /// <summary>Gets the command label.</summary>
    public string Text { get; }

    /// <summary>Gets the optional display-only shortcut text.</summary>
    public string ShortcutText { get; }

    /// <summary>Gets the item background drawing.</summary>
    public DirectRectangle Background { get; }

    /// <summary>Gets the command label drawing.</summary>
    public TextBlock Label { get; }

    /// <summary>Gets the shortcut label drawing.</summary>
    public TextBlock ShortcutLabel { get; }

    /// <summary>Gets whether this item can currently be invoked.</summary>
    public bool IsEnabled => _isEnabled;

    /// <summary>Gets whether this item is selected by hover or keyboard navigation.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Enables or disables this item.</summary>
    public MenuItemWidget SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        ApplyInputState(true);
        UpdateVisualState();
        return this;
    }

    /// <summary>Programmatically invokes the item when enabled.</summary>
    public void PerformClick()
    {
        if (!_isEnabled || !IsInputEnabled)
            return;

        // The owning menu closes in response to this event before the command runs.
        Invoked?.Invoke(this);
        _action?.Invoke();
    }

    internal void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisualState();
    }

    internal void ApplyInputState(bool menuAcceptsInput)
    {
        bool enabled = menuAcceptsInput && _isEnabled;
        IsInputEnabled = enabled;
        IsPointerInputEnabled = enabled;
        IsKeyboardInputEnabled = enabled;
        CanReceiveFocus = enabled;
    }

    internal void SetItemZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        Label.ZOrder = zOrder + 1;
        ShortcutLabel.ZOrder = zOrder + 1;
    }

    internal void SetBounds(Rectangle bounds)
    {
        SetPosition(bounds.X, bounds.Y);

        Background.ScreenBounds = new Rectangle(
            Background.ScreenBounds.Location,
            bounds.Size);

        Label.ScreenBounds = new Rectangle(
            Label.ScreenBounds.Location,
            bounds.Size);

        ShortcutLabel.ScreenBounds = new Rectangle(
            ShortcutLabel.ScreenBounds.Location,
            bounds.Size);
    }

    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);

        if (!_isEnabled)
            return;

        _isPointerOver = true;
        Hovered?.Invoke(this);
        UpdateVisualState();
    }

    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        _isPointerOver = false;
        UpdateVisualState();
    }

    protected override void OnPointerDown(WidgetPointerEventArgs args)
    {
        base.OnPointerDown(args);

        if (_isEnabled && args.IsPrimaryButton)
            SetBackgroundColor(_theme.ItemPressedColor);
    }

    protected override void OnPointerUp(WidgetPointerEventArgs args)
    {
        base.OnPointerUp(args);
        UpdateVisualState();
    }

    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!_isEnabled || !args.IsPrimaryButton)
            return;

        args.Handled = true;
        PerformClick();
    }

    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (!_isEnabled || args.KeyAction != KeyAction.Pressed)
            return;

        if (args.Key is not 13 and not 32)
            return;

        args.Handled = true;
        PerformClick();
    }

    private void UpdateVisualState()
    {
        Color backgroundColor = _isEnabled && (_isPointerOver || _isSelected)
            ? _theme.ItemHoverColor
            : _theme.ItemNormalColor;

        Color textColor = _isEnabled
            ? _theme.TextColor
            : _theme.DisabledTextColor;

        Color shortcutColor = _isEnabled
            ? _theme.ShortcutTextColor
            : _theme.DisabledTextColor;

        SetBackgroundColor(backgroundColor);
        Label.SetColors(ToSkColor(textColor), SKColors.Transparent);
        ShortcutLabel.SetColors(ToSkColor(shortcutColor), SKColors.Transparent);
    }

    private void SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        Background.SetPosition(Background.GetPosition());
    }

    private static SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
